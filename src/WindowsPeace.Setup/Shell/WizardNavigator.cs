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
    private readonly Action<IWizardPage>? _onEntered;
    private int _index;

    /// <param name="onEntered">
    /// Вызывается на каждом входе на страницу — при создании и при каждом переходе.
    /// Нужен, чтобы весь проход по экранам уходил в журнал: в WinPE после
    /// перезагрузки это единственное свидетельство того, что человек прошёл.
    /// Холостой переход (уходить некуда) входом не считается и сюда не приходит.
    /// </param>
    public WizardNavigator(IReadOnlyList<IWizardPage> pages, Action<IWizardPage>? onEntered = null)
    {
        if (pages.Count == 0)
        {
            throw new ArgumentException("Мастеру нужна хотя бы одна страница", nameof(pages));
        }

        _pages = pages;
        _onEntered = onEntered;

        foreach (var page in _pages)
        {
            page.CanGoNextChanged += OnPageReadinessChanged;
        }

        EnterCurrent();
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
        EnterCurrent();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    public void GoBack()
    {
        if (!CanGoBack)
        {
            return;
        }

        _index--;
        EnterCurrent();
        CurrentChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// Вход на текущую страницу: сама страница узнаёт об этом через OnEnter,
    /// а наблюдатель — через onEntered. Собрано в одном месте, чтобы то и другое
    /// случалось ровно один раз на вход и в одном и том же порядке.
    /// </summary>
    private void EnterCurrent()
    {
        Current.OnEnter();
        _onEntered?.Invoke(Current);
    }

    private void OnPageReadinessChanged(object? sender, EventArgs e)
    {
        if (ReferenceEquals(sender, Current))
        {
            CanGoNextChanged?.Invoke(this, EventArgs.Empty);
        }
    }
}
