namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    private readonly Dictionary<Guid, string> _activities = new();

    /// <summary>Retain the actual document while a host activity owns drivers/readers.
    /// The host must await its existing stop/disconnect/disposal chain before releasing it.</summary>
    public IDisposable AcquireActivityLease(string activityName)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(activityName);
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            var id = Guid.NewGuid();
            _activities.Add(id, activityName);
            return new ActivityLease(this, id);
        }
    }

    public IReadOnlyList<string> ActiveActivities
    {
        get { lock (_gate) return Array.AsReadOnly(_activities.Values.ToArray()); }
    }

    private sealed class ActivityLease(DocumentSession session, Guid id) : IDisposable
    {
        private DocumentSession? _session = session;
        public void Dispose()
        {
            var current = Interlocked.Exchange(ref _session, null);
            if (current is not null) lock (current._gate) current._activities.Remove(id);
        }
    }
}
