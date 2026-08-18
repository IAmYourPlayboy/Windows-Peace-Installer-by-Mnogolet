using System;
using WindowsPeace.Setup.Shell;
using CoreLocalization = WindowsPeace.Core.Localization;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Экран завершения. Каркас: итог установки и перезагрузка придут на шаге В.
///
/// Про журнал здесь не говорится ни слова. Журнал нужен нам, а не человеку:
/// разбираться в наших ошибках он не должен, а мы обязаны сделать так, чтобы
/// место для записи находилось всегда. Решение автора.
/// </summary>
public sealed class DoneViewModel : IWizardPage
{
    public string Title => CoreLocalization.Localization.Current[CoreLocalization.Keys.Done.Title];

    public string Explanation => CoreLocalization.Localization.Current[CoreLocalization.Keys.Done.Explanation];

    public bool CanGoNext => false;

    /// <summary>Назад отсюда некуда: установка позади.</summary>
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
