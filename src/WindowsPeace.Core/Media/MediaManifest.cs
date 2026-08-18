using System;
using System.Collections.Generic;

namespace WindowsPeace.Core.Media;

/// <summary>Один образ Windows, лежащий на носителе.</summary>
public sealed class MediaImage
{
    /// <summary>Путь к образу от корня раздела, где лежит опись.</summary>
    public string File { get; init; } = string.Empty;

    /// <summary>
    /// Номер издания внутри образа. При сборке носителя ищется по имени,
    /// а не задаётся на глаз: в разных сборках Windows порядок изданий разный,
    /// и ошибка здесь уводит установку не на то издание.
    /// </summary>
    public int Index { get; init; }

    /// <summary>Имя издания. Хранится, чтобы номер можно было перепроверить.</summary>
    public string? ImageName { get; init; }

    public ulong? SizeBytes { get; init; }
}

/// <summary>Один рецепт из описи: что человек увидит на первом экране.</summary>
public sealed class MediaRecipe
{
    /// <summary>Устойчивое имя рецепта. По нему запоминается выбор человека.</summary>
    public string Id { get; init; } = string.Empty;

    public string Name { get; init; } = string.Empty;

    public string? Description { get; init; }

    /// <summary>Путь к файлу рецепта от корня раздела, где лежит опись.</summary>
    public string RecipeFile { get; init; } = string.Empty;

    public MediaImage Image { get; init; } = new();
}

/// <summary>
/// Опись носителя. Единственный файл, который читается раньше рецепта:
/// по нему строится первый экран и по нему носитель опознаёт сам себя.
/// </summary>
public sealed class MediaManifest
{
    public int SchemaVersion { get; init; }

    /// <summary>
    /// Идентификатор сборки носителя. Нужен, чтобы отличать носители друг
    /// от друга: на шаге В — при возобновлении установки, в Studio — чтобы
    /// понять, что уже записано.
    /// </summary>
    public string BuildId { get; init; } = string.Empty;

    public DateTimeOffset CreatedUtc { get; init; }

    public IReadOnlyList<MediaRecipe> Recipes { get; init; } = new List<MediaRecipe>();
}
