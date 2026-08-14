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

    /// <summary>Закрытие мастера подставное: настоящее закрыло бы и сам тест.</summary>
    private static RecipePickerViewModel Screen(MediaManifestResult result, Action? onClose = null)
        => new(result, onClose ?? (() => { }));

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
    /// ровно ничего. Он остаётся на экране, но второй строкой и как подробность,
    /// а не как объяснение. Дефект нашёлся при взгляде на экран.
    /// </summary>
    [Fact]
    public void Технический_текст_идёт_подробностью_а_не_объяснением()
    {
        var page = Screen(MediaManifestReader.Read("{ мусор"));

        Assert.DoesNotContain("json", page.Trouble, StringComparison.OrdinalIgnoreCase);
        Assert.True(page.HasTroubleDetail);
        Assert.False(string.IsNullOrEmpty(page.TroubleDetail));
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
        var page = RecipePickerViewModel.WithoutMedia(new[] { @"C:\", @"X:\" }, () => { });

        Assert.False(page.CanGoNext);
        Assert.Contains("не найден", page.Trouble, StringComparison.Ordinal);

        // Где искали и что искали — подробностью: объяснение от этого списка
        // понятнее не становится, а разбираться по нему придётся.
        Assert.Contains("C:", page.TroubleDetail, StringComparison.Ordinal);
        Assert.Contains("X:", page.TroubleDetail, StringComparison.Ordinal);
        Assert.Contains(MediaLayout.ManifestFileName, page.TroubleDetail, StringComparison.Ordinal);
    }

    [Fact]
    public void Когда_разделов_нет_вовсе_так_и_говорится()
    {
        var page = RecipePickerViewModel.WithoutMedia(Array.Empty<string>(), () => { });

        Assert.False(page.CanGoNext);
        Assert.Contains("не нашлось вовсе", page.TroubleDetail, StringComparison.Ordinal);
    }

    /// <summary>
    /// Тупик обязан иметь выход. В WinPE окно занимает весь экран и креста
    /// на нём нет: не предложи мы закрыть мастер — человеку осталось бы
    /// только выключить машину из розетки.
    /// </summary>
    [Fact]
    public void В_тупике_предлагается_закрыть_мастер_и_кнопка_работает()
    {
        var closed = 0;
        var page = Screen(MediaManifestReader.Read("{ мусор"), () => closed++);

        Assert.True(page.CloseCommand.CanExecute(null));

        page.CloseCommand.Execute(null);

        Assert.Equal(1, closed);
    }

    [Fact]
    public void Когда_всё_в_порядке_закрывать_не_предлагается()
    {
        var page = Screen(OneRecipe());

        Assert.False(page.HasTrouble);
        Assert.Equal(string.Empty, page.Trouble);
        Assert.False(page.HasTroubleDetail);
        Assert.False(page.CloseCommand.CanExecute(null));
    }
}
