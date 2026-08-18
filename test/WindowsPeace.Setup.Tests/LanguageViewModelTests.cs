using System.Linq;
using WindowsPeace.Setup.Pages;
using Xunit;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Setup.Tests;

[Collection(LocalizationCollection.Name)]
public class LanguageViewModelTests
{
    [Fact]
    public void Пока_язык_не_выбран_дальше_нельзя()
        => Assert.False(new LanguageViewModel().CanGoNext);

    [Fact]
    public void Выбор_английского_переключает_службу_и_пускает_дальше()
    {
        CoreLocalization.Localization.Current.Language = CoreLocalization.Language.Russian;
        var vm = new LanguageViewModel();
        vm.Selected = vm.Options.Single(o => o.Language == CoreLocalization.Language.English);
        Assert.True(vm.CanGoNext);
        Assert.Equal(CoreLocalization.Language.English, CoreLocalization.Localization.Current.Language);
        CoreLocalization.Localization.Current.Language = CoreLocalization.Language.Russian; // вернуть для соседних тестов
    }

    // Строка списка называет себя средствам доступности своей подписью, а не
    // именем класса: иначе экранный диктор читает «...LanguageOption». UIA берёт
    // имя строки из ToString, поэтому проверяем именно его.
    [Fact]
    public void Пункт_называет_себя_подписью_а_не_именем_класса()
    {
        var vm = new LanguageViewModel();
        foreach (var option in vm.Options)
        {
            Assert.Equal(option.NativeLabel, option.ToString());
        }
    }
}
