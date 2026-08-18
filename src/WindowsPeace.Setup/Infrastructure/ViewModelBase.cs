using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Windows;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Setup.Infrastructure;

/// <summary>Уведомления об изменении свойств.</summary>
public abstract class ViewModelBase : INotifyPropertyChanged
{
    protected ViewModelBase()
    {
        WeakEventManager<CoreLocalization.Localization, EventArgs>.AddHandler(
            CoreLocalization.Localization.Current, nameof(CoreLocalization.Localization.LanguageChanged), OnLanguageChanged);
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    protected void Raise([CallerMemberName] string? propertyName = null)
        => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    protected bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }

        field = value;
        Raise(propertyName);
        return true;
    }

    private void OnLanguageChanged(object? sender, EventArgs e) => Raise(null);
}
