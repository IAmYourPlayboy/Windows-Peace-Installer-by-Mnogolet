using System.Collections.Generic;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Setup.Tests;

/// <summary>
/// Сборка дисков для тестов оболочки. Живое железо здесь не участвует.
///
/// Такой же сборщик есть в тестах ядра. Слить их в один нельзя: тот заполняет
/// содержимое разделов, а этот доступ открыт только тестам ядра. Общее здесь —
/// одни лишь открытые части модели, и повторяются они целиком.
/// </summary>
internal static class TestDisks
{
    public const ulong Gib = 1024UL * 1024UL * 1024UL;

    /// <summary>Имя по умолчанию. Оно же модель: у настоящего диска это одна и та же строка.</summary>
    public const string DefaultModel = "Тестовый диск";

    public static DiskInfo Disk(
        string? serial = "SN-1",
        ulong size = 500 * Gib,
        bool isSystem = false,
        IReadOnlyList<PartitionInfo>? partitions = null,
        string? probeError = null,
        string model = DefaultModel,
        BusType bus = BusType.Nvme,
        MediaKind media = MediaKind.Ssd)
    {
        var actualPartitions = partitions ?? new List<PartitionInfo>();

        return new DiskInfo(
            DiskIdentity.Create(serial, null, null, null, null, model, size, bus),
            number: 0,
            friendlyName: model,
            media: media,
            partitionStyle: PartitionStyle.Gpt,
            isSystem: isSystem,
            isBoot: false,
            isOffline: false,
            isReadOnly: false,
            isRemovable: false,
            partitions: actualPartitions,
            freeSpaces: FreeSpaceCalculator.Calculate(size, actualPartitions),
            probeError: probeError);
    }
}
