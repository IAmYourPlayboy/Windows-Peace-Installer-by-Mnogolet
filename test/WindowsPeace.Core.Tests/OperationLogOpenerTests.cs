using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using WindowsPeace.Core.Diagnostics;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Журнал заводится ровно затем, чтобы разбирать по нему неудачи, — и остаться
/// без него нельзя. Поэтому мест несколько, а в каждом месте пробуется несколько
/// имён: занятый файл не повод молчать весь запуск. Решение автора: человеку
/// про журнал не рассказываем, просто всегда находим, куда писать.
/// </summary>
public class OperationLogOpenerTests
{
    /// <summary>Подставной открыватель: открывается только то, что ему разрешили.</summary>
    private sealed class FakeOpener : ILogFileOpener
    {
        private readonly HashSet<string> _allowed;

        public FakeOpener(params string[] allowed)
            => _allowed = new HashSet<string>(allowed, StringComparer.OrdinalIgnoreCase);

        public List<string> Tried { get; } = new();

        public LogOpenResult Open(string path)
        {
            Tried.Add(path);
            return _allowed.Contains(path)
                ? LogOpenResult.Opened(NullOperationLog.Instance)
                : LogOpenResult.Refused("файл занят");
        }
    }

    private static string Name(int attempt) => JsonLinesOperationLog.FileNameFor(attempt);

    [Fact]
    public void Журнал_открывается_в_первом_же_месте_если_туда_пишется()
    {
        var opener = new FakeOpener(Path.Combine(@"E:\logs", Name(1)));

        var opened = OperationLogOpener.Open(new[] { @"E:\logs", @"X:\logs" }, opener);

        Assert.True(opened.IsWriting);
        Assert.Equal(Path.Combine(@"E:\logs", Name(1)), opened.Path);
        Assert.Empty(opened.Refusals);
    }

    [Fact]
    public void Занятый_файл_не_оставляет_запуск_без_журнала()
    {
        // Так бывает, когда мастер запущен во второй раз: первый держит файл.
        var opener = new FakeOpener(Path.Combine(@"E:\logs", Name(2)));

        var opened = OperationLogOpener.Open(new[] { @"E:\logs" }, opener);

        Assert.True(opened.IsWriting);
        Assert.Equal(Path.Combine(@"E:\logs", Name(2)), opened.Path);
    }

    [Fact]
    public void Недоступное_место_уступает_следующему()
    {
        var opener = new FakeOpener(Path.Combine(@"X:\logs", Name(1)));

        var opened = OperationLogOpener.Open(new[] { @"E:\logs", @"X:\logs" }, opener);

        Assert.Equal(Path.Combine(@"X:\logs", Name(1)), opened.Path);
        Assert.Equal(@"E:\logs", Path.GetDirectoryName(opener.Tried[0]));
    }

    [Fact]
    public void Места_перебираются_по_порядку_а_не_как_придётся()
    {
        var opener = new FakeOpener();

        OperationLogOpener.Open(new[] { @"E:\logs", @"X:\logs", @"T:\logs" }, opener);

        var order = opener.Tried.Select(Path.GetDirectoryName).Distinct().ToList();
        Assert.Equal(new[] { @"E:\logs", @"X:\logs", @"T:\logs" }, order);
    }

    [Fact]
    public void Когда_не_вышло_нигде_программа_не_падает_а_журнал_пустышка()
    {
        var opened = OperationLogOpener.Open(new[] { @"E:\logs" }, new FakeOpener());

        Assert.False(opened.IsWriting);
        Assert.Equal(string.Empty, opened.Path);
        Assert.Same(NullOperationLog.Instance, opened.Log);

        // Запись всё равно должна проходить: проверок на null в местах вызова нет.
        opened.Log.Write(new OperationRecord(
            DateTimeOffset.Now, "Тест", "Запись в никуда", TimeSpan.Zero, OperationOutcome.Success, null));
    }

    [Fact]
    public void Отказы_запоминаются_чтобы_попасть_в_журнал()
    {
        var opener = new FakeOpener(Path.Combine(@"X:\logs", Name(1)));

        var opened = OperationLogOpener.Open(new[] { @"E:\logs", @"X:\logs" }, opener);

        Assert.NotEmpty(opened.Refusals);
        // IndexOf, а не Contains: под net48 у Contains нет перегрузки со сравнением,
        // а ядро собирается под обе цели.
        Assert.Contains(opened.Refusals, r =>
            r.IndexOf(@"E:\logs", StringComparison.OrdinalIgnoreCase) >= 0 &&
            r.IndexOf("занят", StringComparison.OrdinalIgnoreCase) >= 0);
    }

    [Fact]
    public void Имя_второй_попытки_отличается_от_первого_а_не_повторяет_его()
    {
        Assert.Equal(JsonLinesOperationLog.FileName, Name(1));
        Assert.NotEqual(Name(1), Name(2));
        Assert.EndsWith(".jsonl", Name(2), StringComparison.Ordinal);
    }

    [Fact]
    public void Мест_для_журнала_несколько_и_первое_из_них_рядом_с_приложением()
    {
        var places = LogPlaces.InOrder(@"E:\WindowsPeace");

        Assert.True(places.Count >= 2, "Одно место — это не запас, а обещание остаться без журнала.");
        Assert.Equal(Path.Combine(@"E:\WindowsPeace", JsonLinesOperationLog.FolderName), places[0]);
    }
}
