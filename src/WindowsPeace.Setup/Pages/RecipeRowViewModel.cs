using System.Globalization;
using WindowsPeace.Core.Media;
using WindowsPeace.Core.Storage;

namespace WindowsPeace.Setup.Pages;

/// <summary>Один рецепт в списке «что ставим».</summary>
public sealed class RecipeRowViewModel
{
    public RecipeRowViewModel(MediaRecipe recipe)
    {
        Recipe = recipe;
    }

    public MediaRecipe Recipe { get; }

    public string Name => Recipe.Name;

    public string Description => Recipe.Description ?? string.Empty;

    /// <summary>
    /// Издание Windows. В описи оно необязательно, но показать пустоту нельзя:
    /// человек должен видеть, что именно ставится. Поэтому в ход идёт имя файла
    /// образа — оно есть всегда.
    /// </summary>
    public string Image => string.IsNullOrEmpty(Recipe.Image.ImageName)
        ? Recipe.Image.File
        : Recipe.Image.ImageName!;

    /// <summary>Объём образа. Пусто, когда в описи его нет: ноль был бы неправдой.</summary>
    public string Size => Recipe.Image.SizeBytes is { } bytes
        ? ByteSize.Format(bytes)
        : string.Empty;

    // Средствам доступности строка обязана называть себя по-человечески,
    // а не именем класса. Тот же дефект уже ловили на шаге А.
    public override string ToString() => string.IsNullOrEmpty(Description)
        ? Name
        : string.Format(CultureInfo.CurrentCulture, "{0}. {1}", Name, Description);
}
