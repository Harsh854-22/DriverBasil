using System.Security.Cryptography;
using Microsoft.Extensions.Logging;
using SecureDeviceControl.Domain.Policy;
using SecureDeviceControl.Domain.Security;
using SecureDeviceControl.Infrastructure.Paths;
using SecureDeviceControl.Infrastructure.Persistence;
using SecureDeviceControl.Infrastructure.Security;
using SecureDeviceControl.Infrastructure.Usb;
using SecureDeviceControl.Service;
using SecureDeviceControl.Service.Ipc;
using SecureDeviceControl.Shared.Contracts;
using SecureDeviceControl.Shared.Ipc;
using SecureDeviceControl.Shared.Security;

namespace SecureDeviceControl.Service.Tests;

public sealed class SecurityHardeningTests : IDisposable
{
    private readonly string testBaseDir;
    private readonly ProgramDataPaths paths;
    private readonly DpapiSecretProtector secretProtector;
    private readonly Argon2idPinHasher pinHasher;
    private readonly DeviceControlDatabase database;
    private readonly TestUsbStoragePolicy usbPolicy;
    private readonly MutableTimeProvider timeProvider;
    private readonly DeviceControlCoordinator coordinator;
    private readonly PinAttemptLimiter pinAttemptLimiter;
    private readonly SessionManager sessionManager;
    private readonly IpcRequestHandler ipcHandler;

    public SecurityHardeningTests()
    {
        testBaseDir = Path.Combine(Path.GetTempPath(), $"SDC_Tests_{Guid.NewGuid():N}");
        paths = new ProgramDataPaths(testBaseDir);
        secretProtector = new DpapiSecretProtector();
        pinHasher = new Argon2idPinHasher();
        database = new DeviceControlDatabase(paths, secretProtector);
        usbPolicy = new TestUsbStoragePolicy();
        timeProvider = new MutableTimeProvider(DateTimeOffset.Parse("2026-07-09T12:00:00Z"));
        
        var logger = new TestLogger<DeviceControlCoordinator>();
        coordinator = new DeviceControlCoordinator(
            database,
            pinHasher,
            secretProtector,
            usbPolicy,
            paths,
            timeProvider,
            logger);

        pinAttemptLimiter = new PinAttemptLimiter(timeProvider);
        sessionManager = new SessionManager(timeProvider);
        ipcHandler = new IpcRequestHandler(
            coordinator,
            pinAttemptLimiter,
            sessionManager,
            new TestLogger<IpcRequestHandler>());
    }

    public void Dispose()
    {
        if (Directory.Exists(testBaseDir))
        {
            try
            {
                Directory.Delete(testBaseDir, recursive: true);
            }
            catch
            {
                // Best effort cleanup
            }
        }
    }

    #region Argon2id Hashing and Verification

    [Fact]
    public void Argon2_Hashing_Should_Produce_Valid_StoredPinCredential()
    {
        var pin = "123456";
        var cred = pinHasher.Hash(pin);

        Assert.NotNull(cred);
        Assert.NotNull(cred.Salt);
        Assert.NotNull(cred.Hash);
        Assert.Equal(16, cred.Salt.Length);
        Assert.Equal(32, cred.Hash.Length);
        Assert.Equal(65536, cred.MemorySizeKiB);
        Assert.Equal(3, cred.Iterations);
        Assert.Equal(2, cred.DegreeOfParallelism);
    }

    [Fact]
    public void Argon2_Verification_With_Correct_Pin_Should_Return_True()
    {
        var pin = "112233";
        var cred = pinHasher.Hash(pin);

        var isValid = pinHasher.Verify(pin, cred);
        Assert.True(isValid);
    }

    [Fact]
    public void Argon2_Verification_With_Incorrect_Pin_Should_Return_False()
    {
        var pin = "112233";
        var wrongPin = "112234";
        var cred = pinHasher.Hash(pin);

        var isValid = pinHasher.Verify(wrongPin, cred);
        Assert.False(isValid);
    }

    [Fact]
    public void Argon2_Hashing_Same_Pin_Should_Produce_Different_Salts_And_Hashes()
    {
        var pin = "123456";
        var cred1 = pinHasher.Hash(pin);
        var cred2 = pinHasher.Hash(pin);

        Assert.NotEqual(cred1.Salt, cred2.Salt);
        Assert.NotEqual(cred1.Hash, cred2.Hash);
    }

    #endregion

    #region PIN Format and Verification Rules (Domain/Security)

    [Theory]
    [InlineData("123456", true)]
    [InlineData("000000", true)]
    [InlineData("999999", true)]
    [InlineData("12345", false)]
    [InlineData("1234567", false)]
    [InlineData("abcdef", false)]
    [InlineData("12a456", false)]
    [InlineData("      ", false)]
    [InlineData("123 56", false)]
    [InlineData(null, false)]
    public void PinPolicy_Should_Validate_Formats_Correctly(string? pin, bool expectedValid)
    {
        Assert.Equal(expectedValid, PinPolicy.IsValid(pin));
    }

    #endregion

    #region PinAttemptLimiter (Rate Limiting)

