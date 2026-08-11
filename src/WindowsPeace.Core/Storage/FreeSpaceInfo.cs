namespace WindowsPeace.Core.Storage;

/// <summary>Незанятый промежуток на диске. Не раздел: у него нет номера и файловой системы.</summary>
public sealed class FreeSpaceInfo
{
    public FreeSpaceInfo(ulong offset, ulong size)
    {
        Offset = offset;
        Size = size;
    }

    public ulong Offset { get; }
    public ulong Size { get; }
    public ulong End => Offset + Size;
}
