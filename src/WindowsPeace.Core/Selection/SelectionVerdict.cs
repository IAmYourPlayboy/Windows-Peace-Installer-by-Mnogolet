namespace WindowsPeace.Core.Selection;

/// <summary>Можно ли выбрать цель. Отказ всегда сопровождается причиной.</summary>
public sealed class SelectionVerdict
{
    private SelectionVerdict(bool isAllowed, string? reason)
    {
        IsAllowed = isAllowed;
        Reason = reason;
    }

    public static SelectionVerdict Allowed { get; } = new(true, null);

    public static SelectionVerdict Denied(string reason) => new(false, reason);

    public bool IsAllowed { get; }
    public string? Reason { get; }
}
