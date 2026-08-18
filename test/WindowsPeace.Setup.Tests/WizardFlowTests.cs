using System.Linq;
using WindowsPeace.Setup.Pages;
using Xunit;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Setup.Tests;

public class WizardFlowTests
{
    /// <summary>
    /// Посредник сводит выбор с трёх первых экранов, а не с двух: язык системы
    /// для шага В берётся с экрана выбора языка, а не из службы локализации
    /// напрямую — служба переключает сам мастер, а не то, что ставится.
    /// </summary>
    [Fact]
    public void SystemLanguage_повторяет_выбор_на_экране_языка()
    {
        var recipes = RecipePickerViewModel.WithoutMedia();
        var disks = new DiskPickerViewModel(
            new FakeEnumerator(new WindowsPeace.Core.Storage.DiskSnapshot(System.Array.Empty<WindowsPeace.Core.Storage.DiskInfo>(), null)),
            new NoopInspector(),
            new EmptyFileSystem());
        var language = new LanguageViewModel();
        language.Selected = language.Options.Single(o => o.Language == CoreLocalization.Language.English);

        var choice = new WizardChoice(recipes, disks, language);

        Assert.Equal(CoreLocalization.Language.English, choice.SystemLanguage);
        CoreLocalization.Localization.Current.Language = CoreLocalization.Language.Russian; // вернуть
    }
}
