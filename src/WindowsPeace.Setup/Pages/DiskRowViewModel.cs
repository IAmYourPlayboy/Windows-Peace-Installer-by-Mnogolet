using System.Globalization;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Infrastructure;
using CoreLocalization = WindowsPeace.Core.Localization;
using Keys = WindowsPeace.Core.Localization.Keys;

namespace WindowsPeace.Setup.Pages;

/// <summary>Что представляет строка списка.</summary>
public enum RowKind
{
    Disk,
    Partition,
    FreeSpace,
}

/// <summary>Одна строка двухуровневого списка. Плоский список с отступом проще дерева и ведёт себя предсказуемее.</summary>
public sealed class DiskRowViewModel : ViewModelBase
{
    private bool _isExpanded = true;
    private DiskRowViewModel(RowKind kind, SelectionTarget target, string name, string size, string free, string type, string note)
    {
        Kind = kind;
        Target = target;
        Name = name;
        Size = size;
        Free = free;
        Type = type;
        Note = note;
        Verdict = SelectionRules.Evaluate(target);
    }

    public RowKind Kind { get; }
    public SelectionTarget Target { get; }
    public string Name { get; }
    public string Size { get; }
    public string Free { get; }
    public string Type { get; }
    public string Note { get; }
    public SelectionVerdict Verdict { get; }

    public int Indent => Kind == RowKind.Disk ? 0 : 24;
    public bool IsSelectable => Verdict.IsAllowed;

    /// <summary>
    /// Развёрнут ли диск. По умолчанию да - всё видно сразу (выбор автора).
    /// Значимо только для строк-дисков; переключается кликом или стрелками.
    /// </summary>
    public bool IsExpanded
    {
        get => _isExpanded;
        set
        {
            if (Set(ref _isExpanded, value))
            {
                Raise(nameof(IsCollapsed));
            }
        }
    }

    /// <summary>Обратное IsExpanded — для показа свёрнутой стрелки в разметке.</summary>
    public bool IsCollapsed => !_isExpanded;

    /// <summary>
    /// Строку-диск с разделами или незанятым местом можно свернуть. У остальных
    /// строк стрелки нет. У невыбираемого диска (носитель, система) тоже нет:
    /// такая строка отключена, чтобы клавиатура её пропускала и не выбирала,
    /// а отключённую строку не свернуть - показывать стрелку было бы обманом.
    /// </summary>
    public bool CanToggle => Kind == RowKind.Disk && IsSelectable
        && (Target.Disk.Partitions.Count > 0 || Target.Disk.FreeSpaces.Count > 0);

    /// <summary>
    /// Как строка называется для средств доступности и автоматизации.
    /// Без этого экранный диктор читает вслух имя класса, а не имя диска:
    /// проверено на живой машине, см. docs/superpowers/notes/2026-08-11-step-a-acceptance.md.
    /// </summary>
    public override string ToString()
    {
        var text = Name + ", " + Size;

        if (!string.IsNullOrEmpty(Type) && Type != "-")
        {
            text += ", " + Type;
        }

        if (!string.IsNullOrEmpty(Note))
        {
            text += ". " + Note;
        }

        return text;
    }

    public static DiskRowViewModel ForDisk(DiskInfo disk)
        => new(RowKind.Disk, SelectionTarget.ForWholeDisk(disk),
            disk.FriendlyName,
            ByteSize.Format(disk.Identity.SizeBytes),
            string.Empty,
            DiskDescription.Bus(disk),
            DescribeDisk(disk));

    public static DiskRowViewModel ForPartition(DiskInfo disk, PartitionInfo partition)
        => new(RowKind.Partition, SelectionTarget.ForPartition(disk, partition),
            DescribePartitionName(partition),
            ByteSize.Format(partition.Size),
            partition.Volume is null ? "-" : ByteSize.Format(partition.Volume.FreeBytes),
            DescribeKind(partition.Kind),
            DescribeContent(partition));

    public static DiskRowViewModel ForFreeSpace(DiskInfo disk, FreeSpaceInfo freeSpace)
        => new(RowKind.FreeSpace, SelectionTarget.ForFreeSpace(disk, freeSpace),
            CoreLocalization.Localization.Current[Keys.Disk.FreeSpace], ByteSize.Format(freeSpace.Size), string.Empty, "-", string.Empty);

    private static string DescribePartitionName(PartitionInfo partition)
    {
        var label = partition.Volume?.Label;
        var letter = partition.DriveLetter is null ? string.Empty : " (" + partition.DriveLetter + ":)";
        var name = string.IsNullOrWhiteSpace(label)
            ? string.Format(CultureInfo.CurrentCulture, CoreLocalization.Localization.Current[Keys.Disk.Partition], partition.Number)
            : string.Format(CultureInfo.CurrentCulture, CoreLocalization.Localization.Current[Keys.Disk.PartitionLabel], partition.Number, label);
        return name + letter;
    }

    private static string DescribeKind(PartitionKind kind) => kind switch
    {
        PartitionKind.EfiSystem => CoreLocalization.Localization.Current[Keys.PartitionType.Efi],
        PartitionKind.MicrosoftReserved => CoreLocalization.Localization.Current[Keys.PartitionType.Msr],
        PartitionKind.WindowsRecovery => CoreLocalization.Localization.Current[Keys.PartitionType.Recovery],
        PartitionKind.BasicData => CoreLocalization.Localization.Current[Keys.PartitionType.Basic],
        _ => CoreLocalization.Localization.Current[Keys.PartitionType.Unknown],
    };

    private static string DescribeDisk(DiskInfo disk)
    {
        if (disk.IsWindowsPeaceMedia) return CoreLocalization.Localization.Current[Keys.Disk.NoteMedia];
        if (disk.IsSystem || disk.IsBoot) return CoreLocalization.Localization.Current[Keys.Disk.NoteSystem];
        if (disk.ProbeError is not null) return disk.ProbeError;
        if (disk.Partitions.Count == 0) return CoreLocalization.Localization.Current[Keys.Disk.NoteEmpty];
        return string.Format(CultureInfo.CurrentCulture, CoreLocalization.Localization.Current[Keys.Disk.NotePartitions], disk.Partitions.Count);
    }

    private static string DescribeContent(PartitionInfo partition)
    {
        if (!partition.Content.Inspected) return partition.Content.NotInspectedReason ?? string.Empty;
        if (partition.Content.WindowsFound && partition.Content.UserFilesFound) return CoreLocalization.Localization.Current[Keys.Content.WindowsAndFiles];
        if (partition.Content.WindowsFound) return CoreLocalization.Localization.Current[Keys.Content.Windows];
        if (partition.Content.UserFilesFound) return CoreLocalization.Localization.Current[Keys.Content.UserFiles];
        return string.Empty;
    }

}
