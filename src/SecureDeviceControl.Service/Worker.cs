using SecureDeviceControl.Service.Ipc;

namespace SecureDeviceControl.Service;

public sealed class Worker : BackgroundService
{
    private readonly DeviceControlCoordinator coordinator;
    private readonly NamedPipeServer namedPipeServer;

    public Worker(DeviceControlCoordinator coordinator, NamedPipeServer namedPipeServer)
    {
        this.coordinator = coordinator;
        this.namedPipeServer = namedPipeServer;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await coordinator.InitializeAsync(stoppingToken);
        await Task.WhenAll(
            coordinator.RunPolicyLoopAsync(stoppingToken),
            namedPipeServer.RunAsync(stoppingToken));
    }
}
