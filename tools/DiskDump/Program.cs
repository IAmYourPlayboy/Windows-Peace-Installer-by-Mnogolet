using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Storage;
using WindowsPeace.Core.Storage.Native;

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
    private static int Main(string[] args)
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

        // По умолчанию печатаются оба перечислителя и их сличение. Это и есть
        // проверка перехода на прямой разговор с Windows: слепок шага А уже
        // сверен автором с «Управлением дисками», и новый источник обязан
        // дать то же самое. Ключами --wmi и --native можно оставить один.
        var wantWmi = !HasFlag(args, "--native");
        var wantNative = !HasFlag(args, "--wmi");

        DiskSnapshot? wmi = null;
        DiskSnapshot? native = null;

        if (wantWmi)
        {
            wmi = Run("WMI, System.Management", new WmiDiskEnumerator(log), cts.Token);
        }

        if (wantNative)
        {
            native = Run("Напрямую у Windows", new NativeDiskEnumerator(new Win32StorageSource(), log), cts.Token);
        }

        if (wmi is not null && native is not null)
        {
            Compare(wmi, native);
        }

        var snapshot = native ?? wmi!;
        return snapshot.IsFailed ? 1 : 0;
    }

    private static bool HasFlag(string[] args, string flag)
    {
        foreach (var arg in args)
        {
            if (string.Equals(arg, flag, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    private static DiskSnapshot Run(string title, IDiskEnumerator enumerator, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var snapshot = enumerator.Enumerate(cancellationToken);
        stopwatch.Stop();

        Console.WriteLine($"=== {title} — {stopwatch.ElapsedMilliseconds} мс ===");

        if (snapshot.IsFailed)
        {
            Console.WriteLine("     ОТКАЗ: " + snapshot.EnumerationError);
            Console.WriteLine();
            return snapshot;
        }

        var probe = new RealFileSystemProbe();
        var inspector = new FileSystemContentInspector(probe);

        foreach (var disk in snapshot.Disks)
        {
            inspector.Inspect(disk, cancellationToken);
        }

        BootMediaLocator.Mark(snapshot.Disks, probe);
        Print(snapshot);
        return snapshot;
    }

    private static void Print(DiskSnapshot snapshot)
    {
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
    }

    /// <summary>
    /// Сличение двух источников. Расхождение здесь — не мелочь: на серийном номере
    /// стоит отпечаток диска, а на отпечатке — вся защита от дефекта A.
    /// </summary>
    private static void Compare(DiskSnapshot wmi, DiskSnapshot native)
    {
        Console.WriteLine("=== Сличение ===");

        if (wmi.IsFailed || native.IsFailed)
        {
            Console.WriteLine("     Сличать нечего: один из источников не ответил.");
            Console.WriteLine();
            return;
        }

        var differences = new List<string>();

        if (wmi.Disks.Count != native.Disks.Count)
        {
            differences.Add($"дисков: WMI {wmi.Disks.Count}, напрямую {native.Disks.Count}");
        }

        for (var i = 0; i < Math.Min(wmi.Disks.Count, native.Disks.Count); i++)
        {
            var a = wmi.Disks[i];
            var b = native.Disks[i];
            var where = $"диск {a.Number}";

            Check(differences, where, "серийный номер", a.Identity.SerialNumber, b.Identity.SerialNumber);
            Check(differences, where, "доверие", a.Identity.Confidence.ToString(), b.Identity.Confidence.ToString());
            Check(differences, where, "модель", a.Identity.Model, b.Identity.Model);
            Check(differences, where, "объём", a.Identity.SizeBytes.ToString(CultureInfo.InvariantCulture),
                b.Identity.SizeBytes.ToString(CultureInfo.InvariantCulture));
            Check(differences, where, "шина", a.Identity.BusType.ToString(), b.Identity.BusType.ToString());
            Check(differences, where, "носитель", a.Media.ToString(), b.Media.ToString());
            Check(differences, where, "стиль разметки", a.PartitionStyle.ToString(), b.PartitionStyle.ToString());
            Check(differences, where, "система", a.IsSystem.ToString(), b.IsSystem.ToString());
            Check(differences, where, "съёмный", a.IsRemovable.ToString(), b.IsRemovable.ToString());
            Check(differences, where, "разделов", a.Partitions.Count.ToString(CultureInfo.InvariantCulture),
                b.Partitions.Count.ToString(CultureInfo.InvariantCulture));

            for (var p = 0; p < Math.Min(a.Partitions.Count, b.Partitions.Count); p++)
            {
                var pa = a.Partitions[p];
                var pb = b.Partitions[p];
                var pw = $"{where}, раздел {pa.Number}";

                Check(differences, pw, "смещение", pa.Offset.ToString(CultureInfo.InvariantCulture),
                    pb.Offset.ToString(CultureInfo.InvariantCulture));
                Check(differences, pw, "размер", pa.Size.ToString(CultureInfo.InvariantCulture),
                    pb.Size.ToString(CultureInfo.InvariantCulture));
                Check(differences, pw, "назначение", pa.Kind.ToString(), pb.Kind.ToString());
                Check(differences, pw, "буква", pa.DriveLetter?.ToString(), pb.DriveLetter?.ToString());
                Check(differences, pw, "файловая система", pa.Volume?.FileSystem, pb.Volume?.FileSystem);
            }
        }

        if (differences.Count == 0)
        {
            Console.WriteLine("     Расхождений нет. Оба источника видят одно и то же.");
        }
        else
        {
            Console.WriteLine($"     РАСХОЖДЕНИЙ: {differences.Count}");
            foreach (var difference in differences)
            {
                Console.WriteLine("     - " + difference);
            }
        }

        Console.WriteLine();
    }

    private static void Check(List<string> into, string where, string what, string? fromWmi, string? fromNative)
    {
        if (!string.Equals(fromWmi, fromNative, StringComparison.Ordinal))
        {
            into.Add($"{where}, {what}: WMI «{fromWmi ?? "нет"}», напрямую «{fromNative ?? "нет"}»");
        }
    }

    private static string Gb(ulong bytes) => (bytes / 1024d / 1024d / 1024d).ToString("0.0") + " ГБ";
}
