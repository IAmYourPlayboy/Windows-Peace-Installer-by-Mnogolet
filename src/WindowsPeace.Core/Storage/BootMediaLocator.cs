using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Находит носитель Windows Peace по описи в его корне. Именно по файлу,
/// а не по буквам, номерам или догадкам о том, откуда шла загрузка:
/// цена ошибки здесь — форматирование собственной флешки.
/// </summary>
public static class BootMediaLocator
{
    /// <summary>Имя описи. То же значение используется Studio при записи носителя.</summary>
    public const string ManifestFileName = "windows-peace-media.json";

    public static void Mark(IReadOnlyList<DiskInfo> disks, IFileSystemProbe probe)
    {
        foreach (var disk in disks)
        {
            disk.IsWindowsPeaceMedia = false;

            foreach (var partition in disk.Partitions)
            {
                if (partition.DriveLetter is null)
                {
                    continue;
                }

                var root = string.Format(CultureInfo.InvariantCulture, "{0}:\\", partition.DriveLetter.Value);
                if (probe.FileExists(Path.Combine(root, ManifestFileName)))
                {
                    disk.IsWindowsPeaceMedia = true;
                    break;
                }
            }
        }
    }
}
