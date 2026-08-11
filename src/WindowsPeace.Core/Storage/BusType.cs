namespace WindowsPeace.Core.Storage;

/// <summary>Шина подключения диска. Значения совпадают с MSFT_Disk.BusType.</summary>
public enum BusType
{
    Unknown = 0,
    Scsi = 1,
    Atapi = 2,
    Ata = 3,
    Ieee1394 = 4,
    Ssa = 5,
    FibreChannel = 6,
    Usb = 7,
    Raid = 8,
    Iscsi = 9,
    Sas = 10,
    Sata = 11,
    Sd = 12,
    Mmc = 13,
    Max = 14,
    FileBackedVirtual = 15,
    StorageSpaces = 16,
    Nvme = 17,
}
