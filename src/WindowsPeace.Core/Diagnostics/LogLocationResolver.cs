using System;
using System.IO;

namespace WindowsPeace.Core.Diagnostics;

/// <summary>Может ли туда писать. Отдельным интерфейсом, чтобы проверялось тестом.</summary>
public interface IWritabilityProbe
{
    bool CanWrite(string directory);
}

/// <summary>
/// Настоящая проверка: пробным файлом, а не догадкой по признакам. Носитель
/// бывает защищён от записи, а по имени папки этого не видно.
/// </summary>
public sealed class RealWritabilityProbe : IWritabilityProbe
{
    public bool CanWrite(string directory)
    {
        try
        {
            Directory.CreateDirectory(directory);
            var probe = Path.Combine(directory, ".peace-write-probe");
            File.WriteAllText(probe, "1");
            File.Delete(probe);
            return true;
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
        catch (NotSupportedException)
        {
            return false;
        }
    }
}

/// <summary>Где будет лежать журнал и переживёт ли он перезагрузку.</summary>
public sealed class LogLocation
{
    public LogLocation(bool isAvailable, string directory, bool isTemporary, string reason)
    {
        IsAvailable = isAvailable;
        Directory = directory;
        IsTemporary = isTemporary;
        Reason = reason;
    }

    public bool IsAvailable { get; }

    public string Directory { get; }

    public bool IsTemporary { get; }

    public string Reason { get; }
}

/// <summary>
/// Журнал нужен именно тогда, когда что-то пошло не так, — то есть после
/// перезагрузки. Оперативный диск WinPE её не переживает, поэтому сначала
/// пробуем носитель и только потом отступаем.
/// </summary>
public static class LogLocationResolver
{
    public static LogLocation Resolve(string preferred, string fallback, IWritabilityProbe probe)
    {
        if (probe is null)
        {
            throw new ArgumentNullException(nameof(probe));
        }

        if (probe.CanWrite(preferred))
        {
            return new LogLocation(true, preferred, false, "Журнал лежит рядом с приложением.");
        }

        if (probe.CanWrite(fallback))
        {
            return new LogLocation(true, fallback, true,
                "Рядом с приложением писать не удалось. Журнал временный: он лежит в оперативной памяти и погибнет при перезагрузке.");
        }

        return new LogLocation(false, string.Empty, false,
            "Записать журнал некуда: ни рядом с приложением, ни на оперативном диске.");
    }
}
