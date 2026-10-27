using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.VMControls.RenderInterface;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

/// <summary>A borrowed, fixed physical-frame edit scope. Geometry remains in the host's ROI records.</summary>
public sealed class NativeBoxRoiSession : INotifyPropertyChanged,IDisposable
{
    private readonly Func<IReadOnlyList<RoiEditItem>> _read;
    private readonly Func<ImageFrameAddress,RoiGeometryChange,bool> _accept;
    private readonly Func<bool> _isCurrent;
    private readonly Dictionary<(string,Guid),NativeBoxRoiProjection> _items=new();
    private readonly ObservableCollection<NativeBoxRoiProjection> _rows=new();
    private bool _disposed,_committing,_selecting;
    private Func<bool>? _editorCommit;
    private NativeBoxRoiProjection? _selected;
    public ImageFrameAddress Frame {get;}
    public int ImageWidth {get;}
    public int ImageHeight {get;}
    public IReadOnlyImage Image {get;}
    public ReadOnlyObservableCollection<NativeBoxRoiProjection> Items {get;}
    public NativeBoxRoiProjection? CurROIData=>_selected;
    public bool IsHaveData=>_selected is not null&&Owns(_selected);
    public bool IsCurrent=>!_disposed&&_isCurrent();
    public bool BoxEnable_ModuleEnableA=>IsHaveData;
    public bool BoxA {get=>IsHaveData;set{}}
    public bool Is3DROI=>false;
    public string ROISelectionA=>"Box";
    public string DisplayNameA=>"ROI形状";
    public string DescriptionA=>DisplayNameA;
    public string Error {get;private set;}="";
    public ICommand ROIValueChangedCommand {get;}
    public ICommand ValueChangedCommand {get;}
    public event PropertyChangedEventHandler? PropertyChanged;
    public event Action? Changed;
    public event Action? DrawRequested;
    public NativeBoxRoiSession(IReadOnlyImage image,Func<IReadOnlyList<RoiEditItem>> read,
        Func<ImageFrameAddress,RoiGeometryChange,bool> accept,Func<bool> isCurrent)
    {
        ResourceContractRules.ValidateImage(image);
        Image=image;Frame=image.Frame;ImageWidth=image.Width;ImageHeight=image.Height;
        _read=read;_accept=accept;_isCurrent=isCurrent;Items=new(_rows);
        ROIValueChangedCommand=new EditCommand(_=>{if(_selected is {} selected)TryCommit(selected);});
        ValueChangedCommand=new EditCommand(_=>{if(IsCurrent)DrawRequested?.Invoke();});
        Refresh();
    }
    public void Refresh()
    {
        if(!IsCurrent){CancelPending();Notify();return;}
        var rows=_read().ToArray();var keys=new HashSet<(string,Guid)>();
        if(!IsCurrent)return; // Reading the host can synchronously close this scope.
        foreach(var row in rows)
        {
            if(string.IsNullOrWhiteSpace(row.Group)||row.Id==Guid.Empty||!keys.Add((row.Group,row.Id)))
                throw new ArgumentException("ROI组与稳定ID必须非空且唯一。");
            RoiGeometryEditing.Validate(row.Geometry);
            if(row.Geometry.Get<string>("kind")!="rectangle")throw new NotSupportedException("当前原Box组合只接纳矩形，其他形状需其原参数栈。");
        }
        foreach(var key in _items.Keys.Where(key=>!keys.Contains(key)).ToArray())
        {var removed=_items[key];removed.Detach();_items.Remove(key);_rows.Remove(removed);if(ReferenceEquals(_selected,removed))_selected=null;}
        foreach(var row in rows)
        {
            var key=(row.Group,row.Id);
            if(!_items.TryGetValue(key,out var item)){item=new(this,row);_items.Add(key,item);_rows.Add(item);}
            else item.Project(row);
        }
        Notify();
    }
    internal bool Owns(NativeBoxRoiProjection item)=>IsCurrent&&_items.TryGetValue((item.Group,item.Id),out var current)
        &&ReferenceEquals(current,item)&&item.Enabled&&item.Visible;
    internal IDisposable RegisterEditorCommit(Func<bool> commit)
    {
        if(_editorCommit is not null)throw new InvalidOperationException("一个ROI编辑会话只允许一份参数编辑器。");
        _editorCommit=commit;return new Registration(()=>{if(ReferenceEquals(_editorCommit,commit))_editorCommit=null;});
    }
    internal bool PrepareEditor()=>_editorCommit?.Invoke()??IsCurrent;
    private sealed class Registration(Action release):IDisposable
    {private Action? _release=release;public void Dispose(){var action=_release;_release=null;action?.Invoke();}}
    internal bool CanEdit(NativeBoxRoiProjection item)=>Owns(item)&&ReferenceEquals(item,_selected);
    public bool Select(string group,Guid? id)
    {
        if(!IsCurrent)return false;
        NativeBoxRoiProjection? next=null;
        if(id is {} key&&(!_items.TryGetValue((group,key),out next)||!Owns(next)))return false;
        if(ReferenceEquals(_selected,next))return true;
        if(_selecting)return false;_selecting=true;
        try
        {
            if(!PrepareEditor()||!IsCurrent)return false;
            if(id is {} target&&(!_items.TryGetValue((group,target),out next)||!Owns(next)))return false;
        }finally{_selecting=false;}
        CancelPending();_selected=next;Error="";Notify();return true;
    }
    public void CancelPending()
    {NativeRoiCallbacks.Run(_rows.ToArray().Select(item=>(Action)item.Cancel));}
    public bool TryCommit(NativeBoxRoiProjection item)
    {
        if(_committing||!CanEdit(item))return false;
        if(!item.HasPending)return true;
        _committing=true;
        try
        {
            var current=_read().SingleOrDefault(row=>row.Group==item.Group&&row.Id==item.Id);
            if(current is null||!current.Enabled||!current.Visible||!item.Matches(current.Geometry))
                throw new InvalidOperationException("ROI已删除或已被另一编辑修改，请重新选择。");
            var candidate=item.Prepare();
            if(!CanEdit(item))return false;
            if(!_accept(Frame,new(item.Group,item.Id,candidate)))throw new ArgumentException("宿主拒绝区域几何候选。");
        }
        catch(Exception error) when(error is ArgumentException or InvalidOperationException or OverflowException)
        {
            item.Cancel();
            try{if(IsCurrent)Refresh();}
            finally{Error=error.Message;PropertyChanged?.Invoke(this,new(nameof(Error)));}
            return false;
        }
        finally{_committing=false;}
        if(!IsCurrent)return true; // Acceptance has already happened; a host rebind is not a rejection.
        Error="";Refresh();return true;
    }
    private void Notify()
    {
        var callbacks=new List<Action>();
        var handlers=PropertyChanged?.GetInvocationList().Cast<PropertyChangedEventHandler>().ToArray()??[];
        foreach(var name in new[]{nameof(CurROIData),nameof(IsHaveData),nameof(BoxEnable_ModuleEnableA),nameof(BoxA),nameof(Error)})
            foreach(var handler in handlers)callbacks.Add(()=>handler(this,new(name)));
        foreach(var handler in Changed?.GetInvocationList().Cast<Action>().ToArray()??[])callbacks.Add(handler);
        NativeRoiCallbacks.Run(callbacks);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        foreach(var item in _rows)item.Detach();_items.Clear();_selected=null;_editorCommit=null;
        try{NativeRoiCallbacks.Run([()=>_rows.Clear(),Notify]);}
        finally{PropertyChanged=null;Changed=null;DrawRequested=null;}
    }
    private sealed class EditCommand(Action<object?> action):ICommand
    {
        public bool CanExecute(object? parameter)=>true;
        public void Execute(object? parameter)=>action(parameter);
        public event EventHandler? CanExecuteChanged {add{}remove{}}
    }
}

