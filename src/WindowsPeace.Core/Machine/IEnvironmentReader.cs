using System.Collections.Generic;

namespace WindowsPeace.Core.Machine;

/// <summary>
/// Откуда берутся сведения о машине. Отдельно от разбора, чтобы разбор
/// проверялся тестом: в тесте нельзя ни завести ключ реестра, ни добавить память.
/// </summary>
public interface IEnvironmentReader
{
    bool RegistryKeyExists(string path);

    bool FileExists(string path);

    string OsVersion();

    ulong TotalMemoryBytes();

    IReadOnlyList<string> VolumeRoots();

    string WindowsDirectory();
}
