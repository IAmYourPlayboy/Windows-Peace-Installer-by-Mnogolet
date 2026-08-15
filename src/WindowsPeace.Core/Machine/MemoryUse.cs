using System.Globalization;

namespace WindowsPeace.Core.Machine;

/// <summary>
/// Сколько памяти уходит: у мастера и у всей машины.
///
/// Замеряется после первой отрисовки и уходит в журнал. Это будущее системное
/// требование: WinPE держит весь образ в оперативной памяти, и на машине
/// с четырьмя гигабайтами свободного места остаётся немного. Число нужно нам,
/// а не человеку, — на экране его нет.
/// </summary>
public sealed class MemoryUse
{
    private const ulong Mib = 1024UL * 1024UL;

    public ulong TotalBytes { get; init; }

    public ulong AvailableBytes { get; init; }

    /// <summary>Сколько занимает сам мастер.</summary>
    public ulong ProcessBytes { get; init; }

    /// <summary>Сколько занято на машине всем сразу, вместе с образом WinPE.</summary>
    public ulong UsedBytes => TotalBytes > AvailableBytes ? TotalBytes - AvailableBytes : 0UL;

    public static MemoryUse Measure(IEnvironmentReader reader) => new()
    {
        TotalBytes = reader.TotalMemoryBytes(),
        AvailableBytes = reader.AvailableMemoryBytes(),
        ProcessBytes = reader.ProcessMemoryBytes(),
    };

    // Числа без разделителей тысяч: строка идёт в журнал, а там «4,096»
    // читается то как четыре тысячи, то как четыре целых.
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "мастер: {0} МБ; занято на машине: {1} из {2} МБ; свободно: {3} МБ",
        ProcessBytes / Mib,
        UsedBytes / Mib,
        TotalBytes / Mib,
        AvailableBytes / Mib);
}
