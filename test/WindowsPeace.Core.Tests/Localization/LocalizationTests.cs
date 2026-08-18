using System.Globalization;
using WindowsPeace.Core.Localization;
using Xunit;

namespace WindowsPeace.Core.Tests.Localization;

public class LocalizationTests
{
    [Fact] public void По_умолчанию_русский()
        => Assert.Equal(Language.Russian, new WindowsPeace.Core.Localization.Localization().Language);

    [Fact] public void Индексатор_даёт_текст_текущего_языка()
    {
        var loc = new WindowsPeace.Core.Localization.Localization();
        Assert.Equal("Далее", loc[Keys.Common.Next]);
        loc.Language = Language.English;
        Assert.Equal("Next", loc[Keys.Common.Next]);
    }

    [Fact] public void Неизвестный_ключ_даёт_видимый_маркер()
        => Assert.Equal("⟨нет.такого⟩", new WindowsPeace.Core.Localization.Localization()["нет.такого"]);

    [Fact] public void Смена_языка_поднимает_оба_уведомления()
    {
        var loc = new WindowsPeace.Core.Localization.Localization();
        var changed = false; string? prop = null;
        loc.LanguageChanged += (_, _) => changed = true;
        loc.PropertyChanged += (_, e) => prop = e.PropertyName;
        loc.Language = Language.English;
        Assert.True(changed);
        Assert.Equal("Item[]", prop);
    }

    [Fact] public void Присвоение_того_же_языка_молчит()
    {
        var loc = new WindowsPeace.Core.Localization.Localization();
        var count = 0;
        loc.LanguageChanged += (_, _) => count++;
        loc.Language = Language.Russian;
        Assert.Equal(0, count);
    }

    /// <summary>
    /// Win32StorageSource читает разметку через DeviceIoControl и юнит-тестом
    /// не покрывается (нужен настоящий диск). Здесь проверяется только контракт
    /// перевода: слово переводится, код ошибки остаётся числом — так же, как
    /// собирает строку сам источник, string.Format(CultureInfo.CurrentCulture, ...).
    /// </summary>
    [Fact] public void Префикс_ошибки_разметки_переводится_код_остаётся_числом()
    {
        var loc = new WindowsPeace.Core.Localization.Localization();

        var ru = string.Format(CultureInfo.CurrentCulture, loc[Keys.Layout.ReadFailed], 5);
        Assert.Equal("Разметку прочитать не удалось, код ошибки 5", ru);

        loc.Language = Language.English;
        var en = string.Format(CultureInfo.CurrentCulture, loc[Keys.Layout.ReadFailed], 5);
        Assert.Equal("Could not read the layout, error code 5", en);
    }
}
