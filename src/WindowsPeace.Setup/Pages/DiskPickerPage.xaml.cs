using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace WindowsPeace.Setup.Pages;

public partial class DiskPickerPage : UserControl
{
    public DiskPickerPage() => InitializeComponent();

    private DiskPickerViewModel? Model => DataContext as DiskPickerViewModel;

    /// <summary>
    /// Клик по строке диска сворачивает или разворачивает его разделы. Делаем это
    /// на отпускании кнопки, а не на нажатии: к этому моменту список уже выбрал
    /// строку, и добавление-удаление детей не мешает его собственному выбору.
    /// </summary>
    private void DiskList_PreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (FindAncestor<ListViewItem>(e.OriginalSource as DependencyObject)?.DataContext
            is DiskRowViewModel row && row.CanToggle)
        {
            Model?.Toggle(row);
        }
    }

    /// <summary>
    /// Стрелки влево и вправо сворачивают и разворачивают выбранный диск - так же,
    /// как в дереве. Вверх и вниз список обрабатывает сам.
    /// </summary>
    private void DiskList_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (Model?.Selected is not DiskRowViewModel row || !row.CanToggle)
        {
            return;
        }

        if (e.Key == Key.Right && row.IsCollapsed)
        {
            Model.Toggle(row);
            e.Handled = true;
        }
        else if (e.Key == Key.Left && row.IsExpanded)
        {
            Model.Toggle(row);
            e.Handled = true;
        }
    }

    private static T? FindAncestor<T>(DependencyObject? current) where T : DependencyObject
    {
        while (current is not null and not T)
        {
            // По дереву визуалов ходит только Visual/Visual3D. Если клик попал
            // во что-то иное, дальше идти нельзя - иначе GetParent бросит.
            if (current is not Visual and not System.Windows.Media.Media3D.Visual3D)
            {
                return null;
            }

            current = VisualTreeHelper.GetParent(current);
        }

        return current as T;
    }

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
