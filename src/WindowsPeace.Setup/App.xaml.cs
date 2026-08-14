using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Threading;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Machine;
using WindowsPeace.Core.Storage;
using WindowsPeace.Core.Storage.Native;
using WindowsPeace.Setup.Pages;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup;

public partial class App : Application
{
    private readonly Stopwatch _sinceStart = Stopwatch.StartNew();
    private JsonLinesOperationLog? _log;
    private IOperationLog _journal = NullOperationLog.Instance;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        // Место для журнала выбирается раньше всего остального. Если дальше
        // что-то упадёт, единственным следом останется этот файл: в WinPE
        // после перезагрузки не остаётся ни экрана, ни памяти, ни временных папок.
        var location = LogLocationResolver.Resolve(
            Path.Combine(AppContext.BaseDirectory, "logs"),
            @"X:\WindowsPeace\logs",
            new RealWritabilityProbe());

        if (location.IsAvailable)
        {
            _log = new JsonLinesOperationLog(Path.Combine(location.Directory, "windows-peace.jsonl"));
            _journal = _log;
        }

        // Падение после старта тоже должно оставлять след. Без этого журнал
        // обрывается на последней удачной точке, и непонятно, была ли ошибка
        // или человек просто закрыл окно.
        DispatcherUnhandledException += OnDispatcherUnhandledException;
        AppDomain.CurrentDomain.UnhandledException += OnDomainUnhandledException;

        Checkpoint("Место для журнала выбрано", location.Reason);

        var snapshot = HostEnvironment.Describe(new RealEnvironmentReader(_journal));
        Checkpoint("Снимок среды", snapshot.ToString());

        var probe = new RealFileSystemProbe();

        // Прямой разговор с Windows вместо WMI: библиотека System.Management
        // в WinPE не работает, она подгружает модуль из .NET Framework, которого
        // там нет. Проверено опытом, см. docs/superpowers/notes/2026-08-14-step-b-pe-experiments.md.
        var diskPicker = new DiskPickerViewModel(
            new NativeDiskEnumerator(new Win32StorageSource(), _journal),
            new FileSystemContentInspector(probe),
            probe);

        Checkpoint("Модели экранов созданы", null);

        var navigator = new WizardNavigator(new List<IWizardPage>
        {
            diskPicker,
            new PlaceholderViewModel(),
        });

        var window = new ShellWindow { DataContext = new ShellViewModel(navigator) };
        Checkpoint("Окно создано", null);

        window.ContentRendered += (_, _) => Checkpoint("Первая отрисовка прошла", null);
        window.Show();
        Checkpoint("Show вызван", null);
    }

    /// <summary>
    /// Контрольная точка старта. В WinPE падение до окна не оставляет ничего,
    /// кроме журнала: по этим записям видно, на каком шаге всё оборвалось
    /// и сколько времени прошло до него.
    /// </summary>
    private void Checkpoint(string what, string? detail)
        => _journal.Write(new OperationRecord(
            DateTimeOffset.Now, "Setup.Startup", what, _sinceStart.Elapsed, OperationOutcome.Success, detail));

    private void OnDispatcherUnhandledException(object sender, DispatcherUnhandledExceptionEventArgs e)
        => _journal.Write(new OperationRecord(
            DateTimeOffset.Now, "Setup.Startup", "Необработанная ошибка в окне", _sinceStart.Elapsed,
            OperationOutcome.Failure, e.Exception.ToString()));

    private void OnDomainUnhandledException(object sender, UnhandledExceptionEventArgs e)
        => _journal.Write(new OperationRecord(
            DateTimeOffset.Now, "Setup.Startup", "Необработанная ошибка вне окна", _sinceStart.Elapsed,
            OperationOutcome.Failure, e.ExceptionObject?.ToString()));

    protected override void OnExit(ExitEventArgs e)
    {
        Checkpoint("Мастер закрыт", null);
        _log?.Dispose();
        base.OnExit(e);
    }
}
