using System.Configuration;
using System.Data;
using System.Windows;

namespace SecureDeviceControl.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startMinimized = e.Args.Any(argument =>
            string.Equals(argument, "--snapshot-agent", StringComparison.OrdinalIgnoreCase));
        var window = new MainWindow(startMinimized);
        MainWindow = window;
        window.Show();
    }
}

