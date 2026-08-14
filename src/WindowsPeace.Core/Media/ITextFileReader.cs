using System;
using System.IO;

namespace WindowsPeace.Core.Media;

/// <summary>
/// Чтение текстового файла. Отдельным интерфейсом, чтобы разбор описи
/// проверялся без диска: заводить настоящий носитель ради каждого теста
/// нельзя, а проверять разбор надо на всех его исходах.
/// </summary>
public interface ITextFileReader
{
    bool Exists(string path);

    string ReadAllText(string path);
}

/// <summary>Настоящее чтение с диска.</summary>
public sealed class FileTextReader : ITextFileReader
{
    public bool Exists(string path)
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

    public string ReadAllText(string path) => File.ReadAllText(path);
}
