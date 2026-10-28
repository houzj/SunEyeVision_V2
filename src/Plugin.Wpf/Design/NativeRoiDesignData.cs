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
        private readonly NativeRoiSamples.Image _image=new();
        private readonly Guid _scope=Guid.NewGuid();
        private readonly List<RoiEditItem> _rows;
        private sealed record HistoryState(IReadOnlyList<RoiEditItem> Rows,RoiEditTarget? Selection);
        private readonly Stack<HistoryState> _undo=new(),_redo=new();
        private long _revision,_generation;
        private bool _disposed;
        private DispatcherOperation? _pending;
        internal NativeRoiSession Session {get;}
        internal Owner(NativeRoiScene scene)
        {
            _scene=scene;
            _rows=NativeRoiSamples.CreateItems().ToList();
            Session=new(_image,Read,Accept,()=>!_disposed,()=>new(_undo.Count>0,_redo.Count>0),"detect");
        }
        internal void Attach()
        {
            if(_disposed)return;
            _scene.Session=Session;
            if(_disposed||!ReferenceEquals(_scene.Session,Session)||!ReferenceEquals(_scene.GetValue(OwnerProperty),this))return;
            _scene.Loaded+=Loaded;_scene.Unloaded+=Unloaded;_scene.Disposing+=Dispose;
        }
        private RoiEditSnapshot Read()=>new(new(_scope,_revision,_image.Frame),Array.AsReadOnly(_rows.ToArray()));
        private HistoryState CaptureHistory()=>new(Array.AsReadOnly(_rows.ToArray()),Session.CurROIData is {} selected?new(selected.Group,selected.Id):null);
        private RoiEditAcceptance Accept(RoiEditStamp expected,RoiEditProposal change)
        {
            var snapshot=Read();if(_disposed||expected!=snapshot.Stamp)return RoiEditAcceptance.Reject(snapshot,"旧预览范围");
            var before=CaptureHistory();RoiEditSelection selection;IReadOnlyList<RoiEditTarget> affected;
            switch(change)
            {
                case RoiEditProposal.Create create:
                    var id=Guid.NewGuid();_rows.Add(new(create.Group,id,"新区域",create.Geometry));
                    affected=new[]{new RoiEditTarget(create.Group,id)};selection=new RoiEditSelection.Select(affected[0]);break;
                case RoiEditProposal.Replace replace:
                    var index=_rows.FindIndex(row=>row.Group==replace.Target.Group&&row.Id==replace.Target.Id&&row.Enabled&&row.Visible);
                    if(index<0)return RoiEditAcceptance.Reject(snapshot,"预览对象不存在");
                    _rows[index]=_rows[index] with{Geometry=replace.Geometry};affected=new[]{replace.Target};selection=new RoiEditSelection.Keep();break;
                case RoiEditProposal.Delete delete:
                    if(delete.Targets.Any(target=>!_rows.Any(row=>row.Group==target.Group&&row.Id==target.Id&&row.Enabled&&row.Visible)))
                        return RoiEditAcceptance.Reject(snapshot,"删除对象不存在或不可编辑");
                    _rows.RemoveAll(row=>delete.Targets.Contains(new(row.Group,row.Id)));affected=delete.Targets;
                    selection=before.Selection is {} old&&delete.Targets.Contains(old)?new RoiEditSelection.Clear():new RoiEditSelection.Keep();break;
                case RoiEditProposal.Undo or RoiEditProposal.Redo:
                    var source=change is RoiEditProposal.Undo?_undo:_redo;var destination=change is RoiEditProposal.Undo?_redo:_undo;
                    if(source.Count==0)return RoiEditAcceptance.Reject(snapshot,"没有可恢复历史");
                    var restore=source.Pop();destination.Push(before);_rows.Clear();_rows.AddRange(restore.Rows);_revision++;
                    var restored=Read();return new(true,restored,RoiEditAcceptance.ChangedTargets(snapshot,restored),restore.Selection is {} target?new RoiEditSelection.Select(target):new RoiEditSelection.Clear());
                default:throw new NotSupportedException("未知区域提案");
            }
            _undo.Push(before);_redo.Clear();_revision++;return new(true,Read(),affected,selection);
        }
        internal void Select()
        {
            if(_disposed||!ReferenceEquals(_scene.Session,Session))throw new InvalidOperationException("Preview session is detached.");
            var row=_rows.ElementAtOrDefault(GetSampleIndex(_scene));
            if(row is null){Session.Select("detect",null);return;}
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
}
