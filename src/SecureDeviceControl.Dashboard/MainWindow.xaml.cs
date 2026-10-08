using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media.Imaging;
using SecureDeviceControl.Dashboard.Cloud;
using SecureDeviceControl.Dashboard.Imaging;
using SecureDeviceControl.Dashboard.Settings;
using SecureDeviceControl.Shared.Snapshots;

namespace SecureDeviceControl.Dashboard;

public partial class MainWindow : Window
{
    private readonly DashboardSettings settings;
    private readonly ObservableCollection<SessionCard> sessionCards = new();
    private IReadOnlyList<DeviceInfo> devices = Array.Empty<DeviceInfo>();

    public MainWindow(DashboardSettings settings)
    {
        InitializeComponent();
        this.settings = settings;
        SessionItems.ItemsSource = sessionCards;
        DayPicker.SelectedDate = DateTime.Today;
        Loaded += async (_, _) => await LoadDevicesAsync();
    }

    private async Task LoadDevicesAsync()
    {
        StatusText.Text = "Loading registered devices...";
        RefreshDevicesButton.IsEnabled = false;

        try
        {
            var serviceKey = settings.SupabaseServiceKey;
            if (string.IsNullOrWhiteSpace(serviceKey))
            {
                StatusText.Text = "Missing Supabase service key. Open Settings to add it.";
                return;
            }

            using var client = new SupabaseSnapshotClient(settings.SupabaseUrl, serviceKey);
            devices = await client.GetRegisteredDevicesAsync(CancellationToken.None);

            DeviceCombo.Items.Clear();
            foreach (var device in devices)
            {
                DeviceCombo.Items.Add(device);
            }

            DeviceCombo.DisplayMemberPath = nameof(DeviceInfo.DisplayName);
            if (DeviceCombo.Items.Count > 0)
            {
                DeviceCombo.SelectedIndex = 0;
            }

            DeviceCountText.Text = $"{devices.Count} device(s) registered";
            StatusText.Text = devices.Count == 0
                ? "No registered devices found in the cloud database."
                : "Ready. Pick a device and date, then load snapshots.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Could not reach Supabase: {ex.Message}";
        }
        finally
        {
            RefreshDevicesButton.IsEnabled = true;
        }
    }

