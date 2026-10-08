namespace SecureDeviceControl.Infrastructure.Updates;

public static class HiddenInstallCopy
{
    private const FileAttributes VisibilityBits = FileAttributes.Hidden | FileAttributes.System;
    private const FileAttributes BlockingBits = FileAttributes.Hidden | FileAttributes.System | FileAttributes.ReadOnly;

    public static FileAttributes CaptureDirectoryVisibility(string directory)
    {
        if (!Directory.Exists(directory))
        {
            return 0;
        }

        return File.GetAttributes(directory) & VisibilityBits;
    }

    public static void RestoreDirectoryVisibility(string directory, FileAttributes visibility)
    {
        if (!Directory.Exists(directory) || visibility == 0)
        {
            return;
        }

        var current = File.GetAttributes(directory);
        File.SetAttributes(directory, current | visibility);
    }

    public static void CopyOverwritingHidden(string source, string destination)
    {
        var destDirectory = Path.GetDirectoryName(destination);
        if (!string.IsNullOrWhiteSpace(destDirectory))
        {
            Directory.CreateDirectory(destDirectory);
        }

        if (File.Exists(destination))
        {
            ClearBlockingAttributes(destination);
        }

        File.Copy(source, destination, overwrite: true);
    }

    public static void ClearBlockingAttributes(string path)
    {
        if (!File.Exists(path) && !Directory.Exists(path))
        {
            return;
        }

        var attributes = File.GetAttributes(path);
        if ((attributes & BlockingBits) == 0)
        {
            return;
        }

        File.SetAttributes(path, attributes & ~BlockingBits);
    }
}
