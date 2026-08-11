using System;
using System.Collections.Generic;
using System.Linq;
using System.Management;
using System.Threading;
using WindowsPeace.Core.Diagnostics;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Перечисление дисков через пространство имён root\Microsoft\Windows\Storage.
/// Оно есть и в обычной Windows, и в WinPE базового образа — проверено описью
/// содержимого boot.wim, см. docs/ARCHITECTURE.md, раздел 6.
/// </summary>
public sealed class WmiDiskEnumerator : IDiskEnumerator
{
    private const string StorageNamespace = @"root\Microsoft\Windows\Storage";
    private const string CimNamespace = @"root\cimv2";
    private const string Component = "Storage";

    private readonly IOperationLog _log;

    public WmiDiskEnumerator(IOperationLog log) => _log = log;

    public DiskSnapshot Enumerate(CancellationToken cancellationToken)
    {
        using var scope = OperationScope.Start(_log, Component, "Перечисление дисков");

        try
        {
            var disks = QueryAll(cancellationToken);
            scope.Success();
            return new DiskSnapshot(disks, enumerationError: null);
        }
        catch (OperationCanceledException)
        {
            scope.TimedOut();
            return DiskSnapshot.Failed("Опрос дисков превысил отведённое время");
        }
        catch (ManagementException exception)
        {
            scope.Failure(exception.Message);
            return DiskSnapshot.Failed("Не удалось обратиться к службе хранилища: " + exception.Message);
        }
        catch (UnauthorizedAccessException exception)
        {
            scope.Failure(exception.Message);
            return DiskSnapshot.Failed("Недостаточно прав для опроса дисков");
        }
#pragma warning disable CA1031 // Перехват любого исключения здесь обязателен — объяснение в теле.
        catch (Exception exception)
        {
            // Последний рубеж, и единственное место во всём проекте, где ловится
            // любое исключение. Спека шага А требует буквально: «WMI недоступно —
            // показывает окно с текстом ошибки и кнопкой повтора, не падает».
            // Перечисления конкретных типов для этого недостаточно: драйвер чужого
            // контроллера умеет бросить COMException, Win32Exception и что угодно ещё,
            // а инструмент предназначен для чужих машин.
            //
            // Исключение при этом не проглатывается: полный текст уходит в журнал,
            // короткий — на экран. Это не пустой catch, запрещённый разделом 9
            // архитектуры, а его противоположность.
            scope.Failure(exception.ToString());
            return DiskSnapshot.Failed("Опрос дисков сорвался: " + exception.Message);
        }
#pragma warning restore CA1031
    }

    private List<DiskInfo> QueryAll(CancellationToken cancellationToken)
    {
        var physical = Query(StorageNamespace, "SELECT * FROM MSFT_PhysicalDisk", cancellationToken);
        var partitions = Query(StorageNamespace, "SELECT * FROM MSFT_Partition", cancellationToken);
        var volumes = Query(StorageNamespace, "SELECT * FROM MSFT_Volume", cancellationToken);
        var win32 = Query(CimNamespace, "SELECT Index, SerialNumber FROM Win32_DiskDrive", cancellationToken);
        var disks = Query(StorageNamespace, "SELECT * FROM MSFT_Disk", cancellationToken);

        var volumeByLetter = volumes
            .Where(v => WmiValue.Char(v, "DriveLetter") is not null)
            .GroupBy(v => WmiValue.Char(v, "DriveLetter")!.Value)
            .ToDictionary(g => g.Key, g => g.First());

        var result = new List<DiskInfo>();

        foreach (var disk in disks)
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(BuildDisk(disk, physical, partitions, win32, volumeByLetter));
        }

