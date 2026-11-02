using System.ComponentModel;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Native.Display.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.Regions;

internal sealed record NativeRoiField(string Key,bool MultiBinding);

/// <summary>Transient normalized view of a host ROI; the host retains the sole accepted model.</summary>
public abstract class NativeRoiProjection : IROI,IROIVisible
{
    private NativeRoiSession? _scope;
    protected RoiEditItem Accepted {get;private set;}
    private readonly Dictionary<string,double> _pending=new();
    protected int ImageWidth {get;}
    protected int ImageHeight {get;}
    public string Group=>Accepted.Group;
    internal RecordValue AcceptedGeometry=>Accepted.Geometry;
    public Guid Id=>Accepted.Id;
    public string Kind=>Accepted.Geometry.Get<string>("kind");
    public bool Enabled=>Accepted.Enabled;
    public bool Visible=>Accepted.Visible;
    public bool IsVisible {get=>Visible;set{if(value!=Visible)throw new InvalidOperationException("可见性由当前区域记录管理。");}}
    public bool HasPending=>_pending.Count!=0;
    internal RoiEditStamp? PendingStamp {get;private set;}
    internal abstract IReadOnlyList<NativeRoiField> Fields {get;}
    protected abstract IEnumerable<string> PropertyNames {get;}
    public event PropertyChangedEventHandler? PropertyChanged;
    public ICommand DataChangedCommand {get;set;}
    protected NativeRoiProjection(NativeRoiSession scope,RoiEditItem item)
    {_scope=scope;Accepted=item;ImageWidth=scope.ImageWidth;ImageHeight=scope.ImageHeight;DataChangedCommand=new CommitCommand(this);}
    internal static Type ProjectionType(RoiEditItem item)=>item.Geometry.Get<string>("kind") switch
    {"rectangle"=>typeof(NativeBoxRoiProjection),"sector"=>typeof(NativeSectorRoiProjection),"annulus" when !item.Enabled=>typeof(NativeInheritedAnnulusProjection),"annulus" when !IsFullAnnulus(item.Geometry)=>typeof(NativeSectorRoiProjection),"annulus"=>typeof(NativeRingRoiProjection),"polygon"=>typeof(NativePolygonRoiProjection),_=>throw new NotSupportedException()};
    internal static bool IsFullAnnulus(RecordValue geometry)=>Math.Abs(Math.Abs(geometry.Get<double>("end-radians")-geometry.Get<double>("start-radians"))-2*Math.PI)<=1e-9;
    internal static NativeRoiProjection Create(NativeRoiSession scope,RoiEditItem item)=>ProjectionType(item) switch
    {
        var type when type==typeof(NativeBoxRoiProjection)=>new NativeBoxRoiProjection(scope,item),
        var type when type==typeof(NativeSectorRoiProjection)=>new NativeSectorRoiProjection(scope,item),
        var type when type==typeof(NativeInheritedAnnulusProjection)=>new NativeInheritedAnnulusProjection(scope,item),
        var type when type==typeof(NativeRingRoiProjection)=>new NativeRingRoiProjection(scope,item),
        _=>new NativePolygonRoiProjection(scope,item)
    };
    protected bool CanStage=>_scope is {} scope&&scope.CanEdit(this);
    protected abstract double AcceptedValue(string key);
    protected double Value(string key)=>_pending.TryGetValue(key,out var value)?value:AcceptedValue(key);
    protected void Stage(string key,double value)
    {
        if(_scope is not {} scope||!scope.CanEdit(this)||Value(key)==value)return;
        PendingStamp??=scope.Stamp;_pending[key]=value;Notify();
    }
    internal virtual void StageFields(IReadOnlyDictionary<int,double> fields)
    {
        if(_scope is not {} scope||!scope.CanEdit(this))return;
        foreach(var (index,value) in fields)
        {if(index<0||index>=Fields.Count)throw new ArgumentOutOfRangeException(nameof(fields));PendingStamp??=scope.Stamp;_pending[Fields[index].Key]=value;}
        Notify();
    }
    protected bool Changed(string key)=>_pending.ContainsKey(key);
    public double CenterX {get=>Value("x")/ImageWidth;set=>Stage("x",value*ImageWidth);}
    public double CenterY {get=>Value("y")/ImageHeight;set=>Stage("y",value*ImageHeight);}
    public Point CenterPoint {get=>new(CenterX,CenterY);set{CenterX=value.X;CenterY=value.Y;}}
    internal bool Matches(RoiEditItem row)=>row.Group==Group&&row.Id==Id&&row.Enabled==Enabled&&row.Visible==Visible&&row.Name==Accepted.Name&&
        MatchesGeometry(row.Geometry,Accepted.Geometry);
    internal bool MatchesGeometry(RecordValue left,RecordValue right)
    {
        var kind=left.Get<string>("kind");
        if(kind!=right.Get<string>("kind"))return false;
        if(kind=="polygon")return RoiGeometryEditing.Vertices(left).SequenceEqual(RoiGeometryEditing.Vertices(right));
        return RoiGeometryEditing.Center(left)==RoiGeometryEditing.Center(right)&&GeometryKeys.All(key=>left.Get<double>(key)==right.Get<double>(key));
    }
    protected abstract IEnumerable<string> GeometryKeys {get;}
    internal abstract RecordValue Prepare();
    protected Dictionary<string,object?> PrepareCenter()
    {
        if(!double.IsFinite(Value("x"))||!double.IsFinite(Value("y"))||Value("x") is < -30000 or >30000||Value("y") is < -30000 or >30000)
            throw new ArgumentException("中心超出原区域控件范围。");
        var values=RoiGeometryEditing.Values(Accepted.Geometry);
        if(Changed("x")||Changed("y"))values["center"]=RoiGeometryEditing.Point(new(Value("x"),Value("y")));
        return values;
    }
    internal void Install(RoiEditItem accepted,bool cancel){Accepted=accepted;if(cancel){_pending.Clear();PendingStamp=null;}OnInstalled(cancel);}
    public void Cancel(){_pending.Clear();PendingStamp=null;Notify();}
    internal void Detach(){_scope=null;_pending.Clear();PendingStamp=null;OnDetached();PropertyChanged=null;}
    protected virtual void OnInstalled(bool cancel){}
    protected virtual void OnDetached(){}
    protected virtual IEnumerable<Action> AdditionalNotifications()=>Array.Empty<Action>();
    internal void Notify()
    {
        var callbacks=new List<Action>(AdditionalNotifications());
        var handlers=PropertyChanged?.GetInvocationList().Cast<PropertyChangedEventHandler>().ToArray()??[];
        foreach(var property in PropertyNames.Concat(new[]{nameof(CenterX),nameof(CenterY),nameof(CenterPoint),nameof(IsVisible),nameof(Enabled)}))
            foreach(var handler in handlers)callbacks.Add(()=>handler(this,new(property)));
        NativeRoiCallbacks.Run(callbacks);
    }
    private sealed class CommitCommand(NativeRoiProjection item):ICommand
    {
        public bool CanExecute(object? parameter)=>item._scope?.CanEdit(item)==true;
        public void Execute(object? parameter){if(CanExecute(parameter))item._scope!.TryCommit(item);}
        public event EventHandler? CanExecuteChanged{add{}remove{}}
    }
}

