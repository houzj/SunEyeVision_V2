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
public sealed record RoiEditAcceptance(bool Accepted,RoiEditSnapshot Snapshot,RoiEditTarget? Affected=null,string? Error=null);

/// <summary>One borrowed edit scope for all original ROI parameter stacks and image handles.</summary>
public sealed class NativeRoiSession : INotifyPropertyChanged,IDisposable
{
    private readonly Func<RoiEditSnapshot> _read;
    private readonly Func<RoiEditStamp,RoiEditProposal,RoiEditAcceptance> _accept;
    private readonly Func<bool> _isCurrent;
    private readonly Func<string,bool>? _canDraw;
    private Dictionary<(string,Guid),NativeRoiProjection> _items=new();
    private readonly ProjectionRows<NativeRoiProjection> _rows=new();
    private bool _disposed,_invalid,_committing,_selecting,_refreshing,_reading;
    private Func<bool>? _editorCommit;
    private NativeRoiProjection? _selected;
    private RoiEditStamp? _stamp;
    private RoiDrawRequest? _drawRequest;
    private long _drawGeneration,_groupGeneration;
    private bool _requesting;
    public string DrawingGroup {get;private set;}
    public RoiDrawRequest? ActiveDraw=>_drawRequest;
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
    public event Action<RoiDrawRequest>? DrawRequested;
    public event Action<RoiDrawRequest>? DrawCancelled;
    public NativeRoiSession(IReadOnlyImage image,Func<RoiEditSnapshot> read,
        Func<RoiEditStamp,RoiEditProposal,RoiEditAcceptance> accept,Func<bool> isCurrent,string drawingGroup,bool isAvoid=false,
        Func<string,bool>? canDraw=null)
    {
        ResourceContractRules.ValidateImage(image);
        if(string.IsNullOrWhiteSpace(drawingGroup))throw new ArgumentException("绘制组必须由宿主明确提供。",nameof(drawingGroup));
        DrawingGroup=drawingGroup;_canDraw=canDraw;
        IsAvoid=isAvoid;Image=image;Frame=image.Frame;ImageWidth=image.Width;ImageHeight=image.Height;
        _read=read;_accept=accept;_isCurrent=isCurrent;
        ROIValueChangedCommand=new EditCommand(_=>{if(_selected is {} selected)TryCommit(selected);});
        ValueChangedCommand=new EditCommand(value=>
        {
            var kind=value switch
            {
                Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Box=>"rectangle",
                Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.SectorRing=>"annulus",
                Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Sector=>"sector",
                Native.Dependencies.Apps.XmlParser.ParamTab.RoiType.Polygon=>"polygon",
                _=>throw new NotSupportedException("该原绘制类型尚未接入。")
            };
            BeginDraw(kind);
        });
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
            ValidateNativeGeometry(row.Geometry,row.Enabled);
        }
        return new(stamp,Array.AsReadOnly(rows));
    }
    private static void ValidateNativeGeometry(RecordValue geometry,bool editable=true)
    {
        RoiGeometryEditing.Validate(geometry);
        var kind=geometry.Get<string>("kind");
        if(kind is not ("rectangle" or "sector" or "annulus" or "polygon"))
            throw new NotSupportedException("当前原组合接纳矩形、完整圆环、扇形及多边形。");
        if(editable&&kind=="annulus"&&Math.Abs(Math.Abs(geometry.Get<double>("end-radians")-geometry.Get<double>("start-radians"))-2*Math.PI)>1e-9)
            throw new NotSupportedException("原SectorRing四项参数只接纳完整圆环。");
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
    private void Install(RoiEditSnapshot snapshot,RoiEditTarget? selected=null)
    {
        if(!IsCurrent||_refreshing)return;
        _refreshing=true;
        try
        {
            var changedVersion=_stamp!=snapshot.Stamp;
            var cancellation=changedVersion?InvalidateDraw():Array.Empty<Action>();
            if(!changedVersion&&(_items.Count!=snapshot.Items.Count||snapshot.Items.Any(row=>
                !_items.TryGetValue((row.Group,row.Id),out var old)||!old.Matches(row))))
                throw new InvalidOperationException("宿主修改区域内容时必须递增编辑版本。");
            if(!changedVersion)return; // A display refresh must not cancel a live gesture or typed raw input.
            var next=new Dictionary<(string,Guid),NativeRoiProjection>();
            foreach(var row in snapshot.Items)
            {
                var key=(row.Group,row.Id);
                var item=_items.TryGetValue(key,out var previous)&&previous.GetType()==NativeRoiProjection.ProjectionType(row)
                    ?previous:NativeRoiProjection.Create(this,row);
                next.Add(key,item);
            }
            var notifications=new List<Action>(cancellation);
            foreach(var old in _items.Values.Where(old=>!next.Values.Contains(old)))old.Detach();
            _items=next;_stamp=snapshot.Stamp;
            foreach(var row in snapshot.Items)
            {
                var item=next[(row.Group,row.Id)];item.Install(row,changedVersion);
                if(changedVersion)notifications.Add(item.Notify);
            }
            if(_selected is not null&&!Owns(_selected))_selected=null;
            if(selected is not null)_selected=next[(selected.Group,selected.Id)];
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
    public void CancelPending()=>NativeRoiCallbacks.Run(new Action[]{CancelDraw}.Concat(_rows.ToArray().Select(item=>(Action)item.Cancel)));
    public void SetDrawingGroup(string group)
    {
        if(string.IsNullOrWhiteSpace(group))throw new ArgumentException("绘制组必须非空。",nameof(group));
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(group==DrawingGroup)return;
        DrawingGroup=group;++_groupGeneration;CancelDraw();
    }
    private Action[] InvalidateDraw()
    {
        var request=_drawRequest;_drawRequest=null;++_drawGeneration;
        return request is null?[]:(DrawCancelled?.GetInvocationList().Cast<Action<RoiDrawRequest>>()??[]).Select(handler=>(Action)(()=>handler(request))).ToArray();
    }
    public void CancelDraw()=>NativeRoiCallbacks.Run(InvalidateDraw());
    private bool CanDrawGroup(string group)
    {
        if(!IsCurrent)return false;
        var stamp=_stamp;var groups=_groupGeneration;var draws=_drawGeneration;
        var allowed=_canDraw?.Invoke(group)!=false;
        return allowed&&IsCurrent&&stamp==_stamp&&groups==_groupGeneration&&draws==_drawGeneration&&DrawingGroup==group;
    }
    public RoiDrawRequest? BeginDraw(string kind)
    {
        if(!IsCurrent||_committing||_refreshing||_reading||_selecting||_requesting)return null;
        if(kind is not ("rectangle" or "annulus" or "sector" or "polygon"))throw new NotSupportedException("需要该形状的原绘制入口。");
        var handlers=DrawRequested?.GetInvocationList().Cast<Action<RoiDrawRequest>>().ToArray()??[];
        if(handlers.Length!=1){Error="绘制需要一份明确的视口接收器。";Notify();return null;}
        _requesting=true;var group=DrawingGroup;var groupGeneration=_groupGeneration;
        try
        {
            if(!CanDrawGroup(group)||groupGeneration!=_groupGeneration)return null;
            if(!PrepareEditor()||!IsCurrent||groupGeneration!=_groupGeneration||!CanDrawGroup(group))return null;
            var stamp=Stamp;var generation=_drawGeneration+1;CancelPending();
            if(!IsCurrent||Stamp!=stamp||generation!=_drawGeneration||groupGeneration!=_groupGeneration||!CanDrawGroup(group))return null;
            var snapshot=Capture(ReadCurrent());
            if(!IsCurrent||generation!=_drawGeneration||groupGeneration!=_groupGeneration||!CanDrawGroup(group))return null;
            if(snapshot.Stamp!=stamp){Error="区域根已改变，请在当前范围重新绘制。";Install(snapshot);return null;}
            handlers=DrawRequested?.GetInvocationList().Cast<Action<RoiDrawRequest>>().ToArray()??[];
            if(handlers.Length!=1){Error="绘制需要一份明确的视口接收器。";Notify();return null;}
            var request=new RoiDrawRequest(stamp,group,kind);_drawRequest=request;Error="";
            try{handlers[0](request);}
            catch(Exception error)
            {
                NativeRoiCallbacks.Run(new Action[]{()=>System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(error).Throw(),()=>{if(ReferenceEquals(_drawRequest,request))CancelDraw();}});
                throw;
            }
            return IsCurrent&&ReferenceEquals(_drawRequest,request)?request:null;
        }
        finally{_requesting=false;}
    }
    internal void DrawStatus(RoiDrawRequest request,string message,bool reject)
    {
        if(!IsCurrent||!ReferenceEquals(_drawRequest,request))return;
        Error=message;
        NativeRoiCallbacks.Run(reject?new Action[]{CancelDraw,Notify}:new Action[]{Notify});
    }
    public bool CompleteDraw(RoiDrawRequest request,RecordValue geometry)
    {
        if(!IsCurrent||_committing||_refreshing||_reading||_selecting||!ReferenceEquals(_drawRequest,request))return false;
        _drawRequest=null;var generation=++_drawGeneration;_committing=true;
        try
        {
            bool Current()=>IsCurrent&&generation==_drawGeneration&&Stamp==request.Stamp;
            if(!Current()||!CanDrawGroup(request.Group)||!Current())return false;
            ValidateNativeGeometry(geometry);
            if(geometry.Get<string>("kind")!=request.Kind)throw new ArgumentException("完成形状与原绘制请求不符。",nameof(geometry));
            var current=Capture(ReadCurrent());if(!Current())return false;
            if(current.Stamp!=request.Stamp){Error="绘制期间区域根已改变。";InstallAfterCommit(current);return false;}
            if(!CanDrawGroup(request.Group)||!Current())return false;
            var proposal=new RoiEditProposal(RoiEditOperation.Create,request.Group,null,geometry);
            var result=_accept(current.Stamp,proposal);
            if(!Current())return result.Accepted;
            var installed=ValidateAcceptance(current,proposal,result);
            Error=result.Accepted?"":result.Error??"宿主拒绝新区域。";
            InstallAfterCommit(installed,result.Accepted?result.Affected:null);
            if(!result.Accepted&&installed.Stamp==current.Stamp)Notify();
            return result.Accepted;
        }
        finally{_committing=false;}
    }
    private static bool SameGeometry(RecordValue left,RecordValue right)
    {
        bool Same(object? a,object? b)=>a switch
        {
            PointGeometry point=>b is PointGeometry other&&point.Frame==other.Frame&&point.Point==other.Point,
            SequenceValue sequence=>b is SequenceValue other&&sequence.Declaration==other.Declaration&&sequence.Count==other.Count&&sequence.Zip(other).All(pair=>Same(pair.First,pair.Second)),
            _=>Equals(a,b)
        };
        if(left.Declaration!=right.Declaration)return false;
        var a=left.CopyFields();var b=right.CopyFields();
        return a.Count==b.Count&&a.All(pair=>b.TryGetValue(pair.Key,out var value)&&Same(pair.Value,value));
    }
    private RoiEditSnapshot ValidateAcceptance(RoiEditSnapshot current,RoiEditProposal proposal,RoiEditAcceptance result)
    {
        ArgumentNullException.ThrowIfNull(result);
        var snapshot=Capture(result.Snapshot);
        if(!result.Accepted)
        {
            if(result.Affected is not null)throw new InvalidOperationException("拒绝回执不得分配区域身份。");
            return snapshot;
        }
        var target=result.Affected;
        if(snapshot.Stamp.Revision<=current.Stamp.Revision||target is null||target.Id==Guid.Empty||target.Group!=proposal.Group)
            throw new InvalidOperationException("宿主接纳必须递增版本并回传正确组和稳定身份。");
        if(proposal.Operation==RoiEditOperation.Replace&&target.Id!=proposal.TargetId)
            throw new InvalidOperationException("替换不得更改对象身份。");
        if(proposal.Operation==RoiEditOperation.Create&&(proposal.TargetId is not null||current.Items.Any(row=>row.Id==target.Id)))
            throw new InvalidOperationException("创建必须由宿主分配全新稳定身份。");
        var row=snapshot.Items.SingleOrDefault(row=>row.Group==target.Group&&row.Id==target.Id);
        if(row is null||!row.Enabled||!row.Visible||!SameGeometry(row.Geometry,proposal.Geometry))
            throw new InvalidOperationException("接纳回执未安装完整可编辑候选几何。");
        return snapshot;
    }
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
            var proposal=new RoiEditProposal(RoiEditOperation.Replace,item.Group,item.Id,candidate);
            var result=_accept(current.Stamp,proposal);
            if(!IsCurrent)return result.Accepted;
            var installed=ValidateAcceptance(current,proposal,result);
            Error=result.Accepted?"":result.Error??"宿主拒绝区域几何候选。";
            var unchanged=installed.Stamp==_stamp;
            NativeRoiCallbacks.Run([()=>{if(!result.Accepted)item.Cancel();},()=>InstallAfterCommit(installed),()=>{if(unchanged)Notify();}]);
            return result.Accepted;
        }
        finally{_committing=false;}
    }
    private void InstallAfterCommit(RoiEditSnapshot snapshot,RoiEditTarget? selected=null)
    {var committing=_committing;_committing=false;try{Install(snapshot,selected);}finally{_committing=committing;}}
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
        try{NativeRoiCallbacks.Run([CancelDraw,_rows.NotifyReset,Notify]);}
        finally{PropertyChanged=null;Changed=null;DrawRequested=null;DrawCancelled=null;}
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
