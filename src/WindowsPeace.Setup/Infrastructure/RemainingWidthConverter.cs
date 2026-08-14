using System;
using System.Globalization;
using System.Windows.Controls;
using System.Windows.Data;

namespace WindowsPeace.Setup.Infrastructure;

/// <summary>
/// Ширина последнего столбца таблицы: всё, что осталось от ширины списка.
///
/// Столбцы GridView сами не растягиваются. Без этого последний столбец имеет
/// постоянную ширину, и получается одно из двух: на узком окне текст обрезан,
/// на широком справа пустое место. В нашем случае обрезалось объяснение,
/// почему на диск нельзя ставить, — то есть предупреждение, а не украшение.
///
/// Ширины остальных столбцов не переписываются сюда числами, а спрашиваются
/// у самой таблицы: иначе правка разметки молча ломала бы расчёт.
/// </summary>
public sealed class RemainingWidthConverter : IMultiValueConverter
{
    /// <summary>Запас на рамку списка и полосу прокрутки.</summary>
    private const double Chrome = 28;

    /// <summary>Уже этого столбец делать бессмысленно — лучше полоса прокрутки.</summary>
    private const double Minimum = 160;

    public object Convert(object[] values, Type targetType, object? parameter, CultureInfo culture)
    {
        if (values.Length < 2 ||
            values[0] is not double totalWidth ||
            values[1] is not ListView list ||
            list.View is not GridView grid ||
            grid.Columns.Count == 0)
        {
            return Minimum;
        }

        var taken = 0.0;
        for (var i = 0; i < grid.Columns.Count - 1; i++)
        {
            taken += grid.Columns[i].ActualWidth;
        }

        var remaining = totalWidth - taken - Chrome;
        return remaining < Minimum ? Minimum : remaining;
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object? parameter, CultureInfo culture)
        => throw new NotSupportedException("Ширина столбца только вычисляется, обратно не разбирается.");
}
