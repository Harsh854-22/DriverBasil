using System.Runtime.InteropServices;
using System.Text;

namespace SecureDeviceControl.Service.Snapshots;

internal static class InteractiveProcessLauncher
{
    private const int WtsActive = 0;
    private const uint CreateUnicodeEnvironment = 0x00000400;
    private const uint NormalPriorityClass = 0x00000020;
    private const uint TokenQuery = 0x0008;
    private const uint TokenDuplicate = 0x0002;
    private const uint TokenAssignPrimary = 0x0001;
    private const int SecurityImpersonation = 2;
    private const int TokenPrimary = 1;

    public static bool TryStart(string executablePath, string arguments)
    {
        if (!OperatingSystem.IsWindows() || string.IsNullOrWhiteSpace(executablePath) || !File.Exists(executablePath))
        {
            return false;
        }

        if (!WTSEnumerateSessions(IntPtr.Zero, 0, 1, out var sessionList, out var sessionCount) || sessionList == IntPtr.Zero)
        {
            return false;
        }

        try
        {
            var structSize = Marshal.SizeOf<WtsSessionInfo>();
            for (var i = 0; i < sessionCount; i++)
            {
                var session = Marshal.PtrToStructure<WtsSessionInfo>(sessionList + (i * structSize));
                if (session.State != WtsActive)
                {
                    continue;
                }

                if (TryStartInSession(session.SessionId, executablePath, arguments))
                {
                    return true;
                }
            }
        }
        finally
        {
            WTSFreeMemory(sessionList);
        }

        return false;
    }

    private static bool TryStartInSession(int sessionId, string executablePath, string arguments)
    {
        if (!WTSQueryUserToken((uint)sessionId, out var userToken) || userToken == IntPtr.Zero)
        {
            return false;
        }

        var primaryToken = IntPtr.Zero;
        var environment = IntPtr.Zero;
        var processInfo = new ProcessInformation();
        try
        {
            var sa = new SecurityAttributes
            {
                Length = Marshal.SizeOf<SecurityAttributes>()
            };
            if (!DuplicateTokenEx(
                    userToken,
                    TokenQuery | TokenDuplicate | TokenAssignPrimary,
                    ref sa,
                    SecurityImpersonation,
                    TokenPrimary,
                    out primaryToken) ||
                primaryToken == IntPtr.Zero)
            {
                return false;
            }

            CreateEnvironmentBlock(out environment, primaryToken, false);

            var startup = new StartupInfo();
            startup.Cb = Marshal.SizeOf<StartupInfo>();
            startup.Desktop = "winsta0\\default";

            var commandLine = new StringBuilder($"\"{executablePath}\" {arguments}");
            if (!CreateProcessAsUser(
                    primaryToken,
                    executablePath,
                    commandLine,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    false,
                    CreateUnicodeEnvironment | NormalPriorityClass,
                    environment,
                    Path.GetDirectoryName(executablePath),
                    ref startup,
                    out processInfo))
            {
                return false;
            }

            return processInfo.Process != IntPtr.Zero;
        }
        finally
        {
            if (processInfo.Thread != IntPtr.Zero)
            {
                CloseHandle(processInfo.Thread);
            }

            if (processInfo.Process != IntPtr.Zero)
            {
                CloseHandle(processInfo.Process);
            }

            if (environment != IntPtr.Zero)
            {
                DestroyEnvironmentBlock(environment);
            }

            if (primaryToken != IntPtr.Zero)
            {
                CloseHandle(primaryToken);
            }

            CloseHandle(userToken);
        }
    }

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSEnumerateSessions(
        IntPtr server,
        int reserved,
        int version,
        out IntPtr sessionInfo,
        out int count);

    [DllImport("wtsapi32.dll")]
    private static extern void WTSFreeMemory(IntPtr memory);

    [DllImport("wtsapi32.dll", SetLastError = true)]
    private static extern bool WTSQueryUserToken(uint sessionId, out IntPtr token);

    [DllImport("advapi32.dll", SetLastError = true)]
    private static extern bool DuplicateTokenEx(
        IntPtr existingToken,
        uint desiredAccess,
        ref SecurityAttributes tokenAttributes,
        int impersonationLevel,
        int tokenType,
        out IntPtr newToken);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool CreateEnvironmentBlock(out IntPtr environment, IntPtr token, bool inherit);

    [DllImport("userenv.dll", SetLastError = true)]
    private static extern bool DestroyEnvironmentBlock(IntPtr environment);

    [DllImport("advapi32.dll", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool CreateProcessAsUser(
        IntPtr token,
        string applicationName,
        StringBuilder commandLine,
        IntPtr processAttributes,
        IntPtr threadAttributes,
        bool inheritHandles,
        uint creationFlags,
        IntPtr environment,
        string? currentDirectory,
        ref StartupInfo startupInfo,
        out ProcessInformation processInformation);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool CloseHandle(IntPtr handle);

    [StructLayout(LayoutKind.Sequential)]
    private struct WtsSessionInfo
    {
        public int SessionId;
        public IntPtr WinStationName;
        public int State;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct SecurityAttributes
    {
        public int Length;
        public IntPtr SecurityDescriptor;
        public bool InheritHandle;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct StartupInfo
    {
        public int Cb;
        public string? Reserved;
        public string? Desktop;
        public string? Title;
        public int X;
        public int Y;
        public int XSize;
        public int YSize;
        public int XCountChars;
        public int YCountChars;
        public int FillAttribute;
        public int Flags;
        public short ShowWindow;
        public short Reserved2;
        public IntPtr Reserved3;
        public IntPtr StdInput;
        public IntPtr StdOutput;
        public IntPtr StdError;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ProcessInformation
    {
        public IntPtr Process;
        public IntPtr Thread;
        public int ProcessId;
        public int ThreadId;
    }
}
