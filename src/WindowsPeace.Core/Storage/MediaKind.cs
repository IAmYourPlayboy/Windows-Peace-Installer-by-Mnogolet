namespace WindowsPeace.Core.Storage;

/// <summary>Тип носителя. Значения совпадают с MSFT_PhysicalDisk.MediaType.</summary>
public enum MediaKind
{
    Unspecified = 0,
    Hdd = 3,
    Ssd = 4,
    Scm = 5,
}
