using System.Windows.Data;
using CoreLocalization = WindowsPeace.Core.Localization;
using WindowsPeace.Setup.Localization;
using Xunit;

namespace WindowsPeace.Setup.Tests.Localization;

public class TMarkupExtensionTests
{
    [Fact]
    public void Расширение_строит_привязку_к_индексатору_службы()
    {
        var ext = new T(CoreLocalization.Keys.Common.Next);
        // ProvideValue без IServiceProvider возвращает сам Binding в WPF —
        // проверяем его настройку.
        var value = ext.ProvideValue(serviceProvider: null!);
        var binding = Assert.IsType<Binding>(value);
        Assert.Equal("[common.next]", binding.Path.Path);
        Assert.Same(CoreLocalization.Localization.Current, binding.Source);
    }
}