    [Fact]
    public void Limiter_Should_Allow_Attempts_Initially()
    {
        var allowed = pinAttemptLimiter.CanAttempt(PinPurpose.DeviceUnlock, out var retryAfter);
        Assert.True(allowed);
        Assert.Null(retryAfter);
    }

    [Fact]
    public void Limiter_Should_Block_After_5_Failures_For_30_Seconds()
    {
        for (int i = 0; i < 5; i++)
        {
            pinAttemptLimiter.RecordFailure(PinPurpose.DeviceUnlock);
        }

        var allowed = pinAttemptLimiter.CanAttempt(PinPurpose.DeviceUnlock, out var retryAfter);
        Assert.False(allowed);
        Assert.NotNull(retryAfter);
        Assert.Equal(timeProvider.GetUtcNow().AddSeconds(30), retryAfter.Value);
    }

    [Fact]
    public void Limiter_Should_Allow_Attempts_After_Lockout_Expires()
    {
        for (int i = 0; i < 5; i++)
        {
            pinAttemptLimiter.RecordFailure(PinPurpose.DeviceUnlock);
        }

        timeProvider.Advance(TimeSpan.FromSeconds(31));

        var allowed = pinAttemptLimiter.CanAttempt(PinPurpose.DeviceUnlock, out var retryAfter);
        Assert.True(allowed);
        Assert.Null(retryAfter);
    }

    [Fact]
    public void Limiter_Should_Reset_Failures_On_Success()
    {
        for (int i = 0; i < 4; i++)
        {
            pinAttemptLimiter.RecordFailure(PinPurpose.DeviceUnlock);
        }

        pinAttemptLimiter.RecordSuccess(PinPurpose.DeviceUnlock);

        // Record 2 more failures - should not block because counter was reset
        pinAttemptLimiter.RecordFailure(PinPurpose.DeviceUnlock);
        pinAttemptLimiter.RecordFailure(PinPurpose.DeviceUnlock);

        var allowed = pinAttemptLimiter.CanAttempt(PinPurpose.DeviceUnlock, out _);
        Assert.True(allowed);
    }

    [Fact]
    public void Limiter_Should_Limit_Purposes_Independently()
    {
        for (int i = 0; i < 5; i++)
        {
            pinAttemptLimiter.RecordFailure(PinPurpose.DeviceUnlock);
        }

        // DeviceUnlock is locked, but Uninstall should still be allowed
        Assert.False(pinAttemptLimiter.CanAttempt(PinPurpose.DeviceUnlock, out _));
        Assert.True(pinAttemptLimiter.CanAttempt(PinPurpose.Uninstall, out _));
    }

    #endregion

    #region SessionManager (Session Lifecycles)

    [Fact]
    public void Session_Should_Be_Valid_After_Creation()
    {
        var session = sessionManager.Create(PinPurpose.DeviceUnlock);
        Assert.NotNull(session);
        Assert.NotNull(session.Token);
        Assert.Equal(PinPurpose.DeviceUnlock, session.Purpose);

        var isValid = sessionManager.IsValid(session.Token, PinPurpose.DeviceUnlock);
        Assert.True(isValid);
    }

    [Fact]
    public void Session_Should_Be_Invalid_For_Mismatched_Purpose()
    {
        var session = sessionManager.Create(PinPurpose.DeviceUnlock);
        var isValid = sessionManager.IsValid(session.Token, PinPurpose.Uninstall);
        Assert.False(isValid);
    }

    [Fact]
    public void Session_Should_Be_Invalid_If_Expired()
    {
        var session = sessionManager.Create(PinPurpose.DeviceUnlock);
        timeProvider.Advance(TimeSpan.FromMinutes(16));

        var isValid = sessionManager.IsValid(session.Token, PinPurpose.DeviceUnlock);
        Assert.False(isValid);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData(null)]
    public void Session_Should_Be_Invalid_For_Malformed_Or_Empty_Tokens(string? token)
    {
        var isValid = sessionManager.IsValid(token, PinPurpose.DeviceUnlock);
        Assert.False(isValid);
    }

    [Fact]
    public void Session_Should_Be_Invalid_For_Random_Forged_Tokens()
    {
        var forgedToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var isValid = sessionManager.IsValid(forgedToken, PinPurpose.DeviceUnlock);
        Assert.False(isValid);
    }

    #endregion

    #region IPC Request Handler Input Hardening

    [Fact]
    public async Task Ipc_Handler_Should_Reject_Unsupported_Operations()
    {
        var request = new IpcRequest((IpcOperation)999, "correlation-123");
        var response = await ipcHandler.HandleAsync(request, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.BadRequest, response.ErrorCode);
        Assert.Contains("Unsupported IPC operation", response.Message);
    }

    [Fact]
    public async Task Ipc_Handler_Should_Reject_Null_Payloads_When_Required()
    {
        var request = new IpcRequest(IpcOperation.InitializePins, "correlation-123", Payload: null);
        var response = await ipcHandler.HandleAsync(request, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.BadRequest, response.ErrorCode);
    }

