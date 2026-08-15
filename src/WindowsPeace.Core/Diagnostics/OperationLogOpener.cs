using System;
using System.Collections.Generic;
using System.IO;
using WindowsPeace.Core.Machine;
using WindowsPeace.Core.Media;

namespace WindowsPeace.Core.Diagnostics;

/// <summary>Открывает файл журнала. Отказ возвращается словами, а не исключением.</summary>
public interface ILogFileOpener
{
    LogOpenResult Open(string path);
}

/// <summary>Чем кончилась попытка открыть один файл журнала.</summary>
public sealed class LogOpenResult
{
    private LogOpenResult(IOperationLog? log, string? refusal)
    {
        Log = log;
        Refusal = refusal;
    }

    public IOperationLog? Log { get; }

    /// <summary>Почему не вышло. Заполнено ровно тогда, когда журнала нет.</summary>
    public string? Refusal { get; }

    public static LogOpenResult Opened(IOperationLog log) => new(log, null);

    public static LogOpenResult Refused(string reason) => new(null, reason);
}

/// <summary>Настоящее открытие файла: тот же журнал, но отказы не летят наружу.</summary>
public sealed class JsonLinesLogOpener : ILogFileOpener
{
    public LogOpenResult Open(string path)
    {
        try
        {
            return LogOpenResult.Opened(new JsonLinesOperationLog(path));
        }
        catch (IOException error)
        {
            // Сюда же приходит занятый файл, отсутствующий диск и полный носитель.
            return LogOpenResult.Refused(error.Message);
        }
        catch (UnauthorizedAccessException error)
        {
            return LogOpenResult.Refused(error.Message);
        }
        catch (NotSupportedException error)
        {
            return LogOpenResult.Refused(error.Message);
        }
        catch (ArgumentException error)
        {
            return LogOpenResult.Refused(error.Message);
        }
    }
}

/// <summary>Открытый журнал: сам приёмник записей, его путь и что по дороге не вышло.</summary>
public sealed class OpenedLog : IDisposable
{
    public OpenedLog(IOperationLog log, string path, IReadOnlyList<string> refusals)
    {
        Log = log;
        Path = path;
        Refusals = refusals;
    }

    /// <summary>Куда писать. Никогда не null: в худшем случае это журнал-пустышка.</summary>
    public IOperationLog Log { get; }

    /// <summary>Путь к файлу. Пусто, когда записать не вышло нигде.</summary>
    public string Path { get; }

    public bool IsWriting => Path.Length > 0;

    /// <summary>
    /// Места, которые не подошли, и почему. Уходит первыми записями в тот журнал,
    /// который всё-таки открылся: иначе о причине отказа не узнает никто.
    /// </summary>
    public IReadOnlyList<string> Refusals { get; }

    public void Dispose()
    {
        if (Log is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

/// <summary>
/// Где вести журнал. Мест несколько, и в каждом пробуется несколько имён.
///
/// Раньше место выбиралось пробным файлом, а открывалось потом — и между
/// проверкой и открытием файл успевал стать занятым; запуск оставался
/// без журнала целиком. Теперь проверка и есть открытие: открылось — пишем,
/// не открылось — идём дальше. Человеку об этом не сообщается ничего: журнал
/// нужен нам, а не ему, и разбираться в наших бедах его дело последнее.
/// </summary>
public static class OperationLogOpener
{
    /// <summary>
    /// Сколько имён пробовать в одной папке. Занятое имя означает, что рядом
    /// работает другой наш запуск; больше горстки их не бывает, а если все заняты —
    /// с этим местом что-то не так, и разумнее сменить место, чем перебирать имена.
    /// </summary>
    public const int NamesPerPlace = 5;

    public static OpenedLog Open(IReadOnlyList<string> places, ILogFileOpener opener)
    {
        if (places is null)
        {
            throw new ArgumentNullException(nameof(places));
        }

        if (opener is null)
        {
            throw new ArgumentNullException(nameof(opener));
        }

        var refusals = new List<string>();

        foreach (var place in places)
        {
            for (var attempt = 1; attempt <= NamesPerPlace; attempt++)
            {
                var path = Path.Combine(place, JsonLinesOperationLog.FileNameFor(attempt));
                var result = opener.Open(path);

                if (result.Log is not null)
                {
                    return new OpenedLog(result.Log, path, refusals);
                }

                refusals.Add(path + " — " + result.Refusal);
            }
        }

        return new OpenedLog(NullOperationLog.Instance, string.Empty, refusals);
    }
}

/// <summary>
/// Места для журнала по порядку предпочтения. Одно на весь проект: тем же
/// порядком журнал будут искать и Agent, и Studio, и человек, которому мы
/// скажем, где смотреть.
/// </summary>
public static class LogPlaces
{
    public static IReadOnlyList<string> InOrder(string appDirectory)
        => new[]
        {
            // Рядом с приложением — единственное место, которое переживёт
            // перезагрузку: оперативный диск WinPE исчезает вместе с ней.
            Path.Combine(appDirectory, JsonLinesOperationLog.FolderName),

            // Оперативный диск WinPE. Носитель бывает защищён от записи.
            Path.Combine(HostEnvironment.RamDriveRoot, MediaLayout.AppFolderName, JsonLinesOperationLog.FolderName),

            // Временная папка системы. На обычной Windows она есть всегда,
            // в WinPE лежит на том же оперативном диске — хуже не будет.
            Path.Combine(Path.GetTempPath(), MediaLayout.AppFolderName, JsonLinesOperationLog.FolderName),
        };
}
