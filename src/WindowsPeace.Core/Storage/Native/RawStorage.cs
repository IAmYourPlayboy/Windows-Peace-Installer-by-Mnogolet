using System.Collections.Generic;
using System.Threading;

namespace WindowsPeace.Core.Storage.Native;

/// <summary>Физический диск так, как о нём рассказало ядро Windows. Без выводов и догадок.</summary>
public sealed class RawDisk
{
    public int Number { get; init; }

    /// <summary>Изготовитель и модель, склеенные так же, как их показывает система.</summary>
    public string Model { get; init; } = string.Empty;

    public string? SerialNumber { get; init; }

    /// <summary>Идентификатор диска из заголовка GPT. Запасной отпечаток, если серийника нет.</summary>
    public string? DiskGuid { get; init; }

    public BusType BusType { get; init; }
    public MediaKind Media { get; init; }
    public bool IsRemovable { get; init; }
    public bool IsReadOnly { get; init; }
    public bool IsOffline { get; init; }
    public ulong SizeBytes { get; init; }
    public PartitionStyle PartitionStyle { get; init; }

    public IReadOnlyList<RawPartition> Partitions { get; init; } = new List<RawPartition>();

    /// <summary>Заполнено, если разметку прочитать не удалось. Сам диск при этом остаётся в списке.</summary>
    public string? Error { get; init; }
}

/// <summary>Раздел так, как он записан в таблице разметки диска.</summary>
public sealed class RawPartition
{
    public int Number { get; init; }
    public ulong Offset { get; init; }
    public ulong Size { get; init; }

    /// <summary>Тип раздела строкой GUID — в том же виде, в каком его понимает PartitionKinds.</summary>
    public string? GptType { get; init; }

    public bool IsHidden { get; init; }
}

/// <summary>
/// Том с буквой и файловой системой. С разделом соединяется не по имени,
/// а по тому, на каком диске и с какого смещения он лежит: имена и буквы
/// в WinPE непостоянны, а смещение — свойство самого диска.
/// </summary>
public sealed class RawVolume
{
    public int DiskNumber { get; init; }
    public ulong StartingOffset { get; init; }
    public char? DriveLetter { get; init; }
    public string? FileSystem { get; init; }
    public string? Label { get; init; }
    public ulong SizeBytes { get; init; }
    public ulong FreeBytes { get; init; }
}

/// <summary>
/// Источник сведений о хранилище. За интерфейсом — чтобы сборка модели дисков
/// проверялась тестами на подделке, а не на живом железе.
/// </summary>
public interface IRawStorageSource
{
    IReadOnlyList<RawDisk> Disks(CancellationToken cancellationToken);

    IReadOnlyList<RawVolume> Volumes(CancellationToken cancellationToken);

    /// <summary>
    /// Буква диска, на котором работает текущая система. В WinPE это оперативный
    /// диск X:, у него нет места на физическом диске — и ни один диск не будет
    /// помечен системным, что и правильно.
    /// </summary>
    char? SystemDriveLetter();
}
