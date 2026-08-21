using SecureDeviceControl.Dashboard.Cloud;

namespace SecureDeviceControl.Dashboard.Imaging;

public static class SnapshotSessionizer
{
    public static IReadOnlyList<SnapshotSession> GroupSessions(
        IReadOnlyList<(SnapshotEntry Entry, ulong Hash)> orderedFrames,
        int similarityThreshold)
    {
        var sessions = new List<SnapshotSession>();
        if (orderedFrames.Count == 0)
        {
            return sessions;
        }

        var sorted = orderedFrames
            .OrderBy(frame => frame.Entry.CapturedAtUtc)
            .ToList();

        var current = new SnapshotSession(sorted[0].Entry);
        sessions.Add(current);

        for (var i = 1; i < sorted.Count; i++)
        {
            var distance = PerceptualHash.HammingDistance(sorted[i - 1].Hash, sorted[i].Hash);
            if (distance <= similarityThreshold)
            {
                current.Add(sorted[i].Entry);
            }
            else
            {
                current = new SnapshotSession(sorted[i].Entry);
                sessions.Add(current);
            }
        }

        return sessions;
    }
}
