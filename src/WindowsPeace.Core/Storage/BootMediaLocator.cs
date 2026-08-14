using System.Collections.Generic;
using System.Globalization;
using System.IO;
using WindowsPeace.Core.Media;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Находит носитель Windows Peace по описи в его корне. Именно по файлу,
/// а не по буквам, номерам или догадкам о том, откуда шла загрузка:
/// цена ошибки здесь — форматирование собственной флешки.
/// </summary>
public static class BootMediaLocator
{
    /// <summary>Имя описи. Живёт в раскладке носителя, здесь только ссылка на неё.</summary>
    public const string ManifestFileName = MediaLayout.ManifestFileName;

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
