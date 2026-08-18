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

    /// <summary>
    /// Первый найденный носитель, либо ничего. Пометку дисков не трогает:
    /// пометить их надо все, а работаем мы с одним.
    /// </summary>
    public static MediaLocation? Find(IReadOnlyList<DiskInfo> disks, IFileSystemProbe probe)
    {
        var roots = new List<string>();
        foreach (var disk in disks)
        {
            foreach (var partition in disk.Partitions)
            {
                if (partition.DriveLetter is not null)
                {
                    roots.Add(string.Format(CultureInfo.InvariantCulture, "{0}:\\", partition.DriveLetter.Value));
                }
            }
        }

        return FindAmong(roots, probe);
    }

    /// <summary>
    /// То же самое, но по готовому списку корней. Нужно на старте: список
    /// томов известен сразу, а перечисление дисков идёт своим чередом
    /// и ждать его ради описи незачем.
    /// </summary>
    public static MediaLocation? FindAmong(IReadOnlyList<string> volumeRoots, IFileSystemProbe probe)
    {
        foreach (var root in volumeRoots)
        {
            if (probe.FileExists(Path.Combine(root, ManifestFileName)))
            {
                return new MediaLocation(root);
            }
        }

        return null;
    }
}
