using System;
using System.Collections.Generic;
using WindowsPeace.Setup.Infrastructure;
using WindowsPeace.Setup.Shell;
using CoreLocalization = WindowsPeace.Core.Localization;
using Language = WindowsPeace.Core.Localization.Language;
using Keys = WindowsPeace.Core.Localization.Keys;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Экран выбора языка — второй экран мастера, сразу после приветствия.
/// Выбор здесь не только заполняет поле для шага В, но и переключает язык
/// самого мастера: экраны дальше по потоку уже говорят выбранным языком.
/// </summary>
public sealed class LanguageViewModel : ViewModelBase, IWizardPage
{
    /// <summary>
    /// Один пункт списка. Подпись — всегда на своём языке, а не из словаря:
    /// человек, ещё не выбравший язык, должен узнать родной по надписи,
    /// а не по переводу на текущем.
    /// </summary>
    public sealed class LanguageOption
    {
        public LanguageOption(Language language, string nativeLabel)
        {
            Language = language;
            NativeLabel = nativeLabel;
        }

        public Language Language { get; }

        public string NativeLabel { get; }
    }

    private LanguageOption? _selected;

    public LanguageViewModel()
    {
        Options = new List<LanguageOption>
        {
            new(Language.Russian, "Русский"),
            new(Language.English, "English"),
        };
    }

    public IReadOnlyList<LanguageOption> Options { get; }

    public LanguageOption? Selected
    {
        get => _selected;
        set
        {
            if (Set(ref _selected, value))
            {
                CoreLocalization.Localization.Current.Language = value!.Language;
                Raise(nameof(CanGoNext));
                CanGoNextChanged?.Invoke(this, EventArgs.Empty);
            }
        }
    }

    public string Title => CoreLocalization.Localization.Current[Keys.Language.Title];

    public bool CanGoBack => true;

    public bool CanGoNext => Selected is not null;

    public event EventHandler? CanGoNextChanged;

    public void OnEnter()
    {
    }
}
