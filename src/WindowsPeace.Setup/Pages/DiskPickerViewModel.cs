using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Infrastructure;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Состояние экрана выбора диска. Решения о допустимости и предупреждениях
/// принимает Core; здесь только показ и переключение доступности кнопок.
///
/// Опрос дисков уходит в отдельный поток. Это не украшение: раздел 9
/// архитектуры запрещает состояние «крутится, и непонятно, живо ли оно».
/// Опрос показывает, чем занят, допускает отмену и сам прекращается
/// по истечении Timeouts.DiskEnumeration.
/// </summary>
public sealed class DiskPickerViewModel : ViewModelBase, IWizardPage
{
    private readonly IDiskEnumerator _enumerator;
    private readonly IDiskContentInspector _inspector;
    private readonly IFileSystemProbe _probe;

    private CancellationTokenSource? _cancellation;

    private DiskRowViewModel? _selected;
    private string _planSummary = string.Empty;
    private string _statusText = string.Empty;
    private string? _denialReason;
    private string? _enumerationError;
    private bool _isBusy;
    private IReadOnlyList<DiskInfo> _disks = Array.Empty<DiskInfo>();

    public DiskPickerViewModel(IDiskEnumerator enumerator, IDiskContentInspector inspector, IFileSystemProbe probe)
    {
        _enumerator = enumerator;
        _inspector = inspector;
        _probe = probe;

        RefreshCommand = new RelayCommand(() => _ = RefreshAsync(), () => !IsBusy);
        CancelCommand = new RelayCommand(Cancel, () => IsBusy);
    }

    public string Title => "Куда установить Windows?";

    public ObservableCollection<DiskRowViewModel> Rows { get; } = new();

    public ObservableCollection<PlanWarning> Warnings { get; } = new();

    /// <summary>
    /// Все диски машины, как их вернул последний опрос. Нужны не только этому
    /// экрану: по ним правила выбора смотрят, не стоит ли Windows на соседнем.
    /// </summary>
    public IReadOnlyList<DiskInfo> Disks => _disks;

    public RelayCommand RefreshCommand { get; }

    public RelayCommand CancelCommand { get; }

