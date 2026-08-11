using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

public class SelectionRulesTests
{
    [Fact]
    public void Обычный_диск_выбрать_можно()
    {
        var verdict = SelectionRules.Evaluate(SelectionTarget.ForWholeDisk(TestDisks.Disk()));

        Assert.True(verdict.IsAllowed);
        Assert.Null(verdict.Reason);
    }

    [Fact]
    public void Загрузочный_носитель_выбрать_нельзя()
    {
        var verdict = SelectionRules.Evaluate(SelectionTarget.ForWholeDisk(TestDisks.Disk(isMedia: true)));

        Assert.False(verdict.IsAllowed);
        Assert.Contains("носител", verdict.Reason!, System.StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Диск_работающей_системы_выбрать_нельзя()
    {
        Assert.False(SelectionRules.Evaluate(SelectionTarget.ForWholeDisk(TestDisks.Disk(isSystem: true))).IsAllowed);
        Assert.False(SelectionRules.Evaluate(SelectionTarget.ForWholeDisk(TestDisks.Disk(isBoot: true))).IsAllowed);
    }

    [Fact]
    public void Отключённый_и_защищённый_от_записи_диски_выбрать_нельзя()
    {
        Assert.False(SelectionRules.Evaluate(SelectionTarget.ForWholeDisk(TestDisks.Disk(isOffline: true))).IsAllowed);
        Assert.False(SelectionRules.Evaluate(SelectionTarget.ForWholeDisk(TestDisks.Disk(isReadOnly: true))).IsAllowed);
    }

    [Fact]
    public void Запрет_диска_наследуется_его_разделами()
    {
        var partition = TestDisks.Partition();
        var disk = TestDisks.Disk(isSystem: true, partitions: new[] { partition });

        Assert.False(SelectionRules.Evaluate(SelectionTarget.ForPartition(disk, partition)).IsAllowed);
    }

    [Fact]
    public void Раздел_меньше_сорока_гигабайт_выбрать_нельзя_и_сказано_сколько_не_хватает()
    {
        var partition = TestDisks.Partition(size: 30 * TestDisks.Gib);
        var disk = TestDisks.Disk(partitions: new[] { partition });

        var verdict = SelectionRules.Evaluate(SelectionTarget.ForPartition(disk, partition));

        Assert.False(verdict.IsAllowed);
        Assert.Contains("10", verdict.Reason!);
    }

    [Fact]
    public void Служебный_раздел_выбрать_нельзя()
    {
        var partition = TestDisks.Partition(size: 100 * TestDisks.Gib, kind: PartitionKind.EfiSystem);
        var disk = TestDisks.Disk(partitions: new[] { partition });

        Assert.False(SelectionRules.Evaluate(SelectionTarget.ForPartition(disk, partition)).IsAllowed);
    }

    [Fact]
    public void Незанятый_промежуток_меньше_сорока_гигабайт_выбрать_нельзя()
    {
        var disk = TestDisks.Disk(size: 500 * TestDisks.Gib);
        var small = new FreeSpaceInfo(1048576UL, 30 * TestDisks.Gib);

        Assert.False(SelectionRules.Evaluate(SelectionTarget.ForFreeSpace(disk, small)).IsAllowed);
    }

    [Fact]
    public void Диск_с_нечитаемыми_разделами_выбрать_целиком_можно_а_разделы_нет()
    {
        var disk = TestDisks.Disk(probeError: "Разделы прочитать не удалось");

        Assert.True(SelectionRules.Evaluate(SelectionTarget.ForWholeDisk(disk)).IsAllowed);
    }

    [Fact]
    public void Установленная_Windows_и_файлы_пользователя_дают_два_предупреждения()
    {
        var partition = TestDisks.Partition();
        TestDisks.SetContent(partition, windows: true, userFiles: true);
        var disk = TestDisks.Disk(partitions: new[] { partition });

        var warnings = SelectionRules.Warnings(SelectionTarget.ForWholeDisk(disk), new[] { disk });

        Assert.Contains(warnings, w => w.Kind == WarningKind.WindowsOnTarget);
        Assert.Contains(warnings, w => w.Kind == WarningKind.UserFilesOnTarget);
    }

    [Fact]
    public void Windows_на_другом_диске_даёт_предупреждение_о_перехвате_загрузки()
    {
        var target = TestDisks.Disk(serial: "SN-TARGET");

        var otherPartition = TestDisks.Partition();
        TestDisks.SetContent(otherPartition, windows: true);
        var other = TestDisks.Disk(serial: "SN-OTHER", partitions: new[] { otherPartition });

        var warnings = SelectionRules.Warnings(SelectionTarget.ForWholeDisk(target), new[] { target, other });

        Assert.Contains(warnings, w => w.Kind == WarningKind.OtherWindowsFound);
    }

    [Fact]
    public void Ненадёжный_отпечаток_даёт_предупреждение()
    {
        var disk = TestDisks.Disk(serial: null);

        var warnings = SelectionRules.Warnings(SelectionTarget.ForWholeDisk(disk), new[] { disk });

        Assert.Contains(warnings, w => w.Kind == WarningKind.WeakIdentity);
    }

    [Fact]
    public void Непроверенный_раздел_даёт_предупреждение()
    {
        var partition = TestDisks.Partition(letter: null);
        var disk = TestDisks.Disk(partitions: new[] { partition });

        var warnings = SelectionRules.Warnings(SelectionTarget.ForWholeDisk(disk), new[] { disk });

        Assert.Contains(warnings, w => w.Kind == WarningKind.ContentNotInspected);
    }

    [Fact]
    public void Предупреждения_не_повторяются()
    {
        var first = TestDisks.Partition(number: 1);
        var second = TestDisks.Partition(number: 2, offset: 200 * TestDisks.Gib);
        TestDisks.SetContent(first, windows: true);
        TestDisks.SetContent(second, windows: true);
        var disk = TestDisks.Disk(partitions: new[] { first, second });

        var warnings = SelectionRules.Warnings(SelectionTarget.ForWholeDisk(disk), new[] { disk });

        Assert.Single(warnings, w => w.Kind == WarningKind.WindowsOnTarget);
    }
}
