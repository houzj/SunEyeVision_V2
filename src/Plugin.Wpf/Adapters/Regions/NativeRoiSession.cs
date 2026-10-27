using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>Host-owned private edit root, monotonic revision and exact frozen physical frame.</summary>
public sealed record RoiEditStamp(Guid ScopeId,long Revision,ImageFrameAddress Frame);
public sealed record RoiEditSnapshot(RoiEditStamp Stamp,IReadOnlyList<RoiEditItem> Items);
/// <summary>The host atomically compares the expected stamp and returns the installed snapshot.
/// It must return acceptance before publishing fallible observer notifications.</summary>
public sealed record RoiEditAcceptance(bool Accepted,RoiEditSnapshot Snapshot,string? Error=null);

/// <summary>One borrowed edit scope for all original ROI parameter stacks and image handles.</summary>
public sealed class NativeRoiSession : INotifyPropertyChanged,IDisposable
{
    private readonly Func<RoiEditSnapshot> _read;
    private readonly Func<RoiEditStamp,RoiGeometryChange,RoiEditAcceptance> _accept;
    private readonly Func<bool> _isCurrent;
    private Dictionary<(string,Guid),NativeRoiProjection> _items=new();
    private readonly ProjectionRows<NativeRoiProjection> _rows=new();
    private bool _disposed,_invalid,_committing,_selecting,_refreshing,_reading;
    private Func<bool>? _editorCommit;
    private NativeRoiProjection? _selected;
    private RoiEditStamp? _stamp;
    public RoiEditStamp Stamp=>_stamp??throw new InvalidOperationException("区域范围尚未装载。");
    public ImageFrameAddress Frame {get;}
    public int ImageWidth {get;}
    public int ImageHeight {get;}
    public IReadOnlyImage Image {get;}
    public IReadOnlyList<NativeRoiProjection> Items=>_rows;
    public NativeRoiProjection? CurROIData=>_selected;
    public bool IsHaveData=>_selected is not null&&Owns(_selected);
    public bool IsCurrent=>!_disposed&&!_invalid&&_isCurrent();
    public bool BoxEnable_ModuleEnableA=>IsHaveData&&_selected is NativeBoxRoiProjection;
    public bool SectorEnable_ModuleEnableA=>IsHaveData&&_selected is NativeSectorRoiProjection;
    public bool BoxA {get=>BoxEnable_ModuleEnableA;set{}}
    public bool SectorA {get=>SectorEnable_ModuleEnableA;set{}}
    public bool SectorRingEnable_ModuleEnableA=>IsHaveData&&_selected is NativeRingRoiProjection;
    public bool SectorRingA {get=>SectorRingEnable_ModuleEnableA;set{}}
    public bool PolygonEnable_ModuleEnableA=>IsHaveData&&_selected is NativePolygonRoiProjection;
    public bool PolygonA {get=>PolygonEnable_ModuleEnableA;set{}}
    public bool IsAvoid {get;}
    public bool Is3DROI=>false;
    public string ROISelectionA=>"Box SectorRing Sector Polygon";
    public string DisplayNameA=>"ROI形状";
    public string DescriptionA=>DisplayNameA;
    public string Error {get;private set;}="";
    public ICommand ROIValueChangedCommand {get;}
    public ICommand ValueChangedCommand {get;}
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;
    public event Action? DrawRequested;
    public NativeRoiSession(IReadOnlyImage image,Func<RoiEditSnapshot> read,
        Func<RoiEditStamp,RoiGeometryChange,RoiEditAcceptance> accept,Func<bool> isCurrent,bool isAvoid=false)
    {
        ResourceContractRules.ValidateImage(image);
        IsAvoid=isAvoid;Image=image;Frame=image.Frame;ImageWidth=image.Width;ImageHeight=image.Height;
        _read=read;_accept=accept;_isCurrent=isCurrent;
        ROIValueChangedCommand=new EditCommand(_=>{if(_selected is {} selected)TryCommit(selected);});
        ValueChangedCommand=new EditCommand(_=>{if(IsCurrent)DrawRequested?.Invoke();});
        Refresh();
    }
    private RoiEditSnapshot Capture(RoiEditSnapshot snapshot)
    {
        ArgumentNullException.ThrowIfNull(snapshot);var stamp=snapshot.Stamp;ArgumentNullException.ThrowIfNull(stamp);
        if(stamp.ScopeId==Guid.Empty||stamp.Revision<0||stamp.Frame!=Frame||
            (_stamp is {} old&&(old.ScopeId!=stamp.ScopeId||stamp.Revision<old.Revision)))
        {
            _invalid=true;
            Error="区域编辑范围、版本或物理帧已失效。";
            NativeRoiCallbacks.Run([CancelPending,Notify]);
            throw new InvalidOperationException(Error);
        }
        var rows=snapshot.Items.ToArray();var keys=new HashSet<(string,Guid)>();
        foreach(var row in rows)
        {
            if(string.IsNullOrWhiteSpace(row.Group)||row.Id==Guid.Empty||!keys.Add((row.Group,row.Id)))
                throw new ArgumentException("ROI组与稳定ID必须非空且唯一。");
            RoiGeometryEditing.Validate(row.Geometry);
            if(row.Geometry.Get<string>("kind") is not ("rectangle" or "sector" or "annulus" or "polygon"))
                throw new NotSupportedException("当前原组合接纳矩形、完整圆环、扇形及多边形，其他形状需其原参数栈。");
            if(row.Geometry.Get<string>("kind")=="annulus"&&Math.Abs(Math.Abs(row.Geometry.Get<double>("end-radians")-row.Geometry.Get<double>("start-radians"))-2*Math.PI)>1e-9)
                throw new NotSupportedException("原SectorRing四项参数只接纳完整圆环，部分扇环须其原参数栈。");
        }
        return new(stamp,Array.AsReadOnly(rows));
    }
    public void Refresh()
    {
        if(_refreshing||_committing||_reading)return;
        if(!IsCurrent){NativeRoiCallbacks.Run([CancelPending,Notify]);return;}
        var snapshot=ReadCurrent();if(!IsCurrent)return;
        Install(Capture(snapshot));
    }
    private RoiEditSnapshot ReadCurrent()
    {
        if(_reading)throw new InvalidOperationException("区域快照读取不可重入。");
        _reading=true;try{return _read();}finally{_reading=false;}
    }
    private void Install(RoiEditSnapshot snapshot)
    {
        if(!IsCurrent||_refreshing)return;
        _refreshing=true;
        try
        {
            var changedVersion=_stamp!=snapshot.Stamp;
            if(!changedVersion&&(_items.Count!=snapshot.Items.Count||snapshot.Items.Any(row=>
                !_items.TryGetValue((row.Group,row.Id),out var old)||!old.Matches(row))))
                throw new InvalidOperationException("宿主修改区域内容时必须递增编辑版本。");
            if(!changedVersion)return; // A display refresh must not cancel a live gesture or typed raw input.
            var next=new Dictionary<(string,Guid),NativeRoiProjection>();
            foreach(var row in snapshot.Items)
            {
                var key=(row.Group,row.Id);
                var item=_items.TryGetValue(key,out var previous)&&previous.Kind==row.Geometry.Get<string>("kind")
                    ?previous:NativeRoiProjection.Create(this,row);
                next.Add(key,item);
            }
            var notifications=new List<Action>();
            foreach(var old in _items.Values.Where(old=>!next.Values.Contains(old)))old.Detach();
            _items=next;_stamp=snapshot.Stamp;
            foreach(var row in snapshot.Items)
            {
                var item=next[(row.Group,row.Id)];item.Install(row,changedVersion);
                if(changedVersion)notifications.Add(item.Notify);
            }
            if(_selected is not null&&!Owns(_selected))_selected=null;
            var nextRows=snapshot.Items.Select(row=>next[(row.Group,row.Id)]).ToArray();
            if(!_rows.SequenceEqual(nextRows)){_rows.Install(nextRows);notifications.Insert(0,_rows.NotifyReset);}
            notifications.Add(Notify);
            NativeRoiCallbacks.Run(notifications);
        }
        finally{_refreshing=false;}
    }
    internal bool Owns(NativeRoiProjection item)=>IsCurrent&&_items.TryGetValue((item.Group,item.Id),out var current)
        &&ReferenceEquals(current,item)&&item.Enabled&&item.Visible;
    internal IDisposable RegisterEditorCommit(Func<bool> commit)
    {
        if(_editorCommit is not null)throw new InvalidOperationException("一个ROI编辑会话只允许一份参数编辑器。");
        _editorCommit=commit;return new Registration(()=>{if(ReferenceEquals(_editorCommit,commit))_editorCommit=null;});
    }
    internal bool PrepareEditor()=>_editorCommit?.Invoke()??IsCurrent;
    private sealed class Registration(Action release):IDisposable
    {private Action? _release=release;public void Dispose(){var action=_release;_release=null;action?.Invoke();}}
    internal bool CanEdit(NativeRoiProjection item)=>!_refreshing&&!_reading&&!_committing&&Owns(item)&&ReferenceEquals(item,_selected);
    public bool Select(string group,Guid? id)
    {
        if(!IsCurrent||_refreshing||_reading||_selecting||_committing)return false;
        NativeRoiProjection? next=null;
        if(id is {} key&&(!_items.TryGetValue((group,key),out next)||!Owns(next)))return false;
        if(ReferenceEquals(_selected,next))return true;
        _selecting=true;
        try
        {
            if(!PrepareEditor()||!IsCurrent)return false;
            if(id is {} target&&(!_items.TryGetValue((group,target),out next)||!Owns(next)))return false;
            var stamp=Stamp;CancelPending();
            if(!IsCurrent||Stamp!=stamp)return false;
            if(id is {} after&&(!_items.TryGetValue((group,after),out next)||!Owns(next)))return false;
            _selected=next;Error="";Notify();return true;
        }
        finally{_selecting=false;}
    }
    public void CancelPending()=>NativeRoiCallbacks.Run(_rows.ToArray().Select(item=>(Action)item.Cancel));
    public bool TryCommit(NativeRoiProjection item)
    {
        if(_committing||!CanEdit(item))return false;
        if(!item.HasPending)return true;
        _committing=true;
        try
        {
            var read=ReadCurrent();if(!IsCurrent)return false;
            var current=Capture(read);if(!IsCurrent)return false;
            if(item.PendingStamp!=current.Stamp)
            {Error="区域根已改变，旧编辑已取消。";InstallAfterCommit(current);return false;}
            var row=current.Items.SingleOrDefault(row=>row.Group==item.Group&&row.Id==item.Id);
            if(row is null||!row.Enabled||!row.Visible||!item.Matches(row))
                throw new InvalidOperationException("区域内容已变化而版本未更新。");
            RecordValue candidate;
            try{candidate=item.Prepare();}
            catch(Exception error)when(error is ArgumentException or OverflowException)
            {Error=error.Message;NativeRoiCallbacks.Run([item.Cancel,Notify]);return false;}
            if(!Owns(item)||!ReferenceEquals(item,_selected))return false;
            if(item.MatchesGeometry(row.Geometry,candidate))
            {Error="";NativeRoiCallbacks.Run([item.Cancel,Notify]);return true;}
            // Deliberately outside a rejection catch: host/observer exceptions are not false acceptance.
            var result=_accept(current.Stamp,new(item.Group,item.Id,candidate));
            if(!IsCurrent)return result.Accepted;
            var installed=Capture(result.Snapshot);
            if(result.Accepted&&installed.Stamp.Revision<=current.Stamp.Revision)
                throw new InvalidOperationException("宿主接纳必须递增区域版本。");
            if(result.Accepted&&!installed.Items.Any(row=>row.Group==item.Group&&row.Id==item.Id&&item.MatchesGeometry(row.Geometry,candidate)))
                throw new InvalidOperationException("宿主接纳回执没有安装所提交的完整几何。");
            Error=result.Accepted?"":result.Error??"宿主拒绝区域几何候选。";
            var unchanged=installed.Stamp==_stamp;
            NativeRoiCallbacks.Run([()=>{if(!result.Accepted)item.Cancel();},()=>InstallAfterCommit(installed),()=>{if(unchanged)Notify();}]);
            return result.Accepted;
        }
        finally{_committing=false;}
    }
    private void InstallAfterCommit(RoiEditSnapshot snapshot)
    {var committing=_committing;_committing=false;try{Install(snapshot);}finally{_committing=committing;}}
    private void Notify()
    {
        var callbacks=new List<Action>();
        var handlers=PropertyChanged?.GetInvocationList().Cast<PropertyChangedEventHandler>().ToArray()??[];
        foreach(var name in new[]{nameof(CurROIData),nameof(IsHaveData),nameof(BoxEnable_ModuleEnableA),nameof(SectorEnable_ModuleEnableA),nameof(BoxA),nameof(SectorA),nameof(SectorRingEnable_ModuleEnableA),nameof(SectorRingA),nameof(PolygonEnable_ModuleEnableA),nameof(PolygonA),nameof(Error)})
            foreach(var handler in handlers)callbacks.Add(()=>handler(this,new(name)));
        foreach(var handler in Changed?.GetInvocationList().Cast<Action>().ToArray()??[])callbacks.Add(handler);
        NativeRoiCallbacks.Run(callbacks);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        foreach(var item in _rows)item.Detach();_items.Clear();_selected=null;_editorCommit=null;_rows.Install([]);
        try{NativeRoiCallbacks.Run([_rows.NotifyReset,Notify]);}
        finally{PropertyChanged=null;Changed=null;DrawRequested=null;}
    }
    private sealed class EditCommand(Action<object?> action):ICommand
    {
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter)=>action(parameter);
        public event EventHandler? CanExecuteChanged {add{}remove{}}
    }
    private sealed class ProjectionRows<T>:IReadOnlyList<T>,IList,INotifyCollectionChanged,INotifyPropertyChanged where T:class
    {
        private T[] _values=[];
        public int Count=>_values.Length;
        public T this[int index]=>_values[index];
        public event NotifyCollectionChangedEventHandler? CollectionChanged;
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void Install(IEnumerable<T> rows)=>_values=rows.ToArray();
        internal void NotifyReset()
        {
            var callbacks=new List<Action>();
            foreach(var name in new[]{nameof(Count),"Item[]"})
                foreach(var observer in PropertyChanged?.GetInvocationList().Cast<PropertyChangedEventHandler>().ToArray()??[])
                    callbacks.Add(()=>observer(this,new(name)));
            foreach(var observer in CollectionChanged?.GetInvocationList().Cast<NotifyCollectionChangedEventHandler>().ToArray()??[])
                callbacks.Add(()=>observer(this,new(NotifyCollectionChangedAction.Reset)));
            NativeRoiCallbacks.Run(callbacks);
        }
        public IEnumerator<T> GetEnumerator()=>((IEnumerable<T>)_values).GetEnumerator();
        IEnumerator IEnumerable.GetEnumerator()=>GetEnumerator();
        bool IList.IsReadOnly=>true;bool IList.IsFixedSize=>true;bool ICollection.IsSynchronized=>false;object ICollection.SyncRoot=>this;
        object? IList.this[int index]{get=>this[index];set=>throw new NotSupportedException();}
        int IList.Add(object? value)=>throw new NotSupportedException();void IList.Clear()=>throw new NotSupportedException();
        bool IList.Contains(object? value)=>value is T item&&_values.Contains(item);
        int IList.IndexOf(object? value)=>value is T item?Array.IndexOf(_values,item):-1;
        void IList.Insert(int index,object? value)=>throw new NotSupportedException();void IList.Remove(object? value)=>throw new NotSupportedException();
        void IList.RemoveAt(int index)=>throw new NotSupportedException();void ICollection.CopyTo(Array array,int index)=>_values.CopyTo(array,index);
    }
}

internal static class NativeRoiCallbacks
{
    internal static void Run(IEnumerable<Action> callbacks)
    {
        List<Exception>? errors=null;
        foreach(var callback in callbacks)try{callback();}catch(Exception error){(errors??=[]).Add(error);}
        if(errors?.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors?.Count>1)throw new AggregateException(errors);
    }
}
