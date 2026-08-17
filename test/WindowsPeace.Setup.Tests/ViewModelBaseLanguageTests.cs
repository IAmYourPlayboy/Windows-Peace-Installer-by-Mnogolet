using System.ComponentModel;
using CoreLocalization = WindowsPeace.Core.Localization;
using WindowsPeace.Setup.Infrastructure;
using Xunit;

namespace WindowsPeace.Setup.Tests;

public class ViewModelBaseLanguageTests
{
    private sealed class Probe : ViewModelBase { }

    [Fact]
    public void Смена_языка_поднимает_обновление_всех_свойств()
    {
        var loc = CoreLocalization.Localization.Current;
        loc.Language = CoreLocalization.Language.Russian;
        var vm = new Probe();
        string? prop = "нетронуто";
        ((INotifyPropertyChanged)vm).PropertyChanged += (_, e) => prop = e.PropertyName;
        loc.Language = CoreLocalization.Language.English;
        Assert.True(string.IsNullOrEmpty(prop)); // null или "" = «все свойства»
        loc.Language = CoreLocalization.Language.Russian; // вернуть для соседних тестов
    }
}
