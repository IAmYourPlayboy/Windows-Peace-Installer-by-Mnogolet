using System.Collections.Generic;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Core.Tests;

/// <summary>Сборка дисков для тестов. Живое железо здесь не участвует.</summary>
internal static class TestDisks
{
    public const ulong Gib = 1024UL * 1024UL * 1024UL;

    /// <summary>Имя по умолчанию. Оно же модель: у настоящего диска это одна и та же строка.</summary>
    public const string DefaultModel = "Тестовый диск";

    public static DiskIdentity Identity(
        string? serial = "SN-1",
        ulong size = 500 * Gib,
        string model = DefaultModel,
        BusType bus = BusType.Nvme,
        string? gptGuid = null)
        => DiskIdentity.Create(serial, null, null, null, gptGuid, model, size, bus);

    public static PartitionInfo Partition(
        int number = 1,
        ulong offset = 1048576UL,
        ulong size = 100 * Gib,
        PartitionKind kind = PartitionKind.BasicData,
        char? letter = 'C',
        VolumeInfo? volume = null)
        => new(number, offset, size, kind, letter, isSystem: false, isHidden: false, volume: volume);

    public static DiskInfo Disk(
        string? serial = "SN-1",
        ulong size = 500 * Gib,
        bool isSystem = false,
        bool isBoot = false,
        bool isOffline = false,
        bool isReadOnly = false,
        bool isRemovable = false,
        bool isMedia = false,
        IReadOnlyList<PartitionInfo>? partitions = null,
        string? probeError = null,
        string model = DefaultModel,
        BusType bus = BusType.Nvme,
        MediaKind media = MediaKind.Ssd,
        string? gptGuid = null)
    {
        var actualPartitions = partitions ?? new List<PartitionInfo>();
        var disk = new DiskInfo(
            Identity(serial, size, model, bus, gptGuid),
            number: 0,
            friendlyName: model,
            media: media,
            partitionStyle: PartitionStyle.Gpt,
            isSystem: isSystem,
            isBoot: isBoot,
            isOffline: isOffline,
            isReadOnly: isReadOnly,
            isRemovable: isRemovable,
            partitions: actualPartitions,
            freeSpaces: FreeSpaceCalculator.Calculate(size, actualPartitions),
            probeError: probeError);

        disk.IsWindowsPeaceMedia = isMedia;
        return disk;
    }

    public static void SetContent(PartitionInfo partition, bool windows = false, bool userFiles = false)
        => partition.Content = new PartitionContent(windows, windows ? "Windows 11 Pro" : null, userFiles, inspected: true, notInspectedReason: null);
}
