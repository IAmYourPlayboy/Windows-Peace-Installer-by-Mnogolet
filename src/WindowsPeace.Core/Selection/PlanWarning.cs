namespace WindowsPeace.Core.Selection;

/// <summary>Разновидность предупреждения. По ней интерфейс подбирает вид и порядок.</summary>
public enum WarningKind
{
    WindowsOnTarget,
    UserFilesOnTarget,
    OtherWindowsFound,
    WeakIdentity,
    ContentNotInspected,
    PartitionsNotRead,
}

/// <summary>Насколько предупреждение серьёзно.</summary>
public enum WarningSeverity
{
    Notice,
    Important,
}

/// <summary>Предупреждение с готовым текстом для человека.</summary>
public sealed class PlanWarning
{
    public PlanWarning(WarningKind kind, WarningSeverity severity, string text)
    {
        Kind = kind;
        Severity = severity;
        Text = text;
    }

    public WarningKind Kind { get; }
    public WarningSeverity Severity { get; }
    public string Text { get; }
}
