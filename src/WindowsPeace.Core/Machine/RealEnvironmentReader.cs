using System;
using System.Collections.Generic;
using System.IO;
using System.Security;
using Microsoft.Win32;
using WindowsPeace.Core.Diagnostics;

namespace WindowsPeace.Core.Machine;

/// <summary>
/// Настоящие сведения о машине.
///
/// Снимок среды не имеет права уронить старт: он делается ровно затем, чтобы
/// было что почитать, когда всё плохо. Поэтому каждый вызов защищён — но
/// не молча: то, что не удалось прочесть, объясняется записью в журнале.
/// Иначе в снимке появились бы нули, неотличимые от честного «памяти нет».
/// </summary>
public sealed class RealEnvironmentReader : IEnvironmentReader
{
    private readonly IOperationLog _log;

    public RealEnvironmentReader(IOperationLog log)
        => _log = log ?? throw new ArgumentNullException(nameof(log));

    public bool RegistryKeyExists(string path)
    {
        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(path);
            return key is not null;
        }
        catch (SecurityException error)
        {
            Complain("Чтение реестра", path, error);
            return false;
        }
        catch (UnauthorizedAccessException error)
        {
            Complain("Чтение реестра", path, error);
            return false;
        }
        catch (IOException error)
        {
            Complain("Чтение реестра", path, error);
            return false;
        }
    }

    public bool FileExists(string path)
    {
        try
        {
            return File.Exists(path);
        }
        catch (IOException error)
        {
            Complain("Проверка файла", path, error);
            return false;
        }
        catch (UnauthorizedAccessException error)
        {
            Complain("Проверка файла", path, error);
            return false;
        }
    }

    public string OsVersion() => System.Environment.OSVersion.VersionString;

    public ulong TotalMemoryBytes()
    {
        var status = default(NativeMethods.MemoryStatusEx);
        status.Length = (uint)System.Runtime.InteropServices.Marshal.SizeOf(status);

        if (NativeMethods.GlobalMemoryStatusEx(ref status))
        {
            return status.TotalPhys;
        }

        var code = System.Runtime.InteropServices.Marshal.GetLastWin32Error();
        _log.Write(new OperationRecord(
            DateTimeOffset.Now, "Setup.Environment", "Размер памяти", TimeSpan.Zero,
            OperationOutcome.Failure, "GlobalMemoryStatusEx отказал, код " + code.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        return 0UL;
    }

    public IReadOnlyList<string> VolumeRoots()
    {
        var roots = new List<string>();
        try
        {
            foreach (var drive in DriveInfo.GetDrives())
            {
                roots.Add(drive.Name);
            }
        }
        catch (IOException error)
        {
            Complain("Перечисление томов", string.Empty, error);
        }
        catch (UnauthorizedAccessException error)
        {
            Complain("Перечисление томов", string.Empty, error);
        }

        return roots;
    }

    public string WindowsDirectory()
    {
        var directory = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Windows);

        // В WinPE эта папка иногда не объявлена: система живёт на оперативном
        // диске и заводит не все привычные пути. Тогда берём её оттуда, где
        // она там лежит всегда.
        return string.IsNullOrEmpty(directory) ? @"X:\Windows" : directory;
    }

    private void Complain(string what, string subject, Exception error)
    {
        var reason = string.IsNullOrEmpty(subject)
            ? error.Message
            : subject + ": " + error.Message;

        _log.Write(new OperationRecord(
            DateTimeOffset.Now, "Setup.Environment", what, TimeSpan.Zero, OperationOutcome.Failure, reason));
    }
}
