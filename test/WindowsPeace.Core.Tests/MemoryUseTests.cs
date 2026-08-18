using System;
using System.Collections.Generic;
using WindowsPeace.Core.Machine;
using Xunit;

namespace WindowsPeace.Core.Tests;

/// <summary>
/// Расход памяти — будущее системное требование: WinPE держит весь образ
/// в оперативной памяти, и запас там небольшой. Число нужно нам, а не человеку.
/// </summary>
public class MemoryUseTests
{
    private const ulong Mib = 1024UL * 1024UL;

    private sealed class Reader : IEnvironmentReader
    {
        public ulong Total { get; set; } = 4096 * Mib;
        public ulong Available { get; set; } = 3000 * Mib;
        public ulong Process { get; set; } = 120 * Mib;

        public bool RegistryKeyExists(string path) => false;
        public bool FileExists(string path) => false;
        public string OsVersion() => "10.0";
        public ulong TotalMemoryBytes() => Total;
        public ulong AvailableMemoryBytes() => Available;
        public ulong ProcessMemoryBytes() => Process;
        public IReadOnlyList<string> VolumeRoots() => Array.Empty<string>();
        public string WindowsDirectory() => @"X:\Windows";
    }

    [Fact]
    public void Замер_считает_занятое_как_разницу()
    {
        var use = MemoryUse.Measure(new Reader());

        Assert.Equal(1096 * Mib, use.UsedBytes);
        Assert.Equal(120 * Mib, use.ProcessBytes);
    }

    /// <summary>
    /// Свободной памяти больше, чем всей, быть не может, но отказ вызова
    /// возвращает ноль — и вычитание ушло бы в минус, а тип беззнаковый.
    /// Ноль честнее числа, полученного переполнением.
    /// </summary>
    [Fact]
    public void Несуразный_ответ_не_превращается_в_огромное_число()
    {
        var use = MemoryUse.Measure(new Reader { Total = 0, Available = 3000 * Mib });

        Assert.Equal(0UL, use.UsedBytes);
    }

    [Fact]
    public void Замер_описывает_себя_одной_строкой_для_журнала()
    {
        var text = MemoryUse.Measure(new Reader()).ToString();

        Assert.Contains("мастер: 120 МБ", text, StringComparison.Ordinal);
        Assert.Contains("1096 из 4096 МБ", text, StringComparison.Ordinal);
        Assert.Contains("свободно: 3000 МБ", text, StringComparison.Ordinal);
    }
}
