using System;
using System.Collections.Generic;

namespace WindowsPeace.Setup.Shell;

/// <summary>
/// Единственное место, где меняется текущая страница. Собрано в одном классе,
/// чтобы переходы нельзя было совершить в обход и чтобы их можно было проверить
/// тестами без запуска интерфейса.
/// </summary>
public sealed class WizardNavigator
{
    private readonly IReadOnlyList<IWizardPage> _pages;
    private int _index;

    public WizardNavigator(IReadOnlyList<IWizardPage> pages)
    {
        if (pages.Count == 0)
        {
            throw new ArgumentException("Мастеру нужна хотя бы одна страница", nameof(pages));
        }

        _pages = pages;

        foreach (var page in _pages)
        {
            page.CanGoNextChanged += OnPageReadinessChanged;
        }

        Current.OnEnter();
    }

    public IWizardPage Current => _pages[_index];

    public bool CanGoBack => _index > 0 && Current.CanGoBack;

    public bool CanGoNext => _index < _pages.Count - 1 && Current.CanGoNext;

    public event EventHandler? CurrentChanged;

    public event EventHandler? CanGoNextChanged;

    public void GoNext()
    {
        if (!CanGoNext)
        {
            return;
        }

        _index++;
        Current.OnEnter();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        _index--;
        Current.OnEnter();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void OnPageReadinessChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, Current))
        {
            CanGoNextChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
