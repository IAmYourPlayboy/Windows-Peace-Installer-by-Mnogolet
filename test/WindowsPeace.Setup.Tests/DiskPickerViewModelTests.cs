using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using WindowsPeace.Core.Storage;
using WindowsPeace.Setup.Pages;
using Xunit;

namespace WindowsPeace.Setup.Tests;

internal sealed class FakeEnumerator : IDiskEnumerator
{
    private readonly DiskSnapshot _snapshot;

    public FakeEnumerator(DiskSnapshot snapshot) => _snapshot = snapshot;

    public DiskSnapshot Enumerate(CancellationToken cancellationToken) => _snapshot;
}

internal sealed class NoopInspector : IDiskContentInspector
{
    public void Inspect(DiskInfo disk, CancellationToken cancellationToken)
    {
    }
}

/// <summary>
/// Пустая файловая система вместо настоящей: иначе поиск описи носителя
/// полез бы на диск C: живой машины и тест зависел бы от того, что там лежит.
/// </summary>
internal sealed class EmptyFileSystem : IFileSystemProbe
{
    public bool DirectoryExists(string path) => false;

    public bool FileExists(string path) => false;

    public IReadOnlyList<string> EnumerateDirectories(string path) => Array.Empty<string>();
}

public class DiskPickerViewModelTests
{
    private const ulong Gib = 1024UL * 1024UL * 1024UL;

    private static DiskInfo Disk(string serial, ulong size, bool isSystem = false, IReadOnlyList<PartitionInfo>? partitions = null)
    {
        var list = partitions ?? new List<PartitionInfo>();
        return new DiskInfo(
            DiskIdentity.Create(serial, null, null, null, null, "Диск " + serial, size, BusType.Nvme),
            number: 0, friendlyName: "Диск " + serial, media: MediaKind.Ssd,
            partitionStyle: PartitionStyle.Gpt, isSystem: isSystem, isBoot: false,
            isOffline: false, isReadOnly: false, isRemovable: false,
            partitions: list, freeSpaces: FreeSpaceCalculator.Calculate(size, list), probeError: null);
    }

    private static DiskPickerViewModel Create(params DiskInfo[] disks)
    {
        var model = new DiskPickerViewModel(
            new FakeEnumerator(new DiskSnapshot(disks, null)),
            new NoopInspector(),
            new EmptyFileSystem());
        model.Refresh();
        return model;
    }

    [Fact]
    public void Диски_попадают_в_список()
    {
        var model = Create(Disk("A", 500 * Gib), Disk("B", 1000 * Gib));

        Assert.Equal(2, model.Rows.Count(r => r.Kind == RowKind.Disk));
    }

    [Fact]
    public void Разделы_идут_строками_под_своим_диском()
    {
        var partition = new PartitionInfo(1, 1048576UL, 100 * Gib, PartitionKind.BasicData, 'C', false, false, null);
        var model = Create(Disk("A", 500 * Gib, partitions: new[] { partition }));

        Assert.Equal(RowKind.Disk, model.Rows[0].Kind);
        Assert.Equal(RowKind.Partition, model.Rows[1].Kind);
    }

    [Fact]
    public void Незанятое_пространство_показывается_отдельной_строкой()
    {
        var model = Create(Disk("A", 500 * Gib));

        Assert.Contains(model.Rows, r => r.Kind == RowKind.FreeSpace);
    }

    [Fact]
    public void Пока_ничего_не_выбрано_идти_дальше_нельзя()
    {
        var model = Create(Disk("A", 500 * Gib));

        Assert.False(model.CanGoNext);
    }

    [Fact]
    public void Выбор_допустимого_диска_разрешает_идти_дальше_и_строит_план()
    {
        var model = Create(Disk("A", 500 * Gib));

        model.Selected = model.Rows.First(r => r.Kind == RowKind.Disk);

        Assert.True(model.CanGoNext);
        Assert.Contains("EFI", model.PlanSummary);
    }

    [Fact]
    public void Выбор_запрещённого_диска_не_разрешает_идти_дальше_и_объясняет_причину()
    {
        var model = Create(Disk("A", 500 * Gib, isSystem: true));

        model.Selected = model.Rows.First(r => r.Kind == RowKind.Disk);

        Assert.False(model.CanGoNext);
        Assert.False(string.IsNullOrEmpty(model.DenialReason));
    }

    [Fact]
    public void Кнопки_разделов_включаются_по_виду_выбранной_строки()
    {
        var partition = new PartitionInfo(1, 1048576UL, 100 * Gib, PartitionKind.BasicData, 'C', false, false, null);
        var model = Create(Disk("A", 500 * Gib, partitions: new[] { partition }));

        model.Selected = model.Rows.First(r => r.Kind == RowKind.Partition);
        Assert.True(model.CanDelete);
        Assert.False(model.CanCreate);

        model.Selected = model.Rows.First(r => r.Kind == RowKind.FreeSpace);
        Assert.True(model.CanCreate);
        Assert.False(model.CanDelete);
    }

    [Fact]
    public void Сбой_перечисления_показывается_текстом_и_список_остаётся_пустым()
    {
        var model = new DiskPickerViewModel(
            new FakeEnumerator(DiskSnapshot.Failed("WMI недоступно")),
            new NoopInspector(),
            new EmptyFileSystem());

        model.Refresh();

        Assert.Empty(model.Rows);
        Assert.Equal("WMI недоступно", model.EnumerationError);
    }
}
