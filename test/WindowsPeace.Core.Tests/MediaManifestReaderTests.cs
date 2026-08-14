using System;
using System.IO;
using WindowsPeace.Core.Media;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Опись читается раньше рецепта: по ней строится первый экран и по ней
/// носитель опознаёт сам себя. Молча продолжать нельзя ни в одном случае —
/// дальше по пути форматирование диска, и «наверное, там было что-то похожее»
/// не годится. Отсюда четыре разных исхода вместо «прочиталось или нет».
/// </summary>
public class MediaManifestReaderTests
{
    private const string Whole = """
    {
      "schemaVersion": 1,
      "buildId": "8f3c9d2e",
      "createdUtc": "2026-08-14T12:00:00Z",
      "recipes": [{
        "id": "atlas-25h2-ru",
        "name": "Atlas 25H2 RU",
        "recipeFile": "recipes\\atlas.recipe.json",
        "image": { "file": "sources\\install.wim", "index": 1, "imageName": "Windows 11 Pro" }
      }]
    }
    """;

    [Fact]
    public void Целая_опись_читается()
    {
        var result = MediaManifestReader.Read(Whole);

        Assert.Equal(MediaManifestStatus.Ok, result.Status);
        Assert.Single(result.Manifest!.Recipes);
        Assert.Equal("Atlas 25H2 RU", result.Manifest!.Recipes[0].Name);
        Assert.Equal(1, result.Manifest!.Recipes[0].Image.Index);
    }

    [Fact]
    public void Испорченный_текст_объявляется_повреждением()
    {
        var result = MediaManifestReader.Read("{ это не json ");

        Assert.Equal(MediaManifestStatus.Damaged, result.Status);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public void Версия_из_будущего_не_читается_молча()
    {
        var result = MediaManifestReader.Read(Whole.Replace("\"schemaVersion\": 1", "\"schemaVersion\": 99"));

        Assert.Equal(MediaManifestStatus.TooNew, result.Status);
        Assert.Contains("99", result.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Пустой_список_рецептов_это_отдельный_исход()
    {
        var result = MediaManifestReader.Read("""
        { "schemaVersion": 1, "buildId": "a", "createdUtc": "2026-08-14T12:00:00Z", "recipes": [] }
        """);

        Assert.Equal(MediaManifestStatus.NoRecipes, result.Status);
        Assert.NotEmpty(result.Message);
    }

    [Fact]
    public void Рецепт_без_обязательного_поля_это_повреждение()
    {
        var result = MediaManifestReader.Read("""
        { "schemaVersion": 1, "buildId": "a", "createdUtc": "2026-08-14T12:00:00Z",
          "recipes": [ { "id": "x", "name": "X" } ] }
        """);

        Assert.Equal(MediaManifestStatus.Damaged, result.Status);
    }

    [Fact]
    public void Без_версии_формата_опись_считается_повреждённой()
    {
        var result = MediaManifestReader.Read("""
        { "buildId": "a", "createdUtc": "2026-08-14T12:00:00Z", "recipes": [] }
        """);

        Assert.Equal(MediaManifestStatus.Damaged, result.Status);
    }

    [Fact]
    public void Пустой_файл_это_повреждение_а_не_отсутствие_рецептов()
    {
        // Разница существенная: «рецептов нет» — исправный носитель без начинки,
        // а пустой файл означает, что запись оборвалась на середине.
        var result = MediaManifestReader.Read(string.Empty);

        Assert.Equal(MediaManifestStatus.Damaged, result.Status);
    }

    [Fact]
    public void Прямая_косая_черта_в_пути_принимается_наравне_с_обратной()
    {
        // Опись пишется на Windows, но её могут собрать и другим средством.
        // Читатель приводит путь к одному виду, чтобы дальше об этом никто
        // не думал: спека раздел 5.
        var result = MediaManifestReader.Read(
            Whole.Replace(@"recipes\\atlas.recipe.json", "recipes/atlas.recipe.json"));

        Assert.Equal(MediaManifestStatus.Ok, result.Status);
        Assert.Equal(@"recipes\atlas.recipe.json", result.Manifest!.Recipes[0].RecipeFile);
    }

    [Fact]
    public void Образец_из_контракта_читается_этим_же_разборщиком()
    {
        // Контракт и код обязаны сходиться. Образец лежит в contract/examples,
        // и если он разойдётся с разборщиком, ломаться должна сборка,
        // а не установка на чужой машине через полгода.
        var result = MediaManifestReader.Read(File.ReadAllText(ContractExample()));

        Assert.Equal(MediaManifestStatus.Ok, result.Status);
        var recipe = Assert.Single(result.Manifest!.Recipes);
        Assert.Equal("atlas-25h2-ru", recipe.Id);
        Assert.Equal(@"sources\install.wim", recipe.Image.File);
        Assert.Equal(9711000000UL, recipe.Image.SizeBytes);
        Assert.Equal(new DateTimeOffset(2026, 8, 14, 12, 0, 0, TimeSpan.Zero), result.Manifest!.CreatedUtc);
    }

    private static string ContractExample()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "WindowsPeace.sln")))
        {
            directory = directory.Parent;
        }

        Assert.NotNull(directory);
        return Path.Combine(directory!.FullName, "contract", "examples", "one-recipe.media.json");
    }
}