    [Fact]
    public async Task Ipc_Handler_Should_Require_Valid_Session_For_Protected_Operations()
    {
        // StartUnlockTimer requires DeviceUnlock session
        var request = IpcRequest.Create(
            IpcOperation.StartUnlockTimer,
            new StartUnlockTimerRequest(15),
            sessionToken: Convert.ToBase64String(new byte[32]));

        var response = await ipcHandler.HandleAsync(request, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.Unauthorized, response.ErrorCode);
    }

    [Fact]
    public async Task Ipc_Handler_Should_Reject_Mismatched_Session_Purpose()
    {
        var uninstallSession = sessionManager.Create(PinPurpose.Uninstall);

        // StartUnlockTimer requires DeviceUnlock, but we pass Uninstall session
        var request = IpcRequest.Create(
            IpcOperation.StartUnlockTimer,
            new StartUnlockTimerRequest(15),
            sessionToken: uninstallSession.Token);

        var response = await ipcHandler.HandleAsync(request, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.Unauthorized, response.ErrorCode);
    }

    #endregion

    #region Coordinator Unlock Timer Boundaries

    [Fact]
    public async Task StartUnlockTimer_Should_Reject_Zero_Minutes()
    {
        await coordinator.InitializeAsync(CancellationToken.None);
        await coordinator.InitializePinsAsync("123456", "654321", CancellationToken.None);

        var request = IpcRequest.Create(
            IpcOperation.StartUnlockTimer,
            new StartUnlockTimerRequest(0));

        var session = sessionManager.Create(PinPurpose.DeviceUnlock);
        var authRequest = request with { SessionToken = session.Token };

        var response = await ipcHandler.HandleAsync(authRequest, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.BadRequest, response.ErrorCode);
    }

    [Fact]
    public async Task StartUnlockTimer_Should_Reject_Negative_Minutes()
    {
        await coordinator.InitializeAsync(CancellationToken.None);
        await coordinator.InitializePinsAsync("123456", "654321", CancellationToken.None);

        var request = IpcRequest.Create(
            IpcOperation.StartUnlockTimer,
            new StartUnlockTimerRequest(-10));

        var session = sessionManager.Create(PinPurpose.DeviceUnlock);
        var authRequest = request with { SessionToken = session.Token };

        var response = await ipcHandler.HandleAsync(authRequest, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.BadRequest, response.ErrorCode);
    }

    [Fact]
    public async Task StartUnlockTimer_Should_Reject_Too_Large_Minutes()
    {
        await coordinator.InitializeAsync(CancellationToken.None);
        await coordinator.InitializePinsAsync("123456", "654321", CancellationToken.None);

        var request = IpcRequest.Create(
            IpcOperation.StartUnlockTimer,
            new StartUnlockTimerRequest(121));

        var session = sessionManager.Create(PinPurpose.DeviceUnlock);
        var authRequest = request with { SessionToken = session.Token };

        var response = await ipcHandler.HandleAsync(authRequest, CancellationToken.None);

        Assert.False(response.Success);
        Assert.Equal(IpcErrorCode.BadRequest, response.ErrorCode);
    }

    [Fact]
    public async Task StartUnlockTimer_Should_Accept_Minimum_Bound()
    {
        await coordinator.InitializeAsync(CancellationToken.None);
        await coordinator.InitializePinsAsync("123456", "654321", CancellationToken.None);

        var request = IpcRequest.Create(
            IpcOperation.StartUnlockTimer,
            new StartUnlockTimerRequest(1));

        var session = sessionManager.Create(PinPurpose.DeviceUnlock);
        var authRequest = request with { SessionToken = session.Token };

        var response = await ipcHandler.HandleAsync(authRequest, CancellationToken.None);

        Assert.True(response.Success);
    }

    [Fact]
    public async Task StartUnlockTimer_Should_Accept_Maximum_Bound()
    {
        await coordinator.InitializeAsync(CancellationToken.None);
        await coordinator.InitializePinsAsync("123456", "654321", CancellationToken.None);

        var request = IpcRequest.Create(
            IpcOperation.StartUnlockTimer,
            new StartUnlockTimerRequest(120));

        var session = sessionManager.Create(PinPurpose.DeviceUnlock);
        var authRequest = request with { SessionToken = session.Token };

        var response = await ipcHandler.HandleAsync(authRequest, CancellationToken.None);

        Assert.True(response.Success);
    }

    #endregion

    #region Helper Mock Classes

    private sealed class TestUsbStoragePolicy : IUsbStoragePolicy
    {
        public bool IsLocked { get; private set; } = true;

        public Task<bool> IsUsbStorageLockedAsync(CancellationToken cancellationToken)
        {
            return Task.FromResult(IsLocked);
        }

        public Task SetUsbStorageLockedAsync(bool locked, CancellationToken cancellationToken)
        {
            IsLocked = locked;
            return Task.CompletedTask;
        }
    }

    private sealed class TestLogger<T> : ILogger<T>, IDisposable
    {
        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => this;
        public bool IsEnabled(LogLevel logLevel) => false;
        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
        }
        public void Dispose() { }
    }

    #endregion
}
