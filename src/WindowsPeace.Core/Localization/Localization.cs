using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace WindowsPeace.Core.Localization;

/// <summary>
/// Текущий язык интерфейса и текст по ключу. Язык — окружающее состояние
/// (как культура потока), поэтому одиночка: и статические методы ядра, и оболочка
/// берут текст из одного места. Тесты создают отдельный экземпляр.
/// </summary>
public sealed class Localization : INotifyPropertyChanged
{
    private readonly IReadOnlyDictionary<Language, IReadOnlyDictionary<string, string>> _packs;
    private Language _language = Language.Russian;

    public Localization() : this(new RussianStrings(), new EnglishStrings()) { }

    public Localization(params ILanguagePack[] packs)
    {
        var map = new Dictionary<Language, IReadOnlyDictionary<string, string>>();
        foreach (var pack in packs) map[pack.Language] = pack.Strings;
        _packs = map;
    }

    public static Localization Current { get; } = new();

    public Language Language
    {
        get => _language;
        set
        {
            if (_language == value) return;
            _language = value;
            LanguageChanged?.Invoke(this, EventArgs.Empty);
            // "Item[]" — соглашение WPF об изменении индексатора: все привязки
            // {loc:T ...} к нему перечитываются.
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        }
    }

    /// <summary>Текст по ключу. Пропущенный перевод — видимый маркер, не пустота.</summary>
    public string this[string key]
        => _packs.TryGetValue(_language, out var pack) && pack.TryGetValue(key, out var text)
            ? text
            : "⟨" + key + "⟩";

    public event EventHandler? LanguageChanged;
    public event PropertyChangedEventHandler? PropertyChanged;
}