    private async void OnLoadClicked(object sender, RoutedEventArgs e)
    {
        if (DeviceCombo.SelectedItem is not DeviceInfo device)
        {
            StatusText.Text = "Select a device first.";
            return;
        }

        if (DayPicker.SelectedDate is not DateTime day)
        {
            StatusText.Text = "Select a date first.";
            return;
        }

        var serviceKey = settings.SupabaseServiceKey;
        if (string.IsNullOrWhiteSpace(serviceKey))
        {
            StatusText.Text = "Missing Supabase service key. Open Settings to add it.";
            return;
        }

        LoadButton.IsEnabled = false;
        sessionCards.Clear();
        SummaryText.Text = "";

        try
        {
            StatusText.Text = "Listing snapshots...";
            var prefixes = SnapshotObjectKey.DayPrefixes(device.MachineName, device.Email, day);

            using var client = new SupabaseSnapshotClient(settings.SupabaseUrl, serviceKey);
            var keys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var prefix in prefixes)
            {
                foreach (var key in await client.ListObjectKeysAsync(prefix, CancellationToken.None))
                {
                    keys.Add(key);
                }
            }

            var entries = keys
                .Select(SnapshotEntry.TryParse)
                .Where(entry => entry is not null)
                .Select(entry => entry!)
                .OrderBy(entry => entry.CapturedAtUtc)
                .ToList();

            if (entries.Count == 0)
            {
                StatusText.Text = $"No snapshots for {device.MachineName} on {day:dd MMM yyyy}.";
                return;
            }

            StatusText.Text = $"Downloading {entries.Count} snapshot(s)...";
            var frames = await DownloadAndHashAsync(client, device, entries);

            StatusText.Text = "Grouping similar frames...";
            var sessions = SnapshotSessionizer.GroupSessions(frames, settings.SimilarityThreshold);

            foreach (var session in sessions)
            {
                var cachedPath = CachePathFor(session.Representative);
                var card = new SessionCard(session, device, CreateThumbnail(cachedPath));
                sessionCards.Add(card);
            }

            var totalFrames = sessions.Sum(session => session.FrameCount);
            SummaryText.Text =
                $"{device.DisplayName} · {day:dd MMM yyyy} · {totalFrames} snapshots collapsed into {sessions.Count} unique view(s) " +
                $"({totalFrames - sessions.Count} near-duplicates hidden)";
            StatusText.Text = "Done.";
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Load failed: {ex.Message}";
        }
        finally
        {
            LoadButton.IsEnabled = true;
        }
    }

    private async Task<List<(SnapshotEntry Entry, ulong Hash)>> DownloadAndHashAsync(
        SupabaseSnapshotClient client,
        DeviceInfo device,
        IReadOnlyList<SnapshotEntry> entries)
    {
        var frames = new List<(SnapshotEntry Entry, ulong Hash)>(entries.Count);
        var gate = new SemaphoreSlim(4);
        var completed = 0;

        var tasks = entries.Select(async entry =>
        {
            await gate.WaitAsync();
            try
            {
                var cachePath = CachePathFor(entry);
                byte[] imageBytes;

                if (File.Exists(cachePath))
                {
                    imageBytes = await File.ReadAllBytesAsync(cachePath);
                }
                else
                {
                    imageBytes = await client.DownloadSnapshotAsync(entry.ObjectKey, CancellationToken.None);
                    Directory.CreateDirectory(Path.GetDirectoryName(cachePath)!);
                    await File.WriteAllBytesAsync(cachePath, imageBytes);
                }

                var hash = PerceptualHash.ComputeDHash(imageBytes);
                var done = Interlocked.Increment(ref completed);
                Dispatcher.Invoke(() => StatusText.Text = $"Downloaded {done}/{entries.Count}...");
                return (entry, hash);
            }
            finally
            {
                gate.Release();
            }
        });

        var results = await Task.WhenAll(tasks);
        frames.AddRange(results);
        return frames;
    }

    private string CachePathFor(SnapshotEntry entry)
    {
        var fileName = entry.ObjectKey.Split('/').Last();
        return Path.Combine(
            DashboardSettings.CacheDirectory,
            entry.DeviceFolder,
            entry.CapturedAtUtc.ToString("yyyy-MM-dd"),
            fileName);
    }

    private static BitmapImage CreateThumbnail(string path)
    {
        var image = new BitmapImage();
        using var stream = File.OpenRead(path);
        image.BeginInit();
        image.CacheOption = BitmapCacheOption.OnLoad;
        image.DecodePixelWidth = 560;
        image.StreamSource = stream;
        image.EndInit();
        image.Freeze();
        return image;
    }

    private void OnSessionCardClicked(object sender, MouseButtonEventArgs e)
    {
        if (sender is Border border && border.Tag is SessionCard card)
        {
            var allSessions = sessionCards.Select(sessionCard => sessionCard.Session).ToList();
            var viewer = new ViewerWindow(allSessions, allSessions.IndexOf(card.Session), card.Device);
            viewer.Owner = this;
            viewer.ShowDialog();
        }
    }

    private async void OnRefreshDevicesClicked(object sender, RoutedEventArgs e)
    {
        await LoadDevicesAsync();
    }

    private void OnSettingsClicked(object sender, RoutedEventArgs e)
    {
        var dialog = new SettingsWindow(settings);
        dialog.Owner = this;
        dialog.ShowDialog();
    }

    private void OnLogoutClicked(object sender, RoutedEventArgs e)
    {
        var loginWindow = new LoginWindow();
        System.Windows.Application.Current.MainWindow = loginWindow;
        loginWindow.Show();
        Close();
    }
}

public sealed class SessionCard
{
    public SessionCard(SnapshotSession session, DeviceInfo device, BitmapImage thumbnail)
    {
        Session = session;
        Device = device;
        Thumbnail = thumbnail;

        var localStart = session.StartUtc.ToLocalTime();
        var localEnd = session.EndUtc.ToLocalTime();
        TimeRangeText = localStart.Date == localEnd.Date
            ? $"{localStart:HH:mm:ss} - {localEnd:HH:mm:ss}"
            : $"{localStart:dd MMM HH:mm} - {localEnd:dd MMM HH:mm}";
        FrameCountText = session.FrameCount == 1
            ? "1 snapshot"
            : $"{session.FrameCount} similar snapshots (showing 1)";
        DeviceText = device.DisplayName;
    }

    public SnapshotSession Session { get; }
    public DeviceInfo Device { get; }
    public BitmapImage Thumbnail { get; }
    public string TimeRangeText { get; }
    public string FrameCountText { get; }
    public string DeviceText { get; }
}
