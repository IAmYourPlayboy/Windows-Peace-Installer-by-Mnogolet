using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Threading;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Machine;
using WindowsPeace.Core.Media;
using WindowsPeace.Core.Storage;
using WindowsPeace.Core.Storage.Native;
using WindowsPeace.Setup.Pages;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup;

public partial class App : Application
{
    private readonly Stopwatch _sinceStart = Stopwatch.StartNew();
    private OpenedLog? _opened;
    private IOperationLog _journal = NullOperationLog.Instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Журнал открывается раньше всего остального. Если дальше что-то упадёт,
        // единственным следом останется этот файл: в WinPE после перезагрузки
        // не остаётся ни экрана, ни памяти, ни временных папок.
        //
        // Мест несколько, и в каждом пробуется несколько имён, поэтому ни занятый
        // файл, ни защищённый от записи носитель не оставляют запуск без журнала.
        // Человеку об этом не сообщается ничего: журнал нужен нам, а не ему.
        _opened = OperationLogOpener.Open(LogPlaces.InOrder(AppContext.BaseDirectory), new JsonLinesLogOpener());
        _journal = _opened.Log;

        // Падение после старта тоже должно оставлять след. Без этого журнал
        // обрывается на последней удачной точке, и непонятно, была ли ошибка
        // или человек просто закрыл окно.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        Checkpoint("Журнал открыт", _opened.Path);
        foreach (var refusal in _opened.Refusals)
        {
            // Место не подошло — это не авария, но знать о ней надо: сегодня
            // это занятый файл, а завтра окажется, что носитель защищён от записи.
            Checkpoint("Место для журнала не подошло", refusal, OperationOutcome.Failure);
        }

        var machine = new RealEnvironmentReader(_journal);
        var snapshot = HostEnvironment.Describe(machine);
        Checkpoint("Снимок среды", snapshot.ToString());

        var probe = new RealFileSystemProbe();

        var welcome = new WelcomeViewModel();
        var language = new LanguageViewModel();
        var recipePicker = CreateRecipePicker(e.Args, snapshot, probe);

        // Прямой разговор с Windows вместо WMI: библиотека System.Management
        // в WinPE не работает, она подгружает модуль из .NET Framework, которого
        // там нет. Проверено опытом, см. docs/superpowers/notes/2026-08-14-step-b-pe-experiments.md.
        var diskPicker = new DiskPickerViewModel(
            new NativeDiskEnumerator(new Win32StorageSource(), _journal),
            new FileSystemContentInspector(probe),
            probe);

        // Сводка перед установкой собирается не здесь, а при входе на неё:
        // в этот момент диск ещё не выбран. Посредник знает оба первых экрана,
        // поэтому сама сводка не знает ни одного.
        var choice = new WizardChoice(recipePicker, diskPicker, language);

        Checkpoint("Модели экранов созданы", null);

        // Весь проход по экранам уходит в журнал: в WinPE после перезагрузки
        // это единственное свидетельство, что человек прошёл вперёд и назад.
        // Без него пункт приёмки о переходах держался бы на памяти проверяющего.
        var navigator = new WizardNavigator(
            new List<IWizardPage>
            {
                welcome,
                language,
                recipePicker,
                diskPicker,
                new ConfirmViewModel(choice),
                new ProgressViewModel(),
                new DoneViewModel(),
            },
            ScreenEntered);

        var window = new ShellWindow(snapshot.IsWindowsPe)
        {
            DataContext = new ShellViewModel(navigator, Shutdown),
        };
        Checkpoint("Окно создано", null);

