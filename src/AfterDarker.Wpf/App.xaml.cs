using System.Windows;

namespace AfterDarker.Wpf;

public partial class App : Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);
        MainWindow = new MainWindow(e.Args);
        MainWindow.Show();
    }
}
