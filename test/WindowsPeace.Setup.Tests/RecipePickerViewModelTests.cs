using System;
using WindowsPeace.Core.Media;
using WindowsPeace.Setup.Pages;
using Xunit;

namespace WindowsPeace.Setup.Tests;

public class RecipePickerViewModelTests
{
    private static MediaManifestResult OneRecipe() => MediaManifestReader.Read("""
    { "schemaVersion": 1, "buildId": "a", "createdUtc": "2026-08-14T12:00:00Z",
      "recipes": [ { "id": "atlas", "name": "Atlas 25H2 RU", "recipeFile": "r.json",
                     "image": { "file": "sources\\install.wim", "index": 1 } } ] }
    """);

    private static RecipePickerViewModel Screen(MediaManifestResult result) => new(result);

    [Fact]
    public void Экран_ничего_не_выбирает_за_человека()
    {
        var page = Screen(OneRecipe());

        Assert.Single(page.Recipes);
        Assert.Null(page.SelectedRecipe);
        Assert.False(page.CanGoNext);
    }

    [Fact]
    public void После_выбора_можно_идти_дальше()
    {
        var page = Screen(OneRecipe());
        page.SelectedRow = page.Recipes[0];

        Assert.NotNull(page.SelectedRecipe);
        Assert.Equal("atlas", page.SelectedRecipe!.Id);
        Assert.True(page.CanGoNext);
    }

    [Fact]
    public void Выбор_сообщается_оболочке_чтобы_ожила_кнопка_Далее()
    {
        var page = Screen(OneRecipe());
        var told = 0;
        page.CanGoNextChanged += (_, _) => told++;

        page.SelectedRow = page.Recipes[0];

        Assert.Equal(1, told);
    }

    [Fact]
    public void Повреждённая_опись_объясняется_и_дальше_не_пускает()
    {
        var page = Screen(MediaManifestReader.Read("{ мусор"));

        Assert.Empty(page.Recipes);
        Assert.False(page.CanGoNext);
        Assert.True(page.HasTrouble);
        Assert.Contains("испорчена", page.Trouble, StringComparison.Ordinal);
    }

    /// <summary>
    /// Текст разборщика JSON приходит по-английски и объясняет человеку у флешки
    /// ровно ничего. На экран он не попадает вовсе — только в журнал, разбираться
    /// в наших ошибках человеку незачем. Решение автора.
    /// </summary>
    [Fact]
    public void Технический_текст_на_экран_не_попадает()
    {
        var damaged = MediaManifestReader.Read("{ мусор");
        var page = Screen(damaged);

        Assert.DoesNotContain("json", page.Trouble, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(damaged.Message, page.Trouble);

        // А в журнал причина уходит: без неё разбирать нечего.
        Assert.False(string.IsNullOrEmpty(damaged.Detail));
    }

    [Fact]
    public void Опись_из_будущего_называет_настоящую_причину()
    {
        var page = Screen(MediaManifestReader.Read("""
        { "schemaVersion": 99, "buildId": "a", "createdUtc": "2026-08-14T12:00:00Z", "recipes": [] }
        """));

        Assert.False(page.CanGoNext);
        Assert.Contains("более новой версией", page.Trouble, StringComparison.Ordinal);
    }

    [Fact]
    public void Пустой_список_рецептов_объясняется_своими_словами()
    {
        var page = Screen(MediaManifestReader.Read("""
        { "schemaVersion": 1, "buildId": "a", "createdUtc": "2026-08-14T12:00:00Z", "recipes": [] }
        """));

        Assert.False(page.CanGoNext);
        Assert.Contains("ни одного рецепта", page.Trouble, StringComparison.Ordinal);
    }

    [Fact]
    public void Носитель_не_найден_вовсе()
    {
        var page = RecipePickerViewModel.WithoutMedia();

        Assert.Empty(page.Recipes);
        Assert.False(page.CanGoNext);
        Assert.True(page.HasTrouble);
        Assert.Contains("не найден", page.Trouble, StringComparison.Ordinal);
    }

    /// <summary>
    /// Выход из тупика есть, но не здесь: «Выйти из установщика» стоит
    /// в нижнем ряду оболочки и одинакова на всех экранах — искать её
    /// в разных местах разных экранов человек не должен. См. ShellViewModelTests.
    /// </summary>
    [Fact]
    public void Когда_всё_в_порядке_беды_на_экране_нет()
    {
        var page = Screen(OneRecipe());

        Assert.False(page.HasTrouble);
        Assert.Equal(string.Empty, page.Trouble);
    }
}
