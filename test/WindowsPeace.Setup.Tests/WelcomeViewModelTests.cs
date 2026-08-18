using WindowsPeace.Setup.Pages;
using Xunit;

namespace WindowsPeace.Setup.Tests;

public class WelcomeViewModelTests
{
    [Fact]
    public void Приветствие_пускает_дальше_и_не_назад()
    {
        var vm = new WelcomeViewModel();

        Assert.True(vm.CanGoNext);
        Assert.False(vm.CanGoBack);
        Assert.Equal("Далее / Next", vm.NextTitle);
        Assert.Equal(string.Empty, vm.Title);
    }
}
