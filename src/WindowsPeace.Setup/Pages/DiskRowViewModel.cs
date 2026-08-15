using System.Globalization;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Setup.Pages;

/// <summary>Что представляет строка списка.</summary>
public enum RowKind
{
    Disk,
    Partition,
    FreeSpace,
}

/// <summary>Одна строка двухуровневого списка. Плоский список с отступом проще дерева и ведёт себя предсказуемее.</summary>
public sealed class DiskRowViewModel
{
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
    /// Как строка называется для средств доступности и автоматизации.
    /// Без этого экранный диктор читает вслух имя класса, а не имя диска:
    /// проверено на живой машине, см. docs/superpowers/notes/2026-08-11-step-a-acceptance.md.
    /// </summary>
    public override string ToString()
    {
        var text = Name + ", " + Size;

        if (!string.IsNullOrEmpty(Type) && Type != "—")
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
            partition.Volume is null ? "—" : ByteSize.Format(partition.Volume.FreeBytes),
            DescribeKind(partition.Kind),
            DescribeContent(partition));

    public static DiskRowViewModel ForFreeSpace(DiskInfo disk, FreeSpaceInfo freeSpace)
        => new(RowKind.FreeSpace, SelectionTarget.ForFreeSpace(disk, freeSpace),
            "Незанятое пространство", ByteSize.Format(freeSpace.Size), string.Empty, "—", string.Empty);

    private static string DescribePartitionName(PartitionInfo partition)
    {
        var label = partition.Volume?.Label;
        var letter = partition.DriveLetter is null ? string.Empty : " (" + partition.DriveLetter + ":)";
        var name = string.IsNullOrWhiteSpace(label)
            ? string.Format(CultureInfo.CurrentCulture, "Раздел {0}", partition.Number)
            : string.Format(CultureInfo.CurrentCulture, "Раздел {0}: {1}", partition.Number, label);
        return name + letter;
    }

    private static string DescribeKind(PartitionKind kind) => kind switch
    {
        PartitionKind.EfiSystem => "Системный EFI",
        PartitionKind.MicrosoftReserved => "MSR",
        PartitionKind.WindowsRecovery => "Восстановление",
        PartitionKind.BasicData => "Основной",
        _ => "Неизвестный",
    };

    private static string DescribeDisk(DiskInfo disk)
    {
        if (disk.IsWindowsPeaceMedia) return "Загрузочный носитель — установка сюда невозможна";
        if (disk.IsSystem || disk.IsBoot) return "Здесь работает текущая система";
        if (disk.ProbeError is not null) return disk.ProbeError;
        if (disk.Partitions.Count == 0) return "Пустой";
        return string.Format(CultureInfo.CurrentCulture, "Разделов: {0}", disk.Partitions.Count);
    }

    private static string DescribeContent(PartitionInfo partition)
    {
        if (!partition.Content.Inspected) return partition.Content.NotInspectedReason ?? string.Empty;
        if (partition.Content.WindowsFound && partition.Content.UserFilesFound) return "Windows и файлы пользователя";
        if (partition.Content.WindowsFound) return "Windows";
        if (partition.Content.UserFilesFound) return "Файлы пользователя";
        return string.Empty;
    }

}
