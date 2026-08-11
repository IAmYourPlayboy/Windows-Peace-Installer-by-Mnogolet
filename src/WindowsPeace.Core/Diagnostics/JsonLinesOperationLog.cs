using System;
using System.Globalization;
using System.IO;
using System.Text;

namespace WindowsPeace.Core.Diagnostics;

/// <summary>
/// Журнал в файл: одна запись — одна строка JSON. Формат выбран потому,
/// что такой файл читается и человеком, и разбором, и дописывается без перечитывания.
/// Собственная сериализация вместо библиотеки — чтобы под net48 не тянуть зависимость.
/// </summary>
public sealed class JsonLinesOperationLog : IOperationLog, IDisposable
{
    private readonly object _gate = new();
    private readonly StreamWriter _writer;

    public JsonLinesOperationLog(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _writer = new StreamWriter(new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read),
            new UTF8Encoding(encoderShouldEmitUTF8Identifier: false))
        {
            AutoFlush = true,
        };
    }

    /// <summary>Путь журнала по умолчанию: рядом с приложением, чтобы работало и в WinPE.</summary>
    public static string DefaultPath(string baseDirectory)
        => Path.Combine(baseDirectory, "logs", "windows-peace.jsonl");

    public void Write(OperationRecord record)
    {
        var line = new StringBuilder()
            .Append('{')
            .Append("\"time\":\"").Append(record.StartedAt.ToString("o", CultureInfo.InvariantCulture)).Append("\",")
            .Append("\"component\":\"").Append(Escape(record.Component)).Append("\",")
            .Append("\"operation\":\"").Append(Escape(record.Operation)).Append("\",")
            .Append("\"durationMs\":").Append(((long)record.Duration.TotalMilliseconds).ToString(CultureInfo.InvariantCulture)).Append(',')
            .Append("\"outcome\":\"").Append(record.Outcome).Append('"');

        if (record.Reason is not null)
        {
            line.Append(",\"reason\":\"").Append(Escape(record.Reason)).Append('"');
        }

        line.Append('}');

        lock (_gate)
        {
            _writer.WriteLine(line.ToString());
        }
    }

    private static string Escape(string value)
    {
        var builder = new StringBuilder(value.Length + 8);
        foreach (var c in value)
        {
            switch (c)
            {
                case '"': builder.Append("\\\""); break;
                case '\\': builder.Append("\\\\"); break;
                case '\n': builder.Append("\\n"); break;
                case '\r': builder.Append("\\r"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (c < ' ')
                    {
                        builder.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                    }
                    else
                    {
                        builder.Append(c);
                    }
                    break;
            }
        }

        return builder.ToString();
    }

    public void Dispose()
    {
        lock (_gate)
        {
            _writer.Dispose();
        }
    }
}
