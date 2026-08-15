using System;
using WindowsPeace.Setup.Infrastructure;

namespace WindowsPeace.Setup.Shell;

/// <summary>Состояние оболочки: заголовок, текущая страница, доступность переходов.</summary>
public sealed class ShellViewModel : ViewModelBase
{
    private readonly WizardNavigator _navigator;

    public ShellViewModel(WizardNavigator navigator)
    {
        _navigator = navigator;

        BackCommand = new RelayCommand(_navigator.GoBack, () => _navigator.CanGoBack);
        NextCommand = new RelayCommand(_navigator.GoNext, () => _navigator.CanGoNext);

        _navigator.CurrentChanged += OnNavigationChanged;
        _navigator.CanGoNextChanged += OnReadinessChanged;
    }

    public object CurrentPage => _navigator.Current;

    public string Title => _navigator.Current.Title;

    /// <summary>Что написано на кнопке перехода вперёд. Слово даёт сама страница.</summary>
    public string NextTitle => _navigator.Current.NextTitle;

    public RelayCommand BackCommand { get; }

    public RelayCommand NextCommand { get; }

    private void OnNavigationChanged(object? sender, EventArgs e)
    {
        Raise(nameof(CurrentPage));
        Raise(nameof(Title));
        Raise(nameof(NextTitle));
        OnReadinessChanged(sender, e);
    }

    private void OnReadinessChanged(object? sender, EventArgs e)
    {
        BackCommand.RaiseCanExecuteChanged();
        NextCommand.RaiseCanExecuteChanged();
    }
}