    public DiskRowViewModel? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                UpdateSelection();
            }
        }
    }

    public string PlanSummary
    {
        get => _planSummary;
        private set => Set(ref _planSummary, value);
    }

    /// <summary>Чем занят опрос прямо сейчас. Пусто, когда опрос не идёт.</summary>
    public string StatusText
    {
        get => _statusText;
        private set => Set(ref _statusText, value);
    }

    /// <summary>Идёт ли опрос. От него зависит вид ожидания и доступность кнопок.</summary>
    public bool IsBusy
    {
        get => _isBusy;
        private set
        {
            if (Set(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string? DenialReason
    {
        get => _denialReason;
        private set => Set(ref _denialReason, value);
    }

    public string? EnumerationError
    {
        get => _enumerationError;
        private set => Set(ref _enumerationError, value);
    }

    public bool CanGoNext => Selected?.IsSelectable == true;

    public bool CanCreate => Selected?.Kind == RowKind.FreeSpace;

    public bool CanDelete => Selected?.Kind == RowKind.Partition;

    public bool CanFormat => Selected?.Kind == RowKind.Partition && Selected.Target.Partition?.Volume is not null;

    public bool CanExtend => Selected?.Kind == RowKind.Partition && HasAdjacentFreeSpace();

    public bool CanShowDetails => Selected is not null;

    public event EventHandler? CanGoNextChanged;

    public void OnEnter()
    {
        if (Rows.Count == 0 && EnumerationError is null && !IsBusy)
        {
            // Намеренно без ожидания: вход на страницу не должен блокировать
            // оболочку. RefreshAsync не выпускает исключений наружу, поэтому
            // брошенная задача не оставит необработанного отказа.
            _ = RefreshAsync();
        }
    }

    /// <summary>Прекращает идущий опрос. Безопасно вызывать, когда опроса нет.</summary>
    public void Cancel() => _cancellation?.Cancel();

    /// <summary>
    /// Опрашивает диски и строит список. Тяжёлая часть выполняется в стороннем
    /// потоке, разбор результата — там же, где вызвали, поэтому коллекции
    /// меняются в потоке интерфейса.
    /// </summary>
    public async Task RefreshAsync()
    {
        if (IsBusy)
        {
            return;
        }

        _cancellation?.Dispose();
        _cancellation = new CancellationTokenSource(Timeouts.DiskEnumeration);
        var token = _cancellation.Token;

        IsBusy = true;
        Rows.Clear();
        Warnings.Clear();
        Selected = null;
        EnumerationError = null;
        StatusText = "Опрашиваю диски…";

        var progress = new Progress<string>(text => StatusText = text);

        try
        {
            var snapshot = await Task.Run(() => Scan(progress, token), token).ConfigureAwait(true);

            EnumerationError = snapshot.EnumerationError;
            _disks = snapshot.Disks;

            BuildRows();
        }
        catch (OperationCanceledException)
        {
            _disks = Array.Empty<DiskInfo>();
            EnumerationError = "Опрос дисков прерван. Нажмите «Обновить», чтобы попробовать снова.";
        }
        finally
        {
            IsBusy = false;
            StatusText = string.Empty;
        }
    }

    /// <summary>
    /// Всё, что обращается наружу: перечисление, чтение содержимого, поиск описи
    /// носителя. Выполняется в стороннем потоке и ничего не трогает в интерфейсе.
    /// </summary>
    private DiskSnapshot Scan(IProgress<string> progress, CancellationToken token)
    {
        var snapshot = _enumerator.Enumerate(token);

        if (snapshot.IsFailed)
        {
            return snapshot;
        }

        var index = 0;
        foreach (var disk in snapshot.Disks)
        {
            token.ThrowIfCancellationRequested();

            index++;
            progress.Report($"Смотрю, что лежит на диске {index} из {snapshot.Disks.Count}…");
            _inspector.Inspect(disk, token);
        }

        token.ThrowIfCancellationRequested();
        progress.Report("Ищу загрузочный носитель…");

        // Отметка носителя ставится до построения строк: от неё зависит вердикт,
        // а вердикт вычисляется в конструкторе строки.
        BootMediaLocator.Mark(snapshot.Disks, _probe);

        return snapshot;
    }

    private void BuildRows()
    {
        foreach (var disk in _disks)
        {
            Rows.Add(DiskRowViewModel.ForDisk(disk));

            foreach (var partition in disk.Partitions)
            {
                Rows.Add(DiskRowViewModel.ForPartition(disk, partition));
            }

            foreach (var gap in disk.FreeSpaces)
            {
                Rows.Add(DiskRowViewModel.ForFreeSpace(disk, gap));
            }
        }
    }

    private void UpdateSelection()
    {
        Warnings.Clear();
        DenialReason = null;
        PlanSummary = string.Empty;

        if (Selected is not null)
        {
            DenialReason = Selected.Verdict.Reason;

            if (Selected.IsSelectable)
            {
                PlanSummary = DeploymentPlanner.Build(Selected.Target).Summary;

                foreach (var warning in SelectionRules.Warnings(Selected.Target, _disks))
                {
                    Warnings.Add(warning);
                }
            }
        }

        Raise(nameof(CanCreate));
        Raise(nameof(CanDelete));
        Raise(nameof(CanFormat));
        Raise(nameof(CanExtend));
        Raise(nameof(CanShowDetails));
        Raise(nameof(CanGoNext));
        CanGoNextChanged?.Invoke(this, EventArgs.Empty);
    }

    private bool HasAdjacentFreeSpace()
    {
        var partition = Selected?.Target.Partition;
        return partition is not null
               && Selected!.Target.Disk.FreeSpaces.Any(gap => gap.Offset == partition.End);
    }
}
