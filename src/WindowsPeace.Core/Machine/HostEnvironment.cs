using System.IO;

namespace WindowsPeace.Core.Machine;

/// <summary>
/// Сборка снимка среды. Признак WinPE — ключ MiniNT: обычная Windows его
/// не заводит, а предзагрузочная заводит всегда. Это надёжнее, чем гадать
/// по букве системного диска или по именам файлов.
/// </summary>
public static class HostEnvironment
{
    public const string MiniNtKey = @"SYSTEM\CurrentControlSet\Control\MiniNT";

    /// <summary>Обычное начертание Segoe UI. В образе WinPE его нет — есть жирное, курсив и светлое.</summary>
    public const string SegoeUiRegularFile = "segoeui.ttf";

    /// <summary>
    /// Оперативный диск WinPE. Сама среда живёт здесь, и буква эта постоянная —
    /// в отличие от букв носителя, которые каждый раз разные. Всё, что сюда
    /// записано, гибнет при перезагрузке.
    /// </summary>
    public const string RamDriveRoot = @"X:\";

    /// <summary>Папка Windows в предзагрузочной среде: там же, на оперативном диске.</summary>
    public const string RamDriveWindows = @"X:\Windows";

    public static EnvironmentSnapshot Describe(IEnvironmentReader reader) => new()
    {
        OsVersion = reader.OsVersion(),
        IsWindowsPe = reader.RegistryKeyExists(MiniNtKey),
        TotalMemoryBytes = reader.TotalMemoryBytes(),
        SegoeUiRegularPresent = reader.FileExists(
            Path.Combine(reader.WindowsDirectory(), "Fonts", SegoeUiRegularFile)),
        VolumeRoots = reader.VolumeRoots(),
    };
}
