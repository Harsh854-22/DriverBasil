using System.Text.Json;
using System.Windows;
using SecureDeviceControl.Shared.Contracts;
using SecureDeviceControl.Shared.Ipc;

namespace SecureDeviceControl.Desktop;

/// <summary>
/// Interaction logic for App.xaml
/// </summary>
public partial class App : System.Windows.Application
{
    private const string MutexName = @"Global\SecureDeviceControl.Desktop.SingleInstance";
    private const string ShowEventName = @"Global\SecureDeviceControl.Desktop.Show";

    private readonly IpcClient ipcClient = new();
    private SnapshotCaptureService? snapshotCaptureService;
    private System.Windows.Forms.NotifyIcon? trayIcon;
    private PeriodicTimer? snapshotPollTimer;
    private CancellationTokenSource? snapshotPollCts;
    private MainWindow? mainWindow;
    private bool isExplicitExit;
    private Mutex? instanceMutex;
    private EventWaitHandle? showEvent;
    private CancellationTokenSource? showSignalCts;

    public IpcClient IpcClient => ipcClient;
    public SnapshotCaptureService SnapshotCaptureService => snapshotCaptureService!;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var startMinimized = e.Args.Any(argument =>
            string.Equals(argument, "--snapshot-agent", StringComparison.OrdinalIgnoreCase));

        if (!TryClaimSingleInstance())
        {
            if (!startMinimized)
            {
                SignalExistingInstanceToShow();
            }

            Shutdown();
            return;
        }

        snapshotCaptureService = new SnapshotCaptureService(ipcClient);
        SnapshotStartupRegistrar.EnableForCurrentUser();
        SetupTrayIcon();

        mainWindow = new MainWindow(startMinimized);
        MainWindow = mainWindow;
        mainWindow.Closed += (_, _) => { if (isExplicitExit) Shutdown(); };
        mainWindow.Show();
        if (startMinimized)
        {
            mainWindow.WindowState = WindowState.Minimized;
            mainWindow.Hide();
        }

        snapshotPollCts = new CancellationTokenSource();
        snapshotPollTimer = new PeriodicTimer(TimeSpan.FromSeconds(15));
        _ = PollSnapshotSettingsAsync(snapshotPollCts.Token);
        _ = RefreshSnapshotSettingsWithRetryAsync(snapshotPollCts.Token);
        StartShowSignalWatcher();
    }

    private bool TryClaimSingleInstance()
    {
        try
        {
            instanceMutex = new Mutex(true, MutexName, out var createdNew);
            return createdNew;
        }
        catch (UnauthorizedAccessException)
        {
            instanceMutex = new Mutex(true, @"Local\SecureDeviceControl.Desktop.SingleInstance", out var createdNew);
            return createdNew;
        }
    }

    private static void SignalExistingInstanceToShow()
    {
        try
        {
            using var existing = EventWaitHandle.OpenExisting(ShowEventName);
            existing.Set();
        }
        catch
        {
        }
    }

    private void StartShowSignalWatcher()
    {
        try
        {
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, ShowEventName);
        }
        catch
        {
            showEvent = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\SecureDeviceControl.Desktop.Show");
        }

        showSignalCts = new CancellationTokenSource();
        var token = showSignalCts.Token;
        _ = Task.Run(() =>
        {
            while (!token.IsCancellationRequested)
            {
                try
                {
                    if (showEvent.WaitOne(TimeSpan.FromSeconds(1)))
                    {
                        Dispatcher.Invoke(ShowMainWindow);
                    }
                }
                catch (OperationCanceledException)
                {
                    break;
                }
                catch
                {
                }
            }
        }, token);
    }

    private void SetupTrayIcon()
    {
        var menu = new System.Windows.Forms.ContextMenuStrip();
        menu.Items.Add("Show", null, (_, _) => ShowMainWindow());
        menu.Items.Add("Exit", null, (_, _) => ExitApp());

        trayIcon = new System.Windows.Forms.NotifyIcon
        {
            Text = "Secure Device Control - snapshots run in background",
            Icon = System.Drawing.SystemIcons.Shield,
            Visible = true,
            ContextMenuStrip = menu
        };
        trayIcon.DoubleClick += (_, _) => ShowMainWindow();
        trayIcon.BalloonTipTitle = "Secure Device Control";
        trayIcon.BalloonTipText = "Snapshots keep running in background. Use tray > Show to open, Exit to quit.";
    }

    public void ShowMainWindow()
    {
        if (mainWindow is null) return;
        if (!mainWindow.IsVisible) mainWindow.Show();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
    }

    public void HideMainWindow()
    {
        mainWindow?.Hide();
        trayIcon?.ShowBalloonTip(3000);
    }

    public void ExitApp()
    {
        isExplicitExit = true;
        mainWindow?.Close();
        Shutdown();
    }

    public bool IsExplicitExit => isExplicitExit;

    private async Task RefreshSnapshotSettingsWithRetryAsync(CancellationToken token)
    {
        var delays = new[] { 1, 2, 3, 5, 8, 13 };
        foreach (var seconds in delays)
        {
            if (token.IsCancellationRequested)
            {
                return;
            }

            await RefreshSnapshotSettingsOnceAsync();
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(seconds), token);
            }
            catch (OperationCanceledException)
            {
                return;
            }
        }
    }

    private async Task RefreshSnapshotSettingsOnceAsync()
    {
        try
        {
            var resp = await ipcClient.SendAsync(IpcRequest.Create(IpcOperation.GetServiceStatus));
            if (resp.Success && resp.Payload.HasValue)
            {
                var status = resp.Payload.Value.Deserialize<ServiceStatusDto>(SecureDeviceControl.Shared.Ipc.IpcJson.Options);
                if (status is not null)
                {
                    snapshotCaptureService?.UpdateSettings(status.SnapshotEnabled, status.SnapshotIntervalMinutes, status.SnapshotMonitoringAcknowledged);
                }
            }
        }
        catch { }
    }

    private async Task PollSnapshotSettingsAsync(CancellationToken token)
    {
        try
        {
            while (await snapshotPollTimer!.WaitForNextTickAsync(token))
            {
                await RefreshSnapshotSettingsOnceAsync();
            }
        }
        catch (OperationCanceledException) { }
    }

    protected override async void OnExit(ExitEventArgs e)
    {
        showSignalCts?.Cancel();
        snapshotPollCts?.Cancel();
        snapshotPollTimer?.Dispose();
        trayIcon?.Dispose();
        showEvent?.Dispose();
        try { instanceMutex?.ReleaseMutex(); } catch { }
        instanceMutex?.Dispose();
        if (snapshotCaptureService is not null)
        {
            await snapshotCaptureService.DisposeAsync();
        }
        base.OnExit(e);
    }
}
