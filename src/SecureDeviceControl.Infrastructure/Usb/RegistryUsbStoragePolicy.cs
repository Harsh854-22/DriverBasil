using Microsoft.Win32;
using System.Runtime.Versioning;

namespace SecureDeviceControl.Infrastructure.Usb;

[SupportedOSPlatform("windows")]
public sealed class RegistryUsbStoragePolicy : IUsbStoragePolicy
{
    private const string UsbStorRegistryPath = @"SYSTEM\CurrentControlSet\Services\USBSTOR";
    private const string StartValueName = "Start";
    private const int EnabledStartValue = 3;
    private const int DisabledStartValue = 4;

    public Task<bool> IsUsbStorageLockedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = Registry.LocalMachine.OpenSubKey(UsbStorRegistryPath, writable: false);
        var value = key?.GetValue(StartValueName);
        return Task.FromResult(value is int startValue && startValue == DisabledStartValue);
    }

    public Task SetUsbStorageLockedAsync(bool locked, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        try
        {
            using var key = Registry.LocalMachine.OpenSubKey(UsbStorRegistryPath, writable: true);
            if (key != null)
            {
                key.SetValue(
                    StartValueName,
                    locked ? DisabledStartValue : EnabledStartValue,
                    RegistryValueKind.DWord);
            }
        }
        catch (Exception)
        {
            // Suppress if running in non-elevated context or registry key access is restricted
        }

        return Task.CompletedTask;
    }
}
