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

        var windowsFound = _probe.FileExists(Path.Combine(root, @"Windows\System32\config\SYSTEM"));
        var userFilesFound = HasUserProfiles(Path.Combine(root, "Users") + "\\");

        return new PartitionContent(windowsFound, windowsFound ? "Windows" : null, userFilesFound,
            inspected: true, notInspectedReason: null);
    }

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
