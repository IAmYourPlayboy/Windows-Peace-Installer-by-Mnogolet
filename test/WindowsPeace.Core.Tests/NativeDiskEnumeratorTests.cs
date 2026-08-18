using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WindowsPeace.Core.Storage;
using WindowsPeace.Core.Storage.Native;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Сборка модели дисков из того, что рассказало ядро. Сам разговор с ядром
/// сюда не попадает — он за интерфейсом IRawStorageSource, и здесь подделан.
/// </summary>
public class NativeDiskEnumeratorTests
{
    private const ulong Gib = 1024UL * 1024UL * 1024UL;
    private const ulong Mib = 1024UL * 1024UL;

    private sealed class FakeSource : IRawStorageSource
    {
        public List<RawDisk> RawDisks { get; } = new();
        public List<RawVolume> RawVolumes { get; } = new();
        public char? SystemDrive { get; set; }

        public IReadOnlyList<RawDisk> Disks(CancellationToken cancellationToken) => RawDisks;
        public IReadOnlyList<RawVolume> Volumes(CancellationToken cancellationToken) => RawVolumes;
        public char? SystemDriveLetter() => SystemDrive;
    }

    private static RawDisk Disk(
        int number = 0,
        string? serial = "ZN1WMV9E",
        string? diskGuid = null,
        string? error = null,
        params RawPartition[] partitions) => new()
    {
        Number = number,
        Model = "ST1000DM010-2EP102",
        SerialNumber = serial,
        DiskGuid = diskGuid,
        BusType = BusType.Sata,
        Media = MediaKind.Hdd,
        SizeBytes = 1000UL * Gib,
        PartitionStyle = PartitionStyle.Gpt,
        Partitions = partitions,
        Error = error,
    };

    private static RawPartition Partition(int number, ulong offsetGib, ulong sizeGib, string gptType)
        => new()
        {
            Number = number,
            Offset = offsetGib * Gib,
            Size = sizeGib * Gib,
            GptType = gptType,
        };

    private const string BasicData = "{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}";
    private const string EfiSystem = "{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}";

    private static DiskSnapshot Enumerate(FakeSource source)
        => new NativeDiskEnumerator(source).Enumerate(CancellationToken.None);

    [Fact]
    public void Серийный_номер_устройства_становится_отпечатком()
    {
        var source = new FakeSource();
        source.RawDisks.Add(Disk());

        var disk = Assert.Single(Enumerate(source).Disks);

        Assert.Equal("ZN1WMV9E", disk.Identity.SerialNumber);
        Assert.Equal(IdentityConfidence.Hardware, disk.Identity.Confidence);
        Assert.True(disk.Identity.CanBePinned);
    }

    [Fact]
    public void Без_серийного_номера_отпечаток_берётся_из_разметки_и_доверия_к_нему_меньше()
    {
        var source = new FakeSource();
        source.RawDisks.Add(Disk(serial: null, diskGuid: "{11111111-2222-3333-4444-555555555555}"));

        var disk = Assert.Single(Enumerate(source).Disks);

        Assert.Equal("{11111111-2222-3333-4444-555555555555}", disk.Identity.SerialNumber);
        Assert.Equal(IdentityConfidence.Volatile, disk.Identity.Confidence);
        Assert.False(disk.Identity.CanBePinned);
    }

    [Fact]
    public void Том_соединяется_с_разделом_по_диску_и_смещению()
    {
        var source = new FakeSource();
        source.RawDisks.Add(Disk(partitions: new[] { Partition(1, 1, 900, BasicData) }));
        source.RawVolumes.Add(new RawVolume
        {
            DiskNumber = 0,
            StartingOffset = 1 * Gib,
            DriveLetter = 'D',
            FileSystem = "NTFS",
            Label = "Данные",
            SizeBytes = 900 * Gib,
            FreeBytes = 300 * Gib,
        });

        var partition = Assert.Single(Assert.Single(Enumerate(source).Disks).Partitions);

        Assert.Equal('D', partition.DriveLetter);
        Assert.Equal("NTFS", partition.Volume!.FileSystem);
        Assert.Equal("Данные", partition.Volume!.Label);
        Assert.Equal(300 * Gib, partition.Volume!.FreeBytes);
    }

