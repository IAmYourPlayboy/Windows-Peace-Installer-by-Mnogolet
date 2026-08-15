using System.Globalization;
using WindowsPeace.Core.Storage;
using Xunit;

namespace WindowsPeace.Core.Tests;

public class ByteSizeTests
{
    private const ulong Mib = 1024UL * 1024UL;
    private const ulong Gib = 1024UL * Mib;

    /// <summary>
    /// Разделитель дробной части зависит от языка системы, а тесты идут на любой.
    /// Поэтому язык задаётся на время вызова и возвращается назад.
    /// </summary>
    private static string InCulture(string culture, ulong bytes)
    {
        var previous = CultureInfo.CurrentCulture;
        CultureInfo.CurrentCulture = CultureInfo.GetCultureInfo(culture);
        try
        {
            return ByteSize.Format(bytes);
        }
        finally
        {
            CultureInfo.CurrentCulture = previous;
        }
    }

    [Fact]
    public void От_гигабайта_и_больше_счёт_идёт_в_гигабайтах()
    {
        Assert.Equal("500 ГБ", InCulture("ru-RU", 500 * Gib));
        Assert.Equal("1 ГБ", InCulture("ru-RU", Gib));
    }

    [Fact]
    public void Мельче_гигабайта_счёт_идёт_в_мегабайтах()
    {
        Assert.Equal("300 МБ", InCulture("ru-RU", 300 * Mib));
        Assert.Equal("1 МБ", InCulture("ru-RU", Mib));
    }

    [Fact]
    public void Мелочь_не_выдаётся_за_ноль()
    {
        // «0 МБ» там, где что-то есть, — неправда, пусть и мелкая. А вот ровно
        // ноль так и остаётся нулём.
        Assert.Equal("менее 1 МБ", InCulture("ru-RU", 512UL * 1024UL));
        Assert.Equal("менее 1 МБ", InCulture("ru-RU", 1UL));
        Assert.Equal("0 МБ", InCulture("ru-RU", 0UL));
    }

    [Fact]
    public void Дробная_часть_одна_цифра_и_считается_как_в_Windows()
    {
        // 476,94 ГиБ — тот самый диск с машины автора. «Управление дисками»
        // показывает 476,9 ГБ, и мастер обязан показывать то же самое:
        // расхождение здесь читается как «программа видит другой диск».
        Assert.Equal("476,9 ГБ", InCulture("ru-RU", 512_110_190_592UL));
    }

    [Fact]
    public void Разделитель_берётся_из_языка_системы()
    {
        Assert.Equal("476.9 ГБ", InCulture("en-US", 512_110_190_592UL));
    }
}
