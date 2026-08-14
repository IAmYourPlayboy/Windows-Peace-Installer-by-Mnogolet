using System;
using System.Collections.Generic;
using WindowsPeace.Core.Machine;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Снимок среды уходит первой записью в журнал. В WinPE это единственное,
/// что останется после перезагрузки, и по нему потом разбирают, где всё
/// оборвалось: та ли это среда, сколько памяти, есть ли шрифт, какие тома видны.
/// </summary>
public class HostEnvironmentTests
{
    private sealed class Reader : IEnvironmentReader
    {
        public bool MiniNt { get; set; }
        public bool SegoeUi { get; set; }

        public bool RegistryKeyExists(string path)
            => MiniNt && path.EndsWith("MiniNT", StringComparison.Ordinal);

        public bool FileExists(string path)
            => SegoeUi && path.EndsWith("segoeui.ttf", StringComparison.OrdinalIgnoreCase);

        public string OsVersion() => "10.0.26100";

        public ulong TotalMemoryBytes() => 4UL * 1024 * 1024 * 1024;

        public IReadOnlyList<string> VolumeRoots() => new[] { @"X:\", @"E:\" };

        public string WindowsDirectory() => @"X:\Windows";
    }

    [Fact]
    public void Ключ_MiniNT_означает_что_мы_в_WinPE()
    {
        var snapshot = HostEnvironment.Describe(new Reader { MiniNt = true });

        Assert.True(snapshot.IsWindowsPe);
    }

    [Fact]
    public void Без_ключа_MiniNT_это_обычная_Windows()
    {
        var snapshot = HostEnvironment.Describe(new Reader { MiniNt = false });

        Assert.False(snapshot.IsWindowsPe);
    }

    [Fact]
    public void Отсутствие_обычного_Segoe_UI_попадает_в_снимок()
    {
        var snapshot = HostEnvironment.Describe(new Reader { SegoeUi = false });

        Assert.False(snapshot.SegoeUiRegularPresent);
    }

    [Fact]
    public void Присутствие_обычного_Segoe_UI_тоже_попадает_в_снимок()
    {
        var snapshot = HostEnvironment.Describe(new Reader { SegoeUi = true });

        Assert.True(snapshot.SegoeUiRegularPresent);
    }

    [Fact]
    public void Тома_и_память_переносятся_как_есть()
    {
        var snapshot = HostEnvironment.Describe(new Reader());

        Assert.Equal(4UL * 1024 * 1024 * 1024, snapshot.TotalMemoryBytes);
        Assert.Equal(new[] { @"X:\", @"E:\" }, snapshot.VolumeRoots);
        Assert.Equal("10.0.26100", snapshot.OsVersion);
    }

    [Fact]
    public void Снимок_описывает_себя_одной_строкой_для_журнала()
    {
        var snapshot = HostEnvironment.Describe(new Reader { MiniNt = true, SegoeUi = false });
        var text = snapshot.ToString();

        Assert.Contains("WinPE", text, StringComparison.Ordinal);
        Assert.Contains("Segoe UI", text, StringComparison.Ordinal);
        Assert.Contains("4096", text, StringComparison.Ordinal);
    }
}
