using System.Windows;

namespace WindowsPeace.Setup;

/// <summary>
/// Точка входа приложения.
/// На этом шаге окна ещё нет: оболочка мастера появляется в задаче 10 плана.
/// До тех пор приложение говорит об этом вслух и завершается — висеть
/// невидимым процессом ему запрещено, см. docs/ARCHITECTURE.md, раздел 9.
/// </summary>
public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        MessageBox.Show(
            "Оболочка мастера ещё не собрана: она появляется в задаче 10 плана шага А.",
            "Windows Peace",
            MessageBoxButton.OK,
            MessageBoxImage.Information);

        Shutdown();
    }
}
