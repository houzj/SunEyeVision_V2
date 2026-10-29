using System.Runtime.CompilerServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class ROIControlBase
{
    private sealed class GestureSlot {internal RoiGesture? Value;internal long Generation;}
    private static readonly ConditionalWeakTable<ImageViewBox,GestureSlot> Gestures=new();
    private static readonly ConditionalWeakTable<FrameworkElement,GestureSlot> Captures=new();
    private RoiGesture? _roiGesture;
    private long _roiRequest,_roiTemplate;
    private long _applyingRoiTemplate;
    private ControlTemplate? _roiTemplateValue;
    public ROIControlBase()
    {
        Unloaded+=(_,_)=>CancelRoiGesture();
        DataContextChanged+=(_,_)=>CancelRoiGesture();
        IsEnabledChanged+=(_,_)=>{if(!IsEnabled)CancelRoiGesture();};
        IsVisibleChanged+=(_,_)=>{if(!IsVisible)CancelRoiGesture();};
    }
    protected RoiGesture? ActiveRoiGesture=>_roiGesture is {IsLive:true} gesture?gesture:null;
    protected FrameworkElement? RoiInteractionHandle=>ActiveRoiGesture?.Handle;
    protected long RoiRequestVersion=>_roiRequest;
    protected Func<bool> CaptureRoiUpdateProof()
    {
        var request=_roiRequest;var version=_roiTemplate;var template=Template;var context=DataContext;var parent=VisualParent;
        var view=this.GetParent<ImageViewBox>();var generation=view is null?0:GestureGeneration(view);
        return ()=>request==_roiRequest&&version==_roiTemplate&&ReferenceEquals(template,Template)
            &&ReferenceEquals(context,DataContext)&&ReferenceEquals(parent,VisualParent)
            &&ReferenceEquals(view,this.GetParent<ImageViewBox>())&&(view is null||generation==GestureGeneration(view));
    }
    internal static long GestureGeneration(ImageViewBox view)=>Gestures.TryGetValue(view,out var slot)?slot.Generation:0;
    internal Action CaptureGestureCancellation()
    {
        var gesture=ActiveRoiGesture;
        return ()=>{if(gesture is not null)CancelRoiGesture(gesture);};
    }

    protected long BeginRoiTemplate()
    {
        var template=++_roiTemplate;_applyingRoiTemplate=template;_roiTemplateValue=Template;
        try{CancelRoiGesture();return template;}
        catch{EndRoiTemplate(template);throw;}
    }
    protected bool CurrentRoiTemplate(long template)=>template==_roiTemplate&&ReferenceEquals(_roiTemplateValue,Template);
    protected void EndRoiTemplate(long template){if(template==_roiTemplate)_applyingRoiTemplate=0;}

    protected RoiGesture? PrepareRoiGesture(FrameworkElement handle,Action resetLocal,Func<bool>? extraCurrent=null)
    {
        var request=++_roiRequest;var previous=_roiGesture;_roiGesture=null;
        previous?.Cancel();
        if(request!=_roiRequest||_applyingRoiTemplate!=0||!ReferenceEquals(_roiTemplateValue,Template)||!IsLoaded||!IsEnabled||!IsHitTestVisible
            ||!ReferenceEquals(handle.GetParent<ROIControlBase>(),this)||this.GetParent<ImageViewBox>() is not {} view)return null;
        var context=DataContext;var template=Template;var parent=VisualParent;
        RoiGesture? candidate=null;
        bool Current()=>request==_roiRequest&&_applyingRoiTemplate==0&&ReferenceEquals(_roiGesture,candidate)
            &&ReferenceEquals(context,DataContext)&&ReferenceEquals(template,Template)&&ReferenceEquals(parent,VisualParent)
            &&IsLoaded&&IsEnabled&&IsHitTestVisible&&(extraCurrent?.Invoke()!=false);
        candidate=new(this,view,handle,context,Current,resetLocal);
        _roiGesture=candidate;
        try
        {
            candidate.Claim();
            if(candidate.IsCurrent)return candidate;
            candidate.Cancel();return null;
        }
        catch(Exception error)
        {
            try{candidate.Cancel();}
            catch(Exception cleanup){throw new AggregateException(error,cleanup);}
            throw;
        }
    }
    protected void CancelRoiGesture()
    {
        ++_roiRequest;var previous=_roiGesture;_roiGesture=null;previous?.Cancel();
    }
    protected void CancelRoiGesture(RoiGesture gesture)
    {
        if(ReferenceEquals(_roiGesture,gesture)){++_roiRequest;_roiGesture=null;}
        gesture.Cancel();
    }
    protected void FinishRoiGesture(RoiGesture gesture,bool changed)=>gesture.Finish(changed);

    /// <summary>Transient ownership of an original handle. Never a persistent ROI model.</summary>
    protected sealed class RoiGesture
    {
        private readonly ROIControlBase _control;
        private readonly ImageViewBox _view;
        private readonly object? _context;
        private readonly Func<bool> _current;
        private readonly Action _resetLocal;
        private readonly Action? _cancelDraft;
        private readonly List<Action> _detach=new();
        private bool _ending,_ended,_draftCancelled;
        internal RoiGesture(ROIControlBase control,ImageViewBox view,FrameworkElement handle,object? context,Func<bool> current,Action resetLocal)
        {_control=control;_view=view;Handle=handle;_context=context;_current=current;_resetLocal=resetLocal;_cancelDraft=view.NativeRoiCaptureCancelEdit?.Invoke(context);}
        public FrameworkElement Handle {get;}
        public bool IsLive=>!_ending&&!_ended;
        public bool IsCurrent=>!_ended&&Gestures.TryGetValue(_view,out var slot)&&ReferenceEquals(slot.Value,this)&&_current();
        public bool IsActive=>IsLive&&IsCurrent&&Captures.TryGetValue(Handle,out var capture)&&ReferenceEquals(capture.Value,this)&&ReferenceEquals(Mouse.Captured,Handle);
        internal void Claim()
        {
            var slot=Gestures.GetValue(_view,_=>new());var previous=slot.Value;slot.Generation++;slot.Value=this;
            previous?.Cancel();
        }
        public bool Capture()
        {
            if(!IsCurrent)return false;
            var slot=Captures.GetValue(Handle,_=>new());slot.Value=this;
            return Handle.CaptureMouse()&&IsCurrent&&ReferenceEquals(slot.Value,this)&&ReferenceEquals(Mouse.Captured,Handle);
        }
        public bool Attach(MouseEventHandler move,MouseButtonEventHandler up,MouseWheelEventHandler? wheel=null)
        {
            if(!IsActive)return false;
            MouseEventHandler guardedMove=(sender,args)=>Invoke(()=>move(sender,args));
            MouseButtonEventHandler guardedUp=(sender,args)=>Invoke(()=>up(sender,args));
            _detach.Add(()=>Handle.MouseMove-=guardedMove);Handle.MouseMove+=guardedMove;
            _detach.Add(()=>Handle.MouseLeftButtonUp-=guardedUp);Handle.MouseLeftButtonUp+=guardedUp;
            if(wheel is not null)
            {
                MouseWheelEventHandler guardedWheel=(sender,args)=>Invoke(()=>wheel(sender,args));
                _detach.Add(()=>Handle.MouseWheel-=guardedWheel);Handle.MouseWheel+=guardedWheel;
            }
            _view.IsDragROI=true;
            return IsActive;
        }
        private void Invoke(Action callback)
        {
            if(!IsActive)return;
            try{callback();}
            catch(Exception error)
            {
                try{_control.CancelRoiGesture(this);}
                catch(Exception cleanup){throw new AggregateException(error,cleanup);}
                throw;
            }
        }
        private void ReleaseCapture()
        {
            if(!Captures.TryGetValue(Handle,out var slot)||!ReferenceEquals(slot.Value,this))return;
            slot.Value=null;
            if(ReferenceEquals(Mouse.Captured,Handle))Handle.ReleaseMouseCapture();
        }
        private void Complete()
        {
            _ended=true;
            if(!Gestures.TryGetValue(_view,out var slot)||!ReferenceEquals(slot.Value,this))return;
            slot.Value=null;_view.IsDragROI=false;
        }
        public void Cancel()=>End(false);
        public void Finish(bool changed){if(IsActive)End(changed);else End(false);}
        private void CancelDraft()
        {
            if(_draftCancelled)return;
            _draftCancelled=true;_cancelDraft?.Invoke();
        }
        private void End(bool commit)
        {
            if(_ended)return;
            if(_ending){ParameterCleanup.Run(new Action[]{CancelDraft,Complete});return;}
            _ending=true;
            var detach=_detach.ToArray();_detach.Clear();
            var command=_control.DataChangedCommand;
            var released=false;var committed=false;
            ParameterCleanup.Run(new Action[]{()=>{if(_control._roiGesture is null||ReferenceEquals(_control._roiGesture,this))_resetLocal();},
                ()=>ParameterCleanup.Run(detach),()=>{if(!commit)CancelDraft();},()=>{ReleaseCapture();released=true;},
                ()=>{if(commit&&released&&IsCurrent){command?.Execute(_context);committed=command is not null;if(IsCurrent)_view.NotifyROIParamChanged(_context,()=>IsCurrent);}},
                ()=>{if(!committed)CancelDraft();},Complete});
        }
    }
}
