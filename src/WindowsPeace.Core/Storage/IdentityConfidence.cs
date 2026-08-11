namespace WindowsPeace.Core.Storage;

/// <summary>Откуда взят опознавательный признак диска.</summary>
public enum IdentitySource
{
    None = 0,
    PhysicalDisk,
    Disk,
    Win32DiskDrive,
    UniqueId,
    GptGuid,
}

/// <summary>
/// Насколько признаку можно верить. Определяет, годится ли диск
/// для режима pinned из рецепта: см. contract/recipe.schema.json, diskFingerprint.
/// </summary>
public enum IdentityConfidence
{
    /// <summary>Опознать нечем. Только выбор человеком, с предупреждением.</summary>
    None = 0,

    /// <summary>Признак меняется при переразметке. Годен внутри одного сеанса.</summary>
    Volatile,

    /// <summary>Признак принадлежит устройству и переживает переразметку.</summary>
    Hardware,
}
