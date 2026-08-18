using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Core.Storage;

/// <summary>Что найдено на разделе. Заполняется отдельным проходом.</summary>
public sealed class PartitionContent
{
    public PartitionContent(bool windowsFound, string? windowsProductName, bool userFilesFound, bool inspected, string? notInspectedReason)
    {
        WindowsFound = windowsFound;
        WindowsProductName = windowsProductName;
        UserFilesFound = userFilesFound;
        Inspected = inspected;
        NotInspectedReason = notInspectedReason;
    }

    public static PartitionContent NotInspected(string reason) => new(false, null, false, false, reason);

    public bool WindowsFound { get; }
    public string? WindowsProductName { get; }
    public bool UserFilesFound { get; }
    public bool Inspected { get; }
    public string? NotInspectedReason { get; }
}

/// <summary>Раздел диска.</summary>
public sealed class PartitionInfo
{
    public PartitionInfo(
        int number,
        ulong offset,
        ulong size,
        PartitionKind kind,
        char? driveLetter,
        bool isSystem,
        bool isHidden,
        VolumeInfo? volume)
    {
        Number = number;
        Offset = offset;
        Size = size;
        Kind = kind;
        DriveLetter = driveLetter;
        IsSystem = isSystem;
        IsHidden = isHidden;
        Volume = volume;
        Content = PartitionContent.NotInspected(
            CoreLocalization.Localization.Current[CoreLocalization.Keys.Content.NotInspected.Pending]);
    }

    public int Number { get; }
    public ulong Offset { get; }
    public ulong Size { get; }
    public ulong End => Offset + Size;
    public PartitionKind Kind { get; }
    public char? DriveLetter { get; }
    public bool IsSystem { get; }
    public bool IsHidden { get; }
    public VolumeInfo? Volume { get; }

    /// <summary>Заполняется инспектором содержимого. До этого — «не проверено».</summary>
    public PartitionContent Content { get; internal set; }
}
