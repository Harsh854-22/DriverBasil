using Microsoft.Win32;
using System.Runtime.Versioning;

namespace SecureDeviceControl.Infrastructure.Usb;

[SupportedOSPlatform("windows")]
public sealed class RegistryMobilePortPolicy : IMobilePortPolicy
{
    private const string WpdMtpDrRegistryPath = @"SYSTEM\CurrentControlSet\Services\WpdMtpDr";
    private const string StartValueName = "Start";
    private const int EnabledStartValue = 3;
    private const int DisabledStartValue = 4;

    public Task<bool> IsMobilePortLockedAsync(CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = Registry.LocalMachine.OpenSubKey(WpdMtpDrRegistryPath, writable: false);
        var value = key?.GetValue(StartValueName);
        return Task.FromResult(value is int startValue && startValue == DisabledStartValue);
    }

    public Task SetMobilePortLockedAsync(bool locked, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        using var key = Registry.LocalMachine.OpenSubKey(WpdMtpDrRegistryPath, writable: true)
            ?? throw new InvalidOperationException("WPD MTP driver registry key was not found.");

        key.SetValue(
            StartValueName,
            locked ? DisabledStartValue : EnabledStartValue,
            RegistryValueKind.DWord);

        return Task.CompletedTask;
    }
}
