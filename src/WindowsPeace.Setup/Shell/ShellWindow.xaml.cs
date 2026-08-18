using System;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;

namespace WindowsPeace.Setup.Shell;

public partial class ShellWindow : Window
{
    private readonly bool _fullScreen;

    /// <param name="fullScreen">
    /// В WinPE нет ни рабочего стола, ни панели задач: окно в рамке посреди
    /// чёрного экрана выглядит поломкой. Установщик Windows там же занимает
    /// весь экран, и человек ждёт того же. Вместе с рамкой пропадает крестик -
    /// поэтому выход мастер предлагает сам, кнопкой в поле.
    /// </param>
    public ShellWindow(bool fullScreen)
    {
        InitializeComponent();
        _fullScreen = fullScreen;

        if (fullScreen)
        {
            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.NoResize;
            WindowState = WindowState.Maximized;
        }

        Loaded += OnLoaded;
    }

    /// <summary>
    /// В WinPE мастер поднимается из командной строки, и та под ним удерживает
    /// ввод: курсор мыши не виден, а Tab не переключает кнопки, потому что окну
    /// не отдан фокус клавиатуры.
    ///
    /// Отсюда два действия, и они с разной областью. Передний план забирается
    /// только в полноэкранном режиме, то есть в WinPE: на обычной Windows окно
    /// поднимает стенд, нарочно не отбирая фокус у человека за клавиатурой,
    /// и отбирать его тогда - мешать разработке. А вот фокус на первый элемент
    /// ставится всегда: это не трогает передний план окна, только внутренний
    /// фокус клавиатуры, зато Tab получает, с чего начать цикл. Дальше кнопки
    /// «Назад», «Далее» и выход живут в самой оболочке, и фокус между экранами
    /// не теряется.
    ///
    /// Проверить передний план и мышь на стенде нельзя: у него нет ни мыши,
    /// ни живой клавиатуры, а клавиши он шлёт в обход фокуса. Это - на живом
    /// железе.
    /// </summary>
    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        Loaded -= OnLoaded;

        if (_fullScreen)
        {
            Activate();
            var handle = new WindowInteropHelper(this).Handle;
            if (handle != IntPtr.Zero)
            {
                SetForegroundWindow(handle);
            }
        }

        // Первому элементу - фокус, иначе Tab не с чего начать цикл. Делаем это
        // после раскладки, отдельным заходом диспетчера: к этому времени
        // содержимое экрана уже построено.
        Dispatcher.BeginInvoke(
            new Action(() => MoveFocus(new TraversalRequest(FocusNavigationDirection.First))),
            System.Windows.Threading.DispatcherPriority.Input);

        // При переходе на новый экран фокус сам заходит внутрь него, чтобы стрелки
        // работали сразу, а не после Tab. Мыши на чужой машине может не быть -
        // клавиатура должна вести за руку.
        if (DataContext is ShellViewModel shell)
        {
            // -= перед += делает подписку идемпотентной: даже если Loaded
            // когда-нибудь сработает повторно, обработчик не задвоится.
            shell.PropertyChanged -= OnShellPropertyChanged;
            shell.PropertyChanged += OnShellPropertyChanged;
        }
    }

    private void OnShellPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName != nameof(ShellViewModel.CurrentPage))
        {
            return;
        }

        // После раскладки: содержимое нового экрана к этому времени построено.
        Dispatcher.BeginInvoke(new Action(FocusIntoPage),
            System.Windows.Threading.DispatcherPriority.Input);
    }

    /// <summary>
    /// Ставит фокус на первый элемент содержимого экрана, с которым можно
    /// работать (список рецептов, список дисков). Если такого нет - экраны
    /// сводки, установки и «Готово» состоят из одного текста, - фокус не трогаем:
    /// он остаётся на кнопке перехода, названной по делу («Установить»).
    /// </summary>
    private void FocusIntoPage()
    {
        var first = FindFirstFocusable(PageHost);
        if (first is not null)
        {
            Keyboard.Focus(first);
        }
    }

    private static IInputElement? FindFirstFocusable(DependencyObject root)
    {
        var count = VisualTreeHelper.GetChildrenCount(root);
        for (var i = 0; i < count; i++)
        {
            var child = VisualTreeHelper.GetChild(root, i);

            if (child is UIElement { Focusable: true, IsEnabled: true, IsVisible: true } element)
            {
                return element;
            }

            var nested = FindFirstFocusable(child);
            if (nested is not null)
            {
                return nested;
            }
        }

        return null;
    }

    [DllImport("user32.dll")]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool SetForegroundWindow(IntPtr hWnd);
}
