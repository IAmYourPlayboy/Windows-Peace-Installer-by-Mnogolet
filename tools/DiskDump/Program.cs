using System;
using System.IO;
using System.Text;
using System.Threading;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Tools.DiskDump;

/// <summary>
/// Пишет одновременно на экран и в файл. В WinPE прочитать экран нечем:
/// там нет ни буфера обмена, ни PowerShell, а оперативный диск исчезает
/// при перезагрузке. Всё, что останется от опыта, — файл на носителе.
/// </summary>
internal sealed class DoubleWriter : TextWriter
{
    private readonly TextWriter _first;
    private readonly TextWriter _second;

    public DoubleWriter(TextWriter first, TextWriter second)
    {
        _first = first;
        _second = second;
    }

    public override Encoding Encoding => _first.Encoding;

    public override void Write(char value)
    {
        _first.Write(value);
        _second.Write(value);
    }

    public override void Flush()
    {
        _first.Flush();
        _second.Flush();
    }
}

/// <summary>
/// Печатает то, что видит WmiDiskEnumerator. Нужна для ручной сверки
/// с оснасткой «Управление дисками»: сам перечислитель разговаривает
/// с живым железом и модульными тестами не покрывается.
/// </summary>
internal static class Program
{
    private static int Main()
    {
        Console.OutputEncoding = Encoding.UTF8;

        // Вывод дублируется в файл рядом с программой: в WinPE это
        // единственное, что переживёт перезагрузку.
        var dumpPath = Path.Combine(AppContext.BaseDirectory, "disk-dump.txt");
        using var file = new StreamWriter(dumpPath, append: false, new UTF8Encoding(false));
        using var both = new DoubleWriter(Console.Out, file);
        Console.SetOut(both);

        Console.WriteLine($"Windows Peace DiskDump, {DateTimeOffset.Now:yyyy-MM-dd HH:mm:ss}");
        Console.WriteLine($"Среда: {System.Environment.OSVersion}");
        Console.WriteLine($"Каталог: {AppContext.BaseDirectory}");
        Console.WriteLine();

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
