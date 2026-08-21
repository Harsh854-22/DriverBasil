using SecureDeviceControl.Dashboard.Cloud;

namespace SecureDeviceControl.Dashboard.Imaging;

public sealed class SnapshotSession
{
    public SnapshotSession(SnapshotEntry representative)
    {
        Representative = representative;
        StartUtc = representative.CapturedAtUtc;
        EndUtc = representative.CapturedAtUtc;
        Entries.Add(representative);
    }

    public SnapshotEntry Representative { get; }
    public DateTimeOffset StartUtc { get; private set; }
    public DateTimeOffset EndUtc { get; private set; }
    public List<SnapshotEntry> Entries { get; } = new();
    public int FrameCount => Entries.Count;

    public void Add(SnapshotEntry entry)
    {
        Entries.Add(entry);
        if (entry.CapturedAtUtc < StartUtc)
        {
            StartUtc = entry.CapturedAtUtc;
        }

        if (entry.CapturedAtUtc > EndUtc)
        {
            EndUtc = entry.CapturedAtUtc;
        }
    }
}
