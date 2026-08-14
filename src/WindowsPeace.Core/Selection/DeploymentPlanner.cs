using System.Collections.Generic;
using System.Linq;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Core.Selection;

/// <summary>Строит предпросмотр разметки по выбранной цели.</summary>
public static class DeploymentPlanner
{
    private const ulong Mib = 1024UL * 1024UL;

    public static DeploymentPlan Build(SelectionTarget target)
        => target.Kind == TargetKind.WholeDisk
            ? BuildWholeDisk(target, DeploymentLayout.Default)
            : BuildSingle(target);

    private static DeploymentPlan BuildWholeDisk(SelectionTarget target, DeploymentLayout layout)
    {
        var esp = (ulong)layout.EspMb * Mib;
        var msr = (ulong)layout.MsrMb * Mib;
        var recovery = (ulong)layout.RecoveryMb * Mib;
        var total = target.Disk.Identity.SizeBytes;
        var service = esp + msr + recovery;
        var windows = total > service ? total - service : 0UL;

        var steps = new List<PlanStep>
        {
            new(PartitionKind.EfiSystem, "EFI", esp),
            new(PartitionKind.MicrosoftReserved, "MSR", msr),
            new(PartitionKind.BasicData, "Windows", windows),
            new(PartitionKind.WindowsRecovery, "Восстановление", recovery),
        };

        return new DeploymentPlan(steps, wipesWholeDisk: true, summary: Summarize(steps));
    }

    private static DeploymentPlan BuildSingle(SelectionTarget target)
    {
        var steps = new List<PlanStep>
        {
            new(PartitionKind.BasicData, "Windows", target.AvailableBytes),
        };

        return new DeploymentPlan(steps, wipesWholeDisk: false,
            summary: "Windows " + ByteSize.Format(target.AvailableBytes) + ". Остальные разделы не изменяются.");
    }

    private static string Summarize(IEnumerable<PlanStep> steps)
        => string.Join(" · ", steps.Select(s => s.Title + " " + ByteSize.Format(s.SizeBytes)));
}
