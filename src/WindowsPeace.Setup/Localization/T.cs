using System;
using System.Windows.Data;
using System.Windows.Markup;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Setup.Localization;

/// <summary>
/// {loc:T ключ} — текст по ключу на текущем языке. Отдаёт привязку к индексатору
/// службы: на смену языка служба поднимает "Item[]", и все такие привязки
/// перечитываются. Привязки WPF держат цель слабо — утечки нет.
/// </summary>
public sealed class T : MarkupExtension
{
    public T() { }
    public T(string key) => Key = key;

    [ConstructorArgument("key")]
    public string Key { get; set; } = string.Empty;

    public override object ProvideValue(IServiceProvider serviceProvider)
    {
        var binding = new Binding($"[{Key}]")
        {
            Source = CoreLocalization.Localization.Current,
            Mode = BindingMode.OneWay,
        };
        return binding.ProvideValue(serviceProvider);
    }
}
