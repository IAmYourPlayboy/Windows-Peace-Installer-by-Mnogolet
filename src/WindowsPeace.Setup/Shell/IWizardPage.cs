using System;

namespace WindowsPeace.Setup.Shell;

/// <summary>
/// Страница мастера. Оболочка знает о странице ровно это и ничего больше —
/// поэтому добавление экрана не требует правки оболочки.
/// </summary>
public interface IWizardPage
{
    string Title { get; }

    /// <summary>Можно ли уходить со страницы вперёд.</summary>
    bool CanGoNext { get; }

    event EventHandler CanGoNextChanged;

    /// <summary>Вызывается каждый раз при появлении страницы.</summary>
    void OnEnter();
}