        window.ContentRendered += (_, _) =>
        {
            Checkpoint("Первая отрисовка прошла", null);

            // Расход памяти замеряется здесь, когда окно уже нарисовано:
            // до этого мерить нечего. Число нужно нам — из него вырастет
            // системное требование, — а человеку о нём знать незачем.
            Checkpoint("Расход памяти", MemoryUse.Measure(machine).ToString());

            FailOnPurpose(e.Args);
        };
        window.Show();
        Checkpoint("Show вызван", null);
    }

    /// <summary>
    /// Отладочный ключ «--crash»: роняет мастера после первой отрисовки.
    /// Нужен затем, что путь обработки неожиданной ошибки — тоже путь, и работать
    /// он обязан проверенно, а не «по замыслу»: настоящую неожиданную ошибку
    /// по заказу не устроишь. В обычном ходе работы ключ не используется.
    ///
    /// Падение откладывается отдельным действием окна, а не бросается прямо
    /// здесь. Так ошибки и случаются на самом деле — в нажатии кнопки,
    /// в продолжении задачи, — а брошенная посреди отрисовки не доходит даже
    /// до собственного объяснения: проверено, окно с объяснением не появилось.
    /// </summary>
    private void FailOnPurpose(string[] args)
    {
        if (!HasFlag(args, "--crash"))
        {
            return;
        }

        Checkpoint("Отладочный ключ --crash", "Роняю мастера нарочно");
        Dispatcher.BeginInvoke(new Action(
            () => throw new InvalidOperationException("Падение по отладочному ключу --crash.")));
    }

    /// <summary>
    /// Находит носитель, читает опись и строит по ней первый экран. Исход чтения
    /// заодно уходит в журнал: экран человек увидит, а журнал останется, когда
    /// смотреть будет уже некому — ради этого он и заводится.
    /// </summary>
    private RecipePickerViewModel CreateRecipePicker(string[] args, EnvironmentSnapshot snapshot, IFileSystemProbe probe)
    {
        var media = LocateMedia(args, snapshot, probe);
        if (media is null)
        {
            Checkpoint("Носитель не найден", "Описи нет ни на одном томе: " + string.Join(" ", snapshot.VolumeRoots),
                OperationOutcome.Failure);
            return RecipePickerViewModel.WithoutMedia();
        }

        var manifest = media.Load(new FileTextReader());
        var trouble = manifest.Detail is null ? manifest.Message : manifest.Message + " " + manifest.Detail;
        var detail = manifest.Status == MediaManifestStatus.Ok
            ? string.Format(CultureInfo.CurrentCulture, "{0}; рецептов: {1}",
                media.ManifestPath, manifest.Manifest!.Recipes.Count)
            : string.Format(CultureInfo.CurrentCulture, "{0}; {1}", media.ManifestPath, trouble);

        Checkpoint("Чтение описи: " + manifest.Status, detail,
            manifest.Status == MediaManifestStatus.Ok ? OperationOutcome.Success : OperationOutcome.Failure);

        return new RecipePickerViewModel(manifest);
    }

    /// <summary>
    /// Где искать опись. На обычной Windows её нет нигде, поэтому мастер
    /// принимает отладочный ключ «--media папка» и берёт опись оттуда:
    /// иначе работать над экранами пришлось бы, перезагружаясь в WinPE.
    /// В обычном ходе работы ключ не используется.
    /// </summary>
    private MediaLocation? LocateMedia(string[] args, EnvironmentSnapshot snapshot, IFileSystemProbe probe)
    {
        var forced = ReadOption(args, "--media");
        if (forced is not null)
        {
            Checkpoint("Отладочный ключ --media", forced);
            return new MediaLocation(forced);
        }

        // Тома, а не диски: их список уже снят, а перечисление дисков идёт
        // своим чередом, и ждать его ради описи незачем.
        return BootMediaLocator.FindAmong(snapshot.VolumeRoots, probe);
    }

    /// <summary>Ключ со значением: «--media папка».</summary>
    private static string? ReadOption(string[] args, string name)
    {
        for (var i = 0; i < args.Length - 1; i++)
        {
            if (string.Equals(args[i], name, StringComparison.Ordinal))
            {
                return args[i + 1];
            }
        }

        return null;
    }

    /// <summary>Ключ без значения: «--crash».</summary>
    private static bool HasFlag(string[] args, string name)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, name, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Контрольная точка старта. В WinPE падение до окна не оставляет ничего,
    /// кроме журнала: по этим записям видно, на каком шаге всё оборвалось
    /// и сколько времени прошло до него.
    /// </summary>
    private void Checkpoint(string what, string? detail, OperationOutcome outcome = OperationOutcome.Success)
        => _journal.Write(new OperationRecord(
            DateTimeOffset.Now, "Setup.Startup", what, _sinceStart.Elapsed, outcome, detail));

    /// <summary>
    /// Вход на экран мастера. Отдельный компонент от стартовых точек: старт —
    /// это подъём среды, а это уже сам проход, и по журналу их надо различать.
    /// Имя экрана — то же, что видит человек в заголовке.
    /// </summary>
    private void ScreenEntered(IWizardPage page)
        => _journal.Write(new OperationRecord(
            DateTimeOffset.Now, "Setup.Navigation", "Открыт экран", _sinceStart.Elapsed,
            OperationOutcome.Success, page.Title));

    /// <summary>
    /// Неожиданная ошибка в окне. Стандартное окно .NET показало бы человеку
    /// трассировку стека по-английски — ровно то, чего он видеть не должен.
    /// Ему одна понятная строка, нам в журнал всё остальное.
    ///
    /// Мастер после этого закрывается, а не продолжает: что именно сломалось,
    /// отсюда неизвестно, и работать дальше на сломанном — хуже, чем закрыться.
    /// </summary>
    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
    {
        _journal.Write(new OperationRecord(
            DateTimeOffset.Now, "Setup.Startup", "Необработанная ошибка в окне", _sinceStart.Elapsed,
            OperationOutcome.Failure, e.Exception.ToString()));

        e.Handled = true;

        Checkpoint("Показываю объяснение", null);

        MessageBox.Show(
            "Windows Peace не смог продолжить работу и сейчас закроется." + Environment.NewLine +
            Environment.NewLine +
            "Разбираться с этим нам, а не вам: что случилось, записано в журнал работы.",
            "Windows Peace",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Checkpoint("Объяснение закрыто", null);

        Shutdown();
    }

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        => _journal.Write(new OperationRecord(
            DateTimeOffset.Now, "Setup.Startup", "Необработанная ошибка вне окна", _sinceStart.Elapsed,
            OperationOutcome.Failure, e.ExceptionObject?.ToString()));

    protected override void OnExit(ExitEventArgs e)
    {
        Checkpoint("Мастер закрыт", null);
        _opened?.Dispose();
        base.OnExit(e);
    }
}
