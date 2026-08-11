using System.Collections.Generic;
using System.Linq;
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

public class FreeSpaceCalculatorTests
{
    private const ulong Mib = 1024UL * 1024UL;
    private const ulong Gib = 1024UL * Mib;

    private static PartitionInfo Partition(ulong offset, ulong size, int number = 1)
        => new(number, offset, size, PartitionKind.BasicData, driveLetter: null,
            isSystem: false, isHidden: false, volume: null);

    [Fact]
    public void Пустой_диск_даёт_один_промежуток_с_учётом_служебного_запаса()
    {
        var gaps = FreeSpaceCalculator.Calculate(100 * Gib, new List<PartitionInfo>());

        var gap = Assert.Single(gaps);
        Assert.Equal(Mib, gap.Offset);
        Assert.Equal(100 * Gib - 2 * Mib, gap.Size);
    }

    [Fact]
    public void Промежуток_между_разделами_находится()
    {
        var partitions = new List<PartitionInfo>
        {
            Partition(Mib, 10 * Gib, 1),
            Partition(20 * Gib, 10 * Gib, 2),
        };

        var gaps = FreeSpaceCalculator.Calculate(100 * Gib, partitions);

        Assert.Contains(gaps, g => g.Offset == Mib + 10 * Gib && g.Size == 20 * Gib - (Mib + 10 * Gib));
    }

    [Fact]
    public void Хвост_после_последнего_раздела_находится()
    {
        var partitions = new List<PartitionInfo> { Partition(Mib, 10 * Gib, 1) };

        var gaps = FreeSpaceCalculator.Calculate(100 * Gib, partitions);

        var tail = gaps.Last();
        Assert.Equal(Mib + 10 * Gib, tail.Offset);
        Assert.Equal(100 * Gib - Mib - (Mib + 10 * Gib), tail.Size);
    }

    [Fact]
    public void Промежутки_меньше_мегабайта_не_показываются()
    {
        var partitions = new List<PartitionInfo>
        {
            Partition(Mib, 10 * Gib, 1),
            Partition(Mib + 10 * Gib + 4096, 10 * Gib, 2),
        };

        var gaps = FreeSpaceCalculator.Calculate(100 * Gib, partitions);

        Assert.DoesNotContain(gaps, g => g.Size < Mib);
    }

    [Fact]
    public void Разделы_в_произвольном_порядке_обрабатываются_правильно()
    {
        var partitions = new List<PartitionInfo>
        {
            Partition(50 * Gib, 10 * Gib, 2),
            Partition(Mib, 10 * Gib, 1),
        };

        var gaps = FreeSpaceCalculator.Calculate(100 * Gib, partitions);

        Assert.Equal(2, gaps.Count);
        Assert.True(gaps[0].Offset < gaps[1].Offset);
    }

    [Fact]
    public void Заполненный_до_конца_диск_не_даёт_промежутков()
    {
        var partitions = new List<PartitionInfo> { Partition(Mib, 100 * Gib - 2 * Mib, 1) };

        Assert.Empty(FreeSpaceCalculator.Calculate(100 * Gib, partitions));
    }
}