    [Fact]
    public void Раздел_без_тома_остаётся_без_буквы_и_это_не_ошибка()
    {
        var source = new FakeSource();
        source.RawDisks.Add(Disk(partitions: new[] { Partition(1, 1, 1, EfiSystem) }));

        var partition = Assert.Single(Assert.Single(Enumerate(source).Disks).Partitions);

        Assert.Null(partition.DriveLetter);
        Assert.Null(partition.Volume);
        Assert.Equal(PartitionKind.EfiSystem, partition.Kind);
        Assert.Null(Assert.Single(Enumerate(source).Disks).ProbeError);
    }

    [Fact]
    public void Том_с_чужого_диска_не_приписывается_нашим_разделам()
    {
        var source = new FakeSource();
        source.RawDisks.Add(Disk(partitions: new[] { Partition(1, 1, 900, BasicData) }));
        source.RawVolumes.Add(new RawVolume { DiskNumber = 7, StartingOffset = 1 * Gib, DriveLetter = 'Z' });

        var partition = Assert.Single(Assert.Single(Enumerate(source).Disks).Partitions);

        Assert.Null(partition.DriveLetter);
    }

    [Fact]
    public void Диск_с_томом_текущей_системы_помечается_системным()
    {
        var source = new FakeSource { SystemDrive = 'C' };
        source.RawDisks.Add(Disk(number: 0, partitions: new[] { Partition(1, 1, 400, BasicData) }));
        source.RawDisks.Add(Disk(number: 1, serial: "OTHER", partitions: new[] { Partition(1, 1, 400, BasicData) }));
        source.RawVolumes.Add(new RawVolume { DiskNumber = 1, StartingOffset = 1 * Gib, DriveLetter = 'C' });

        var disks = Enumerate(source).Disks;

        Assert.False(disks[0].IsSystem);
        Assert.True(disks[1].IsSystem);
        Assert.True(disks[1].IsBoot);
    }

    [Fact]
    public void В_WinPE_системного_тома_нет_и_ни_один_диск_не_помечен()
    {
        // Оперативный диск X: не лежит ни на одном физическом диске,
        // поэтому запрета «здесь работает текущая система» быть не должно.
        var source = new FakeSource { SystemDrive = 'X' };
        source.RawDisks.Add(Disk(partitions: new[] { Partition(1, 1, 900, BasicData) }));

        var disk = Assert.Single(Enumerate(source).Disks);

        Assert.False(disk.IsSystem);
        Assert.False(disk.IsBoot);
    }

    [Fact]
    public void Незанятые_промежутки_считаются()
    {
        var source = new FakeSource();
        source.RawDisks.Add(Disk(partitions: new[] { Partition(1, 1, 100, BasicData) }));

        var disk = Assert.Single(Enumerate(source).Disks);

        // Промежутков два: до раздела и после него. Нам важно, что хвост диска виден.
        Assert.Contains(disk.FreeSpaces, gap => gap.Offset == 101 * Gib);
        Assert.Contains(disk.FreeSpaces, gap => gap.Offset == Mib);
    }

    [Fact]
    public void Нечитаемая_разметка_не_убирает_диск_из_списка()
    {
        var source = new FakeSource();
        source.RawDisks.Add(Disk(error: "Разметку прочитать не удалось: отказано в доступе"));

        var disk = Assert.Single(Enumerate(source).Disks);

        Assert.Contains("отказано", disk.ProbeError!, StringComparison.OrdinalIgnoreCase);
        Assert.Empty(disk.Partitions);
    }

    [Fact]
    public void Отмена_прекращает_перечисление_отказом_а_не_пустым_списком()
    {
        var source = new FakeSource();
        source.RawDisks.Add(Disk());

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var snapshot = new NativeDiskEnumerator(source).Enumerate(cts.Token);

        Assert.True(snapshot.IsFailed);
        Assert.Empty(snapshot.Disks);
    }

    [Fact]
    public void Диски_идут_по_возрастанию_номера()
    {
        var source = new FakeSource();
        source.RawDisks.Add(Disk(number: 2, serial: "C"));
        source.RawDisks.Add(Disk(number: 0, serial: "A"));
        source.RawDisks.Add(Disk(number: 1, serial: "B"));

        var numbers = Enumerate(source).Disks.Select(d => d.Number).ToArray();

        Assert.Equal(new[] { 0, 1, 2 }, numbers);
    }
}
