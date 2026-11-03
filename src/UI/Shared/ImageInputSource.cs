using System.Collections.ObjectModel;
using System.Collections.Specialized;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.Plugin.Infrastructure.ImageSources;

namespace SunEyeVision.UI.Shared;

/// <summary>One transient edit request; selection intent is not persisted as configuration.</summary>
public sealed record ImageSourceEditProposal(ImageSourcePolicy Configuration,bool SelectStart);
/// <summary>Formal acceptance and the continued validity of this projection are distinct facts.</summary>
public readonly record struct ImageSourceEditReceipt(bool FormalAccepted,long? ProjectionVersion,string? ExecutionError = null)
{ public bool RuntimeReady => ExecutionError is null; }
public sealed class ImageSourceSelectionEventArgs(long version,ImageInfo image,int index):EventArgs
{
    public long Version {get;}=version;
    public ImageInfo Image {get;}=image;
    public int Index {get;}=index;
}

/// <summary>One source's display projection. Formal edits are accepted before any live view mutation.</summary>
public sealed class ImageInputSource : SunEyeVision.Plugin.SDK.Models.ObservableObject, INotifyCollectionChanged, IDisposable
{
    private sealed class Entries : ObservableCollection<ImageInfo>
    {
        internal void Install(IReadOnlyList<ImageInfo> rows) { Items.Clear(); foreach (var row in rows) Items.Add(row); }
        internal void Publish() { OnCollectionChanged(new(NotifyCollectionChangedAction.Reset)); OnPropertyChanged(new(nameof(Count))); }
    }
    private readonly Entries _entries = new();
    private readonly Dictionary<string,int> _indices = new(StringComparer.Ordinal);
    private int _currentIndex = -1, _configuredIndex = -1;
    private long _displayGeneration;
    private ImageRunMode _runMode;
    private bool _autoSwitch = true, _readOnly, _committing, _requiresWriter, _disposed, _disposeRequested;
    private string? _acceptedRuntimeError;
    private List<string> _runReferences = new();
    public string NodeId { get; }
    public long EditVersion { get; private set; }
    public string? LastNotificationError { get; private set; }
    public ReadOnlyObservableCollection<ImageInfo> Images { get; }
    /// <summary>Owner-thread acceptance gateway. Throw before acceptance to reject; never write after notification.</summary>
    public Action<ImageSourceEditProposal>? ConfigurationWriter { get; set; }
    internal void RecordAcceptedRuntimeFailure(string message)
    {
        if(!_committing)throw new InvalidOperationException("只能在正式接纳期间记录运行准备失败。");
        _acceptedRuntimeError=string.IsNullOrWhiteSpace(message)?"图像运行游标不可用。":message;
    }
    public event EventHandler<int>? CurrentImageChanged;
    public event EventHandler? ConfigurationEdited;
    public event EventHandler<ImageSourceSelectionEventArgs>? ExplicitSelectionAccepted;
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
        set { if (value != _currentIndex) SelectImage(value); }
    }
    public bool CanSelectImage(int index)=>index>=0&&index<Count&&
        (_runMode!=ImageRunMode.运行选择||_runReferences.Contains(Reference(_entries[index]),StringComparer.Ordinal));
    public ImageSourceEditReceipt SelectImage(int index)
    {
        if(index>=0&&!CanSelectImage(index))throw new InvalidOperationException("运行选择模式只能选择已勾选的图像。");
        return Commit(_entries.ToArray(),index,_runMode,_autoSwitch,_runReferences,selectStart:true);
    }
    public ImageRunMode RunMode
    {
        get => _runMode;
        set { if (value != _runMode) Commit(_entries.ToArray(),_configuredIndex,value,_autoSwitch,_runReferences); }
    }
    public bool AutoSwitchEnabled
    {
        get => _autoSwitch;
        set { if (value != _autoSwitch) Commit(_entries.ToArray(),_configuredIndex,_runMode,value,_runReferences); }
    }
    private static string Reference(ImageInfo image) => image.SourceReference ?? image.FilePath;
    private ImageSourcePolicy Configuration(IReadOnlyList<ImageInfo> rows,int index,ImageRunMode mode,bool auto,IEnumerable<string> selected)
        => new() { NodeId=NodeId,FilePaths=rows.Select(Reference).ToList(),CurrentIndex=index,
            RunMode=(int)mode,AutoSwitchEnabled=auto,ForRunPaths=selected.ToList() };
    public ImageSourcePolicy CapturePolicy() => Configuration(_entries,_configuredIndex,_runMode,_autoSwitch,_runReferences);
    private int RemapDisplay(IReadOnlyList<ImageInfo> rows,int fallback)
    {
        if(CurrentImage is not {} displayed)return fallback;
        for(var i=0;i<rows.Count;i++)if(ReferenceEquals(rows[i],displayed))return i;
        var reference=Reference(displayed);
        var occurrence=_entries.Take(_currentIndex).Count(image=>Reference(image)==reference);
        for(var i=0;i<rows.Count;i++)if(Reference(rows[i])==reference&&occurrence--==0)return i;
        return fallback;
    }
    private ImageSourceEditReceipt Commit(ImageInfo[] rows,int index,ImageRunMode mode,bool auto,IEnumerable<string> selected,bool selectStart=false,int? displayOverride=null)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if (_committing) throw new InvalidOperationException("图像源编辑不能重入。");
        if (_readOnly) throw new InvalidOperationException("图像输入源已锁定。");
        if (_requiresWriter && ConfigurationWriter is null) throw new InvalidOperationException("图像源尚未连接当前方案的正式编辑会话。");
        if (index < -1 || index >= rows.Length || !Enum.IsDefined(mode) || rows.Any(row=>row is null||string.IsNullOrWhiteSpace(row.Id)) || rows.Select(row=>row.Id).Distinct(StringComparer.Ordinal).Count()!=rows.Length)
            throw new InvalidOperationException("图像源条目身份、选择或模式无效。");
        var configuration=Configuration(rows,index,mode,auto,selected);
        var displayIndex=displayOverride??(selectStart?index:RemapDisplay(rows,index));
        var version=EditVersion;
        var writer=ConfigurationWriter;
        var formalAccepted=_requiresWriter && writer is not null;
        if (_requiresWriter && writer is null) throw new InvalidOperationException("图像源所属正式编辑会话已失效。");
        _=checked(version+1);
        ImageSourceSelectionEventArgs? acceptedSelection=null;
        _acceptedRuntimeError=null;
        _committing=true;
        try
        {
            // The writer may reject. Nothing visible or owned has changed yet.
            writer?.Invoke(new(Configuration(rows,index,mode,auto,configuration.ForRunPaths),selectStart));
            // An accepted publication may synchronously replace this projection. Do not revive it.
            if (_disposeRequested || version!=EditVersion) return new(formalAccepted,null,_acceptedRuntimeError);
            Install(rows,configuration,true,displayIndex);
            if(selectStart&&EditVersion==version+1&&index>=0&&index<Count&&ReferenceEquals(CurrentImage,rows[index]))
                acceptedSelection=new(EditVersion,rows[index],index);
        }
        finally
        {
            _committing=false;
            if(_disposeRequested)
            {
                try{Dispose();}catch(Exception error){LastNotificationError ??= error.Message;}
            }
        }
        var runtimeError=_acceptedRuntimeError;
        if(acceptedSelection is not null)
            foreach(var callback in ExplicitSelectionAccepted?.GetInvocationList()??Array.Empty<Delegate>())
            {
                if(EditVersion!=acceptedSelection.Version||!ReferenceEquals(CurrentImage,acceptedSelection.Image))break;
                Notify(()=>((EventHandler<ImageSourceSelectionEventArgs>)callback)(this,acceptedSelection));
            }
        // Notifications may replace the projection or select a different image reentrantly.
        // The formal edit was accepted, but that older projection must not start a run.
        var current=EditVersion==version+1 && (!selectStart || index<0 ||
            (index<Count && CurrentIndex==index && ReferenceEquals(CurrentImage,rows[index])));
        return new(formalAccepted,current?EditVersion:null,runtimeError);
    }
    private void Notify(Action action)
    { try { action(); } catch(Exception error) { LastNotificationError ??= error.Message; } }
    private void Install(ImageInfo[] rows,ImageSourcePolicy config,bool edited,int displayIndex)
    {
        var nextVersion=checked(EditVersion+1);
        var structureChanged=rows.Length!=_entries.Count||rows.Where((row,index)=>!ReferenceEquals(row,_entries[index])).Any();
        var removed=_entries.Where(image=>!rows.Contains(image)).ToArray();
        foreach(var image in removed) image.RequestRunSelection=null;
        ++_displayGeneration; _configuredIndex=config.CurrentIndex; _currentIndex=displayIndex;
        _runMode=(ImageRunMode)config.RunMode; _autoSwitch=config.AutoSwitchEnabled;
        _runReferences=new(config.ForRunPaths); _indices.Clear();
        for(var i=0;i<rows.Length;i++)
        {
            var image=rows[i]; _indices.Add(image.Id,i);
            image.SetRunSelectionRaw(_runReferences.Contains(Reference(image),StringComparer.Ordinal));
            image.RequestRunSelection=value=>SetRunSelection(image,value);
        }
        if(structureChanged)_entries.Install(rows);
        EditVersion=nextVersion; LastNotificationError=null;
        // All state is installed before any fallible consumer sees it.
        if(structureChanged)Notify(_entries.Publish);
        bool Current()=>!_disposed&&!_disposeRequested&&EditVersion==nextVersion;
        foreach(var row in rows) { if(!Current())break; Notify(row.NotifyRunSelection); }
        foreach(var name in new[]{nameof(Count),nameof(HasImages),nameof(CurrentIndex),nameof(CurrentImage),nameof(RunMode),nameof(AutoSwitchEnabled),nameof(GetSelectedImages)})
        { if(!Current())break; Notify(()=>OnPropertyChanged(name)); }
        foreach(var callback in CurrentImageChanged?.GetInvocationList()??Array.Empty<Delegate>())
        { if(!Current())break; Notify(()=>((EventHandler<int>)callback)(this,_currentIndex)); }
        if(edited) foreach(var callback in ConfigurationEdited?.GetInvocationList()??Array.Empty<Delegate>())
        { if(!Current())break; Notify(()=>((EventHandler)callback)(this,EventArgs.Empty)); }
        foreach(var image in removed)
        {
            if(_entries.Contains(image))continue;
            Notify(image.CancelThumbnail); Notify(image.ReleaseFullImage);
            Notify(()=>LocalImageAssetCache.InvalidateImageCache(image.FilePath));
        }
    }
    private void SetRunSelection(ImageInfo image,bool selected)
    {
        if(!_indices.TryGetValue(image.Id,out var index)||!ReferenceEquals(_entries[index],image)) throw new InvalidOperationException("图像条目不再属于当前源。");
        var references=new List<string>(_runReferences);var reference=Reference(image);
        references.RemoveAll(item=>item==reference);if(selected)references.Add(reference);
        Commit(_entries.ToArray(),_configuredIndex,_runMode,_autoSwitch,references);
    }
    /// <summary>Accept a complete checkbox selection as one formal edit.</summary>
    public void SetRunSelection(IEnumerable<string> ids)
    {
        ArgumentNullException.ThrowIfNull(ids);
        var selected=ids.ToHashSet(StringComparer.Ordinal);
        if(selected.Any(id=>!_indices.ContainsKey(id)))
            throw new InvalidOperationException("运行勾选包含不属于当前源的图像条目。");
        var references=_entries.Where(image=>selected.Contains(image.Id)).Select(Reference).Distinct(StringComparer.Ordinal).ToArray();
        if(_runReferences.ToHashSet(StringComparer.Ordinal).SetEquals(references))return;
        Commit(_entries.ToArray(),_configuredIndex,_runMode,_autoSwitch,references);
    }
    public void AddImage(ImageInfo image)=>AddImages(new[]{image});
    public ImageSourceEditReceipt AddImages(IEnumerable<ImageInfo> images,bool selectFirstAdded=false)
    {
        var additions=images.ToArray();if(additions.Length==0)return new(false,null);
        return Commit(_entries.Concat(additions).ToArray(),selectFirstAdded?Count:_configuredIndex,_runMode,_autoSwitch,
            _runReferences.Concat(additions.Where(image=>image.IsForRun).Select(Reference)).Distinct(StringComparer.Ordinal),selectFirstAdded);
    }
    public void RemoveImage(ImageInfo image)
    {
        var index=_entries.IndexOf(image);if(index<0)return;
        var rows=_entries.Where(row=>!ReferenceEquals(row,image)).ToArray();
        var current=_configuredIndex==index?-1:_configuredIndex>index?_configuredIndex-1:_configuredIndex;
        current=Math.Min(current,rows.Length-1);
        var selected=_runReferences.Where(reference=>reference!=Reference(image)||rows.Any(row=>Reference(row)==reference));
        Commit(rows,current,_runMode,_autoSwitch,selected,displayOverride:ReferenceEquals(CurrentImage,image)?-1:null);
    }
    public void ClearImages()=>Commit(Array.Empty<ImageInfo>(),-1,_runMode,_autoSwitch,Array.Empty<string>());
    public void Reset()=>Commit(Array.Empty<ImageInfo>(),-1,ImageRunMode.运行全部,true,Array.Empty<string>());
    public IEnumerable<ImageInfo> GetSelectedImages()=>_runMode==ImageRunMode.运行全部?_entries:_entries.Where(image=>image.IsForRun);
    public string[] GetFilePaths()=>_entries.Select(image=>image.FilePath).ToArray();
    public int PrepareForDisplay()=>Count;
    public bool TryPresentRuntimeCursor(long expectedEditVersion,string entryId,string reference,string address)
    {
        if(_disposed||_committing||EditVersion!=expectedEditVersion||!_indices.TryGetValue(entryId,out var index))return false;
        var image=_entries[index];if(Reference(image)!=reference||image.FilePath!=address)return false;
        var generation=++_displayGeneration;
        bool Current()=>_displayGeneration==generation&&EditVersion==expectedEditVersion&&CurrentIndex==index&&ReferenceEquals(CurrentImage,image);
        _currentIndex=index;LastNotificationError=null;
        Notify(()=>OnPropertyChanged(nameof(CurrentIndex)));if(!Current())return false;
        Notify(()=>OnPropertyChanged(nameof(CurrentImage)));if(!Current())return false;
        foreach(var callback in CurrentImageChanged?.GetInvocationList()??Array.Empty<Delegate>())
        {Notify(()=>((EventHandler<int>)callback)(this,index));if(!Current())return false;}
        return Current();
    }
    public void LoadFromPolicy(ImageSourcePolicy config,IResourcePathResolver resourcePaths)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        ArgumentNullException.ThrowIfNull(config);
        if(_committing)throw new InvalidOperationException("图像源投影不能在编辑接纳中重入加载。");
        var resolver=resourcePaths ?? throw new ArgumentNullException(nameof(resourcePaths));
        var rows=new List<ImageInfo>();
        // Matching by occurrence preserves identities within the projection without confusing duplicate references.
        var previous=_entries.GroupBy(Reference).ToDictionary(group=>group.Key,group=>new Queue<ImageInfo>(group));
        foreach(var reference in config.FilePaths)
        {
            string address;string? error=null;
            try
            {
                address=resolver.Resolve("local.paths",reference,ResourcePurpose.FileRead).Address;
            }
            catch(Exception ex){address=string.Empty;error=ex.Message;}
            var image=previous.TryGetValue(reference,out var candidates)&&candidates.Count>0?candidates.Dequeue():null;
            if(image is null||image.FilePath!=address)image=new(){SourceReference=reference,FilePath=address,Name=System.IO.Path.GetFileName(reference),ResourceError=error};
            rows.Add(image);
        }
        var displayIndex=config.CurrentIndex!=_configuredIndex?config.CurrentIndex:RemapDisplay(rows,config.CurrentIndex);
        Install(rows.ToArray(),config,false,displayIndex);
    }
    public void RemoveMissingImages()
    {
        var rows=_entries.Where(image=>System.IO.File.Exists(image.FilePath)).ToArray();
        if(rows.Length==Count)return;
        var current=_configuredIndex>=0?_entries[_configuredIndex]:null;var index=current is null?-1:Array.IndexOf(rows,current);
        Commit(rows,index,_runMode,_autoSwitch,_runReferences.Where(reference=>rows.Any(row=>Reference(row)==reference)));
    }
    /// <summary>Release only this projection; it never changes the document configuration.</summary>
    public void Dispose()
    {
        if(_disposed)return;
        if(_committing){_disposeRequested=true;_requiresWriter=true;ConfigurationWriter=null;return;}
        _disposed=true; ++_displayGeneration; _requiresWriter=true; ConfigurationWriter=null;
        var rows=_entries.ToArray(); _entries.Install(Array.Empty<ImageInfo>()); _indices.Clear(); _runReferences.Clear();
        _currentIndex=_configuredIndex=-1; _readOnly=true;
        var errors=new List<Exception>();
        foreach(var image in rows)
        {
            image.RequestRunSelection=null;
            try{image.CancelThumbnail();}catch(Exception error){errors.Add(error);}
            try{image.ReleaseFullImage();}catch(Exception error){errors.Add(error);}
        }
        CurrentImageChanged=null; ConfigurationEdited=null; ExplicitSelectionAccepted=null;
        if(errors.Count!=0)throw new AggregateException("图像源投影释放失败。",errors);
    }
    public void ReplaceImage(ImageInfo image,string path)
    {
        var index=_entries.IndexOf(image);if(index<0)return;
        var rows=_entries.ToArray();rows[index]=new(){FilePath=path,Name=System.IO.Path.GetFileName(path),IsForRun=image.IsForRun};
        var selected=_runReferences.Where(reference=>reference!=Reference(image)||rows.Any(row=>Reference(row)==reference)).ToList();
        if(image.IsForRun&&!selected.Contains(path))selected.Add(path);
        // Replacing the displayed file is an explicit choice of that entry. Other
        // row edits preserve the configured start rather than adopting the run cursor.
        var selectsCurrent=_currentIndex==index;
        Commit(rows,selectsCurrent?index:_configuredIndex,_runMode,_autoSwitch,selected,selectsCurrent);
    }
}