/// <summary>The same normalized native ROI object is used by the original image handles and Box stack.
/// Setters collect a transient gesture; only the scope's commit calls the host.</summary>
public sealed class NativeBoxRoiProjection : INormalizedRectROI,IROIVisible
{
    private NativeBoxRoiSession? _scope;
    private RoiEditItem _accepted;
    private readonly int _width,_height;
    private readonly Dictionary<string,double> _pending=new();
    public string Group=>_accepted.Group;
    public Guid Id=>_accepted.Id;
    public bool Enabled=>_accepted.Enabled;
    public bool Visible=>_accepted.Visible;
    public bool IsVisible {get=>Visible;set{if(value!=Visible)throw new InvalidOperationException("可见性由当前区域记录管理。");}}
    public bool HasPending=>_pending.Count!=0;
    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand DataChangedCommand {get;set;}
    internal NativeBoxRoiProjection(NativeBoxRoiSession scope,RoiEditItem item)
    {_scope=scope;_width=scope.ImageWidth;_height=scope.ImageHeight;_accepted=item;DataChangedCommand=new CommitCommand(this);}
    private double Value(string key)=>_pending.TryGetValue(key,out var value)?value:key switch
    {"x"=>RoiGeometryEditing.Center(_accepted.Geometry).X,"y"=>RoiGeometryEditing.Center(_accepted.Geometry).Y,_=>_accepted.Geometry.Get<double>(key)};
    private void Set(string key,double pixel)
    {
        if(_scope is not {} scope||!scope.CanEdit(this))return;
        if(Value(key)==pixel)return;
        _pending[key]=pixel;Notify();
    }
    public double CenterX {get=>Value("x")/_width;set=>Set("x",value*_width);}
    public double CenterY {get=>Value("y")/_height;set=>Set("y",value*_height);}
    public Point CenterPoint {get=>new(CenterX,CenterY);set{CenterX=value.X;CenterY=value.Y;}}
    public double Width {get=>Value("width")/_width;set=>Set("width",value*_width);}
    public double Height {get=>Value("height")/_height;set=>Set("height",value*_height);}
    public double Angle {get=>Value("angle-degrees");set=>Set("angle-degrees",value);}
    internal bool Matches(RecordValue shape)=>RoiGeometryEditing.Center(shape)==RoiGeometryEditing.Center(_accepted.Geometry)
        &&new[]{"width","height","angle-degrees"}.All(key=>shape.Get<double>(key)==_accepted.Geometry.Get<double>(key));
    internal RecordValue Prepare()
    {
        var x=Value("x");var y=Value("y");var w=Value("width");var h=Value("height");var a=Angle;
        if(new[]{x,y,w,h,a}.Any(value=>!double.IsFinite(value))||x is < -30000 or >30000||y is < -30000 or >30000
            ||w is <0 or >30000||h is <0 or >30000||a is < -180 or >180)throw new ArgumentException("几何值超出原Box控件范围。");
        var values=RoiGeometryEditing.Values(_accepted.Geometry);values["center"]=RoiGeometryEditing.Point(new(x,y));
        values["width"]=w;values["height"]=h;values["angle-degrees"]=a;return RoiGeometryEditing.Checked(values);
    }
    internal void Project(RoiEditItem accepted){_accepted=accepted;Cancel();}
    public void Cancel(){_pending.Clear();Notify();}
    internal void Detach(){_scope=null;_pending.Clear();PropertyChanged=null;}
    private void Notify()
    {
        var callbacks=new List<Action>();
        var handlers=PropertyChanged?.GetInvocationList().Cast<PropertyChangedEventHandler>().ToArray()??[];
        foreach(var property in new[]{nameof(CenterX),nameof(CenterY),nameof(CenterPoint),nameof(Width),nameof(Height),nameof(Angle),nameof(IsVisible)})
            foreach(var handler in handlers)callbacks.Add(()=>handler(this,new(property)));
        NativeRoiCallbacks.Run(callbacks);
    }
    private sealed class CommitCommand(NativeBoxRoiProjection item):ICommand
    {
        public bool CanExecute(object? parameter)=>item._scope?.CanEdit(item)==true;
        public void Execute(object? parameter){if(CanExecute(parameter))item._scope!.TryCommit(item);}
        public event EventHandler? CanExecuteChanged{add{}remove{}}
    }
}

// Complete essential notifications and cleanup even when an observer fails; retain the error.
internal static class NativeRoiCallbacks
{
    internal static void Run(IEnumerable<Action> callbacks)
    {
        List<Exception>? errors=null;
        foreach(var callback in callbacks)
            try{callback();}catch(Exception error){(errors??=[]).Add(error);}
        if(errors?.Count==1)System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(errors[0]).Throw();
        if(errors?.Count>1)throw new AggregateException(errors);
    }
}
