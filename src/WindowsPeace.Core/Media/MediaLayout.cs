namespace WindowsPeace.Core.Media;

/// <summary>
/// Раскладка носителя Windows Peace: что и под каким именем на нём лежит.
///
/// Одно место на весь проект. Эти же имена знает сборщик носителя, и разойтись
/// им нельзя: мастер ищет опись по имени, а не по содержимому, и промах здесь
/// означает, что носитель не опознан — то есть предложен под форматирование.
/// </summary>
public static class MediaLayout
{
    /// <summary>Опись в корне раздела данных. По ней носитель опознаёт сам себя.</summary>
    public const string ManifestFileName = "windows-peace-media.json";

    /// <summary>Папка с самим мастером.</summary>
    public const string AppFolderName = "WindowsPeace";

    /// <summary>Папка с рецептами. Пути к ним опись задаёт сама, это лишь принятое место.</summary>
    public const string RecipesFolderName = "recipes";

    /// <summary>Папка с образами Windows.</summary>
    public const string ImagesFolderName = "sources";
}
