using System.Linq;
using WindowsPeace.Core.Selection;
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

public class DeploymentPlannerTests
{
    [Fact]
    public void Для_диска_целиком_план_содержит_четыре_раздела_в_нужном_порядке()
    {
        var plan = DeploymentPlanner.Build(SelectionTarget.ForWholeDisk(TestDisks.Disk(size: 500 * TestDisks.Gib)));

        Assert.Equal(4, plan.Steps.Count);
        Assert.Equal(PartitionKind.EfiSystem, plan.Steps[0].Kind);
        Assert.Equal(PartitionKind.MicrosoftReserved, plan.Steps[1].Kind);
        Assert.Equal(PartitionKind.BasicData, plan.Steps[2].Kind);
        Assert.Equal(PartitionKind.WindowsRecovery, plan.Steps[3].Kind);
    }

    [Fact]
    public void Раздел_Windows_занимает_остаток_диска()
    {
        var size = 500 * TestDisks.Gib;
        var plan = DeploymentPlanner.Build(SelectionTarget.ForWholeDisk(TestDisks.Disk(size: size)));

        var windows = plan.Steps.Single(s => s.Kind == PartitionKind.BasicData);
        var service = plan.Steps.Where(s => s.Kind != PartitionKind.BasicData).Sum(s => (decimal)s.SizeBytes);

        Assert.Equal(size - (ulong)service, windows.SizeBytes);
    }

    [Fact]
    public void Для_существующего_раздела_план_состоит_из_одного_шага_и_остальное_не_трогается()
    {
        var partition = TestDisks.Partition(size: 200 * TestDisks.Gib);
        var disk = TestDisks.Disk(partitions: new[] { partition });

        var plan = DeploymentPlanner.Build(SelectionTarget.ForPartition(disk, partition));

        var step = Assert.Single(plan.Steps);
        Assert.Equal(PartitionKind.BasicData, step.Kind);
        Assert.Equal(200 * TestDisks.Gib, step.SizeBytes);
        Assert.False(plan.WipesWholeDisk);
    }

    [Fact]
    public void План_для_диска_целиком_помечен_как_стирающий_всё()
    {
        Assert.True(DeploymentPlanner.Build(SelectionTarget.ForWholeDisk(TestDisks.Disk())).WipesWholeDisk);
    }

    [Fact]
    public void Краткая_строка_плана_перечисляет_разделы_с_размерами()
    {
        var plan = DeploymentPlanner.Build(SelectionTarget.ForWholeDisk(TestDisks.Disk(size: 500 * TestDisks.Gib)));

        Assert.Contains("EFI", plan.Summary);
        Assert.Contains("300 МБ", plan.Summary);
        Assert.Contains("Восстановление", plan.Summary);
    }
}
