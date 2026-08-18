using System;
using WindowsPeace.Setup.Infrastructure;

namespace WindowsPeace.Setup.Shell;

/// <summary>Состояние оболочки: заголовок, текущая страница, доступность переходов.</summary>
public sealed class ShellViewModel : ViewModelBase
{
    private readonly WizardNavigator _navigator;

    /// <param name="closeWizard">
    /// Выход из мастера. Он один на все экраны и всегда на одном месте, в нижнем
    /// ряду оболочки: в WinPE окно занимает весь экран и крестика на нём нет,
    /// а искать выход в разных местах разных экранов человек не должен.
    /// </param>
    public ShellViewModel(WizardNavigator navigator, Action closeWizard)
    {
        _navigator = navigator;

        BackCommand = new RelayCommand(_navigator.GoBack, () => _navigator.CanGoBack);
        NextCommand = new RelayCommand(_navigator.GoNext, () => _navigator.CanGoNext);
        CloseCommand = new RelayCommand(closeWizard);

        _navigator.CurrentChanged += OnNavigationChanged;
        _navigator.CanGoNextChanged += OnReadinessChanged;
    }

    public object CurrentPage => _navigator.Current;

    public string Title => _navigator.Current.Title;

    /// <summary>Что написано на кнопке перехода вперёд. Слово даёт сама страница.</summary>
    public string NextTitle => _navigator.Current.NextTitle;

    public RelayCommand BackCommand { get; }

    public RelayCommand NextCommand { get; }

    public RelayCommand CloseCommand { get; }

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
