using System;
using WindowsPeace.Setup.Shell;

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
    public string Title => "Готово";

    public string Explanation =>
        "Здесь будет итог установки и кнопка перезагрузки. " +
        "Это появится на следующем шаге работы над программой.";

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
