using Xunit;

namespace WindowsPeace.Core.Tests;

public class BuildSmokeTests
{
    [Fact]
    public void Сборка_ядра_доступна_из_тестов()
    {
        var assembly = typeof(WindowsPeace.Core.Polyfills.AssemblyMarker).Assembly;
        Assert.Equal("WindowsPeace.Core", assembly.GetName().Name);
    }
}
