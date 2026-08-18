using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace WindowsPeace.Core.Machine;

/// <summary>
/// Что мы знаем о машине на момент старта. Уходит первой записью в журнал:
/// в WinPE это единственное, что останется после перезагрузки, и по этой
/// строке потом разбирают, куда попали и почему всё пошло не так.
/// </summary>
public sealed class EnvironmentSnapshot
{
    public string OsVersion { get; init; } = string.Empty;

    public bool IsWindowsPe { get; init; }

    public ulong TotalMemoryBytes { get; init; }

    public bool SegoeUiRegularPresent { get; init; }

    public IReadOnlyList<string> VolumeRoots { get; init; } = new List<string>();

    // Число без разделителей тысяч: строка идёт в журнал, а там «4,096»
    // читается то как четыре тысячи, то как четыре целых.
    public override string ToString() => string.Format(
        CultureInfo.InvariantCulture,
        "{0}; среда: {1}; память: {2} МБ; обычный Segoe UI: {3}; тома: {4}",
        OsVersion,
        IsWindowsPe ? "WinPE" : "обычная Windows",
        TotalMemoryBytes / (1024 * 1024),
        SegoeUiRegularPresent ? "есть" : "нет",
        string.Join(" ", VolumeRoots.ToArray()));
}
