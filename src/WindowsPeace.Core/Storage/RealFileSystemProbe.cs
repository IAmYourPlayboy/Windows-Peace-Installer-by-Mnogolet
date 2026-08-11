using System;
using System.Collections.Generic;
using System.IO;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Обращения к настоящей файловой системе. Каждый вызов защищён от исключений:
/// недоступный или сбойный том не должен ронять перечисление целиком.
/// Пустого catch здесь нет — каждый перехват возвращает осмысленное значение.
/// </summary>
public sealed class RealFileSystemProbe : IFileSystemProbe
{
    public bool DirectoryExists(string path)
    {
        try
        {
            return Directory.Exists(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    public IReadOnlyList<string> EnumerateDirectories(string path)
    {
        try
        {
            return Directory.GetDirectories(path);
        }
        catch (IOException)
        {
            return Array.Empty<string>();
        }
        catch (UnauthorizedAccessException)
        {
            return Array.Empty<string>();
        }
    }
}
