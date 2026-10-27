using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Design;

public partial class NativeRoiScenePreview : UserControl
{
    public NativeRoiScenePreview()=>InitializeComponent();
}

/// <summary>Explicit preview-only values, not a tool's parameter defaults or a second UI.</summary>
public static class NativeRoiDesignData
{
    public static readonly DependencyProperty SampleIndexProperty=DependencyProperty.RegisterAttached("SampleIndex",typeof(int),typeof(NativeRoiDesignData),new PropertyMetadata(-1,OnSample),value=>(int)value is >=-1 and <=3);
    public static int GetSampleIndex(DependencyObject view)=>(int)view.GetValue(SampleIndexProperty);
    public static void SetSampleIndex(DependencyObject view,int value)=>view.SetValue(SampleIndexProperty,value);
    public static readonly DependencyProperty ExpandedParametersProperty=DependencyProperty.RegisterAttached("ExpandedParameters",typeof(bool),typeof(NativeRoiDesignData),new PropertyMetadata(true,OnExpanded));
    public static bool GetExpandedParameters(DependencyObject view)=>(bool)view.GetValue(ExpandedParametersProperty);
    public static void SetExpandedParameters(DependencyObject view,bool value)=>view.SetValue(ExpandedParametersProperty,value);
    private static readonly DependencyProperty OwnerProperty=DependencyProperty.RegisterAttached("Owner",typeof(Owner),typeof(NativeRoiDesignData));
    private static void OnSample(DependencyObject view,DependencyPropertyChangedEventArgs args)
    {
        if(view is not NativeRoiScene scene)throw new ArgumentException("ROI sample requires the real scene.");
        var owner=(Owner?)scene.GetValue(OwnerProperty);
        if((int)args.NewValue<0){scene.ClearValue(OwnerProperty);owner?.Dispose();return;}
        if(owner is null)
        {
            if(scene.Session is not null)throw new InvalidOperationException("A preview cannot replace a host-owned session.");
            owner=new(scene);scene.SetValue(OwnerProperty,owner);
            try {owner.Attach();}
            catch(Exception error)
            {
                if(ReferenceEquals(scene.GetValue(OwnerProperty),owner))scene.ClearValue(OwnerProperty);
                NativeRoiCallbacks.Run(new Action[]{()=>throw error,owner.Dispose});
                throw;
            }
        }
        if(!ReferenceEquals(scene.GetValue(OwnerProperty),owner))return;
        if(!ReferenceEquals(scene.Session,owner.Session))
        {
            scene.ClearValue(OwnerProperty);owner.Dispose();
            throw new InvalidOperationException("Preview does not replace a host-owned session.");
        }
        owner.Select();
    }
    private static void OnExpanded(DependencyObject view,DependencyPropertyChangedEventArgs args)=>((Owner?)view.GetValue(OwnerProperty))?.Queue();
    private sealed class Owner : IDisposable
    {
        private readonly NativeRoiScene _scene;
        private readonly SampleImage _image=new();
        private readonly Guid _scope=Guid.NewGuid();
        private readonly List<RoiEditItem> _rows;
        private long _revision,_generation;
        private bool _disposed;
        private DispatcherOperation? _pending;
        internal NativeRoiSession Session {get;}
        internal Owner(NativeRoiScene scene)
        {
            _scene=scene;
            var sector=RoiGeometryEditing.Values(RoiGeometryEditing.Create("annulus",new(300,150),new(400,150)));
            sector["kind"]="sector";sector["start-radians"]=.3;sector["end-radians"]=2.3;
            _rows=new[]{
                new RoiEditItem("detect",Guid.NewGuid(),"矩形",RoiGeometryEditing.Create("rectangle",new(100,70),new(380,240))),
                new RoiEditItem("mask",Guid.NewGuid(),"圆环",RoiGeometryEditing.Create("annulus",new(300,150),new(400,150))),
                new RoiEditItem("detect",Guid.NewGuid(),"多边形",RoiGeometryEditing.Create("polygon",new(0,0),new(0,0),new[]{new Point2(90,80),new Point2(360,60),new Point2(440,230),new Point2(150,270)})),
                new RoiEditItem("mask",Guid.NewGuid(),"扇形",RoiGeometryEditing.Checked(sector))}.ToList();
            Session=new(_image,Read,Accept,()=>!_disposed,"detect");
        }
        internal void Attach()
        {
            if(_disposed)return;
            _scene.Session=Session;
            if(_disposed||!ReferenceEquals(_scene.Session,Session)||!ReferenceEquals(_scene.GetValue(OwnerProperty),this))return;
            _scene.Loaded+=Loaded;_scene.Unloaded+=Unloaded;_scene.Disposing+=Dispose;
        }
        private RoiEditSnapshot Read()=>new(new(_scope,_revision,_image.Frame),Array.AsReadOnly(_rows.ToArray()));
        private RoiEditAcceptance Accept(RoiEditStamp expected,RoiEditProposal change)
        {
            var snapshot=Read();if(_disposed||expected!=snapshot.Stamp)return new(false,snapshot,Error:"旧预览范围");
            Guid id;
            if(change.Operation==RoiEditOperation.Create)
            {
                if(change.TargetId is not null)return new(false,snapshot,Error:"创建不得指定旧身份");
                id=Guid.NewGuid();_rows.Add(new(change.Group,id,"新区域",change.Geometry));
            }
            else
            {
                var index=_rows.FindIndex(row=>row.Group==change.Group&&row.Id==change.TargetId);
                if(index<0)return new(false,snapshot,Error:"预览对象不存在");
                id=_rows[index].Id;_rows[index]=_rows[index] with{Geometry=change.Geometry};
            }
            _revision++;return new(true,Read(),new(change.Group,id));
        }
        internal void Select()
        {
            if(_disposed||!ReferenceEquals(_scene.Session,Session))throw new InvalidOperationException("Preview session is detached.");
            var row=_rows[GetSampleIndex(_scene)];
            if(!Session.Select(row.Group,row.Id))throw new InvalidOperationException("Sample selection was rejected: "+_scene.Editor.Error);
            Session.SetDrawingGroup(row.Group);Queue();
        }
        private void Loaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,_scene))Queue();}
        private void Unloaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,_scene))Cancel();}
        private void Cancel(){_generation++;_pending?.Abort();_pending=null;}
        internal void Queue()
        {
            Cancel();var generation=_generation;if(_disposed||!ReferenceEquals(_scene.Session,Session))return;
            if(System.ComponentModel.DesignerProperties.GetIsInDesignMode(_scene))ApplyExpansion(generation);
            if(!_scene.IsLoaded)return;
            _pending=_scene.Dispatcher.BeginInvoke(DispatcherPriority.Loaded,new Action(()=>
            {
                if(_disposed||generation!=_generation||!_scene.IsLoaded||!ReferenceEquals(_scene.Session,Session))return;
                _pending=null;ApplyExpansion(generation);
            }));
        }
        private void ApplyExpansion(long generation)
        {
            if(_disposed||generation!=_generation||!ReferenceEquals(_scene.Session,Session))return;
            var native=_scene.Editor.NativeView;native.ApplyTemplate();native.UpdateLayout();
            if(_disposed||generation!=_generation||!ReferenceEquals(_scene.Session,Session))return;
            if(native.Template?.FindName("expand",native) is ToggleButton expand)
                expand.SetCurrentValue(ToggleButton.IsCheckedProperty,GetExpandedParameters(_scene));
        }
        public void Dispose()
        {
            if(_disposed)return;_disposed=true;Cancel();
            _scene.Loaded-=Loaded;_scene.Unloaded-=Unloaded;_scene.Disposing-=Dispose;
            NativeRoiCallbacks.Run(new Action[]{()=>{if(ReferenceEquals(_scene.Session,Session))_scene.Session=null;},Session.Dispose,_image.Dispose});
        }
    }
    // Each lease retains immutable managed state independently; no runtime image factory or native handle.
    private sealed record ImageState(ImageLayout Layout,ImageFrameAddress Frame,byte[] Pixels,RegionRun[] Runs);
    private sealed class SampleImage : IReadOnlyImage,IDisposable
    {
        private ImageState? _state;
        internal SampleImage()
        {
            var layout=new ImageLayout(640,320,648,PixelLayout.Gray8);var pixels=new byte[layout.BufferLength];
            for(var y=0;y<layout.Height;y++)for(var x=0;x<layout.RowStrideBytes;x++)pixels[y*layout.RowStrideBytes+x]=x>=layout.Width?(byte)233:(byte)(45+(x/32+y/32)%2*25);
            _state=new(layout,new(Guid.NewGuid(),"library-roi-design"),pixels,Enumerable.Range(0,320).Select(y=>new RegionRun(y,0,639)).ToArray());
            ResourceContractRules.ValidateImage(this);
        }
        private ImageState State=>_state??throw new ObjectDisposedException(nameof(SampleImage));
        public ImageLayout Layout=>State.Layout;public ImageFrameAddress Frame=>State.Frame;
        public int Width=>Layout.Width;public int Height=>Layout.Height;
        public IReadOnlyRegion EffectiveDomain {get{_=State;return new Domain(()=>State);}}
        public byte[] CopyPixels()=>State.Pixels.ToArray();
        public IImagePixelReadLease AcquirePixels()=>new Pixels(State);
        public void Dispose()=>_state=null;
    }
    private sealed class Domain(Func<ImageState> state):IReadOnlyRegion
    {
        public CoordinateFrame Frame {get {_=state();return RoiDeclarations.ImageFrame;}}
        public ImageFrameAddress SourceFrame=>state().Frame;
        public IRegionReadLease AcquireReader()=>new Runs(state());
    }
    private sealed class Pixels(ImageState state):IImagePixelReadLease
    {
        private ImageState? _state=state;
        private ImageState State=>_state??throw new ObjectDisposedException(nameof(Pixels));
        public ImageLayout Layout=>State.Layout;public ImageFrameAddress Frame=>State.Frame;
        public IReadOnlyRegion EffectiveDomain {get{_=State;return new Domain(()=>State);}}
        public ReadOnlySpan<byte> GetRow(int row)
        {
            var s=State;if(row<0||row>=s.Layout.Height)throw new ArgumentOutOfRangeException(nameof(row));
            return s.Pixels.AsSpan(row*s.Layout.RowStrideBytes,s.Layout.RowStrideBytes);
        }
        public IRegionReadLease AcquireDomainReader()=>new Runs(State);
        public void Dispose()=>_state=null;
    }
    private sealed class Runs(ImageState state):IRegionReadLease
    {
        private ImageState? _state=state;
        private ImageState State=>_state??throw new ObjectDisposedException(nameof(Runs));
        public CoordinateFrame Frame {get{_=State;return RoiDeclarations.ImageFrame;}}
        public ImageFrameAddress SourceFrame=>State.Frame;
        public bool IsEmpty=>State.Runs.Length==0;public int RunCount=>State.Runs.Length;
        public ReadOnlyMemory<RegionRun> ReadRuns(int offset,int maximumCount)
        {
            var s=State;ResourceContractRules.ValidateBatch(offset,maximumCount,s.Runs.Length);
            return s.Runs.AsMemory(offset,Math.Min(maximumCount,s.Runs.Length-offset));
        }
        public void Dispose()=>_state=null;
    }
}
