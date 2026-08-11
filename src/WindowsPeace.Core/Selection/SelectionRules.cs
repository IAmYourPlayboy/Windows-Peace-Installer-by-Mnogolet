using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Core.Selection;

/// <summary>
/// Что выбрать можно, что нельзя и о чём предупредить. Живёт отдельно от интерфейса,
/// потому что теми же правилами будут пользоваться Studio и автоматический режим
/// из рецепта. См. docs/superpowers/specs/2026-08-10-disk-picker-design.md, раздел 5.
/// </summary>
public static class SelectionRules
{
    private const ulong Gib = 1024UL * 1024UL * 1024UL;

    /// <summary>
    /// Нижняя граница windowsSizeGB из contract/recipe.schema.json.
    /// Расхождение со схемой ловится тестом DeploymentLayoutTests.
    /// </summary>
    public const ulong MinimumWindowsPartitionBytes = 40UL * Gib;

    public static SelectionVerdict Evaluate(SelectionTarget target)
    {
        var diskVerdict = EvaluateDisk(target.Disk);
        if (!diskVerdict.IsAllowed)
        {
            return diskVerdict;
        }

        return target.Kind switch
        {
            TargetKind.WholeDisk => Allowed(target),
            TargetKind.ExistingPartition => EvaluatePartition(target),
            TargetKind.FreeSpace => EvaluateSize(target.FreeSpace!.Size),
            _ => SelectionVerdict.Denied("Неизвестный вид цели"),
        };
    }

    private static SelectionVerdict Allowed(SelectionTarget target)
        => target.Disk.Identity.SizeBytes < MinimumWindowsPartitionBytes
            ? EvaluateSize(target.Disk.Identity.SizeBytes)
            : SelectionVerdict.Allowed;

    private static SelectionVerdict EvaluateDisk(DiskInfo disk)
    {
        if (disk.IsWindowsPeaceMedia)
        {
            return SelectionVerdict.Denied("Это загрузочный носитель Windows Peace — установка сюда невозможна");
        }

        if (disk.IsSystem || disk.IsBoot)
        {
            return SelectionVerdict.Denied("На этом диске работает текущая система");
        }

        if (disk.IsOffline)
        {
            return SelectionVerdict.Denied("Диск отключён");
        }

        if (disk.IsReadOnly)
        {
            return SelectionVerdict.Denied("Диск защищён от записи");
        }

        return SelectionVerdict.Allowed;
    }

    private static SelectionVerdict EvaluatePartition(SelectionTarget target)
    {
        var partition = target.Partition!;

        if (PartitionKinds.IsSystemService(partition.Kind))
        {
            return SelectionVerdict.Denied("Это служебный раздел, система создаёт его сама");
        }

        return EvaluateSize(partition.Size);
    }

    private static SelectionVerdict EvaluateSize(ulong sizeBytes)
    {
        if (sizeBytes >= MinimumWindowsPartitionBytes)
        {
            return SelectionVerdict.Allowed;
        }

        var missingGib = (MinimumWindowsPartitionBytes - sizeBytes + Gib - 1) / Gib;
        var text = string.Format(
            CultureInfo.CurrentCulture,
            "Слишком мало места: не хватает {0} ГБ до минимальных 40 ГБ",
            missingGib);

        return SelectionVerdict.Denied(text);
    }

    public static IReadOnlyList<PlanWarning> Warnings(SelectionTarget target, IReadOnlyList<DiskInfo> allDisks)
    {
        var warnings = new List<PlanWarning>();
        var seen = new HashSet<WarningKind>();

        void Add(WarningKind kind, WarningSeverity severity, string text)
        {
            if (seen.Add(kind))
            {
                warnings.Add(new PlanWarning(kind, severity, text));
            }
        }

        var affected = AffectedPartitions(target);

        if (affected.Any(p => p.Content.WindowsFound))
        {
            Add(WarningKind.WindowsOnTarget, WarningSeverity.Important,
                "На цели установлена Windows. Она будет удалена безвозвратно.");
        }

        if (affected.Any(p => p.Content.UserFilesFound))
        {
            Add(WarningKind.UserFilesOnTarget, WarningSeverity.Important,
                "На цели есть файлы пользователя. Они будут удалены безвозвратно.");
        }

        if (target.Disk.ProbeError is not null)
        {
            Add(WarningKind.PartitionsNotRead, WarningSeverity.Important,
                "Разделы этого диска прочитать не удалось, поэтому неизвестно, что на нём лежит.");
        }

        if (affected.Any(p => !p.Content.Inspected))
        {
            Add(WarningKind.ContentNotInspected, WarningSeverity.Notice,
                "Содержимое части разделов проверить не удалось: у них нет буквы диска.");
        }

        if (target.Disk.Identity.Confidence != IdentityConfidence.Hardware)
        {
            Add(WarningKind.WeakIdentity, WarningSeverity.Notice,
                "У диска не удалось прочитать серийный номер, опознать его надёжно нельзя.");
        }

        var otherWindows = allDisks
            .Where(d => !ReferenceEquals(d, target.Disk))
            .SelectMany(d => d.Partitions)
            .Any(p => p.Content.WindowsFound);

        if (otherWindows)
        {
            Add(WarningKind.OtherWindowsFound, WarningSeverity.Notice,
                "На другом диске найдена установленная Windows. Она может перехватывать загрузку.");
        }

        return warnings;
    }

    private static IReadOnlyList<PartitionInfo> AffectedPartitions(SelectionTarget target) => target.Kind switch
    {
        TargetKind.WholeDisk => target.Disk.Partitions,
        TargetKind.ExistingPartition => new[] { target.Partition! },
        _ => new List<PartitionInfo>(),
    };
}
