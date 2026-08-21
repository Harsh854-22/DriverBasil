using System.Windows;
using SecureDeviceControl.Dashboard.Auth;
using SecureDeviceControl.Dashboard.Settings;

namespace SecureDeviceControl.Dashboard;

public partial class LoginWindow : Window
{
    private readonly DashboardSettings settings;
    private readonly bool setupMode;

    public LoginWindow()
    {
        InitializeComponent();
        settings = DashboardSettings.Load();
        setupMode = !settings.HasAdminAccount;

        if (setupMode)
        {
            Title = "Snapshot Dashboard - First run setup";
            SubtitleText.Text = "Create the admin login for this dashboard PC";
            SetupPanel.Visibility = Visibility.Visible;
            ConfirmPanel.Visibility = Visibility.Visible;
            ConnectionPanel.Visibility = Visibility.Visible;
            SupabaseUrlBox.Text = settings.SupabaseUrl;
            ActionButton.Content = "Create admin account";
        }
        else
        {
            UsernameBox.Text = settings.AdminUsername;
        }
    }

    private void OnActionClicked(object sender, RoutedEventArgs e)
    {
        ErrorText.Text = "";

        if (setupMode)
        {
            CompleteSetup();
        }
        else
        {
            CompleteLogin();
        }
    }

    private void CompleteSetup()
    {
        var username = UsernameBox.Text.Trim();
        var password = PasswordBox.Password;
        var confirm = ConfirmPasswordBox.Password;
        var supabaseUrl = SupabaseUrlBox.Text.Trim().TrimEnd('/');
        var serviceKey = ServiceKeyBox.Text.Trim();

        if (username.Length < 3)
        {
            ErrorText.Text = "Admin ID must be at least 3 characters.";
            return;
        }

        if (password.Length < 8)
        {
            ErrorText.Text = "Password must be at least 8 characters.";
            return;
        }

        if (password != confirm)
        {
            ErrorText.Text = "Passwords do not match.";
            return;
        }

        if (!Uri.TryCreate(supabaseUrl, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            ErrorText.Text = "Supabase URL must be a valid https address.";
            return;
        }

        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            ErrorText.Text = "The Supabase service role key is required to read the snapshot bucket.";
            return;
        }

        settings.AdminUsername = username;
        settings.AdminPasswordHash = AdminAccount.HashPassword(password);
        settings.SupabaseUrl = supabaseUrl;
        settings.SupabaseServiceKey = serviceKey;
        settings.Save();

        OpenMainWindow();
    }

    private void CompleteLogin()
    {
        if (!string.Equals(UsernameBox.Text.Trim(), settings.AdminUsername, StringComparison.OrdinalIgnoreCase) ||
            !AdminAccount.Verify(PasswordBox.Password, settings.AdminPasswordHash))
        {
            ErrorText.Text = "Invalid Admin ID or password.";
            return;
        }

        OpenMainWindow();
    }

    private void OpenMainWindow()
    {
        var mainWindow = new MainWindow(settings);
        System.Windows.Application.Current.MainWindow = mainWindow;
        mainWindow.Show();
        Close();
    }
}
