using System.Collections.ObjectModel;
using System.Collections.Specialized;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Shared;

/// <summary>One source's display projection. Formal edits are accepted before any live view mutation.</summary>
public sealed class ImageInputSource : SunEyeVision.Plugin.SDK.Models.ObservableObject, INotifyCollectionChanged
{
    private sealed class Entries : ObservableCollection<ImageInfo>
    {
        internal void Install(IReadOnlyList<ImageInfo> rows) { Items.Clear(); foreach (var row in rows) Items.Add(row); }
        internal void Publish() { OnCollectionChanged(new(NotifyCollectionChangedAction.Reset)); OnPropertyChanged(new(nameof(Count))); }
    }
    private readonly Entries _entries = new();
    private readonly Dictionary<string,int> _indices = new(StringComparer.Ordinal);
    private int _currentIndex = -1, _configuredIndex = -1;
    private ImageRunMode _runMode;
    private bool _autoSwitch = true, _readOnly, _committing, _requiresWriter;
    private List<string> _runReferences = new();
    public string NodeId { get; }
    public long EditVersion { get; private set; }
    public string? LastNotificationError { get; private set; }
    public ReadOnlyObservableCollection<ImageInfo> Images { get; }
    /// <summary>Owner-thread acceptance gateway. Throw before acceptance to reject; never write after notification.</summary>
    public Action<ImageSourceConfig>? ConfigurationWriter { get; set; }
    public event EventHandler<int>? CurrentImageChanged;
    public event EventHandler? ConfigurationEdited;
    public event NotifyCollectionChangedEventHandler? CollectionChanged
    { add => _entries.CollectionChanged += value; remove => _entries.CollectionChanged -= value; }
    public ImageInputSource(string nodeId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(nodeId); NodeId = nodeId; Images = new(_entries);
    }
    internal void AttachFormalOwner() { _requiresWriter = true; ConfigurationWriter = null; }
    internal void DetachFormalOwner() { _requiresWriter = true; ConfigurationWriter = null; }
    public int Count => _entries.Count;
    public bool HasImages => Count != 0;
    public ImageInfo? CurrentImage => _currentIndex >= 0 && _currentIndex < Count ? _entries[_currentIndex] : null;
    public bool IsReadOnly { get => _readOnly; set => SetProperty(ref _readOnly,value); }
    public int CurrentIndex
    {
        get => _currentIndex;
        set { if (value != _currentIndex) Commit(_entries.ToArray(),value,_runMode,_autoSwitch,_runReferences); }
    }
    public ImageRunMode RunMode
    {
        get => _runMode;
        set { if (value != _runMode) Commit(_entries.ToArray(),_currentIndex,value,_autoSwitch,_runReferences); }
    }
    public bool AutoSwitchEnabled
    {
        get => _autoSwitch;
        set { if (value != _autoSwitch) Commit(_entries.ToArray(),_currentIndex,_runMode,value,_runReferences); }
    }
    private static string Reference(ImageInfo image) => image.SourceReference ?? image.FilePath;
    private ImageSourceConfig Configuration(IReadOnlyList<ImageInfo> rows,int index,ImageRunMode mode,bool auto,IEnumerable<string> selected)
        => new() { NodeId=NodeId,FilePaths=rows.Select(Reference).ToList(),CurrentIndex=index,
            RunMode=(int)mode,AutoSwitchEnabled=auto,ForRunPaths=selected.ToList() };
    public ImageSourceConfig ToConfig() => Configuration(_entries,_configuredIndex,_runMode,_autoSwitch,_runReferences);
    private void Commit(ImageInfo[] rows,int index,ImageRunMode mode,bool auto,IEnumerable<string> selected)
    {
        if (_committing) throw new InvalidOperationException("图像源编辑不能重入。");
        if (_readOnly) throw new InvalidOperationException("图像输入源已锁定。");
        if (_requiresWriter && ConfigurationWriter is null) throw new InvalidOperationException("图像源尚未连接当前方案的正式编辑会话。");
        if (index < -1 || index >= rows.Length || !Enum.IsDefined(mode) || rows.Any(row=>row is null||string.IsNullOrWhiteSpace(row.Id)) || rows.Select(row=>row.Id).Distinct(StringComparer.Ordinal).Count()!=rows.Length)
            throw new InvalidOperationException("图像源条目身份、选择或模式无效。");
        var configuration=Configuration(rows,index,mode,auto,selected);
        var version=EditVersion;
        _=checked(version+1);
        _committing=true;
        try
        {
            // The writer may reject. Nothing visible or owned has changed yet.
            ConfigurationWriter?.Invoke(Configuration(rows,index,mode,auto,configuration.ForRunPaths));
            // An accepted publication may synchronously replace this projection. Do not revive it.
            if (version!=EditVersion) return;
            Install(rows,configuration,true);
        }
        finally { _committing=false; }
    }
    private void Notify(Action action)
    { try { action(); } catch(Exception error) { LastNotificationError ??= error.Message; } }
    private void Install(ImageInfo[] rows,ImageSourceConfig config,bool edited)
    {
        var nextVersion=checked(EditVersion+1);
        var removed=_entries.Where(image=>!rows.Contains(image)).ToArray();
        foreach(var image in removed) image.RequestRunSelection=null;
        _configuredIndex=config.CurrentIndex; _currentIndex=config.CurrentIndex;
        _runMode=(ImageRunMode)config.RunMode; _autoSwitch=config.AutoSwitchEnabled;
        _runReferences=new(config.ForRunPaths); _indices.Clear();
        for(var i=0;i<rows.Length;i++)
        {
            var image=rows[i]; _indices.Add(image.Id,i);
            image.SetRunSelectionRaw(_runReferences.Contains(Reference(image),StringComparer.Ordinal));
            image.RequestRunSelection=value=>SetRunSelection(image,value);
        }
        _entries.Install(rows); EditVersion=nextVersion; LastNotificationError=null;
        // All state is installed before any fallible consumer sees it.
        Notify(_entries.Publish);
        foreach(var row in rows) Notify(row.NotifyRunSelection);
        foreach(var name in new[]{nameof(Count),nameof(HasImages),nameof(CurrentIndex),nameof(CurrentImage),nameof(RunMode),nameof(AutoSwitchEnabled),nameof(GetSelectedImages)})
            Notify(()=>OnPropertyChanged(name));
        foreach(var callback in CurrentImageChanged?.GetInvocationList()??Array.Empty<Delegate>()) Notify(()=>((EventHandler<int>)callback)(this,_currentIndex));
        if(edited) foreach(var callback in ConfigurationEdited?.GetInvocationList()??Array.Empty<Delegate>()) Notify(()=>((EventHandler)callback)(this,EventArgs.Empty));
        foreach(var image in removed)
        {
            Notify(image.CancelThumbnail); Notify(image.ReleaseFullImage);
            Notify(()=>LocalImageAssetCache.InvalidateImageCache(image.FilePath));
        }
    }
    private void SetRunSelection(ImageInfo image,bool selected)
    {
        if(!_indices.TryGetValue(image.Id,out var index)||!ReferenceEquals(_entries[index],image)) throw new InvalidOperationException("图像条目不再属于当前源。");
        var references=new List<string>(_runReferences);var reference=Reference(image);
        references.RemoveAll(item=>item==reference);if(selected)references.Add(reference);
        Commit(_entries.ToArray(),_currentIndex,_runMode,_autoSwitch,references);
    }
    public void AddImage(ImageInfo image)=>AddImages(new[]{image});
    public void AddImages(IEnumerable<ImageInfo> images,bool selectFirstAdded=false)
    {
        var additions=images.ToArray();if(additions.Length==0)return;
        Commit(_entries.Concat(additions).ToArray(),selectFirstAdded?Count:_currentIndex,_runMode,_autoSwitch,
            _runReferences.Concat(additions.Where(image=>image.IsForRun).Select(Reference)).Distinct(StringComparer.Ordinal));
    }
    public void RemoveImage(ImageInfo image)
    {
        var index=_entries.IndexOf(image);if(index<0)return;
        var rows=_entries.Where(row=>!ReferenceEquals(row,image)).ToArray();
        var current=_currentIndex>index?_currentIndex-1:_currentIndex;
        current=Math.Min(current,rows.Length-1);
        var selected=_runReferences.Where(reference=>reference!=Reference(image)||rows.Any(row=>Reference(row)==reference));
        Commit(rows,current,_runMode,_autoSwitch,selected);
    }
    public void ClearImages()=>Commit(Array.Empty<ImageInfo>(),-1,_runMode,_autoSwitch,Array.Empty<string>());
    public void Reset()=>Commit(Array.Empty<ImageInfo>(),-1,ImageRunMode.运行全部,true,Array.Empty<string>());
    public IEnumerable<ImageInfo> GetSelectedImages()=>_runMode==ImageRunMode.运行全部?_entries:_entries.Where(image=>image.IsForRun);
    public string[] GetFilePaths()=>_entries.Select(image=>image.FilePath).ToArray();
    public int PrepareForDisplay()=>Count;
    public bool TryPresentRuntimeCursor(long expectedEditVersion,string entryId,string reference,string address)
    {
        if(_committing||EditVersion!=expectedEditVersion||!_indices.TryGetValue(entryId,out var index))return false;
        var image=_entries[index];if(Reference(image)!=reference||image.FilePath!=address)return false;
        _currentIndex=index;LastNotificationError=null;
        Notify(()=>OnPropertyChanged(nameof(CurrentIndex)));Notify(()=>OnPropertyChanged(nameof(CurrentImage)));
        foreach(var callback in CurrentImageChanged?.GetInvocationList()??Array.Empty<Delegate>())Notify(()=>((EventHandler<int>)callback)(this,index));
        return true;
    }
    public void LoadFromConfig(ImageSourceConfig config,IResourcePathResolver? resourcePaths=null)
    {
        ArgumentNullException.ThrowIfNull(config);
        if(_committing)throw new InvalidOperationException("图像源投影不能在编辑接纳中重入加载。");
        var resolver=resourcePaths;
        var rows=new List<ImageInfo>();
        // Matching by occurrence preserves identities within the projection without confusing duplicate references.
        var previous=_entries.GroupBy(Reference).ToDictionary(group=>group.Key,group=>new Queue<ImageInfo>(group));
        foreach(var reference in config.FilePaths)
        {
            string address;string? error=null;
            try
            {
                resolver??=SunEyeVision.Workflow.Persistence.SolutionResourceResolver.For(
                    SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters.ServiceInitializer.SolutionManager.CurrentSolution);
                address=resolver.Resolve("ImageSourceConfig.FilePaths",reference,ResourcePurpose.FileRead).Address;
            }
            catch(Exception ex){address=string.Empty;error=ex.Message;}
            var image=previous.TryGetValue(reference,out var candidates)&&candidates.Count>0?candidates.Dequeue():null;
            if(image is null||image.FilePath!=address)image=new(){SourceReference=reference,FilePath=address,Name=System.IO.Path.GetFileName(reference),ResourceError=error};
            rows.Add(image);
        }
        Install(rows.ToArray(),config,false);
    }
    public void RemoveMissingImages()
    {
        var rows=_entries.Where(image=>System.IO.File.Exists(image.FilePath)).ToArray();
        if(rows.Length==Count)return;
        var current=CurrentImage;var index=current is null?-1:Array.IndexOf(rows,current);
        Commit(rows,index,_runMode,_autoSwitch,_runReferences.Where(reference=>rows.Any(row=>Reference(row)==reference)));
    }
    public void ReplaceImage(ImageInfo image,string path)
    {
        var index=_entries.IndexOf(image);if(index<0)return;
        var rows=_entries.ToArray();rows[index]=new(){FilePath=path,Name=System.IO.Path.GetFileName(path),IsForRun=image.IsForRun};
        var selected=_runReferences.Where(reference=>reference!=Reference(image)||rows.Any(row=>Reference(row)==reference)).ToList();
        if(image.IsForRun&&!selected.Contains(path))selected.Add(path);
        Commit(rows,_currentIndex,_runMode,_autoSwitch,selected);
    }
}
