namespace WindowsPeace.Core.Storage;

/// <summary>Том на разделе. Отсутствует, если раздел не смонтирован.</summary>
public sealed class VolumeInfo
{
    public VolumeInfo(string? fileSystem, string? label, ulong sizeBytes, ulong freeBytes)
    {
        FileSystem = fileSystem;
        Label = label;
        SizeBytes = sizeBytes;
        FreeBytes = freeBytes;
    }

    public string? FileSystem { get; }
    public string? Label { get; }
    public ulong SizeBytes { get; }
    public ulong FreeBytes { get; }
    public ulong UsedBytes => SizeBytes > FreeBytes ? SizeBytes - FreeBytes : 0UL;
}
