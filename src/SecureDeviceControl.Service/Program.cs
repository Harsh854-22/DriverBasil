using SecureDeviceControl.Infrastructure.Paths;
using SecureDeviceControl.Infrastructure.Persistence;
using SecureDeviceControl.Infrastructure.Security;
using SecureDeviceControl.Infrastructure.Usb;
using SecureDeviceControl.Service;
using SecureDeviceControl.Service.Ipc;
using SecureDeviceControl.Shared;

using SecureDeviceControl.Infrastructure.Web;

// CRITICAL: Windows Services start with CWD = C:\Windows\System32.
// appsettings.json lives next to the EXE, so we must change directory
// BEFORE Host.CreateApplicationBuilder reads configuration files.
Directory.SetCurrentDirectory(AppContext.BaseDirectory);

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = ServiceIdentity.ServiceName;
});

// Add Windows Event Log as a fallback logging sink for diagnostics
if (OperatingSystem.IsWindows())
{
    builder.Logging.AddEventLog(new Microsoft.Extensions.Logging.EventLog.EventLogSettings
    {
        SourceName = "SecureDeviceControl.Service",
        LogName = "Application"
    });
}

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ProgramDataPaths>();
builder.Services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
builder.Services.AddSingleton<IPinHasher, Argon2idPinHasher>();
builder.Services.AddSingleton<DeviceControlDatabase>();
builder.Services.AddSingleton<IUsbStoragePolicy, RegistryUsbStoragePolicy>();
builder.Services.AddSingleton<IMobilePortPolicy, RegistryMobilePortPolicy>();
builder.Services.AddSingleton<IWebFilterPolicy, HostsWebFilterPolicy>();
builder.Services.AddSingleton<SecureDeviceControl.Infrastructure.Vpn.IVpnFilterPolicy, SecureDeviceControl.Infrastructure.Vpn.VpnFilterPolicy>();
builder.Services.AddSingleton<RestrictedAccessBlockServer>();
builder.Services.AddSingleton<IRemovableDriveMonitor, RemovableDriveMonitor>();
builder.Services.AddSingleton<ICloudRepository, PostgresCloudRepository>();
builder.Services.AddSingleton<IWindowsAccountManager, WindowsAccountManager>();
builder.Services.AddSingleton<SecureDeviceControl.Infrastructure.Updates.ISoftwareUpdater, SecureDeviceControl.Infrastructure.Updates.SoftwareUpdater>();
builder.Services.AddSingleton<DeviceControlCoordinator>();
builder.Services.AddSingleton<PinAttemptLimiter>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<IpcRequestHandler>();
builder.Services.AddSingleton<NamedPipeServer>();
builder.Services.AddHostedService<Worker>();
builder.Services.AddHostedService<SupabaseSyncWorker>();
builder.Services.AddHostedService<SoftwareUpdateWorker>();

var host = builder.Build();
host.Run();
