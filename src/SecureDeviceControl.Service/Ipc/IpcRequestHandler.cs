using System.Text.Json;
using SecureDeviceControl.Shared.Contracts;
using SecureDeviceControl.Shared.Ipc;
using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Service.Ipc;

public sealed class IpcRequestHandler
{
    private readonly DeviceControlCoordinator coordinator;
    private readonly PinAttemptLimiter pinAttemptLimiter;
    private readonly SessionManager sessionManager;
    private readonly ILogger<IpcRequestHandler> logger;

    public IpcRequestHandler(
        DeviceControlCoordinator coordinator,
        PinAttemptLimiter pinAttemptLimiter,
        SessionManager sessionManager,
        ILogger<IpcRequestHandler> logger)
    {
        this.coordinator = coordinator;
        this.pinAttemptLimiter = pinAttemptLimiter;
        this.sessionManager = sessionManager;
        this.logger = logger;
    }

    public async Task<IpcResponse> HandleAsync(IpcRequest request, CancellationToken cancellationToken)
    {
        try
        {
            return request.Operation switch
            {
                IpcOperation.GetServiceStatus => await GetServiceStatusAsync(request, cancellationToken),
                IpcOperation.InitializePins => await InitializePinsAsync(request, cancellationToken),
                IpcOperation.ValidatePin => await ValidatePinAsync(request, cancellationToken),
                IpcOperation.StartUnlockTimer => await StartUnlockTimerAsync(request, cancellationToken),
                IpcOperation.RequestUninstallAuthorization => await RequestUninstallAuthorizationAsync(request, cancellationToken),
                IpcOperation.ListActivityLogs => await ListActivityLogsAsync(request, cancellationToken),
                _ => IpcResponse.Fail(request.CorrelationId, IpcErrorCode.BadRequest, "Unsupported IPC operation.")
            };
        }
        catch (IpcRequestException ex)
        {
            return IpcResponse.Fail(request.CorrelationId, ex.ErrorCode, ex.Message);
        }
        catch (JsonException)
        {
            return IpcResponse.Fail(request.CorrelationId, IpcErrorCode.BadRequest, "Malformed IPC payload.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unhandled IPC operation failure for {Operation}.", request.Operation);
            return IpcResponse.Fail(request.CorrelationId, IpcErrorCode.InternalError, "The service could not complete the request.");
        }
    }

    private async Task<IpcResponse> GetServiceStatusAsync(
        IpcRequest request,
        CancellationToken cancellationToken)
    {
        var status = await coordinator.GetStatusAsync(cancellationToken);
        return IpcResponse.Ok(request.CorrelationId, status);
    }

    private async Task<IpcResponse> InitializePinsAsync(
        IpcRequest request,
        CancellationToken cancellationToken)
    {
        var payload = ReadPayload<InitializePinsRequest>(request);
        await coordinator.InitializePinsAsync(payload.DeviceUnlockPin, payload.UninstallPin, cancellationToken);
        return IpcResponse.Ok(request.CorrelationId);
    }

    private async Task<IpcResponse> ValidatePinAsync(
        IpcRequest request,
        CancellationToken cancellationToken)
    {
        var payload = ReadPayload<ValidatePinRequest>(request);
        if (!pinAttemptLimiter.CanAttempt(payload.Purpose, out var retryAfter))
        {
            return IpcResponse.Fail(
                request.CorrelationId,
                IpcErrorCode.RateLimited,
                $"Too many invalid attempts. Try again after {retryAfter:O}.");
        }

        var isValid = await coordinator.ValidatePinAsync(payload.Purpose, payload.Pin, cancellationToken);
        if (!isValid)
        {
            pinAttemptLimiter.RecordFailure(payload.Purpose);
            return IpcResponse.Fail(request.CorrelationId, IpcErrorCode.InvalidPin, "Invalid PIN.");
        }

        pinAttemptLimiter.RecordSuccess(payload.Purpose);
        var session = sessionManager.Create(payload.Purpose);
        return IpcResponse.Ok(
            request.CorrelationId,
            new ValidatePinResult(payload.Purpose, session.Token, session.ExpiresAt));
    }

    private async Task<IpcResponse> StartUnlockTimerAsync(
        IpcRequest request,
        CancellationToken cancellationToken)
    {
        RequireSession(request.SessionToken, PinPurpose.DeviceUnlock);
        var payload = ReadPayload<StartUnlockTimerRequest>(request);
        var result = await coordinator.StartUnlockTimerAsync(payload.Minutes, cancellationToken);
        return IpcResponse.Ok(request.CorrelationId, result);
    }

    private async Task<IpcResponse> RequestUninstallAuthorizationAsync(
        IpcRequest request,
        CancellationToken cancellationToken)
    {
        RequireSession(request.SessionToken, PinPurpose.Uninstall);
        var result = await coordinator.IssueUninstallAuthorizationAsync(cancellationToken);
        return IpcResponse.Ok(request.CorrelationId, result);
    }

    private async Task<IpcResponse> ListActivityLogsAsync(
        IpcRequest request,
        CancellationToken cancellationToken)
    {
        RequireAnySession(request.SessionToken);
        var payload = request.Payload is null
            ? new ListActivityLogsRequest()
            : ReadPayload<ListActivityLogsRequest>(request);
        var result = await coordinator.ListActivityLogsAsync(payload.Limit, cancellationToken);
        return IpcResponse.Ok(request.CorrelationId, result);
    }

    private void RequireSession(string? sessionToken, PinPurpose purpose)
    {
        if (!sessionManager.IsValid(sessionToken, purpose))
        {
            throw new IpcRequestException(IpcErrorCode.Unauthorized, "A valid PIN session is required.");
        }
    }

    private void RequireAnySession(string? sessionToken)
    {
        if (!sessionManager.IsValid(sessionToken))
        {
            throw new IpcRequestException(IpcErrorCode.Unauthorized, "A valid PIN session is required.");
        }
    }

    private static TPayload ReadPayload<TPayload>(IpcRequest request)
    {
        if (request.Payload is null)
        {
            throw new IpcRequestException(IpcErrorCode.BadRequest, "IPC payload is required.");
        }

        return request.Payload.Value.Deserialize<TPayload>(IpcJson.Options)
            ?? throw new IpcRequestException(IpcErrorCode.BadRequest, "IPC payload is empty.");
    }
}
