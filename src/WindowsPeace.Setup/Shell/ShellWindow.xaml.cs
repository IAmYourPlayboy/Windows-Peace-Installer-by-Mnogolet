using System.Windows;

namespace WindowsPeace.Setup.Shell;

public partial class ShellWindow : Window
{
    /// <param name="fullScreen">
    /// В WinPE нет ни рабочего стола, ни панели задач: окно в рамке посреди
    /// чёрного экрана выглядит поломкой. Установщик Windows там же занимает
    /// весь экран, и человек ждёт того же. Вместе с рамкой пропадает крестик —
    /// поэтому выход мастер предлагает сам, кнопкой в углу.
    /// </param>
    public ShellWindow(bool fullScreen)
    {
        InitializeComponent();

        if (fullScreen)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }
    }
}
