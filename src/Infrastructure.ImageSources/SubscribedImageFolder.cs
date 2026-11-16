using System.Runtime.InteropServices;

namespace SunEyeVision.Infrastructure.ImageSources;

/// <summary>One local folder and its change stamp. The cursor owns each accepted file list.</summary>
internal sealed class SubscribedImageFolder : IDisposable
{
    private readonly string _address;
    private readonly FileSystemWatcher? _watcher;
    private readonly Action? _requestRun;
    private long _revision = 1;
    internal long Revision => Volatile.Read(ref _revision);

    internal SubscribedImageFolder(string address, Action? requestRun)
    {
        _address = address;
        _requestRun = requestRun;
        var watcher = new FileSystemWatcher(address)
        {
            NotifyFilter = NotifyFilters.FileName | NotifyFilters.LastWrite | NotifyFilters.Size
        };
        try
        {
            watcher.Created += ListChanged;
            watcher.Deleted += ListChanged;
            watcher.Changed += Changed;
            watcher.Error += OnError;
            watcher.EnableRaisingEvents = true;
            _watcher = watcher;
        }
        catch
        {
            watcher.Dispose();
            throw;
        }
    }

    internal LocalImageSourcePlan Capture(ImageSourcePolicy policy, int previousPosition)
        => CaptureList(_address,policy,previousPosition);

    internal static LocalImageSourcePlan CaptureList(string address,ImageSourcePolicy policy,int previousPosition)
    {
        var entries = Directory.EnumerateFiles(address)
            .Where(path => Path.GetExtension(path) is ".jpg" or ".png" or ".bmp" or ".tif")
            .OrderBy(path => path, Comparer<string>.Create(ComparePath))
            .Select((path, index) => new LocalImageSequenceEntry(path, path, path, index)).ToArray();
        var selected = previousPosition >= 0 && entries.Length > 0
            ? entries[Math.Min(previousPosition, entries.Length - 1)].EntryId : null;
        return new(policy.NodeId, Array.AsReadOnly(entries), selected, policy.AutoSwitchEnabled, policy.StopAtLastImage);
    }

    private static int ComparePath(string left, string right)
    {
        var order = StrCmpLogicalW(left, right);
        return order != 0 ? order : StringComparer.Ordinal.Compare(left, right);
    }

    [DllImport("shlwapi.dll", CharSet = CharSet.Unicode, ExactSpelling = true)]
    private static extern int StrCmpLogicalW(string left, string right);

    private void MarkDirty()
    {
        Interlocked.Increment(ref _revision);
    }
    private void ListChanged(object sender, FileSystemEventArgs args)
    {
        MarkDirty();
        _requestRun?.Invoke();
    }
    private void Changed(object sender, FileSystemEventArgs args) => MarkDirty();
    private void OnError(object sender, ErrorEventArgs args) => MarkDirty();
    public void Dispose() => _watcher?.Dispose();
}
