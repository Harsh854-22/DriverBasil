using Microsoft.Extensions.Logging.Abstractions;
using SecureDeviceControl.Infrastructure.Paths;
using SecureDeviceControl.Infrastructure.Persistence;
using SecureDeviceControl.Infrastructure.Security;
using SecureDeviceControl.Service.Provisioning;
using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Service.Tests;

public sealed class DeviceProvisionerTests : IDisposable
{
    private readonly string tempDir;
    private readonly DeviceProvisioner provisioner;
    private readonly DeviceControlDatabase database;
    private readonly IPinHasher pinHasher;

    public DeviceProvisionerTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "SdcProvisionTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);

        var paths = new ProgramDataPaths(tempDir);
        pinHasher = new Argon2idPinHasher();
        database = new DeviceControlDatabase(paths, new DpapiSecretProtector());
        provisioner = new DeviceProvisioner(
            paths, database, pinHasher, NullLogger<DeviceProvisioner>.Instance);
    }

    public void Dispose()
    {
        try { Directory.Delete(tempDir, recursive: true); } catch { }
    }

    [Fact]
    public async Task FreshProvision_SetsPinsEmailAndPendingFlag()
    {
        var result = await provisioner.ProvisionAsync(
            new ProvisionRequest("tech@company.com", "421301", "121356", true),
            CancellationToken.None);

        Assert.Equal(ProvisionOutcome.ProvisionedFresh, result.Outcome);
        Assert.Equal("tech@company.com", result.UserEmail);
        Assert.False(result.DatabaseWasRecovered);

        Assert.True(await database.HasPinCredentialsAsync(CancellationToken.None));
        Assert.Equal(
            "tech@company.com",
            await database.GetPolicySettingAsync("user_email", "", CancellationToken.None));
        Assert.Equal(
            "true",
            await database.GetPolicySettingAsync("cloud_registration_pending", "", CancellationToken.None));
        Assert.Equal(
            "true",
            await database.GetPolicySettingAsync("snapshot_monitoring_acknowledged", "", CancellationToken.None));

        var deviceCred = await database.GetPinCredentialAsync(PinPurpose.DeviceUnlock, CancellationToken.None);
        var uninstallCred = await database.GetPinCredentialAsync(PinPurpose.Uninstall, CancellationToken.None);
        Assert.NotNull(deviceCred);
        Assert.NotNull(uninstallCred);
        Assert.True(pinHasher.Verify("421301", deviceCred!));
        Assert.True(pinHasher.Verify("121356", uninstallCred!));
        Assert.False(pinHasher.Verify("000000", deviceCred!));
    }

    [Fact]
    public async Task Provision_OverwritesExistingPinsWithoutTouchingLockSettings()
    {
        await provisioner.ProvisionAsync(
            new ProvisionRequest("tech@company.com", "111111", "222222", true),
            CancellationToken.None);
        await database.SetPolicySettingAsync("usb_storage_locked", "false", CancellationToken.None);

        var result = await provisioner.ProvisionAsync(
            new ProvisionRequest("tech@company.com", "421301", "121356", true),
            CancellationToken.None);

        Assert.Equal(ProvisionOutcome.UpdatedExisting, result.Outcome);

        var deviceCred = await database.GetPinCredentialAsync(PinPurpose.DeviceUnlock, CancellationToken.None);
        Assert.True(pinHasher.Verify("421301", deviceCred!));
        Assert.False(pinHasher.Verify("111111", deviceCred!));

        Assert.Equal(
            "false",
            await database.GetPolicySettingAsync("usb_storage_locked", "", CancellationToken.None));
    }

    [Fact]
    public async Task Provision_EmptyEmail_KeepsExistingEmail()
    {
        await provisioner.ProvisionAsync(
            new ProvisionRequest("tech@company.com", "421301", "121356", true),
            CancellationToken.None);

        var result = await provisioner.ProvisionAsync(
            new ProvisionRequest("", "421301", "121356", true),
            CancellationToken.None);

        Assert.Equal(ProvisionOutcome.UpdatedExisting, result.Outcome);
        Assert.Equal("tech@company.com", result.UserEmail);
    }

    [Fact]
    public async Task Provision_EmptyEmail_OnFreshPc_ThrowsValidation()
    {
        await Assert.ThrowsAsync<ProvisionValidationException>(() =>
            provisioner.ProvisionAsync(
                new ProvisionRequest("", "421301", "121356", true),
                CancellationToken.None));
    }

    [Theory]
    [InlineData("bad-email", "421301", "121356")]
    [InlineData("tech@company.com", "12345", "121356")]
    [InlineData("tech@company.com", "421301", "421301")]
    public async Task Provision_InvalidInput_ThrowsValidation(
        string email, string devicePin, string uninstallPin)
    {
        await Assert.ThrowsAsync<ProvisionValidationException>(() =>
            provisioner.ProvisionAsync(
                new ProvisionRequest(email, devicePin, uninstallPin, true),
                CancellationToken.None));
    }

    [Fact]
    public async Task Provision_CorruptDatabase_QuarantinesAndProvisionsFresh()
    {
        var dbPath = Path.Combine(tempDir, "secure-device-control.db");
        await File.WriteAllTextAsync(dbPath, "this is not a sqlite database");

        var result = await provisioner.ProvisionAsync(
            new ProvisionRequest("tech@company.com", "421301", "121356", true),
            CancellationToken.None);

        Assert.Equal(ProvisionOutcome.RecoveredCorruptDatabase, result.Outcome);
        Assert.True(result.DatabaseWasRecovered);
        Assert.NotNull(result.RecoveryDirectory);
        Assert.True(Directory.Exists(result.RecoveryDirectory));

        var deviceCred = await database.GetPinCredentialAsync(PinPurpose.DeviceUnlock, CancellationToken.None);
        Assert.True(pinHasher.Verify("421301", deviceCred!));
    }
}
