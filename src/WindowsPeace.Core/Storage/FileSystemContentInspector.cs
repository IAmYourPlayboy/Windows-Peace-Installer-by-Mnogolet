using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;

namespace WindowsPeace.Core.Storage;

/// <summary>Определяет содержимое разделов через файловую систему.</summary>
public sealed class FileSystemContentInspector : IDiskContentInspector
{
    private static readonly string[] ServiceProfiles =
    {
        "Default", "Default User", "Public", "All Users",
    };

    private readonly IFileSystemProbe _probe;

    public FileSystemContentInspector(IFileSystemProbe probe) => _probe = probe;

    public void Inspect(DiskInfo disk, CancellationToken cancellationToken)
    {
        foreach (var partition in disk.Partitions)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                partition.Content = PartitionContent.NotInspected("Проверка прервана");
                continue;
            }

            partition.Content = InspectPartition(partition);
        }
    }

    private PartitionContent InspectPartition(PartitionInfo partition)
    {
        if (PartitionKinds.IsSystemService(partition.Kind))
        {
            return PartitionContent.NotInspected("Служебный раздел, содержимое не проверяется");
        }

        if (partition.DriveLetter is null)
        {
            return PartitionContent.NotInspected("У раздела нет буквы диска");
        }

        var root = string.Format(CultureInfo.InvariantCulture, "{0}:\\", partition.DriveLetter.Value);

        var windowsFound = HasWindows(root);
        var userFilesFound = HasUserProfiles(Path.Combine(root, "Users") + "\\");

        return new PartitionContent(windowsFound, windowsFound ? "Windows" : null, userFilesFound,
            inspected: true, notInspectedReason: null);
    }

    /// <summary>
    /// Ищет установленную Windows по двум признакам подряд.
    ///
    /// Куст реестра SYSTEM — признак основной: он есть только у настоящей установки
    /// и по нему на шаге В будут читаться издание и версия. Но он закрыт правами
    /// доступа, и под обычной учётной записью File.Exists отвечает false даже там,
    /// где Windows заведомо стоит. Проверено на живой машине 11.08.2026, см.
    /// docs/superpowers/notes/2026-08-10-disk-dump.md.
    ///
    /// Поэтому вторым идёт файл ядра: он есть в любой установленной Windows
    /// и читается кем угодно. Без него шаг А на обычной Windows молча отвечал бы
    /// «системы нет» и не показывал предупреждение о потере данных — то есть ровно
    /// тот отказ, который запрещён разделом 9 архитектуры.
    /// </summary>
    private bool HasWindows(string root)
        => _probe.FileExists(Path.Combine(root, @"Windows\System32\config\SYSTEM"))
           || _probe.FileExists(Path.Combine(root, @"Windows\System32\ntoskrnl.exe"));

    private bool HasUserProfiles(string usersPath)
    {
        if (!_probe.DirectoryExists(usersPath))
        {
            return false;
        }

        return _probe.EnumerateDirectories(usersPath)
            .Select(p => new DirectoryInfo(p.TrimEnd('\\')).Name)
            .Any(name => !ServiceProfiles.Contains(name, StringComparer.OrdinalIgnoreCase));
    }
}