public sealed class NativeBoxRoiProjection : NativeRoiProjection,INormalizedRectROI
{
    internal NativeBoxRoiProjection(NativeRoiSession scope,RoiEditItem item):base(scope,item){}
    internal override IReadOnlyList<NativeRoiField> Fields {get;}=new NativeRoiField[]{new("x",true),new("y",true),new("width",true),new("height",true),new("angle-degrees",false)};
    protected override IEnumerable<string> PropertyNames=>new[]{nameof(Width),nameof(Height),nameof(Angle)};
    protected override IEnumerable<string> GeometryKeys=>new[]{"width","height","angle-degrees"};
    protected override double AcceptedValue(string key)=>key switch
    {"x"=>RoiGeometryEditing.Center(Accepted.Geometry).X,"y"=>RoiGeometryEditing.Center(Accepted.Geometry).Y,_=>Accepted.Geometry.Get<double>(key)};
    public double Width {get=>Value("width")/ImageWidth;set=>Stage("width",value*ImageWidth);}
    public double Height {get=>Value("height")/ImageHeight;set=>Stage("height",value*ImageHeight);}
    public double Angle {get=>Value("angle-degrees");set=>Stage("angle-degrees",value);}
    internal override RecordValue Prepare()
    {
        var w=Value("width");var h=Value("height");var a=Angle;
        if(new[]{w,h,a}.Any(value=>!double.IsFinite(value))||w is <0 or >30000||h is <0 or >30000||a is < -180 or >180)
            throw new ArgumentException("几何值超出原Box控件范围。");
        var values=PrepareCenter();values["width"]=w;values["height"]=h;values["angle-degrees"]=a;
        return RoiGeometryEditing.Checked(values);
    }
}

