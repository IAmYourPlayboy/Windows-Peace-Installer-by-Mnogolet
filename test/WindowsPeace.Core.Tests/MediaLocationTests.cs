using System;
using System.Collections.Generic;
using System.IO;
using WindowsPeace.Core.Media;
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Носитель опознаётся по наличию описи, а чтение её содержимого — отдельное
/// дело с отдельным исходом. Разделение не формальное: испорченная опись
/// не делает носитель чужим, и предлагать установку на него всё равно нельзя.
/// </summary>
public class MediaLocationTests
{
    private sealed class FakeTextFiles : ITextFileReader
    {
        private readonly Dictionary<string, string> _files = new(StringComparer.OrdinalIgnoreCase);

        public FakeTextFiles Add(string path, string content)
        {
            _files[path] = content;
            return this;
        }

        public bool Exists(string path) => _files.ContainsKey(path);

        public string ReadAllText(string path) => _files[path];
    }

    /// <summary>Файл есть, но чтение отказывает: так ведёт себя вынутая флешка.</summary>
    private sealed class ThrowingTextFiles : ITextFileReader
    {
        private readonly string _path;
        private readonly string _message;

        public ThrowingTextFiles(string path, string message)
        {
            _path = path;
            _message = message;
        }

        public bool Exists(string path) => string.Equals(path, _path, StringComparison.OrdinalIgnoreCase);

        public string ReadAllText(string path) => throw new IOException(_message);
    }

    private static DiskInfo DiskWithLetters(params char[] letters)
    {
        var partitions = new List<PartitionInfo>();
        foreach (var letter in letters)
        {
            partitions.Add(TestDisks.Partition(letter: letter));
        }

        return TestDisks.Disk(partitions: partitions);
    }

    [Fact]
    public void Поиск_возвращает_корень_раздела_с_описью()
    {
        var disk = DiskWithLetters('C', 'E');
        var probe = new FakeFileSystem().AddFile(@"E:\windows-peace-media.json");

        var location = BootMediaLocator.Find(new[] { disk }, probe);

        Assert.NotNull(location);
        Assert.Equal(@"E:\", location!.Root);
        Assert.Equal(@"E:\windows-peace-media.json", location.ManifestPath);
    }

    [Fact]
    public void Когда_описи_нигде_нет_поиск_возвращает_ничего()
    {
        var disk = DiskWithLetters('C');

        Assert.Null(BootMediaLocator.Find(new[] { disk }, new FakeFileSystem()));
    }

    [Fact]
    public void Раздел_без_буквы_поиску_не_мешает()
    {
        // В WinPE букв может не быть вовсе у половины разделов — у скрытого
        // загрузочного её нет никогда.
        var disk = TestDisks.Disk(partitions: new[]
        {
            TestDisks.Partition(letter: null),
            TestDisks.Partition(letter: 'E'),
        });
        var probe = new FakeFileSystem().AddFile(@"E:\windows-peace-media.json");

        var location = BootMediaLocator.Find(new[] { disk }, probe);

        Assert.Equal(@"E:\", location!.Root);
    }

    [Fact]
    public void Поиск_по_корням_томов_даёт_тот_же_ответ_что_и_по_дискам()
    {
        // На старте список томов известен сразу, а перечисление дисков идёт
        // своим чередом. Ответ обязан совпадать, иначе мастер и экран дисков
        // будут считать носителем разное.
        var probe = new FakeFileSystem().AddFile(@"E:\windows-peace-media.json");

        var byDisks = BootMediaLocator.Find(new[] { DiskWithLetters('C', 'E') }, probe);
        var byRoots = BootMediaLocator.FindAmong(new[] { @"C:\", @"E:\" }, probe);

        Assert.Equal(byDisks!.Root, byRoots!.Root);
    }

    [Fact]
    public void Найденный_носитель_читает_свою_опись()
    {
        var location = new MediaLocation(@"E:\");
        var files = new FakeTextFiles().Add(location.ManifestPath, """
        { "schemaVersion": 1, "buildId": "a", "createdUtc": "2026-08-14T12:00:00Z",
          "recipes": [ { "id": "x", "name": "Икс", "recipeFile": "recipes\\x.json",
                         "image": { "file": "sources\\install.wim", "index": 1 } } ] }
        """);

        var result = location.Load(files);

        Assert.Equal(MediaManifestStatus.Ok, result.Status);
        Assert.Equal("Икс", result.Manifest!.Recipes[0].Name);
    }

    [Fact]
    public void Опись_исчезнувшая_между_поиском_и_чтением_объясняется()
    {
        var location = new MediaLocation(@"E:\");

        var result = location.Load(new FakeTextFiles());

        Assert.Equal(MediaManifestStatus.Damaged, result.Status);
        Assert.NotEmpty(result.Message);
    }

    /// <summary>
    /// Отказ файловой системы приходит на языке Windows и бывает по-английски.
    /// Человеку показывается объяснение своими словами, а сам отказ уходит
    /// подробностью — по ней потом и разбираются.
    /// </summary>
    [Fact]
    public void Отказ_чтения_объясняется_словами_а_причина_идёт_подробностью()
    {
        var location = new MediaLocation(@"E:\");

        var result = location.Load(new ThrowingTextFiles(location.ManifestPath, "Device not ready. 0x80070015"));

        Assert.Equal(MediaManifestStatus.Damaged, result.Status);
        Assert.DoesNotContain("0x", result.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("0x80070015", result.Detail!, StringComparison.Ordinal);
    }

    [Fact]
    public void Имя_описи_у_локатора_и_у_раскладки_одно_и_то_же()
    {
        // Разойдутся — носитель перестанет опознаваться, то есть попадёт
        // в список дисков, доступных под форматирование. Ломаться должна сборка.
        Assert.Equal(MediaLayout.ManifestFileName, BootMediaLocator.ManifestFileName);
    }
}
