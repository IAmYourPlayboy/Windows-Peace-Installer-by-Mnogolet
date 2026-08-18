using System;
using System.Collections.Generic;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Core.Storage.Native;

/// <summary>
/// Настоящий разговор с Windows о дисках и томах. Ни WMI, ни COM, ни .NET Framework —
/// только вопросы ядру, те же самые, какие задаёт diskpart.
///
/// Диски открываются без права чтения данных: нам нужны только их описания,
/// а такой доступ не требует прав администратора. Ни одной операции записи здесь нет.
/// </summary>
public sealed class Win32StorageSource : IRawStorageSource
{
    private const int MaxDiskNumber = 256;

    public IReadOnlyList<RawDisk> Disks(CancellationToken cancellationToken)
    {
        var disks = new List<RawDisk>();

        foreach (var number in PhysicalDriveNumbers())
        {
            cancellationToken.ThrowIfCancellationRequested();

            var disk = ReadDisk(number);
            if (disk is not null)
            {
                disks.Add(disk);
            }
        }

        return disks;
    }

    public IReadOnlyList<RawVolume> Volumes(CancellationToken cancellationToken)
    {
        var volumes = new List<RawVolume>();

        // Пустой картовод или отвалившийся диск умеет вызвать системное окно
        // «Вставьте диск». Инструмент работает без человека рядом, поэтому окна
        // на время перечисления выключаются.
        NativeMethods.SetThreadErrorMode(NativeMethods.SemFailCriticalErrors, out var previousMode);

        try
        {
            var buffer = new StringBuilder(260);
            var find = NativeMethods.FindFirstVolumeW(buffer, buffer.Capacity);
            if (find == IntPtr.Zero || find == new IntPtr(-1))
            {
                return volumes;
            }

            try
            {
                do
                {
                    cancellationToken.ThrowIfCancellationRequested();

                    var volume = ReadVolume(buffer.ToString());
                    if (volume is not null)
                    {
                        volumes.Add(volume);
                    }

                    buffer.Length = 0;
                    buffer.EnsureCapacity(260);
                }
                while (NativeMethods.FindNextVolumeW(find, buffer, buffer.Capacity));
            }
            finally
            {
                NativeMethods.FindVolumeClose(find);
            }
        }
        finally
        {
            NativeMethods.SetThreadErrorMode(previousMode, out _);
        }

        return volumes;
    }

    public char? SystemDriveLetter()
    {
        var systemDirectory = Environment.GetFolderPath(Environment.SpecialFolder.System);
        return systemDirectory.Length >= 2 && systemDirectory[1] == ':'
            ? char.ToUpperInvariant(systemDirectory[0])
            : null;
    }

    /// <summary>
    /// Номера физических дисков берутся из списка имён устройств, а не перебором
    /// подряд: перебор угадывает верхнюю границу, а список её знает.
    /// </summary>
    private static IReadOnlyList<int> PhysicalDriveNumbers()
    {
        var numbers = new List<int>();
        var buffer = new char[1024 * 512];

        var written = NativeMethods.QueryDosDeviceW(null, buffer, buffer.Length);
        if (written == 0)
        {
            // Список имён недоступен — отступаем на перебор. Хуже, но лучше, чем ничего.
            for (var number = 0; number < MaxDiskNumber; number++)
            {
                numbers.Add(number);
            }

            return numbers;
        }

        var start = 0;
        for (var i = 0; i < written; i++)
        {
            if (buffer[i] != '\0')
            {
                continue;
            }

            var name = new string(buffer, start, i - start);
            start = i + 1;

            if (name.StartsWith("PhysicalDrive", StringComparison.OrdinalIgnoreCase) &&
                int.TryParse(name.Substring("PhysicalDrive".Length), NumberStyles.Integer,
                    CultureInfo.InvariantCulture, out var number))
            {
                numbers.Add(number);
            }

            if (name.Length == 0)
            {
                break;
            }
        }

        numbers.Sort();
        return numbers;
    }

