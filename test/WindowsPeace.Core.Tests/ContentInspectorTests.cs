using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WindowsPeace.Core.Storage;
using Xunit;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Core.Tests;

internal sealed class FakeFileSystem : IFileSystemProbe
{
    private readonly HashSet<string> _directories = new(StringComparer.OrdinalIgnoreCase);
    private readonly HashSet<string> _files = new(StringComparer.OrdinalIgnoreCase);

    public FakeFileSystem AddDirectory(string path)
    {
        _directories.Add(path);
        return this;
    }

    public FakeFileSystem AddFile(string path)
    {
        _files.Add(path);
        return this;
    }

    public bool DirectoryExists(string path) => _directories.Contains(path);

    public bool FileExists(string path) => _files.Contains(path);

    public IReadOnlyList<string> EnumerateDirectories(string path)
        => _directories.Where(d => d.StartsWith(path, StringComparison.OrdinalIgnoreCase)
                                   && d.Length > path.Length
                                   && !d.Substring(path.Length).TrimEnd('\\').Contains('\\'))
            .ToList();
}

[Collection(LocalizationCollection.Name)]
public class ContentInspectorTests
{
    private static DiskInfo DiskWith(PartitionInfo partition) => TestDisks.Disk(partitions: new[] { partition });

    [Fact]
    public void Windows_находится_по_кусту_реестра()
    {
        var fs = new FakeFileSystem().AddFile(@"C:\Windows\System32\config\SYSTEM");
        var partition = TestDisks.Partition(letter: 'C');
        var inspector = new FileSystemContentInspector(fs);

        inspector.Inspect(DiskWith(partition), CancellationToken.None);

        Assert.True(partition.Content.WindowsFound);
        Assert.True(partition.Content.Inspected);
    }

    /// <summary>
    /// Куст реестра SYSTEM закрыт правами доступа: под обычной учётной записью
    /// File.Exists отвечает false даже там, где Windows стоит. Проверено на живой
    /// машине 11.08.2026, см. docs/superpowers/notes/2026-08-10-disk-dump.md.
    /// Поэтому есть второй признак — файл ядра, читаемый кем угодно.
    /// </summary>
    [Fact]
    public void Windows_находится_по_ядру_когда_куст_реестра_недоступен()
    {
        var fs = new FakeFileSystem().AddFile(@"C:\Windows\System32\ntoskrnl.exe");
        var partition = TestDisks.Partition(letter: 'C');

        new FileSystemContentInspector(fs).Inspect(DiskWith(partition), CancellationToken.None);

        Assert.True(partition.Content.WindowsFound);
    }

    [Fact]
    public void Без_куста_реестра_Windows_не_считается_найденной()
    {
        var fs = new FakeFileSystem().AddDirectory(@"C:\Windows\");
        var partition = TestDisks.Partition(letter: 'C');

        new FileSystemContentInspector(fs).Inspect(DiskWith(partition), CancellationToken.None);

        Assert.False(partition.Content.WindowsFound);
    }

    [Fact]
    public void Пользовательские_папки_находятся_а_служебные_не_считаются()
    {
        var fs = new FakeFileSystem()
            .AddDirectory(@"C:\Users\")
            .AddDirectory(@"C:\Users\Default")
            .AddDirectory(@"C:\Users\Public")
            .AddDirectory(@"C:\Users\HugoBoss");
        var partition = TestDisks.Partition(letter: 'C');

        new FileSystemContentInspector(fs).Inspect(DiskWith(partition), CancellationToken.None);

        Assert.True(partition.Content.UserFilesFound);
    }

    [Fact]
    public void Только_служебные_папки_не_считаются_файлами_пользователя()
    {
        var fs = new FakeFileSystem()
            .AddDirectory(@"C:\Users\")
            .AddDirectory(@"C:\Users\Default")
            .AddDirectory(@"C:\Users\Public")
            .AddDirectory(@"C:\Users\All Users");
        var partition = TestDisks.Partition(letter: 'C');

        new FileSystemContentInspector(fs).Inspect(DiskWith(partition), CancellationToken.None);

        Assert.False(partition.Content.UserFilesFound);
    }

    [Fact]
    public void Раздел_без_буквы_помечается_как_непроверенный_с_причиной()
    {
        var partition = TestDisks.Partition(letter: null);

        new FileSystemContentInspector(new FakeFileSystem()).Inspect(DiskWith(partition), CancellationToken.None);

        Assert.False(partition.Content.Inspected);
        Assert.NotNull(partition.Content.NotInspectedReason);
    }

    [Fact]
    public void Служебные_разделы_не_проверяются_вовсе()
    {
        var partition = TestDisks.Partition(letter: 'S', kind: PartitionKind.EfiSystem);

        new FileSystemContentInspector(new FakeFileSystem()).Inspect(DiskWith(partition), CancellationToken.None);

        Assert.False(partition.Content.Inspected);
    }

    [Fact]
    public void Отмена_прекращает_проверку_и_не_бросает()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var partition = TestDisks.Partition(letter: 'C');

        new FileSystemContentInspector(new FakeFileSystem()).Inspect(DiskWith(partition), cts.Token);

        Assert.False(partition.Content.Inspected);
    }

    [Fact]
    public void Диск_с_описью_носителя_помечается_загрузочным()
    {
        var fs = new FakeFileSystem().AddFile(@"D:\windows-peace-media.json");
        var partition = TestDisks.Partition(letter: 'D');
        var disk = DiskWith(partition);

        BootMediaLocator.Mark(new[] { disk }, fs);

        Assert.True(disk.IsWindowsPeaceMedia);
    }

    [Fact]
    public void Без_описи_ни_один_диск_загрузочным_не_считается()
    {
        var partition = TestDisks.Partition(letter: 'D');
        var disk = DiskWith(partition);

        BootMediaLocator.Mark(new[] { disk }, new FakeFileSystem());

        Assert.False(disk.IsWindowsPeaceMedia);
    }

    [Fact]
    public void Причины_непроверенного_содержимого_переводятся_на_английский()
    {
        CoreLocalization.Localization.Current.Language = CoreLocalization.Language.English;
        try
        {
            var freshPartition = TestDisks.Partition();
            Assert.Equal("Contents not inspected yet", freshPartition.Content.NotInspectedReason);

            var noLetter = TestDisks.Partition(letter: null);
            new FileSystemContentInspector(new FakeFileSystem()).Inspect(DiskWith(noLetter), CancellationToken.None);
            Assert.Equal("The partition has no drive letter", noLetter.Content.NotInspectedReason);

            var service = TestDisks.Partition(letter: 'S', kind: PartitionKind.EfiSystem);
            new FileSystemContentInspector(new FakeFileSystem()).Inspect(DiskWith(service), CancellationToken.None);
            Assert.Equal("Service partition; contents are not inspected", service.Content.NotInspectedReason);

            using var cts = new CancellationTokenSource();
            cts.Cancel();
            var cancelled = TestDisks.Partition(letter: 'C');
            new FileSystemContentInspector(new FakeFileSystem()).Inspect(DiskWith(cancelled), cts.Token);
            Assert.Equal("Inspection cancelled", cancelled.Content.NotInspectedReason);
        }
        finally
        {
            CoreLocalization.Localization.Current.Language = CoreLocalization.Language.Russian;
        }
    }
}
