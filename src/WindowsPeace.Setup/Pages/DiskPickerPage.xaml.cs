using System;
using System.Windows;
using System.Windows.Controls;

namespace WindowsPeace.Setup.Pages;

public partial class DiskPickerPage : UserControl
{
    public DiskPickerPage() => InitializeComponent();

    /// <summary>
    /// Операции над разделами появятся на шаге В. Кнопки существуют уже сейчас,
    /// чтобы их доступность проектировалась вместе со списком.
    /// </summary>
    private void NotYet(object sender, RoutedEventArgs e)
        => MessageBox.Show(
            "Эта операция появится на следующем шаге." + Environment.NewLine + Environment.NewLine +
            "Сейчас программа ничего не записывает на диск.",
            "Windows Peace",
            MessageBoxButton.OK,
            MessageBoxImage.Information);
}