    private static RawDisk? ReadDisk(int number)
    {
        var path = string.Format(CultureInfo.InvariantCulture, @"\\.\PhysicalDrive{0}", number);

        using var handle = NativeMethods.CreateFileW(
            path, 0, NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
            IntPtr.Zero, NativeMethods.OpenExisting, 0, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return null;
        }

        var descriptor = ReadDeviceDescriptor(handle);
        var attributes = ReadDiskAttributes(handle);
        var layout = ReadLayout(handle);

        return new RawDisk
        {
            Number = number,
            Model = descriptor.Model,
            SerialNumber = descriptor.SerialNumber,
            DiskGuid = layout.DiskGuid,
            BusType = descriptor.BusType,
            Media = ReadMediaKind(handle),
            IsRemovable = descriptor.IsRemovable,
            IsReadOnly = (attributes & NativeMethods.DiskAttributeReadOnly) != 0,
            IsOffline = (attributes & NativeMethods.DiskAttributeOffline) != 0,
            SizeBytes = ReadSize(handle),
            PartitionStyle = layout.Style,
            Partitions = layout.Partitions,
            Error = layout.Error,
        };
    }

    private readonly struct DeviceDescriptor
    {
        public DeviceDescriptor(string model, string? serialNumber, BusType busType, bool isRemovable)
        {
            Model = model;
            SerialNumber = serialNumber;
            BusType = busType;
            IsRemovable = isRemovable;
        }

        public string Model { get; }
        public string? SerialNumber { get; }
        public BusType BusType { get; }
        public bool IsRemovable { get; }
    }

    private static DeviceDescriptor ReadDeviceDescriptor(SafeFileHandle handle)
    {
        var query = new byte[12];
        WriteInt32(query, 0, NativeMethods.StorageDeviceProperty);
        WriteInt32(query, 4, NativeMethods.PropertyStandardQuery);

        var buffer = new byte[4096];
        if (!NativeMethods.DeviceIoControl(handle, NativeMethods.IoctlStorageQueryProperty,
                query, query.Length, buffer, buffer.Length, out var returned, IntPtr.Zero) || returned < 36)
        {
            return new DeviceDescriptor(string.Empty, null, BusType.Unknown, isRemovable: false);
        }

        var isRemovable = buffer[10] != 0;
        var vendor = AnsiAt(buffer, ReadInt32(buffer, 12), returned);
        var product = AnsiAt(buffer, ReadInt32(buffer, 16), returned);
        var serial = AnsiAt(buffer, ReadInt32(buffer, 24), returned);
        var busType = (BusType)ReadInt32(buffer, 28);

        // Так же склеивает имя и сама система: у SATA-дисков изготовитель обычно
        // пуст и всё имя лежит в модели, у флешек заполнены оба поля.
        var model = string.IsNullOrEmpty(vendor)
            ? product
            : string.IsNullOrEmpty(product) ? vendor : vendor + " " + product;

        return new DeviceDescriptor(model, string.IsNullOrEmpty(serial) ? null : serial, busType, isRemovable);
    }

    private static MediaKind ReadMediaKind(SafeFileHandle handle)
    {
        var query = new byte[12];
        WriteInt32(query, 0, NativeMethods.StorageDeviceSeekPenaltyProperty);
        WriteInt32(query, 4, NativeMethods.PropertyStandardQuery);

        var buffer = new byte[16];
        if (!NativeMethods.DeviceIoControl(handle, NativeMethods.IoctlStorageQueryProperty,
                query, query.Length, buffer, buffer.Length, out var returned, IntPtr.Zero) || returned < 9)
        {
            // Флешки и виртуальные диски на этот вопрос не отвечают — и это не ошибка.
            return MediaKind.Unspecified;
        }

        return buffer[8] != 0 ? MediaKind.Hdd : MediaKind.Ssd;
    }

