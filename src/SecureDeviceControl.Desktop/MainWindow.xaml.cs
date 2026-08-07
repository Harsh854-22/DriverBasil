using System.Text.Json;
using System.Windows;
using SecureDeviceControl.Shared.Contracts;
using SecureDeviceControl.Shared.Ipc;
using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Desktop;

public partial class MainWindow : Window
{
    private readonly IpcClient ipcClient = new();
    private string? deviceUnlockSessionToken;
    private string? uninstallSessionToken;

    public MainWindow()
    {
        InitializeComponent();
    }

    private async void Window_Loaded(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync();
    }

    private async void RefreshButton_Click(object sender, RoutedEventArgs e)
    {
        await RefreshStatusAsync();
    }

    private async void InitializePinsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            var response = await ipcClient.SendAsync(IpcRequest.Create(
                IpcOperation.InitializePins,
                new InitializePinsRequest(UserEmailBox.Text, SetupDevicePinBox.Password, SetupUninstallPinBox.Password)));

            EnsureSuccess(response);
            UserEmailBox.Clear();
            SetupDevicePinBox.Clear();
            SetupUninstallPinBox.Clear();
            MessageText.Text = "Protection initialized and registered. Hardware access is locked until you enter the device PIN.";
            await RefreshStatusAsync();
        });
    }

    private async Task EnsureDeviceUnlockSessionAsync()
    {
        if (string.IsNullOrEmpty(UnlockPinBox.Password))
        {
            if (string.IsNullOrEmpty(deviceUnlockSessionToken))
            {
                throw new InvalidOperationException("Please enter the Device Unlock PIN.");
            }
            return;
        }

        var validateResponse = await ipcClient.SendAsync(IpcRequest.Create(
            IpcOperation.ValidatePin,
            new ValidatePinRequest(PinPurpose.DeviceUnlock, UnlockPinBox.Password)));
        EnsureSuccess(validateResponse);

        var session = ReadPayload<ValidatePinResult>(validateResponse);
        deviceUnlockSessionToken = session.SessionToken;
        UnlockPinBox.Clear();
    }

    private async void LockUsbButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            var response = await ipcClient.SendAsync(IpcRequest.Create(
                IpcOperation.SetDeviceClassLock,
                new SetDeviceClassLockRequest(DeviceClass.RemovableStorage, true),
                deviceUnlockSessionToken));
            EnsureSuccess(response);
            MessageText.Text = "USB Storage has been locked.";
            await RefreshStatusAsync();
        });
    }

    private async void UnlockUsbButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            await EnsureDeviceUnlockSessionAsync();
            var response = await ipcClient.SendAsync(IpcRequest.Create(
                IpcOperation.SetDeviceClassLock,
                new SetDeviceClassLockRequest(DeviceClass.RemovableStorage, false),
                deviceUnlockSessionToken));
            EnsureSuccess(response);
            MessageText.Text = "USB Storage has been unlocked.";
            await RefreshStatusAsync();
        });
    }

    private async void LockMobileButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            var response = await ipcClient.SendAsync(IpcRequest.Create(
                IpcOperation.SetDeviceClassLock,
                new SetDeviceClassLockRequest(DeviceClass.MobileDevice, true),
                deviceUnlockSessionToken));
            EnsureSuccess(response);
            MessageText.Text = "Mobile Port has been locked.";
            await RefreshStatusAsync();
        });
    }

    private async void UnlockMobileButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            await EnsureDeviceUnlockSessionAsync();
            var response = await ipcClient.SendAsync(IpcRequest.Create(
                IpcOperation.SetDeviceClassLock,
                new SetDeviceClassLockRequest(DeviceClass.MobileDevice, false),
                deviceUnlockSessionToken));
            EnsureSuccess(response);
            MessageText.Text = "Mobile Port has been unlocked.";
            await RefreshStatusAsync();
        });
    }

    private async void UnlockTimerButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            await EnsureDeviceUnlockSessionAsync();
            var unlockResponse = await ipcClient.SendAsync(IpcRequest.Create(
                IpcOperation.StartUnlockTimer,
                new StartUnlockTimerRequest(15),
                deviceUnlockSessionToken));
            EnsureSuccess(unlockResponse);

            var unlockResult = ReadPayload<StartUnlockTimerResult>(unlockResponse);
            MessageText.Text = $"All access unlocked temporarily until {unlockResult.ExpiresAt.LocalDateTime}.";
            await RefreshStatusAsync();
        });
    }

    private async void UninstallButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            var validateResponse = await ipcClient.SendAsync(IpcRequest.Create(
                IpcOperation.ValidatePin,
                new ValidatePinRequest(PinPurpose.Uninstall, UninstallPinBox.Password)));
            EnsureSuccess(validateResponse);

            var session = ReadPayload<ValidatePinResult>(validateResponse);
            uninstallSessionToken = session.SessionToken;

            var authResponse = await ipcClient.SendAsync(IpcRequest.Create(
                IpcOperation.RequestUninstallAuthorization,
                uninstallSessionToken));
            EnsureSuccess(authResponse);

            var result = ReadPayload<UninstallAuthorizationResult>(authResponse);
            UninstallPinBox.Clear();
            MessageText.Text = $"Uninstall authorized until {result.ExpiresAt.LocalDateTime}. Token: {result.AuthorizationToken}";
        });
    }

    private async void LoadLogsButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            var sessionToken = deviceUnlockSessionToken ?? uninstallSessionToken;
            var response = await ipcClient.SendAsync(IpcRequest.Create(
                IpcOperation.ListActivityLogs,
                new ListActivityLogsRequest(50),
                sessionToken));
            EnsureSuccess(response);

            var logs = ReadPayload<IReadOnlyList<ActivityLogDto>>(response);
            ActivityLogBox.Text = string.Join(
                Environment.NewLine,
                logs.Select(log => $"{log.Timestamp.LocalDateTime:g} [{log.MachineName} | {log.UserEmail}] {log.EventType}: {log.Message}"));
        });
    }

    private async Task RefreshStatusAsync()
    {
        try
        {
            MessageText.Text = "";
            var response = await ipcClient.SendAsync(IpcRequest.Create(IpcOperation.GetServiceStatus));
            EnsureSuccess(response);
            var status = ReadPayload<ServiceStatusDto>(response);

            ServiceStatusText.Text = status.IsInitialized ? "Ready" : "Needs Registration";
            UserInfoText.Text = !string.IsNullOrEmpty(status.UserEmail)
                ? $"{status.UserEmail} ({status.MachineName})"
                : $"Unregistered ({status.MachineName})";
            LockStatusText.Text = $"USB: {(status.IsUsbStorageLocked ? "Locked" : "Unlocked")} | Mobile: {(status.IsMobilePortLocked ? "Locked" : "Unlocked")}";
            UnlockTimerText.Text = status.IsUnlockTimerActive && status.UnlockExpiresAt is not null
                ? $"Active until {status.UnlockExpiresAt.Value.LocalDateTime:g}"
                : "Inactive";

            SetupPanel.Visibility = status.IsInitialized ? Visibility.Collapsed : Visibility.Visible;
            MainPanel.Visibility = status.IsInitialized ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception ex)
        {
            ServiceStatusText.Text = "Offline";
            UserInfoText.Text = $"Unregistered ({Environment.MachineName})";
            LockStatusText.Text = "Unknown";
            UnlockTimerText.Text = "Inactive";
            MessageText.Text = $"Protection Service is currently offline ({ex.Message}). Enter PINs and click 'Initialize & Register' to start automatically.";
            SetupPanel.Visibility = Visibility.Visible;
            MainPanel.Visibility = Visibility.Collapsed;
        }
    }

    private async Task RunUiActionAsync(Func<Task> action)
    {
        try
        {
            MessageText.Text = "";
            await action();
        }
        catch (OperationCanceledException)
        {
            MessageText.Text = "Operation timed out. Please check your internet connection and try again.";
        }
        catch (System.IO.IOException)
        {
            MessageText.Text = "Could not connect to the background service. Please ensure the service is running and try again.";
        }
        catch (TimeoutException)
        {
            MessageText.Text = "Connection timed out. Please check your internet connection and try again.";
        }
        catch (Exception ex)
        {
            MessageText.Text = ex.Message;
        }
    }

    private static void EnsureSuccess(IpcResponse response)
    {
        if (!response.Success)
        {
            throw new InvalidOperationException(response.Message ?? response.ErrorCode.ToString());
        }
    }

    private static TPayload ReadPayload<TPayload>(IpcResponse response)
    {
        if (response.Payload is null)
        {
            throw new InvalidOperationException("The service response did not include the expected payload.");
        }

        return response.Payload.Value.Deserialize<TPayload>(IpcJson.Options)
            ?? throw new InvalidOperationException("The service response payload was empty.");
    }
}
