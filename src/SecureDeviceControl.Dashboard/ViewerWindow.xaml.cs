using System.IO;
using System.Windows;
using System.Windows.Media.Imaging;
using SecureDeviceControl.Dashboard.Cloud;
using SecureDeviceControl.Dashboard.Imaging;
using SecureDeviceControl.Dashboard.Settings;

namespace SecureDeviceControl.Dashboard;

public partial class ViewerWindow : Window
{
    private readonly IReadOnlyList<SnapshotSession> sessions;
    private readonly DeviceInfo device;
    private int index;

    public ViewerWindow(IReadOnlyList<SnapshotSession> sessions, int startIndex, DeviceInfo device)
    {
        InitializeComponent();
        this.sessions = sessions;
        this.device = device;
        index = Math.Clamp(startIndex, 0, sessions.Count - 1);
        ShowCurrent();
    }

    private void ShowCurrent()
    {
        var session = sessions[index];
        var entry = session.Representative;
        var cachePath = Path.Combine(
            DashboardSettings.CacheDirectory,
            entry.DeviceHash,
            entry.CapturedAtUtc.ToString("yyyy-MM-dd"),
            entry.ObjectKey.Split('/').Last());

        if (File.Exists(cachePath))
        {
            var image = new BitmapImage();
            using var stream = File.OpenRead(cachePath);
            image.BeginInit();
            image.CacheOption = BitmapCacheOption.OnLoad;
            image.StreamSource = stream;
            image.EndInit();
            image.Freeze();
            SnapshotImage.Source = image;
        }
        else
        {
            SnapshotImage.Source = null;
        }

        var localStart = session.StartUtc.ToLocalTime();
        var localEnd = session.EndUtc.ToLocalTime();
        MetaText.Text =
            $"{device.DisplayName}\n" +
            $"{localStart:dd MMM yyyy HH:mm:ss} - {localEnd:HH:mm:ss} · " +
            $"{session.FrameCount} similar frame(s) · view {index + 1} of {sessions.Count}";

        PrevButton.IsEnabled = index > 0;
        NextButton.IsEnabled = index < sessions.Count - 1;
    }

    private void OnPrevClicked(object sender, RoutedEventArgs e)
    {
        if (index > 0)
        {
            index--;
            ShowCurrent();
        }
    }

    private void OnNextClicked(object sender, RoutedEventArgs e)
    {
        if (index < sessions.Count - 1)
        {
            index++;
            ShowCurrent();
        }
    }
}
