using System.Windows;
using System.Windows.Media;
using SecureDeviceControl.Dashboard.Settings;

namespace SecureDeviceControl.Dashboard;

public partial class SettingsWindow : Window
{
    private readonly DashboardSettings settings;

    public SettingsWindow(DashboardSettings settings)
    {
        InitializeComponent();
        this.settings = settings;
        SupabaseUrlBox.Text = settings.SupabaseUrl;
        ServiceKeyBox.Text = settings.SupabaseServiceKey ?? "";
        ThresholdBox.Text = settings.SimilarityThreshold.ToString();
    }

    private void OnSaveClicked(object sender, RoutedEventArgs e)
    {
        var url = SupabaseUrlBox.Text.Trim().TrimEnd('/');
        if (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            ShowMessage("Supabase URL must be a valid https address.", isError: true);
            return;
        }

        if (!int.TryParse(ThresholdBox.Text.Trim(), out var threshold) || threshold is < 0 or > 20)
        {
            ShowMessage("Similarity threshold must be a whole number between 0 and 20.", isError: true);
            return;
        }

        settings.SupabaseUrl = url;
        settings.SupabaseServiceKey = ServiceKeyBox.Text.Trim();
        settings.SimilarityThreshold = threshold;
        settings.Save();
        Close();
    }

    private void OnCancelClicked(object sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ShowMessage(string message, bool isError)
    {
        MessageText.Text = message;
        MessageText.Foreground = isError ? System.Windows.Media.Brushes.OrangeRed : System.Windows.Media.Brushes.LightGreen;
    }
}
