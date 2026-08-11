using System.Collections.Generic;
using System.Threading;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Обращения к файловой системе спрятаны за интерфейсом, чтобы правила
/// определения содержимого проверялись тестами без настоящих дисков.
/// </summary>
public interface IFileSystemProbe
{
    bool DirectoryExists(string path);

    bool FileExists(string path);

    IReadOnlyList<string> EnumerateDirectories(string path);
}

/// <summary>Заполняет Content у разделов диска.</summary>
public interface IDiskContentInspector
{
    void Inspect(DiskInfo disk, CancellationToken cancellationToken);
}