public sealed class NativeSectorRoiProjection : NativeRoiProjection,INormalizedSectorROI
{
    internal NativeSectorRoiProjection(NativeRoiSession scope,RoiEditItem item):base(scope,item){}
    internal override IReadOnlyList<NativeRoiField> Fields {get;}=new NativeRoiField[]{new("x",true),new("y",true),new("outer-radius",true),new("inner-radius",true),new("begin",false),new("sweep",false)};
    protected override IEnumerable<string> PropertyNames=>new[]{nameof(Radius),nameof(MinRadius),nameof(StartAngle),nameof(EndAngle),nameof(BeginAngle),nameof(AngleRange)};
    protected override IEnumerable<string> GeometryKeys=>new[]{"outer-radius","inner-radius","start-radians","end-radians"};
    protected override double AcceptedValue(string key)=>key switch
    {
        "x"=>RoiGeometryEditing.Center(Accepted.Geometry).X,"y"=>RoiGeometryEditing.Center(Accepted.Geometry).Y,
        "begin"=>Normalize(Accepted.Geometry.Get<double>(ReverseAnnulus?"end-radians":"start-radians")*180/Math.PI),
        "sweep"=>AcceptedSweep(),
        _=>Accepted.Geometry.Get<double>(key)
    };
    // Platform adaptation: a partial annulus borrows the six-field Sector editor without changing its persisted identity.
    private bool ReverseAnnulus=>Kind=="annulus"&&Accepted.Geometry.Get<double>("end-radians")<Accepted.Geometry.Get<double>("start-radians");
    private double AcceptedSweep()
    {
        var radians=Accepted.Geometry.Get<double>("end-radians")-Accepted.Geometry.Get<double>("start-radians");
        if(ReverseAnnulus)radians=-radians;
        return Math.Abs(radians-2*Math.PI)<=1e-9?360:radians*180/Math.PI;
    }
    public double Radius {get=>Value("outer-radius")/ImageWidth;set{Stage("outer-radius",value*ImageWidth);if(MinRadius>value)Stage("inner-radius",value*ImageWidth);}}
    public double MinRadius {get=>Value("inner-radius")/ImageWidth;set{Stage("inner-radius",value*ImageWidth);if(Radius<value)Stage("outer-radius",value*ImageWidth);}}
    public double BeginAngle {get=>Value("begin");set{var sweep=CanonicalSweep(value,AngleRange);Stage("begin",value);Stage("sweep",sweep);}}
    public double AngleRange {get=>Value("sweep");set=>Stage("sweep",CanonicalSweep(BeginAngle,value));}
    public double StartAngle
    {
        get=>BeginAngle;
        set{var end=EndAngle;Stage("begin",Normalize(value));Stage("sweep",ForwardSweep(value,end));}
    }
    public double EndAngle {get=>AngleRange==360?BeginAngle:Normalize(BeginAngle+AngleRange);set=>Stage("sweep",ForwardSweep(BeginAngle,value));}
    private static double Normalize(double value)
    {var normalized=((value+180)%360+360)%360-180;return normalized==-180&&value>0?180:normalized;}
    private static double ForwardSweep(double start,double end)
    {
        var startRadians=start*Math.PI/180;var endRadians=end*Math.PI/180;
        var signed=Vector.AngleBetween(new(Math.Cos(endRadians),Math.Sin(endRadians)),new(Math.Cos(startRadians),Math.Sin(startRadians)));
        return 360-(signed<0?360+signed:signed);
    }
    private static double CanonicalSweep(double begin,double sweep)=>sweep>0&&sweep<=360&&Math.Abs(Normalize(begin+sweep)-begin)<0.0009?360:sweep;
    internal override void StageFields(IReadOnlyDictionary<int,double> fields)
    {
        var complete=fields.ToDictionary(pair=>pair.Key,pair=>pair.Value);
        if(fields.TryGetValue(2,out var outer)&&!fields.ContainsKey(3)&&outer<Value("inner-radius"))complete[3]=outer;
        if(fields.TryGetValue(3,out var inner)&&!fields.ContainsKey(2)&&inner>Value("outer-radius"))complete[2]=inner;
        if(fields.ContainsKey(4)||fields.ContainsKey(5))
            complete[5]=CanonicalSweep(fields.TryGetValue(4,out var begin)?begin:BeginAngle,fields.TryGetValue(5,out var sweep)?sweep:AngleRange);
        base.StageFields(complete);
    }
    internal override RecordValue Prepare()
    {
        var outer=Value("outer-radius");var inner=Value("inner-radius");var begin=BeginAngle;var sweep=AngleRange;
        if(new[]{outer,inner,begin,sweep}.Any(value=>!double.IsFinite(value))||outer is <0 or >10000||inner is <0 or >10000||begin is < -180 or >180||sweep is <=0 or >360)
            throw new ArgumentException("扇形超出原参数范围或角跨度退化。");
        var values=PrepareCenter();values["outer-radius"]=outer;values["inner-radius"]=inner;
        if(Changed("begin")||Changed("sweep"))
        {
            var start=Changed("begin")?begin*Math.PI/180:Accepted.Geometry.Get<double>(ReverseAnnulus?"end-radians":"start-radians");
            values["start-radians"]=ReverseAnnulus?start+sweep*Math.PI/180:start;
            values["end-radians"]=ReverseAnnulus?start:start+sweep*Math.PI/180;
        }
        return RoiGeometryEditing.Checked(values);
    }
}

