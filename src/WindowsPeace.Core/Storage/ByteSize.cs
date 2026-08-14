using System.Globalization;

namespace WindowsPeace.Core.Storage;

/// <summary>
/// Размер в байтах, показанный человеку.
///
/// Одно место на весь проект. То же самое было написано дважды — в предпросмотре
/// разметки и в строке списка дисков — и просилось в третий раз, в список
/// рецептов. Разойдясь, они показали бы один и тот же объём по-разному
/// на соседних экранах, а человек в этот момент решает, что стирать.
/// </summary>
public static class ByteSize
{
    private const ulong Mib = 1024UL * 1024UL;
    private const ulong Gib = 1024UL * Mib;

    /// <summary>
    /// Гигабайты считаются от гибибайта — так же, как их считает сама Windows.
    /// Диск, который «Управление дисками» показывает как 476,9 ГБ, обязан
    /// выглядеть здесь ровно так же: расхождение читается как «программа
    /// смотрит на другой диск». Мельче гигабайта счёт идёт в мегабайтах
    /// и целыми: доли мегабайта человеку ничего не говорят.
    /// </summary>
    public static string Format(ulong bytes)
        => bytes >= Gib
            ? ((double)bytes / Gib).ToString("0.#", CultureInfo.CurrentCulture) + " ГБ"
            : (bytes / Mib).ToString(CultureInfo.CurrentCulture) + " МБ";
}