    private static ulong ReadSize(SafeFileHandle handle)
    {
        var buffer = new byte[32];
        if (!NativeMethods.DeviceIoControl(handle, NativeMethods.IoctlDiskGetDriveGeometryEx,
                null, 0, buffer, buffer.Length, out var returned, IntPtr.Zero) || returned < 32)
        {
            return 0UL;
        }

        // DISK_GEOMETRY занимает первые 24 байта, дальше идёт полный размер диска.
        return (ulong)BitConverter.ToInt64(buffer, 24);
    }

    private static ulong ReadDiskAttributes(SafeFileHandle handle)
    {
        var buffer = new byte[16];
        return NativeMethods.DeviceIoControl(handle, NativeMethods.IoctlDiskGetDiskAttributes,
            null, 0, buffer, buffer.Length, out var returned, IntPtr.Zero) && returned >= 16
            ? BitConverter.ToUInt64(buffer, 8)
            : 0UL;
    }

    private readonly struct Layout
    {
        public Layout(PartitionStyle style, string? diskGuid, IReadOnlyList<RawPartition> partitions, string? error)
        {
            Style = style;
            DiskGuid = diskGuid;
            Partitions = partitions;
            Error = error;
        }

        public PartitionStyle Style { get; }
        public string? DiskGuid { get; }
        public IReadOnlyList<RawPartition> Partitions { get; }
        public string? Error { get; }
    }

    /// <summary>
    /// Перевод стиля разметки из чисел Windows в наши. Ловушка: имена совпадают,
    /// а числа нет. В таблице разметки у Windows MBR это 0 и GPT это 1, а наша
    /// модель повторяет нумерацию WMI, где 0 — «неизвестно», 1 — MBR, 2 — GPT.
    /// Прямое приведение молча превращает GPT-диск в MBR, и тогда типы разделов
    /// перестают разбираться вовсе.
    /// </summary>
    private static PartitionStyle StyleFromWindows(int value) => value switch
    {
        0 => PartitionStyle.Mbr,
        1 => PartitionStyle.Gpt,
        _ => PartitionStyle.Unknown,
    };

    private static Layout ReadLayout(SafeFileHandle handle)
    {
        var size = 8192;
        byte[] buffer;
        int returned;

        while (true)
        {
            buffer = new byte[size];
            if (NativeMethods.DeviceIoControl(handle, NativeMethods.IoctlDiskGetDriveLayoutEx,
                    null, 0, buffer, buffer.Length, out returned, IntPtr.Zero))
            {
                break;
            }

            var error = Marshal.GetLastWin32Error();
            if ((error == NativeMethods.ErrorInsufficientBuffer || error == NativeMethods.ErrorMoreData) &&
                size < 1024 * 1024)
            {
                size *= 4;
                continue;
            }

            return new Layout(PartitionStyle.Unknown, null, new List<RawPartition>(),
                string.Format(
                    CultureInfo.CurrentCulture,
                    CoreLocalization.Localization.Current[CoreLocalization.Keys.Layout.ReadFailed],
                    error));
        }

        if (returned < 48)
        {
            return new Layout(PartitionStyle.Unknown, null, new List<RawPartition>(),
                "Разметка прочитана не полностью");
        }

        var style = StyleFromWindows(ReadInt32(buffer, 0));
        var count = ReadInt32(buffer, 4);

        string? diskGuid = null;
        if (style == PartitionStyle.Gpt)
        {
            var raw = new byte[16];
            Array.Copy(buffer, 8, raw, 0, 16);
            diskGuid = new Guid(raw).ToString("B", CultureInfo.InvariantCulture);
        }

        var partitions = new List<RawPartition>();
        const int EntrySize = 144;
        const int FirstEntry = 48;

        for (var index = 0; index < count; index++)
        {
            var at = FirstEntry + (index * EntrySize);
            if (at + EntrySize > returned)
            {
                break;
            }

            var length = (ulong)BitConverter.ToInt64(buffer, at + 16);
            if (length == 0)
            {
                // На дисках MBR ядро всегда отдаёт четыре записи, часть из них пустые.
                continue;
            }

            var typeBytes = new byte[16];
            Array.Copy(buffer, at + 32, typeBytes, 0, 16);
            var attributes = BitConverter.ToUInt64(buffer, at + 64);

            partitions.Add(new RawPartition
            {
                Number = ReadInt32(buffer, at + 24),
                Offset = (ulong)BitConverter.ToInt64(buffer, at + 8),
                Size = length,
                GptType = style == PartitionStyle.Gpt
                    ? new Guid(typeBytes).ToString("B", CultureInfo.InvariantCulture)
                    : null,
                IsHidden = (attributes & NativeMethods.GptBasicDataAttributeHidden) != 0,
            });
        }

        return new Layout(style, diskGuid, partitions, error: null);
    }

