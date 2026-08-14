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
