using SecureDeviceControl.Infrastructure.Updates;

namespace SecureDeviceControl.Service.Tests;

public sealed class HiddenInstallCopyTests : IDisposable
{
    private readonly string tempDir;

    public HiddenInstallCopyTests()
    {
        tempDir = Path.Combine(Path.GetTempPath(), "SdcHiddenCopyTests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempDir);
    }

    public void Dispose()
    {
        try
        {
            HiddenInstallCopy.ClearBlockingAttributes(tempDir);
            foreach (var file in Directory.GetFiles(tempDir, "*", SearchOption.AllDirectories))
            {
                HiddenInstallCopy.ClearBlockingAttributes(file);
            }

            Directory.Delete(tempDir, recursive: true);
        }
        catch
        {
        }
    }

    [Fact]
    public void CopyOverwritingHidden_ReplacesHiddenDestinationAndKeepsFolderHidden()
    {
        var installDir = Path.Combine(tempDir, "SecureDeviceControl");
        Directory.CreateDirectory(installDir);
        var dest = Path.Combine(installDir, "SecureDeviceControl.Service.exe");
        File.WriteAllText(dest, "old-bin");
        File.SetAttributes(dest, FileAttributes.Hidden | FileAttributes.System);
        File.SetAttributes(installDir, File.GetAttributes(installDir) | FileAttributes.Hidden | FileAttributes.System);

        var source = Path.Combine(tempDir, "new-bin.exe");
        File.WriteAllText(source, "new-bin");

        var visibility = HiddenInstallCopy.CaptureDirectoryVisibility(installDir);
        HiddenInstallCopy.CopyOverwritingHidden(source, dest);
        HiddenInstallCopy.RestoreDirectoryVisibility(installDir, visibility);

        Assert.Equal("new-bin", File.ReadAllText(dest));
        Assert.True(File.GetAttributes(installDir).HasFlag(FileAttributes.Hidden));
        Assert.True(File.GetAttributes(installDir).HasFlag(FileAttributes.System));
    }

    [Fact]
    public void CaptureDirectoryVisibility_ReturnsZeroForNormalFolder()
    {
        var folder = Path.Combine(tempDir, "normal");
        Directory.CreateDirectory(folder);
        Assert.Equal((FileAttributes)0, HiddenInstallCopy.CaptureDirectoryVisibility(folder));
    }
}
