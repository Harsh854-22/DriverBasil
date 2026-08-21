using System.Drawing;
using System.IO;
using SecureDeviceControl.Dashboard.Auth;
using SecureDeviceControl.Dashboard.Cloud;
using SecureDeviceControl.Dashboard.Imaging;

namespace SecureDeviceControl.Dashboard;

public static class SelfTest
{
    public static int Run(TextWriter output)
    {
        var failures = 0;

        failures += Check(output, "Snapshot object key parsing", () =>
        {
            var entry = SnapshotEntry.TryParse("abc123def456ab12/2026/08/16/20260816-091530-123.jpg");
            return entry is not null
                   && entry.DeviceHash == "abc123def456ab12"
                   && entry.CapturedAtUtc == new DateTimeOffset(2026, 8, 16, 9, 15, 30, 123, TimeSpan.Zero);
        });

        failures += Check(output, "Invalid object keys are rejected", () =>
        {
            return SnapshotEntry.TryParse("garbage") is null
                   && SnapshotEntry.TryParse("a/b/c/d.txt") is null
                   && SnapshotEntry.TryParse("a/2026/08/16/notatime.jpg") is null;
        });

        failures += Check(output, "Device hash matches service format", () =>
        {
            var hash = DeviceInfo.ComputeDeviceHash("OFFICE-PC-01", "employee@company.com");
            return hash.Length == 16 && hash == DeviceInfo.ComputeDeviceHash("OFFICE-PC-01", "employee@company.com");
        });

        byte[] imageA = CreateGradientJpeg(seed: 1);
        byte[] imageACopy = CreateGradientJpeg(seed: 1);
        byte[] imageB = CreateNoiseJpeg(seed: 99);

        failures += Check(output, "Identical screenshots hash to distance 0", () =>
        {
            var hash1 = PerceptualHash.ComputeDHash(imageA);
            var hash2 = PerceptualHash.ComputeDHash(imageACopy);
            return PerceptualHash.HammingDistance(hash1, hash2) == 0;
        });

        failures += Check(output, "Different screenshots hash far apart", () =>
        {
            var hash1 = PerceptualHash.ComputeDHash(imageA);
            var hash2 = PerceptualHash.ComputeDHash(imageB);
            return PerceptualHash.HammingDistance(hash1, hash2) > 8;
        });

        failures += Check(output, "Duplicate frames collapse into one session", () =>
        {
            var frames = new List<(SnapshotEntry Entry, ulong Hash)>();
            var hashA = PerceptualHash.ComputeDHash(imageA);
            var hashB = PerceptualHash.ComputeDHash(imageB);
            var day = new DateTimeOffset(2026, 8, 16, 10, 0, 0, TimeSpan.Zero);

            for (var i = 0; i < 4; i++)
            {
                frames.Add((new SnapshotEntry($"dev/2026/08/16/f{i}.jpg", "dev", day.AddMinutes(5 * i)), hashA));
            }

            frames.Add((new SnapshotEntry("dev/2026/08/16/f4.jpg", "dev", day.AddMinutes(20)), hashB));
            frames.Add((new SnapshotEntry("dev/2026/08/16/f5.jpg", "dev", day.AddMinutes(25)), hashB));

            var sessions = SnapshotSessionizer.GroupSessions(frames, similarityThreshold: 8);
            return sessions.Count == 2
                   && sessions[0].FrameCount == 4
                   && sessions[0].StartUtc == day
                   && sessions[0].EndUtc == day.AddMinutes(15)
                   && sessions[1].FrameCount == 2;
        });

        failures += Check(output, "Admin password hashing verifies correctly", () =>
        {
            var stored = AdminAccount.HashPassword("Test-Password-123");
            return AdminAccount.Verify("Test-Password-123", stored)
                   && !AdminAccount.Verify("wrong-password", stored)
                   && !AdminAccount.Verify("Test-Password-123", "corrupt");
        });

        output.WriteLine(failures == 0
            ? "SELFTEST RESULT: PASS (all checks succeeded)"
            : $"SELFTEST RESULT: FAIL ({failures} check(s) failed)");
        return failures == 0 ? 0 : 1;
    }

    private static int Check(TextWriter output, string name, Func<bool> test)
    {
        try
        {
            var passed = test();
            output.WriteLine($"  [{(passed ? "PASS" : "FAIL")}] {name}");
            return passed ? 0 : 1;
        }
        catch (Exception ex)
        {
            output.WriteLine($"  [FAIL] {name} -> {ex.Message}");
            return 1;
        }
    }

    private static byte[] CreateGradientJpeg(int seed)
    {
        using var bitmap = new Bitmap(64, 64);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var value = (x * 4 + seed) % 256;
                bitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
            }
        }

        return ToJpeg(bitmap);
    }

    private static byte[] CreateNoiseJpeg(int seed)
    {
        var random = new Random(seed);
        using var bitmap = new Bitmap(64, 64);
        for (var y = 0; y < 64; y++)
        {
            for (var x = 0; x < 64; x++)
            {
                var value = random.Next(256);
                bitmap.SetPixel(x, y, Color.FromArgb(value, value, value));
            }
        }

        return ToJpeg(bitmap);
    }

    private static byte[] ToJpeg(Bitmap bitmap)
    {
        using var stream = new MemoryStream();
        bitmap.Save(stream, System.Drawing.Imaging.ImageFormat.Jpeg);
        return stream.ToArray();
    }
}
