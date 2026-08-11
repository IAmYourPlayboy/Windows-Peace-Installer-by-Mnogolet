using System;

namespace WindowsPeace.Core.Storage;

/// <summary>Назначение раздела, выведенное из типа GPT.</summary>
public enum PartitionKind
{
    Unknown = 0,
    EfiSystem,
    MicrosoftReserved,
    WindowsRecovery,
    BasicData,
}

/// <summary>Разбор типов GPT. Идентификаторы задокументированы Microsoft и не меняются.</summary>
public static class PartitionKinds
{
    private static readonly Guid EfiSystemGuid = new("c12a7328-f81f-11d2-ba4b-00a0c93ec93b");
    private static readonly Guid MicrosoftReservedGuid = new("e3c9e316-0b5c-4db8-817d-f92df00215ae");
    private static readonly Guid WindowsRecoveryGuid = new("de94bba4-06d1-4d40-a16a-bfd50179d6ac");
    private static readonly Guid BasicDataGuid = new("ebd0a0a2-b9e5-4433-87c0-68b6b72699c7");

    /// <summary>
    /// Переводит значение MSFT_Partition.GptType в назначение раздела.
    /// Неразобранное значение не считается ошибкой: диск мог быть размечен чем угодно.
    /// </summary>
    public static PartitionKind FromGptType(string? gptType)
    {
        if (string.IsNullOrWhiteSpace(gptType) || !Guid.TryParse(gptType, out var guid))
        {
            return PartitionKind.Unknown;
        }

        if (guid == EfiSystemGuid) return PartitionKind.EfiSystem;
        if (guid == MicrosoftReservedGuid) return PartitionKind.MicrosoftReserved;
        if (guid == WindowsRecoveryGuid) return PartitionKind.WindowsRecovery;
        if (guid == BasicDataGuid) return PartitionKind.BasicData;

        return PartitionKind.Unknown;
    }

    /// <summary>Служебный раздел — тот, который создаёт и обслуживает сама система.</summary>
    public static bool IsSystemService(PartitionKind kind)
        => kind is PartitionKind.EfiSystem or PartitionKind.MicrosoftReserved or PartitionKind.WindowsRecovery;
}
