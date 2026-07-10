using SecureDeviceControl.Infrastructure.Paths;
using SecureDeviceControl.Infrastructure.Persistence;
using SecureDeviceControl.Infrastructure.Security;
using SecureDeviceControl.Infrastructure.Usb;
using SecureDeviceControl.Service;
using SecureDeviceControl.Service.Ipc;

var builder = Host.CreateApplicationBuilder(args);

builder.Services.AddWindowsService(options =>
{
    options.ServiceName = "Secure Device Control";
});

builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddSingleton<ProgramDataPaths>();
builder.Services.AddSingleton<ISecretProtector, DpapiSecretProtector>();
builder.Services.AddSingleton<IPinHasher, Argon2idPinHasher>();
builder.Services.AddSingleton<DeviceControlDatabase>();
builder.Services.AddSingleton<IUsbStoragePolicy, RegistryUsbStoragePolicy>();
builder.Services.AddSingleton<DeviceControlCoordinator>();
builder.Services.AddSingleton<PinAttemptLimiter>();
builder.Services.AddSingleton<SessionManager>();
builder.Services.AddSingleton<IpcRequestHandler>();
builder.Services.AddSingleton<NamedPipeServer>();
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
