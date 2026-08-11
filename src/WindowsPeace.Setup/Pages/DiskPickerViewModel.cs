using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Infrastructure;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Состояние экрана выбора диска. Решения о допустимости и предупреждениях
/// принимает Core; здесь только показ и переключение доступности кнопок.
/// </summary>
public sealed class DiskPickerViewModel : ViewModelBase, IWizardPage
{
    private readonly IDiskEnumerator _enumerator;
    private readonly IDiskContentInspector _inspector;
    private readonly IFileSystemProbe _probe;

    private DiskRowViewModel? _selected;
    private string _planSummary = string.Empty;
    private string? _denialReason;
    private string? _enumerationError;
    private IReadOnlyList<DiskInfo> _disks = Array.Empty<DiskInfo>();

    public DiskPickerViewModel(IDiskEnumerator enumerator, IDiskContentInspector inspector, IFileSystemProbe probe)
    {
        _enumerator = enumerator;
        _inspector = inspector;
        _probe = probe;
        RefreshCommand = new RelayCommand(Refresh);
    }

    public string Title => "Куда установить Windows?";

    public ObservableCollection<DiskRowViewModel> Rows { get; } = new();

    public ObservableCollection<PlanWarning> Warnings { get; } = new();

    public RelayCommand RefreshCommand { get; }

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
        if (Rows.Count == 0 && EnumerationError is null)
        {
            Refresh();
        }
    }

    public void Refresh()
    {
        Rows.Clear();
        Warnings.Clear();
        Selected = null;

        using var cts = new CancellationTokenSource(Timeouts.DiskEnumeration);
        var snapshot = _enumerator.Enumerate(cts.Token);

        EnumerationError = snapshot.EnumerationError;
        _disks = snapshot.Disks;

        foreach (var disk in _disks)
        {
            _inspector.Inspect(disk, cts.Token);
        }

        // Отметка носителя ставится до построения строк: от неё зависит вердикт,
        // а вердикт вычисляется в конструкторе строки.
        BootMediaLocator.Mark(_disks, _probe);

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