/// <summary>Original SectorRing: a complete annulus, never a six-field Sector substitute.</summary>
public sealed class NativeRingRoiProjection : NativeRoiProjection,INormalizedCircleROI
{
    internal NativeRingRoiProjection(NativeRoiSession scope,RoiEditItem item):base(scope,item){}
    internal override IReadOnlyList<NativeRoiField> Fields {get;}=new NativeRoiField[]{new("x",true),new("y",true),new("outer-radius",true),new("inner-radius",true)};
    protected override IEnumerable<string> PropertyNames=>new[]{nameof(Radius),nameof(MinRadius)};
    // The preserved formal full-circle endpoints also participate in host snapshot identity.
    protected override IEnumerable<string> GeometryKeys=>new[]{"outer-radius","inner-radius","start-radians","end-radians"};
    protected override double AcceptedValue(string key)=>key switch
    {"x"=>RoiGeometryEditing.Center(Accepted.Geometry).X,"y"=>RoiGeometryEditing.Center(Accepted.Geometry).Y,_=>Accepted.Geometry.Get<double>(key)};
    public double Radius {get=>Value("outer-radius")/ImageWidth;set{Stage("outer-radius",value*ImageWidth);if(MinRadius>value)Stage("inner-radius",value*ImageWidth);}}
    public double MinRadius {get=>Value("inner-radius")/ImageWidth;set{Stage("inner-radius",value*ImageWidth);if(Radius<value)Stage("outer-radius",value*ImageWidth);}}
    internal override void StageFields(IReadOnlyDictionary<int,double> fields)
    {
        var complete=fields.ToDictionary(pair=>pair.Key,pair=>pair.Value);
        if(fields.TryGetValue(2,out var outer)&&!fields.ContainsKey(3)&&outer<Value("inner-radius"))complete[3]=outer;
        if(fields.TryGetValue(3,out var inner)&&!fields.ContainsKey(2)&&inner>Value("outer-radius"))complete[2]=inner;
        base.StageFields(complete);
    }
    internal override RecordValue Prepare()
    {
        var outer=Value("outer-radius");var inner=Value("inner-radius");
        if(!double.IsFinite(outer)||!double.IsFinite(inner)||outer is <0 or >10000||inner is <0 or >10000)
            throw new ArgumentException("圆环半径超出原参数范围。");
        var values=PrepareCenter();values["outer-radius"]=outer;values["inner-radius"]=inner;
        // The four-field editor does not reinterpret, round or replace the accepted full-circle angles.
        return RoiGeometryEditing.Checked(values);
    }
}

