using System;
using System.Collections.Generic;
using WindowsPeace.Core.Diagnostics;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Журнал нужен именно тогда, когда что-то пошло не так, — то есть после
/// перезагрузки. Оперативный диск WinPE её не переживает, поэтому выбор места
/// не мелочь: от него зависит, останется ли хоть что-то от неудачного запуска.
/// </summary>
public class LogLocationResolverTests
{
    private sealed class Probe : IWritabilityProbe
    {
        private readonly HashSet<string> _writable;

        public Probe(params string[] writable) => _writable = new HashSet<string>(writable, StringComparer.OrdinalIgnoreCase);

        public bool CanWrite(string directory) => _writable.Contains(directory);
    }

    [Fact]
    public void Предпочтительное_место_выбирается_когда_туда_пишется()
    {
        var location = LogLocationResolver.Resolve(
            @"E:\WindowsPeace\logs", @"X:\WindowsPeace\logs", new Probe(@"E:\WindowsPeace\logs"));

        Assert.True(location.IsAvailable);
        Assert.False(location.IsTemporary);
        Assert.Equal(@"E:\WindowsPeace\logs", location.Directory);
    }

    [Fact]
    public void Откат_помечается_временным_и_объясняется()
    {
        var location = LogLocationResolver.Resolve(
            @"E:\WindowsPeace\logs", @"X:\WindowsPeace\logs", new Probe(@"X:\WindowsPeace\logs"));

        Assert.True(location.IsAvailable);
        Assert.True(location.IsTemporary);
        Assert.Equal(@"X:\WindowsPeace\logs", location.Directory);
        Assert.Contains("перезагрузк", location.Reason, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Когда_писать_некуда_журнала_нет_но_программа_не_падает()
    {
        var location = LogLocationResolver.Resolve(@"E:\logs", @"X:\logs", new Probe());

        Assert.False(location.IsAvailable);
        Assert.NotEmpty(location.Reason);
    }

    [Fact]
    public void Носитель_проверяется_раньше_оперативного_диска()
    {
        var asked = new List<string>();
        var location = LogLocationResolver.Resolve(
            @"E:\logs", @"X:\logs", new RecordingProbe(asked, @"E:\logs", @"X:\logs"));

        // Оба места доступны, и выбрать надо носитель: он переживёт перезагрузку.
        Assert.Equal(@"E:\logs", location.Directory);
        Assert.Equal(@"E:\logs", asked[0]);
    }

    private sealed class RecordingProbe : IWritabilityProbe
    {
        private readonly List<string> _asked;
        private readonly HashSet<string> _writable;

        public RecordingProbe(List<string> asked, params string[] writable)
        {
            _asked = asked;
            _writable = new HashSet<string>(writable, StringComparer.OrdinalIgnoreCase);
        }

        public bool CanWrite(string directory)
        {
            _asked.Add(directory);
            return _writable.Contains(directory);
        }
    }
}
