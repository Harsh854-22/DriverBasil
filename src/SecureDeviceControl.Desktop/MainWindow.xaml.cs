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
                new InitializePinsRequest(SetupDevicePinBox.Password, SetupUninstallPinBox.Password)));

            EnsureSuccess(response);
            SetupDevicePinBox.Clear();
            SetupUninstallPinBox.Clear();
            MessageText.Text = "Protection initialized. Pendrive access is locked until you enter the pendrive PIN.";
            await RefreshStatusAsync();
        });
    }

    private async void UnlockButton_Click(object sender, RoutedEventArgs e)
    {
        await RunUiActionAsync(async () =>
        {
            var validateResponse = await ipcClient.SendAsync(IpcRequest.Create(
                IpcOperation.ValidatePin,
                new ValidatePinRequest(PinPurpose.DeviceUnlock, UnlockPinBox.Password)));
            EnsureSuccess(validateResponse);

            var session = ReadPayload<ValidatePinResult>(validateResponse);
            deviceUnlockSessionToken = session.SessionToken;

            var unlockResponse = await ipcClient.SendAsync(IpcRequest.Create(
                IpcOperation.StartUnlockTimer,
                new StartUnlockTimerRequest(15),
                deviceUnlockSessionToken));
            EnsureSuccess(unlockResponse);

            var unlockResult = ReadPayload<StartUnlockTimerResult>(unlockResponse);
            UnlockPinBox.Clear();
            MessageText.Text = $"Pendrive access unlocked until {unlockResult.ExpiresAt.LocalDateTime}.";
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
                logs.Select(log => $"{log.Timestamp.LocalDateTime:g}  {log.EventType}  {log.Message}"));
        });
    }

    private async Task RefreshStatusAsync()
    {
        await RunUiActionAsync(async () =>
        {
            var response = await ipcClient.SendAsync(IpcRequest.Create(IpcOperation.GetServiceStatus));
            EnsureSuccess(response);
            var status = ReadPayload<ServiceStatusDto>(response);

            ServiceStatusText.Text = status.IsInitialized ? "Ready" : "Needs PIN setup";
            UsbLockText.Text = status.IsUsbStorageLocked ? "Locked" : "Unlocked";
            UnlockTimerText.Text = status.IsUnlockTimerActive && status.UnlockExpiresAt is not null
                ? $"Active until {status.UnlockExpiresAt.Value.LocalDateTime:g}"
                : "Inactive";

            SetupPanel.Visibility = status.IsInitialized ? Visibility.Collapsed : Visibility.Visible;
            MainPanel.Visibility = status.IsInitialized ? Visibility.Visible : Visibility.Collapsed;
        });
    }

    private async Task RunUiActionAsync(Func<Task> action)
    {
        try
        {
            MessageText.Text = "";
            await action();
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
