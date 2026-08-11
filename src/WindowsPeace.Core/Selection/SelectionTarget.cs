using WindowsPeace.Core.Storage;

namespace WindowsPeace.Core.Selection;

/// <summary>Что именно выбрано в списке.</summary>
public enum TargetKind
{
    WholeDisk,
    ExistingPartition,
    FreeSpace,
}

/// <summary>
/// Цель установки. Разница между «диск целиком» и «раздел» — это разница
/// между «размечаем по рецепту» и «ставим сюда, остального не трогаем».
/// </summary>
public sealed class SelectionTarget
{
    private SelectionTarget(TargetKind kind, DiskInfo disk, PartitionInfo? partition, FreeSpaceInfo? freeSpace)
    {
        Kind = kind;
        Disk = disk;
        Partition = partition;
        FreeSpace = freeSpace;
    }

    // Приставка For у фабрик обязательна: без неё метод Partition столкнулся бы
    // с одноимённым свойством, а в C# тип не может содержать и то и другое.
    // Выбрано в пользу свойств: на месте использования target.Partition!.Size
    // читается лучше, чем любое переименование.

    public static SelectionTarget ForWholeDisk(DiskInfo disk) => new(TargetKind.WholeDisk, disk, null, null);

    public static SelectionTarget ForPartition(DiskInfo disk, PartitionInfo partition)
        => new(TargetKind.ExistingPartition, disk, partition, null);

    public static SelectionTarget ForFreeSpace(DiskInfo disk, FreeSpaceInfo freeSpace)
        => new(TargetKind.FreeSpace, disk, null, freeSpace);

    public TargetKind Kind { get; }
    public DiskInfo Disk { get; }
    public PartitionInfo? Partition { get; }
    public FreeSpaceInfo? FreeSpace { get; }

    /// <summary>Сколько места отводится под Windows.</summary>
    public ulong AvailableBytes => Kind switch
    {
        TargetKind.WholeDisk => Disk.Identity.SizeBytes,
        TargetKind.ExistingPartition => Partition!.Size,
        TargetKind.FreeSpace => FreeSpace!.Size,
        _ => 0UL,
    };
}
