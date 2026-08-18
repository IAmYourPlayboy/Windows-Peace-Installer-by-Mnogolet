using WindowsPeace.Core.Media;
using WindowsPeace.Setup.Pages;
using Xunit;

namespace WindowsPeace.Setup.Tests;

public class RecipeRowViewModelTests
{
    private const ulong Gib = 1024UL * 1024UL * 1024UL;

    private static MediaRecipe Recipe(string? description = null, string? imageName = null, ulong? size = null)
        => new()
        {
            Id = "atlas-25h2-ru",
            Name = "Atlas 25H2 RU",
            Description = description,
            RecipeFile = @"recipes\atlas.recipe.json",
            Image = new MediaImage
            {
                File = @"sources\install.wim",
                Index = 1,
                ImageName = imageName,
                SizeBytes = size,
            },
        };

    [Fact]
    public void Строка_показывает_название_издание_и_объём()
    {
        var row = new RecipeRowViewModel(Recipe(
            description: "Windows 11 Pro 25H2 ru-RU, Atlas, Windhawk",
            imageName: "Windows 11 Pro",
            size: 9 * Gib));

        Assert.Equal("Atlas 25H2 RU", row.Name);
        Assert.Equal("Windows 11 Pro 25H2 ru-RU, Atlas, Windhawk", row.Description);
        Assert.Equal("Windows 11 Pro", row.Image);
        Assert.Equal("9 ГБ", row.Size);
    }

    /// <summary>
    /// Издание в описи необязательно. Показать вместо него пустоту нельзя:
    /// человек должен видеть, что именно ставится, поэтому в ход идёт
    /// имя файла образа — оно есть всегда.
    /// </summary>
    [Fact]
    public void Когда_издание_не_записано_показывается_файл_образа()
    {
        var row = new RecipeRowViewModel(Recipe());

        Assert.Equal(@"sources\install.wim", row.Image);
    }

    [Fact]
    public void Когда_объём_не_записан_столбец_пуст_а_не_нулевой()
    {
        var row = new RecipeRowViewModel(Recipe());

        Assert.Equal(string.Empty, row.Size);
    }

    /// <summary>
    /// Средствам доступности строка обязана называть себя по-человечески,
    /// а не именем класса. Этот дефект уже ловили на шаге А живой машиной.
    /// </summary>
    [Fact]
    public void Строка_называет_себя_словами_а_не_именем_класса()
    {
        var row = new RecipeRowViewModel(Recipe(description: "Windows 11 Pro 25H2 ru-RU"));

        Assert.Equal("Atlas 25H2 RU. Windows 11 Pro 25H2 ru-RU", row.ToString());
    }

    [Fact]
    public void Без_описания_строка_называет_хотя_бы_название()
    {
        Assert.Equal("Atlas 25H2 RU", new RecipeRowViewModel(Recipe()).ToString());
    }
}
