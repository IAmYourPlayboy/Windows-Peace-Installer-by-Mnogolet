using System;
using System.Threading;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Tools.DiskDump;

/// <summary>
/// Печатает то, что видит WmiDiskEnumerator. Нужна для ручной сверки
/// с оснасткой «Управление дисками»: сам перечислитель разговаривает
/// с живым железом и модульными тестами не покрывается.
/// </summary>
internal static class Program
{
    private static int Main()
    {
        Console.OutputEncoding = System.Text.Encoding.UTF8;

        using var log = new JsonLinesOperationLog(
            JsonLinesOperationLog.DefaultPath(AppContext.BaseDirectory));

        using var cts = new CancellationTokenSource(Timeouts.DiskEnumeration);

        var snapshot = new WmiDiskEnumerator(log).Enumerate(cts.Token);

        if (snapshot.IsFailed)
        {
            Console.Error.WriteLine("Перечисление не удалось: " + snapshot.EnumerationError);
            return 1;
        }

        var probe = new RealFileSystemProbe();
        var inspector = new FileSystemContentInspector(probe);

        foreach (var disk in snapshot.Disks)
        {
            inspector.Inspect(disk, cts.Token);
        }

        BootMediaLocator.Mark(snapshot.Disks, probe);

        foreach (var disk in snapshot.Disks)
        {
            Console.WriteLine($"[{disk.Number}] {disk.FriendlyName}  {Gb(disk.Identity.SizeBytes)}  {disk.Identity.BusType}  {disk.Media}");
            Console.WriteLine($"     отпечаток: {disk.Identity.SerialNumber ?? "нет"}  источник: {disk.Identity.Source}  доверие: {disk.Identity.Confidence}");
            Console.WriteLine($"     система: {disk.IsSystem}  загрузочный: {disk.IsBoot}  съёмный: {disk.IsRemovable}  носитель WP: {disk.IsWindowsPeaceMedia}");

            if (disk.ProbeError is not null)
            {
                Console.WriteLine("     ОШИБКА: " + disk.ProbeError);
            }

            foreach (var partition in disk.Partitions)
            {
                var letter = partition.DriveLetter is null ? "  " : partition.DriveLetter + ":";
                Console.WriteLine($"     раздел {partition.Number} {letter} {Gb(partition.Size),10}  {partition.Kind,-18} " +
                                  $"Windows={partition.Content.WindowsFound} файлы={partition.Content.UserFilesFound} проверен={partition.Content.Inspected}");
            }

            foreach (var gap in disk.FreeSpaces)
            {
                Console.WriteLine($"     незанято {Gb(gap.Size),10} со смещения {gap.Offset}");
            }

            Console.WriteLine();
        }

        return 0;
    }

    private static string Gb(ulong bytes) => (bytes / 1024d / 1024d / 1024d).ToString("0.0") + " ГБ";
}
