using System.Collections.Generic;
using System.Linq;

namespace WindowsPeace.Core.Storage;

/// <summary>Находит незанятые промежутки между разделами.</summary>
public static class FreeSpaceCalculator
{
    private const ulong Mib = 1024UL * 1024UL;

    /// <summary>Первый мегабайт занят таблицей разделов и выравниванием.</summary>
    private const ulong HeadReserve = Mib;

    /// <summary>Последний мегабайт занят резервной копией таблицы GPT.</summary>
    private const ulong TailReserve = Mib;

    /// <summary>Промежутки меньше мегабайта бесполезны и только засоряют список.</summary>
    private const ulong MinimumUsefulGap = Mib;

    public static IReadOnlyList<FreeSpaceInfo> Calculate(ulong diskSize, IReadOnlyList<PartitionInfo> partitions)
    {
        var result = new List<FreeSpaceInfo>();

        if (diskSize <= HeadReserve + TailReserve)
        {
            return result;
        }

        var limit = diskSize - TailReserve;
        var cursor = HeadReserve;

        foreach (var partition in partitions.OrderBy(p => p.Offset))
        {
            if (partition.Offset > cursor)
            {
                AddIfUseful(result, cursor, partition.Offset);
            }

            if (partition.End > cursor)
            {
                cursor = partition.End;
            }
        }

        if (limit > cursor)
        {
            AddIfUseful(result, cursor, limit);
        }

        return result;
    }

    private static void AddIfUseful(ICollection<FreeSpaceInfo> result, ulong from, ulong to)
    {
        var size = to - from;
        if (size >= MinimumUsefulGap)
        {
            result.Add(new FreeSpaceInfo(from, size));
        }
    }
}
