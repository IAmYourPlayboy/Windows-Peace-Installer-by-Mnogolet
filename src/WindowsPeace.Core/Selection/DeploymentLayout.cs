namespace WindowsPeace.Core.Selection;

/// <summary>
/// Размеры служебных разделов. На шаге А берутся из значений по умолчанию
/// схемы рецепта; на шаге В будут читаться из самого рецепта.
/// Источник: contract/recipe.schema.json, target.layout.
/// </summary>
public sealed class DeploymentLayout
{
    private DeploymentLayout(int espMb, int msrMb, int recoveryMb, bool recoveryAtEnd)
    {
        EspMb = espMb;
        MsrMb = msrMb;
        RecoveryMb = recoveryMb;
        RecoveryAtEnd = recoveryAtEnd;
    }

    public static DeploymentLayout Default { get; } = new(espMb: 300, msrMb: 16, recoveryMb: 1024, recoveryAtEnd: true);

    public int EspMb { get; }
    public int MsrMb { get; }
    public int RecoveryMb { get; }
    public bool RecoveryAtEnd { get; }
}