        return result;
    }

    private DiskInfo BuildDisk(
        ManagementBaseObject disk,
        IReadOnlyList<ManagementBaseObject> physical,
        IReadOnlyList<ManagementBaseObject> allPartitions,
        IReadOnlyList<ManagementBaseObject> win32,
        IReadOnlyDictionary<char, ManagementBaseObject> volumeByLetter)
    {
        var number = WmiValue.Int32(disk, "Number");
        var uniqueId = WmiValue.String(disk, "UniqueId");
        var size = WmiValue.UInt64(disk, "Size");
        var busType = (BusType)WmiValue.Int32(disk, "BusType");
        var friendlyName = WmiValue.String(disk, "FriendlyName") ?? WmiValue.String(disk, "Model") ?? "Диск без имени";

        var matchedPhysical = physical.FirstOrDefault(p =>
            string.Equals(WmiValue.String(p, "UniqueId"), uniqueId, StringComparison.OrdinalIgnoreCase)
            || WmiValue.String(p, "DeviceId") == number.ToString(System.Globalization.CultureInfo.InvariantCulture));

        var matchedWin32 = win32.FirstOrDefault(w => WmiValue.Int32(w, "Index") == number);

        var identity = DiskIdentity.Create(
            physicalDiskSerial: matchedPhysical is null ? null : WmiValue.String(matchedPhysical, "SerialNumber"),
            diskSerial: WmiValue.String(disk, "SerialNumber"),
            win32DiskDriveSerial: matchedWin32 is null ? null : WmiValue.String(matchedWin32, "SerialNumber"),
            uniqueId: uniqueId,
            gptGuid: WmiValue.String(disk, "Guid"),
            model: friendlyName,
            sizeBytes: size,
            busType: busType);

        string? probeError = null;
        var partitions = new List<PartitionInfo>();

        try
        {
            partitions.AddRange(allPartitions
                .Where(p => WmiValue.Int32(p, "DiskNumber") == number)
                .OrderBy(p => WmiValue.UInt64(p, "Offset"))
                .Select(p => BuildPartition(p, volumeByLetter)));
        }
        catch (ManagementException exception)
        {
            probeError = "Разделы прочитать не удалось: " + exception.Message;
        }

        var media = matchedPhysical is null
            ? MediaKind.Unspecified
            : (MediaKind)WmiValue.Int32(matchedPhysical, "MediaType");

        return new DiskInfo(
            identity,
            number,
            friendlyName,
            media,
            (PartitionStyle)WmiValue.Int32(disk, "PartitionStyle"),
            isSystem: WmiValue.Boolean(disk, "IsSystem"),
            isBoot: WmiValue.Boolean(disk, "IsBoot"),
            isOffline: WmiValue.Boolean(disk, "IsOffline"),
            isReadOnly: WmiValue.Boolean(disk, "IsReadOnly"),
            isRemovable: busType == BusType.Usb || busType == BusType.Sd || busType == BusType.Mmc,
            partitions: partitions,
            freeSpaces: FreeSpaceCalculator.Calculate(size, partitions),
            probeError: probeError);
    }

    private static PartitionInfo BuildPartition(
        ManagementBaseObject partition,
        IReadOnlyDictionary<char, ManagementBaseObject> volumeByLetter)
    {
        var letter = WmiValue.Char(partition, "DriveLetter");

        VolumeInfo? volume = null;
        if (letter is not null && volumeByLetter.TryGetValue(letter.Value, out var found))
        {
            volume = new VolumeInfo(
                WmiValue.String(found, "FileSystem"),
                WmiValue.String(found, "FileSystemLabel"),
                WmiValue.UInt64(found, "Size"),
                WmiValue.UInt64(found, "SizeRemaining"));
        }

        return new PartitionInfo(
            WmiValue.Int32(partition, "PartitionNumber"),
            WmiValue.UInt64(partition, "Offset"),
            WmiValue.UInt64(partition, "Size"),
            PartitionKinds.FromGptType(WmiValue.String(partition, "GptType")),
            letter,
            WmiValue.Boolean(partition, "IsSystem"),
            WmiValue.Boolean(partition, "IsHidden"),
            volume);
    }

    private List<ManagementBaseObject> Query(string scope, string query, CancellationToken cancellationToken)
    {
        using var searcher = new ManagementObjectSearcher(
            new ManagementScope(scope),
            new ObjectQuery(query),
            new EnumerationOptions { Timeout = Timeouts.SingleDiskProbe, ReturnImmediately = true, Rewindable = false });

        var result = new List<ManagementBaseObject>();

        foreach (ManagementBaseObject item in searcher.Get())
        {
            cancellationToken.ThrowIfCancellationRequested();
            result.Add(item);
        }

        return result;
    }
}
