using System.Collections.Generic;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Core.Selection;

/// <summary>Один раздел будущей разметки.</summary>
public sealed class PlanStep
{
    public PlanStep(PartitionKind kind, string title, ulong sizeBytes)
    {
        Kind = kind;
        Title = title;
        SizeBytes = sizeBytes;
    }

    public PartitionKind Kind { get; }
    public string Title { get; }
    public ulong SizeBytes { get; }
}

/// <summary>Предпросмотр того, что будет сделано. Ничего не выполняет.</summary>
public sealed class DeploymentPlan
{
    public DeploymentPlan(IReadOnlyList<PlanStep> steps, bool wipesWholeDisk, string summary)
    {
        Steps = steps;
        WipesWholeDisk = wipesWholeDisk;
        Summary = summary;
    }

    public IReadOnlyList<PlanStep> Steps { get; }
    public bool WipesWholeDisk { get; }
    public string Summary { get; }
}
