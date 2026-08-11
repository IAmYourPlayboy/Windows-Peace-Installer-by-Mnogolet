using System;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Заглушка следующего шага. Нужна не для вида: без второй страницы
/// переходы оболочки нечем проверить.
/// </summary>
public sealed class PlaceholderViewModel : IWizardPage
{
    public string Title => "Дальше будет установка";

    public bool CanGoNext => false;

    public event EventHandler? CanGoNextChanged
    {
        add { }
        remove { }
    }

    public void OnEnter()
    {
    }
}
