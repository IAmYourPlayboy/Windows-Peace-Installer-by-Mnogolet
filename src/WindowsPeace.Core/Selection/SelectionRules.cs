using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using WindowsPeace.Core.Storage;
using CoreLocalization = WindowsPeace.Core.Localization;

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
            _ => SelectionVerdict.Denied(CoreLocalization.Localization.Current[CoreLocalization.Keys.Sel.DenyUnknownTarget]),
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
            return SelectionVerdict.Denied(CoreLocalization.Localization.Current[CoreLocalization.Keys.Sel.DenyMedia]);
        }

        if (disk.IsSystem || disk.IsBoot)
        {
            return SelectionVerdict.Denied(CoreLocalization.Localization.Current[CoreLocalization.Keys.Sel.DenySystem]);
        }

        if (disk.IsOffline)
        {
            return SelectionVerdict.Denied(CoreLocalization.Localization.Current[CoreLocalization.Keys.Sel.DenyOffline]);
        }

        if (disk.IsReadOnly)
        {
            return SelectionVerdict.Denied(CoreLocalization.Localization.Current[CoreLocalization.Keys.Sel.DenyReadOnly]);
        }

        return SelectionVerdict.Allowed;
    }

    private static SelectionVerdict EvaluatePartition(SelectionTarget target)
    {
        var partition = target.Partition!;

        if (PartitionKinds.IsSystemService(partition.Kind))
        {
            return SelectionVerdict.Denied(CoreLocalization.Localization.Current[CoreLocalization.Keys.Sel.DenyService]);
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
            CoreLocalization.Localization.Current[CoreLocalization.Keys.Sel.TooSmall],
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
                CoreLocalization.Localization.Current[CoreLocalization.Keys.Warn.WindowsOnTarget]);
        }

        if (affected.Any(p => p.Content.UserFilesFound))
        {
            Add(WarningKind.UserFilesOnTarget, WarningSeverity.Important,
                CoreLocalization.Localization.Current[CoreLocalization.Keys.Warn.UserFilesOnTarget]);
        }

        if (target.Disk.ProbeError is not null)
        {
            Add(WarningKind.PartitionsNotRead, WarningSeverity.Important,
                CoreLocalization.Localization.Current[CoreLocalization.Keys.Warn.PartitionsNotRead]);
        }

        if (affected.Any(p => !p.Content.Inspected))
        {
            Add(WarningKind.ContentNotInspected, WarningSeverity.Notice,
                CoreLocalization.Localization.Current[CoreLocalization.Keys.Warn.ContentNotInspected]);
        }

        if (target.Disk.Identity.Confidence != IdentityConfidence.Hardware)
        {
            Add(WarningKind.WeakIdentity, WarningSeverity.Notice,
                CoreLocalization.Localization.Current[CoreLocalization.Keys.Warn.WeakIdentity]);
        }

        var otherWindows = allDisks
            .Where(d => !ReferenceEquals(d, target.Disk))
            .SelectMany(d => d.Partitions)
            .Any(p => p.Content.WindowsFound);

        if (otherWindows)
        {
            Add(WarningKind.OtherWindowsFound, WarningSeverity.Notice,
                CoreLocalization.Localization.Current[CoreLocalization.Keys.Warn.OtherWindows]);
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
