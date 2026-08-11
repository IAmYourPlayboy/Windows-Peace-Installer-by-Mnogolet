using System;
using System.IO;
using System.Text.Json;
using WindowsPeace.Core.Diagnostics;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Журнал — основа выгрузки для поддержки, поэтому проверяется, что он пишет
/// разбираемый JSON, а не строку, похожую на JSON. См. docs/ARCHITECTURE.md, раздел 9.
/// </summary>
public class JsonLinesOperationLogTests : IDisposable
{
    private readonly string _directory =
        Path.Combine(Path.GetTempPath(), "windows-peace-tests", Guid.NewGuid().ToString("N"));

    private string LogPath => Path.Combine(_directory, "logs", "windows-peace.jsonl");

    private static OperationRecord Record(
        string component = "Storage",
        string operation = "Перечисление дисков",
        OperationOutcome outcome = OperationOutcome.Success,
        string? reason = null)
        => new(DateTimeOffset.Now, component, operation, TimeSpan.FromMilliseconds(1234), outcome, reason);

    [Fact]
    public void Каталог_создаётся_сам_если_его_не_было()
    {
        using (var log = new JsonLinesOperationLog(LogPath))
        {
            log.Write(Record());
        }

        Assert.True(File.Exists(LogPath));
    }

    [Fact]
    public void Одна_операция_даёт_одну_строку_разбираемого_JSON()
    {
        using (var log = new JsonLinesOperationLog(LogPath))
        {
            log.Write(Record(outcome: OperationOutcome.Success));
        }

        var line = Assert.Single(File.ReadAllLines(LogPath));
        using var json = JsonDocument.Parse(line);

        Assert.Equal("Storage", json.RootElement.GetProperty("component").GetString());
        Assert.Equal("Перечисление дисков", json.RootElement.GetProperty("operation").GetString());
        Assert.Equal("Success", json.RootElement.GetProperty("outcome").GetString());
        Assert.Equal(1234, json.RootElement.GetProperty("durationMs").GetInt64());
    }

    [Fact]
    public void Причина_пишется_только_когда_она_есть()
    {
        using (var log = new JsonLinesOperationLog(LogPath))
        {
            log.Write(Record(outcome: OperationOutcome.Success));
            log.Write(Record(outcome: OperationOutcome.Failure, reason: "WMI недоступно"));
        }

        var lines = File.ReadAllLines(LogPath);

        using var success = JsonDocument.Parse(lines[0]);
        Assert.False(success.RootElement.TryGetProperty("reason", out _));

        using var failure = JsonDocument.Parse(lines[1]);
        Assert.Equal("WMI недоступно", failure.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public void Кавычки_косые_черты_и_переводы_строк_не_ломают_разбор()
    {
        const string Nasty = "путь \"C:\\\" \r\n с переводом\tи табуляцией";

        using (var log = new JsonLinesOperationLog(LogPath))
        {
            log.Write(Record(outcome: OperationOutcome.Failure, reason: Nasty));
        }

        var line = Assert.Single(File.ReadAllLines(LogPath));
        using var json = JsonDocument.Parse(line);

        Assert.Equal(Nasty, json.RootElement.GetProperty("reason").GetString());
    }

    [Fact]
    public void Управляющий_символ_не_попадает_в_файл_сырым()
    {
        using (var log = new JsonLinesOperationLog(LogPath))
        {
            log.Write(Record(operation: "звонок \u0007 в конце"));
        }

        var line = Assert.Single(File.ReadAllLines(LogPath));
        Assert.DoesNotContain('\u0007', line);

        using var json = JsonDocument.Parse(line);
        Assert.Equal("звонок \u0007 в конце", json.RootElement.GetProperty("operation").GetString());
    }

    [Fact]
    public void Второй_запуск_дописывает_журнал_а_не_затирает_его()
    {
        using (var first = new JsonLinesOperationLog(LogPath))
        {
            first.Write(Record(operation: "первый запуск"));
        }

        using (var second = new JsonLinesOperationLog(LogPath))
        {
            second.Write(Record(operation: "второй запуск"));
        }

        var lines = File.ReadAllLines(LogPath);

        Assert.Equal(2, lines.Length);
        Assert.Contains("первый запуск", lines[0], StringComparison.Ordinal);
        Assert.Contains("второй запуск", lines[1], StringComparison.Ordinal);
    }

    [Fact]
    public void Путь_по_умолчанию_лежит_рядом_с_приложением()
    {
        var path = JsonLinesOperationLog.DefaultPath(@"X:\peace");

        Assert.Equal(@"X:\peace\logs\windows-peace.jsonl", path);
    }

    public void Dispose()
    {
        if (Directory.Exists(_directory))
        {
            Directory.Delete(_directory, recursive: true);
        }
    }
}
