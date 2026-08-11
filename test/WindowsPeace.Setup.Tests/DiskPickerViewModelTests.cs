using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
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

/// <summary>
/// Источник, который никогда не отвечает сам — только по отмене. Нужен, чтобы
/// проверить правило из раздела 9 архитектуры: ни одной операции без предельного
/// времени и без возможности прервать.
/// </summary>
internal sealed class NeverAnsweringEnumerator : IDiskEnumerator
{
    private readonly TaskCompletionSource<bool> _started =
        new(TaskCreationOptions.RunContinuationsAsynchronously);

    public Task Started => _started.Task;

    public DiskSnapshot Enumerate(CancellationToken cancellationToken)
    {
        _started.TrySetResult(true);
        cancellationToken.WaitHandle.WaitOne();
        cancellationToken.ThrowIfCancellationRequested();
        return new DiskSnapshot(Array.Empty<DiskInfo>(), null);
    }
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

    private static async Task<DiskPickerViewModel> CreateAsync(params DiskInfo[] disks)
    {
        var model = new DiskPickerViewModel(
            new FakeEnumerator(new DiskSnapshot(disks, null)),
            new NoopInspector(),
            new EmptyFileSystem());

        await model.RefreshAsync();
        return model;
    }

    [Fact]
    public async Task Диски_попадают_в_список()
    {
        var model = await CreateAsync(Disk("A", 500 * Gib), Disk("B", 1000 * Gib));

        Assert.Equal(2, model.Rows.Count(r => r.Kind == RowKind.Disk));
    }

    [Fact]
    public async Task Разделы_идут_строками_под_своим_диском()
    {
        var partition = new PartitionInfo(1, 1048576UL, 100 * Gib, PartitionKind.BasicData, 'C', false, false, null);
        var model = await CreateAsync(Disk("A", 500 * Gib, partitions: new[] { partition }));

        Assert.Equal(RowKind.Disk, model.Rows[0].Kind);
        Assert.Equal(RowKind.Partition, model.Rows[1].Kind);
    }

    [Fact]
    public async Task Незанятое_пространство_показывается_отдельной_строкой()
    {
        var model = await CreateAsync(Disk("A", 500 * Gib));

        Assert.Contains(model.Rows, r => r.Kind == RowKind.FreeSpace);
    }

    [Fact]
    public async Task Пока_ничего_не_выбрано_идти_дальше_нельзя()
    {
        var model = await CreateAsync(Disk("A", 500 * Gib));

        Assert.False(model.CanGoNext);
    }

    [Fact]
    public async Task Выбор_допустимого_диска_разрешает_идти_дальше_и_строит_план()
    {
        var model = await CreateAsync(Disk("A", 500 * Gib));

        model.Selected = model.Rows.First(r => r.Kind == RowKind.Disk);

        Assert.True(model.CanGoNext);
        Assert.Contains("EFI", model.PlanSummary);
    }

    [Fact]
    public async Task Выбор_запрещённого_диска_не_разрешает_идти_дальше_и_объясняет_причину()
    {
        var model = await CreateAsync(Disk("A", 500 * Gib, isSystem: true));

        model.Selected = model.Rows.First(r => r.Kind == RowKind.Disk);

        Assert.False(model.CanGoNext);
        Assert.False(string.IsNullOrEmpty(model.DenialReason));
    }

    [Fact]
    public async Task Кнопки_разделов_включаются_по_виду_выбранной_строки()
    {
        var partition = new PartitionInfo(1, 1048576UL, 100 * Gib, PartitionKind.BasicData, 'C', false, false, null);
        var model = await CreateAsync(Disk("A", 500 * Gib, partitions: new[] { partition }));

        model.Selected = model.Rows.First(r => r.Kind == RowKind.Partition);
        Assert.True(model.CanDelete);
        Assert.False(model.CanCreate);

        model.Selected = model.Rows.First(r => r.Kind == RowKind.FreeSpace);
        Assert.True(model.CanCreate);
        Assert.False(model.CanDelete);
    }

    [Fact]
    public async Task Сбой_перечисления_показывается_текстом_и_список_остаётся_пустым()
    {
        var model = new DiskPickerViewModel(
            new FakeEnumerator(DiskSnapshot.Failed("WMI недоступно")),
            new NoopInspector(),
            new EmptyFileSystem());

        await model.RefreshAsync();

        Assert.Empty(model.Rows);
        Assert.Equal("WMI недоступно", model.EnumerationError);
    }

    [Fact]
    public async Task Пока_опрос_идёт_он_помечен_идущим_и_повторно_не_запускается()
    {
        var enumerator = new NeverAnsweringEnumerator();
        var model = new DiskPickerViewModel(enumerator, new NoopInspector(), new EmptyFileSystem());

        var running = model.RefreshAsync();
        await enumerator.Started;

        Assert.True(model.IsBusy);
        Assert.False(model.RefreshCommand.CanExecute(null));
        Assert.True(model.CancelCommand.CanExecute(null));

        await model.RefreshAsync();
        Assert.True(model.IsBusy);

        model.Cancel();
        await running;
    }

    [Fact]
    public async Task Отмена_прекращает_опрос_и_объясняет_это_словами()
    {
        var enumerator = new NeverAnsweringEnumerator();
        var model = new DiskPickerViewModel(enumerator, new NoopInspector(), new EmptyFileSystem());

        var running = model.RefreshAsync();
        await enumerator.Started;

        model.CancelCommand.Execute(null);
        await running;

        Assert.False(model.IsBusy);
        Assert.Empty(model.Rows);
        Assert.False(string.IsNullOrEmpty(model.EnumerationError));
        Assert.Equal(string.Empty, model.StatusText);
        Assert.True(model.RefreshCommand.CanExecute(null));
    }

    [Fact]
    public async Task Опрос_рассказывает_чем_занят_и_замолкает_по_окончании()
    {
        var enumerator = new NeverAnsweringEnumerator();
        var model = new DiskPickerViewModel(enumerator, new NoopInspector(), new EmptyFileSystem());

        var running = model.RefreshAsync();
        await enumerator.Started;

        Assert.False(string.IsNullOrEmpty(model.StatusText));

        model.Cancel();
        await running;

        Assert.Equal(string.Empty, model.StatusText);
    }
}
