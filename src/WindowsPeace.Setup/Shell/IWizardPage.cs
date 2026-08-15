using System;

namespace WindowsPeace.Setup.Shell;

/// <summary>
/// Страница мастера. Оболочка знает о странице ровно это и ничего больше —
/// поэтому добавление экрана не требует правки оболочки.
/// </summary>
public interface IWizardPage
{
    string Title { get; }

    /// <summary>
    /// Что написано на кнопке перехода вперёд. По умолчанию «Далее» — оно
    /// подходит большинству экранов и потому не повторяется в каждом. Экран,
    /// после которого начинается работа с диском, называет кнопку своим словом:
    /// человек должен понимать, что произойдёт по нажатию.
    /// </summary>
    string NextTitle => "Далее";

    /// <summary>Можно ли уходить со страницы вперёд.</summary>
    bool CanGoNext { get; }

    event EventHandler CanGoNextChanged;

    /// <summary>Вызывается каждый раз при появлении страницы.</summary>
    void OnEnter();
}
