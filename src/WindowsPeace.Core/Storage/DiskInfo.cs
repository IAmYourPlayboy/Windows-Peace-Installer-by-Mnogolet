using System.Collections.Generic;

namespace WindowsPeace.Core.Storage;

/// <summary>Стиль разметки диска. Значения совпадают с MSFT_Disk.PartitionStyle.</summary>
public enum PartitionStyle
{
    Unknown = 0,
    Mbr = 1,
    Gpt = 2,
}

/// <summary>Физический диск со всем, что о нём удалось выяснить.</summary>
public sealed class DiskInfo
{
    public DiskInfo(
        DiskIdentity identity,
        int number,
        string friendlyName,
        MediaKind media,
        PartitionStyle partitionStyle,
        bool isSystem,
        bool isBoot,
        bool isOffline,
        bool isReadOnly,
        bool isRemovable,
        IReadOnlyList<PartitionInfo> partitions,
        IReadOnlyList<FreeSpaceInfo> freeSpaces,
        string? probeError)
    {
        Identity = identity;
        Number = number;
        FriendlyName = friendlyName;
        Media = media;
        PartitionStyle = partitionStyle;
        IsSystem = isSystem;
        IsBoot = isBoot;
        IsOffline = isOffline;
        IsReadOnly = isReadOnly;
        IsRemovable = isRemovable;
        Partitions = partitions;
        FreeSpaces = freeSpaces;
        ProbeError = probeError;
    }

    public DiskIdentity Identity { get; }

    /// <summary>
    /// Порядковый номер. Используется ТОЛЬКО для соединения записей WMI между собой
    /// и для отладки. В интерфейсе не показывается, в рецепт не попадает.
    /// </summary>
    public int Number { get; }

    public string FriendlyName { get; }
    public MediaKind Media { get; }
    public PartitionStyle PartitionStyle { get; }

    /// <summary>На диске лежит работающая сейчас система.</summary>
    public bool IsSystem { get; }

    /// <summary>С диска выполнялась текущая загрузка.</summary>
    public bool IsBoot { get; }

    public bool IsOffline { get; }
    public bool IsReadOnly { get; }
    public bool IsRemovable { get; }

    public IReadOnlyList<PartitionInfo> Partitions { get; }
    public IReadOnlyList<FreeSpaceInfo> FreeSpaces { get; }

    /// <summary>Заполнено, если разделы прочитать не удалось. Сам диск при этом показывается.</summary>
    public string? ProbeError { get; }

    /// <summary>Загрузочный носитель Windows Peace. Проставляется BootMediaLocator.</summary>
    public bool IsWindowsPeaceMedia { get; internal set; }
}
