using System;
using WindowsPeace.Setup.Shell;

namespace WindowsPeace.Setup.Pages;

/// <summary>
/// Экран хода установки. Каркас: устройство то же, что понадобится на шаге В, —
/// заголовок этапа, строка текущего действия, время с начала, — но полоска
/// не рисуется, пока за ней нет настоящей работы. Поддельный прогресс
/// убедителен ровно до того дня, когда о его ненастоящести забудут.
/// </summary>
public sealed class ProgressViewModel : IWizardPage
{
    public string Title => "Установка";

    public string Explanation =>
        "Здесь пойдёт разметка диска, распаковка Windows, установка драйверов и загрузчика. " +
        "Это появится на следующем шаге работы над программой. " +
        "Сейчас мастер ничего не записывает на диск.";

    public bool CanGoNext => true;

    /// <summary>
    /// Назад отсюда нельзя: на шаге В за этим экраном уже идёт разметка диска,
    /// и «вернуться» означало бы предложить отменить то, что не отменяется.
    /// </summary>
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
