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

    /// <summary>Сколько памяти машины свободно прямо сейчас.</summary>
    ulong AvailableMemoryBytes();

    /// <summary>Сколько занимает сам мастер прямо сейчас.</summary>
    ulong ProcessMemoryBytes();

    IReadOnlyList<string> VolumeRoots();

    string WindowsDirectory();
}
