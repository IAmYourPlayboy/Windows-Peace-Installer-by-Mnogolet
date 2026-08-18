using System;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Первый экран мастера — крупное имя программы и ничего больше. Язык здесь
/// ещё не выбран, поэтому подпись кнопки — не из словаря, а литерал сразу
/// на двух языках: человек должен понять, куда нажимать, не читая заголовок.
/// </summary>
public sealed class WelcomeViewModel : IWizardPage
{
    /// <summary>Пусто: крупное имя рисует сама страница, а не шапка мастера.</summary>
    public string Title => string.Empty;

    public string NextTitle => "Далее / Next";

    public bool CanGoNext => true;

    /// <summary>Назад отсюда некуда — это первый экран мастера.</summary>
    public bool CanGoBack => false;

    public event EventHandler? CanGoNextChanged
    {
        add { }
        remove { }
    }

    public void OnEnter()
    {
    }
}