/// <summary>Ordered vertex projection over the sole host-owned neutral geometry record.</summary>
public sealed class NativePolygonRoiProjection : NativeRoiProjection,INormalizedPolygonROI
{
    private VertexPoints _points;
    private long _topology;
    public long TopologyGeneration=>_topology;
    internal NativePolygonRoiProjection(NativeRoiSession scope,RoiEditItem item):base(scope,item)
    {_points=new(this,++_topology,Normalized());}
    internal override IReadOnlyList<NativeRoiField> Fields=>Enumerable.Range(0,RoiGeometryEditing.Vertices(Accepted.Geometry).Length)
        .SelectMany(index=>new[]{new NativeRoiField(Key(index,true),true),new NativeRoiField(Key(index,false),true)}).ToArray();
    protected override IEnumerable<string> PropertyNames=>new[]{nameof(PolygonPoints),nameof(TopologyGeneration)};
    protected override IEnumerable<string> GeometryKeys=>Array.Empty<string>();
    private static string Key(int index,bool x)=>$"vertex:{index}:{(x?"x":"y")}";
    protected override double AcceptedValue(string key)
    {
        if(key is "x" or "y"){var center=RoiGeometryEditing.Center(Accepted.Geometry);return key=="x"?center.X:center.Y;}
        var parts=key.Split(':');var point=RoiGeometryEditing.Vertices(Accepted.Geometry)[int.Parse(parts[1],System.Globalization.CultureInfo.InvariantCulture)];
        return parts[2]=="x"?point.X:point.Y;
    }
    public System.Collections.ObjectModel.ObservableCollection<Point> PolygonPoints
    {get=>_points;set{if(!ReferenceEquals(value,_points))throw new InvalidOperationException("正式顶点拓扑只能由区域宿主接纳。");}}
    internal bool TryStageVertex(long topology,int index,bool x,double pixelValue)
    {
        if(!CanStage||topology!=_topology||index<0||index>=_points.Count)return false;
        if(!double.IsFinite(pixelValue)||pixelValue is < -30000 or >30000)throw new ArgumentException("多边形顶点超出原参数范围。");
        StageFields(new Dictionary<int,double>{{index*2+(x?0:1),pixelValue}});return true;
    }
    private Point[] Normalized()=>Enumerable.Range(0,RoiGeometryEditing.Vertices(Accepted.Geometry).Length)
        .Select(index=>new Point(Value(Key(index,true))/ImageWidth,Value(Key(index,false))/ImageHeight)).ToArray();
    protected override void OnInstalled(bool cancel)
    {
        if(cancel)_points=new(this,++_topology,Normalized());
    }
    protected override void OnDetached(){_topology++;}
    protected override IEnumerable<Action> AdditionalNotifications()
    {
        var points=_points;var changes=points.Install(Normalized());var version=points.Version;
        return changes.Select(change=>(Action)(()=>points.Publish(change,version))).ToArray();
    }
    internal override RecordValue Prepare()
    {
        var points=RoiGeometryEditing.Vertices(Accepted.Geometry);
        for(var index=0;index<points.Length;index++)
        {
            var x=Value(Key(index,true));var y=Value(Key(index,false));
            if(!double.IsFinite(x)||!double.IsFinite(y)||x is < -30000 or >30000||y is < -30000 or >30000)
                throw new ArgumentException("多边形顶点超出原参数范围。");
            points[index]=new(x,y);
        }
        var values=RoiGeometryEditing.Values(Accepted.Geometry);
        values["vertices"]=new SequenceValue(RoiDeclarations.Shape.Fields.Single(field=>field.Id=="vertices").Type,
            points.Select(point=>(object?)RoiGeometryEditing.Point(point)));
        return RoiGeometryEditing.Checked(values);
    }
    private sealed class VertexPoints: System.Collections.ObjectModel.ObservableCollection<Point>
    {
        private readonly NativePolygonRoiProjection _owner;private readonly long _generation;
        internal long Version {get;private set;}
        internal VertexPoints(NativePolygonRoiProjection owner,long generation,IEnumerable<Point> points):base(points){_owner=owner;_generation=generation;}
        private void Current(){if(!ReferenceEquals(_owner._points,this)||_generation!=_owner._topology||!_owner.CanStage)throw new InvalidOperationException("多边形顶点租约已失效。");}
        protected override void SetItem(int index,Point value)
        {
            Current();if(index<0||index>=Count)throw new ArgumentOutOfRangeException(nameof(index));
            var x=value.X*_owner.ImageWidth;var y=value.Y*_owner.ImageHeight;
            if(!double.IsFinite(x)||!double.IsFinite(y)||x is < -30000 or >30000||y is < -30000 or >30000)throw new ArgumentException("多边形顶点超出原参数范围。");
            _owner.StageFields(new Dictionary<int,double>{{index*2,x},{index*2+1,y}});
        }
        protected override void InsertItem(int index,Point value){Current();throw new NotSupportedException("顶点拓扑由区域宿主一次接纳。");}
        protected override void RemoveItem(int index){Current();throw new NotSupportedException("顶点拓扑由区域宿主一次接纳。");}
        protected override void ClearItems(){Current();throw new NotSupportedException("顶点拓扑由区域宿主一次接纳。");}
        protected override void MoveItem(int oldIndex,int newIndex){Current();throw new NotSupportedException("顶点拓扑由区域宿主一次接纳。");}
        internal IReadOnlyList<System.Collections.Specialized.NotifyCollectionChangedEventArgs> Install(Point[] values)
        {
            var before=this.ToArray();if(before.SequenceEqual(values))return Array.Empty<System.Collections.Specialized.NotifyCollectionChangedEventArgs>();
            Version++;Items.Clear();foreach(var point in values)Items.Add(point);
            if(before.Length!=values.Length)return new[]{new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Reset)};
            return values.Select((point,index)=>(point,index)).Where(pair=>pair.point!=before[pair.index])
                .Select(pair=>new System.Collections.Specialized.NotifyCollectionChangedEventArgs(System.Collections.Specialized.NotifyCollectionChangedAction.Replace,pair.point,before[pair.index],pair.index)).ToArray();
        }
        public override event System.Collections.Specialized.NotifyCollectionChangedEventHandler? CollectionChanged;
        protected override void OnCollectionChanged(System.Collections.Specialized.NotifyCollectionChangedEventArgs change)=>Publish(change,Version);
        internal void Publish(System.Collections.Specialized.NotifyCollectionChangedEventArgs change,long version)
        {
            bool Current()=>ReferenceEquals(_owner._points,this)&&_owner._topology==_generation&&Version==version;
            NativeRoiCallbacks.Run((CollectionChanged?.GetInvocationList().Cast<System.Collections.Specialized.NotifyCollectionChangedEventHandler>()??Enumerable.Empty<System.Collections.Specialized.NotifyCollectionChangedEventHandler>())
                .Select(handler=>(Action)(()=>{if(Current())handler(this,change);}))); 
        }

    }
}
