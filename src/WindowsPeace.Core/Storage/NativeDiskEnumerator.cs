using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using WindowsPeace.Core.Diagnostics;
using WindowsPeace.Core.Storage.Native;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Перечисление дисков без WMI: Windows спрашивается напрямую, тем же способом,
/// каким работает diskpart. Причина — опыт шага Б: библиотека System.Management
/// в WinPE не работает, потому что подгружает native-модуль из .NET Framework,
/// которого там нет. См. docs/superpowers/notes/2026-08-14-step-b-pe-experiments.md.
///
/// Разговор с ядром живёт за интерфейсом IRawStorageSource. Здесь — только сборка
/// модели из плоских ответов, и она целиком проверяется тестами.
/// </summary>
public sealed class NativeDiskEnumerator : IDiskEnumerator
{
    private const string Component = "Storage";

    private readonly IRawStorageSource _source;
    private readonly IOperationLog _log;

    public NativeDiskEnumerator(IRawStorageSource source, IOperationLog? log = null)
    {
        _source = source;
        _log = log ?? NullOperationLog.Instance;
    }

    public DiskSnapshot Enumerate(CancellationToken cancellationToken)
    {
        using var scope = OperationScope.Start(_log, Component, "Перечисление дисков");

        try
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rawDisks = _source.Disks(cancellationToken);
            var volumes = _source.Volumes(cancellationToken);
            var systemDiskNumber = FindSystemDisk(volumes, _source.SystemDriveLetter());

            var disks = new List<DiskInfo>();
            foreach (var raw in rawDisks.OrderBy(disk => disk.Number))
            {
                cancellationToken.ThrowIfCancellationRequested();
                disks.Add(Build(raw, volumes, systemDiskNumber));
            }

            scope.Success();
            return new DiskSnapshot(disks, enumerationError: null);
        }
        catch (OperationCanceledException)
        {
            scope.TimedOut();
            return DiskSnapshot.Failed(CoreLocalization.Localization.Current[CoreLocalization.Keys.Disk.ErrorTimeout]);
        }
        catch (UnauthorizedAccessException exception)
        {
            scope.Failure(exception.Message);
            return DiskSnapshot.Failed(CoreLocalization.Localization.Current[CoreLocalization.Keys.Disk.ErrorForbidden]);
        }
#pragma warning disable CA1031 // Перехват любого исключения здесь обязателен — объяснение в теле.
        catch (Exception exception)
        {
            // Второй и последний рубеж проекта, устроенный по образцу WmiDiskEnumerator.
            // Мы разговариваем с драйверами чужих контроллеров через ядро, и оттуда
            // прилетает что угодно: Win32Exception, IOException, SEHException.
            // Инструмент предназначен для чужих машин, поэтому «не падать» здесь —
            // требование спеки, а не пожелание.
            //
            // Исключение не проглатывается: полный текст уходит в журнал и на экран.
            scope.Failure(exception.ToString());
            return DiskSnapshot.Failed(string.Format(
                CultureInfo.CurrentCulture,
                CoreLocalization.Localization.Current[CoreLocalization.Keys.Disk.ErrorFailed],
                exception.Message));
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Диск, на котором лежит том работающей системы. В WinPE это оперативный диск,
    /// он не лежит ни на одном физическом — и тогда системного диска нет вовсе.
    /// </summary>
    private static int? FindSystemDisk(IReadOnlyList<RawVolume> volumes, char? systemDriveLetter)
    {
        if (systemDriveLetter is null)
        {
            return null;
        }

        foreach (var volume in volumes)
        {
            if (volume.DriveLetter == systemDriveLetter)
            {
                return volume.DiskNumber;
            }
        }

        return null;
    }

    private static DiskInfo Build(RawDisk raw, IReadOnlyList<RawVolume> volumes, int? systemDiskNumber)
    {
        // Порядок источников отпечатка тот же, что был у WMI: сначала серийный номер
        // самого устройства, и только если его нет — идентификатор из разметки,
        // который переживает не всё и потому доверия ему меньше.
        var identity = DiskIdentity.Create(
            physicalDiskSerial: raw.SerialNumber,
            diskSerial: null,
            win32DiskDriveSerial: null,
            uniqueId: null,
            gptGuid: raw.DiskGuid,
            model: raw.Model,
            sizeBytes: raw.SizeBytes,
            busType: raw.BusType);

        var partitions = new List<PartitionInfo>();
        foreach (var partition in raw.Partitions.OrderBy(item => item.Offset))
        {
            var volume = FindVolume(volumes, raw.Number, partition.Offset);
            var kind = PartitionKinds.FromGptType(partition.GptType);

            partitions.Add(new PartitionInfo(
                partition.Number,
                partition.Offset,
                partition.Size,
                kind,
                volume?.DriveLetter,
                isSystem: kind == PartitionKind.EfiSystem,
                isHidden: partition.IsHidden,
                volume is null
                    ? null
                    : new VolumeInfo(volume.FileSystem, volume.Label, volume.SizeBytes, volume.FreeBytes)));
        }

        var isSystem = systemDiskNumber == raw.Number;

        return new DiskInfo(
            identity,
            raw.Number,
            raw.Model,
            raw.Media,
            raw.PartitionStyle,
            isSystem: isSystem,
            isBoot: isSystem,
            isOffline: raw.IsOffline,
            isReadOnly: raw.IsReadOnly,
            isRemovable: raw.IsRemovable,
            partitions,
            FreeSpaceCalculator.Calculate(raw.SizeBytes, partitions),
            raw.Error);
    }

    /// <summary>
    /// Том ищется по диску и смещению, а не по букве или имени: буквы в WinPE
    /// раздаются как попало, а смещение — свойство самого диска.
    /// </summary>
    private static RawVolume? FindVolume(IReadOnlyList<RawVolume> volumes, int diskNumber, ulong offset)
    {
        foreach (var volume in volumes)
        {
            if (volume.DiskNumber == diskNumber && volume.StartingOffset == offset)
            {
                return volume;
            }
        }

        return null;
    }
}
