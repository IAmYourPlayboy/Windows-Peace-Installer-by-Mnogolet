using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;

namespace WindowsPeace.Core.Media;

/// <summary>Чем закончилось чтение описи.</summary>
public enum MediaManifestStatus
{
    /// <summary>Опись прочитана, рецепты есть.</summary>
    Ok,

    /// <summary>Файл не разбирается или в нём не хватает обязательного.</summary>
    Damaged,

    /// <summary>Носитель собран более новой версией Windows Peace.</summary>
    TooNew,

    /// <summary>Опись цела, но ставить с носителя нечего.</summary>
    NoRecipes,
}

/// <summary>Исход чтения вместе с объяснением для человека.</summary>
public sealed class MediaManifestResult
{
    public MediaManifestResult(MediaManifestStatus status, MediaManifest? manifest, string message)
    {
        Status = status;
        Manifest = manifest;
        Message = message;
    }

    public MediaManifestStatus Status { get; }

    public MediaManifest? Manifest { get; }

    /// <summary>Что сказать человеку. Пусто только тогда, когда говорить нечего.</summary>
    public string Message { get; }
}

/// <summary>
/// Разбор описи. Молча продолжать нельзя ни в одном случае: дальше по пути
/// форматирование диска, и «наверное, там было что-то похожее» не годится.
/// Поэтому исходов четыре, а не два, и у каждого своё объяснение.
/// </summary>
public static class MediaManifestReader
{
    public const int SupportedSchemaVersion = 1;

    public static MediaManifestResult Read(string json)
    {
        JsonDocument document;
        try
        {
            document = JsonDocument.Parse(json);
        }
        catch (JsonException error)
        {
            return new MediaManifestResult(MediaManifestStatus.Damaged,
                null, "Опись носителя не разбирается: " + error.Message);
        }
        catch (ArgumentNullException)
        {
            return new MediaManifestResult(MediaManifestStatus.Damaged,
                null, "Описи носителя нет вовсе.");
        }

        using (document)
        {
            var root = document.RootElement;

            if (!root.TryGetProperty("schemaVersion", out var versionElement) ||
                versionElement.ValueKind != JsonValueKind.Number)
            {
                return new MediaManifestResult(MediaManifestStatus.Damaged,
                    null, "В описи нет версии формата.");
            }

            var version = versionElement.GetInt32();
            if (version > SupportedSchemaVersion)
            {
                return new MediaManifestResult(MediaManifestStatus.TooNew, null, string.Format(
                    CultureInfo.CurrentCulture,
                    "Носитель собран более новой версией Windows Peace: формат описи {0}, эта программа понимает {1}.",
                    version, SupportedSchemaVersion));
            }

            if (!root.TryGetProperty("recipes", out var recipesElement) ||
                recipesElement.ValueKind != JsonValueKind.Array)
            {
                return new MediaManifestResult(MediaManifestStatus.Damaged,
                    null, "В описи нет списка рецептов.");
            }

            var recipes = new List<MediaRecipe>();
            foreach (var item in recipesElement.EnumerateArray())
            {
                var recipe = ReadRecipe(item);
                if (recipe is null)
                {
                    return new MediaManifestResult(MediaManifestStatus.Damaged,
                        null, "В описи есть рецепт без обязательных полей.");
                }

                recipes.Add(recipe);
            }

            var manifest = new MediaManifest
            {
                SchemaVersion = version,
                BuildId = Text(root, "buildId") ?? string.Empty,
                CreatedUtc = Moment(root, "createdUtc"),
                Recipes = recipes,
            };

            return recipes.Count == 0
                ? new MediaManifestResult(MediaManifestStatus.NoRecipes, manifest,
                    "На носителе нет ни одного рецепта: ставить с него нечего.")
                : new MediaManifestResult(MediaManifestStatus.Ok, manifest, string.Empty);
        }
    }

    private static MediaRecipe? ReadRecipe(JsonElement element)
    {
        if (element.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var id = Text(element, "id");
        var name = Text(element, "name");
        var recipeFile = Text(element, "recipeFile");

        if (string.IsNullOrEmpty(id) || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(recipeFile) ||
            !element.TryGetProperty("image", out var imageElement))
        {
            return null;
        }

        var file = Text(imageElement, "file");
        if (string.IsNullOrEmpty(file) ||
            !imageElement.TryGetProperty("index", out var indexElement) ||
            indexElement.ValueKind != JsonValueKind.Number)
        {
            return null;
        }

        return new MediaRecipe
        {
            Id = id!,
            Name = name!,
            Description = Text(element, "description"),
            RecipeFile = Slashes(recipeFile!),
            Image = new MediaImage
            {
                File = Slashes(file!),
                Index = indexElement.GetInt32(),
                ImageName = Text(imageElement, "imageName"),
                SizeBytes = imageElement.TryGetProperty("sizeBytes", out var size) &&
                            size.ValueKind == JsonValueKind.Number
                    ? size.GetUInt64()
                    : null,
            },
        };
    }

    /// <summary>
    /// Приводит разделитель пути к одному виду. Опись пишется на Windows,
    /// но собрать её могут и другим средством; спека разрешает оба разделителя,
    /// и разбираться с этим должно одно место, а не каждый, кто берёт путь.
    /// </summary>
    private static string Slashes(string path) => path.Replace('/', '\\');

    private static string? Text(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    private static DateTimeOffset Moment(JsonElement element, string name)
        => element.TryGetProperty(name, out var value) &&
           value.ValueKind == JsonValueKind.String &&
           DateTimeOffset.TryParse(value.GetString(), CultureInfo.InvariantCulture,
               DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var moment)
            ? moment
            : DateTimeOffset.MinValue;
}
