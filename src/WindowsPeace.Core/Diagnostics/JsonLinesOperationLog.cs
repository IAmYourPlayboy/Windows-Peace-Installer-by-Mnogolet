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
    private readonly FileStream _stream;
    private readonly StreamWriter _writer;

    /// <summary>
    /// Отказала ли запись. Носитель могут вынуть на середине работы, а фоновая
    /// задача — дописать что-то уже после закрытия журнала. Ни то, ни другое
    /// не должно ронять мастера: он ставит человеку систему, а журнал только
    /// рассказывает, как идут дела.
    /// </summary>
    private bool _broken;

    public JsonLinesOperationLog(string path)
    {
        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        _stream = new FileStream(path, FileMode.Append, FileAccess.Write, FileShare.Read);
        _writer = new StreamWriter(_stream, new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
    }

    /// <summary>Папка журнала. Всегда рядом с тем, чей это журнал.</summary>
    public const string FolderName = "logs";

    /// <summary>Имя файла журнала. Одно на весь проект: его ищут и люди, и оснастка.</summary>
    public const string FileName = "windows-peace.jsonl";

    /// <summary>Путь журнала по умолчанию: рядом с приложением, чтобы работало и в WinPE.</summary>
    public static string DefaultPath(string baseDirectory)
        => Path.Combine(baseDirectory, FolderName, FileName);

    /// <summary>
    /// Имя журнала для попытки с этим номером. Первая попытка — обычное имя,
    /// дальше с номером: занятый файл (рядом работает второй наш запуск)
    /// не повод остаться без журнала на весь запуск.
    /// </summary>
    public static string FileNameFor(int attempt)
        => attempt <= 1
            ? FileName
            : Path.GetFileNameWithoutExtension(FileName)
              + "-" + attempt.ToString(CultureInfo.InvariantCulture)
              + Path.GetExtension(FileName);

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
            if (_broken)
            {
                return;
            }

            try
            {
                _writer.WriteLine(line.ToString());

                // Запись доводится до самого носителя, а не до кэша Windows.
                // Журнал нужен как раз тогда, когда машину обесточили, она зависла
                // или её выключили кнопкой: всё, что осталось в кэше, в этот момент
                // пропадает. Один раз так уже пропала единственная запись из WinPE —
                // на её месте в файле оказались нули.
                _writer.Flush();
                _stream.Flush(flushToDisk: true);
            }
            catch (Exception error) when (IsWritingFailure(error))
            {
                // Сказать об этом некому и нечем: единственное место, куда мы умеем
                // говорить, — этот самый файл. Поэтому журнал молча замолкает,
                // а мастер продолжает работу. Записи, дошедшие до отказа, остаются
                // на носителе и показывают, до какого места всё шло хорошо.
                _broken = true;
            }
        }
    }

    /// <summary>
    /// Отказ самой записи, а не дефект в коде. Носитель вынули, место кончилось,
    /// журнал уже закрыт фоновой задачей — всё это переживается молча. Остальное
    /// (например, ошибка в самой сериализации) поднимается выше: это наш дефект,
    /// и прятать его нельзя.
    /// </summary>
    private static bool IsWritingFailure(Exception error)
        => error is IOException
        || error is UnauthorizedAccessException
        || error is ObjectDisposedException;

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
            _stream.Dispose();
        }
    }
}
