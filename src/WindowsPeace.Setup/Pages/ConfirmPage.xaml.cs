using System.Windows.Controls;

namespace WindowsPeace.Setup.Pages;

public partial class ConfirmPage : UserControl
{
    public ConfirmPage()
    {
        InitializeComponent();

        // Поле подтверждения — единственное, что на этом экране делают руками,
        // поэтому оно и получает клавиатуру. Искать его нажатиями Tab человек
        // не должен: мыши на чужой машине может и не оказаться.
        Loaded += (_, _) => ModelBox.Focus();
    }
}
