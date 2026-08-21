using System.Windows;

namespace SecureDeviceControl.Dashboard;

public partial class App : System.Windows.Application
{
    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        if (e.Args.Contains("--selftest", StringComparer.OrdinalIgnoreCase))
        {
            var exitCode = SelfTest.Run(Console.Out);
            Shutdown(exitCode);
            return;
        }

        var loginWindow = new LoginWindow();
        MainWindow = loginWindow;
        loginWindow.Show();
    }
}