    private static RawVolume? ReadVolume(string volumeName)
    {
        // Имя приходит с завершающей косой чертой: она нужна файловым вызовам
        // и мешает открытию тома как устройства.
        var withSlash = volumeName;
        var withoutSlash = volumeName.TrimEnd('\\');

        var extents = ReadVolumeExtents(withoutSlash);
        if (extents is null)
        {
            // Том не лежит ни на одном физическом диске — например, оперативный диск
            // X: в WinPE. Разделу его сопоставить не с чем.
            return null;
        }

        char? letter = null;
        var paths = new char[1024];
        if (NativeMethods.GetVolumePathNamesForVolumeNameW(withSlash, paths, paths.Length, out var written) &&
            written > 3 && paths[1] == ':')
        {
            letter = char.ToUpperInvariant(paths[0]);
        }

        var label = new StringBuilder(261);
        var fileSystem = new StringBuilder(261);
        var hasInformation = NativeMethods.GetVolumeInformationW(
            withSlash, label, label.Capacity, out _, out _, out _, fileSystem, fileSystem.Capacity);

        ulong sizeBytes = 0;
        ulong freeBytes = 0;
        NativeMethods.GetDiskFreeSpaceExW(withSlash, out _, out sizeBytes, out freeBytes);

        return new RawVolume
        {
            DiskNumber = extents.Value.DiskNumber,
            StartingOffset = extents.Value.StartingOffset,
            DriveLetter = letter,
            FileSystem = hasInformation && fileSystem.Length > 0 ? fileSystem.ToString() : null,
            Label = hasInformation && label.Length > 0 ? label.ToString() : null,
            SizeBytes = sizeBytes,
            FreeBytes = freeBytes,
        };
    }

    private static (int DiskNumber, ulong StartingOffset)? ReadVolumeExtents(string devicePath)
    {
        using var handle = NativeMethods.CreateFileW(
            devicePath, 0, NativeMethods.FileShareRead | NativeMethods.FileShareWrite,
            IntPtr.Zero, NativeMethods.OpenExisting, 0, IntPtr.Zero);

        if (handle.IsInvalid)
        {
            return null;
        }

        var buffer = new byte[1024];
        if (!NativeMethods.DeviceIoControl(handle, NativeMethods.IoctlVolumeGetVolumeDiskExtents,
                null, 0, buffer, buffer.Length, out var returned, IntPtr.Zero) || returned < 32)
        {
            return null;
        }

        var extentCount = ReadInt32(buffer, 0);
        if (extentCount < 1)
        {
            return null;
        }

        // Первого куска достаточно: разделы, с которыми мы работаем, лежат целиком.
        return (ReadInt32(buffer, 8), (ulong)BitConverter.ToInt64(buffer, 16));
    }

    private static int ReadInt32(byte[] buffer, int offset) => BitConverter.ToInt32(buffer, offset);

    private static void WriteInt32(byte[] buffer, int offset, int value)
        => Buffer.BlockCopy(BitConverter.GetBytes(value), 0, buffer, offset, 4);

    /// <summary>Строка ANSI по смещению от начала описания. Нулевое смещение означает «поля нет».</summary>
    private static string AnsiAt(byte[] buffer, int offset, int limit)
    {
        if (offset <= 0 || offset >= limit)
        {
            return string.Empty;
        }

        var end = offset;
        while (end < limit && buffer[end] != 0)
        {
            end++;
        }

        return Encoding.ASCII.GetString(buffer, offset, end - offset).Trim();
    }
}
