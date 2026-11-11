using System.Windows;
using System.Windows.Threading;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Tool.BlobAnalysis.Runtime;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

internal sealed partial class BlobLocalRoiOwner
{
    private NativeRoiInputLease? _previewInputs;
    private NativeResultViewport.ReadOnlyRoiLayer? _previewLayer;
    private DispatcherOperation? _previewRefresh;
    private long _previewRequest,_previewRevision;
    private bool _previewing,_previewAgain;
    private readonly HashSet<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldRow> _previewFields=new();
    internal string PreviewError {get;private set;}="";
    private void InitializePreview()
    {
        _controls.Loaded+=PreviewLoaded;_controls.Unloaded+=PreviewUnloaded;_controls.IsVisibleChanged+=PreviewVisibilityChanged;
        TrackPreviewSources();
        if(_source is not null)_source.Changed+=QueuePreview;
    }
    private void TrackPreviewSources()
    {
        foreach(var field in _inputs.Fields)
        {
            var id=field.FieldId;
            if((id==BlobTool.ImageInput||id==BlobMaskInheritance.SourceId||BlobRoiInheritance.Ports.Any(port=>port.Id==id))
                &&_previewFields.Add(field))
                field.Changed+=QueuePreview;
        }
    }
    private void PreviewLoaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,_controls))QueuePreview();}
    private void PreviewUnloaded(object sender,RoutedEventArgs args){if(ReferenceEquals(args.OriginalSource,_controls))RetireFrame();}
    private void PreviewVisibilityChanged(object sender,DependencyPropertyChangedEventArgs args)
    {if(_controls.IsVisible)QueuePreview();else RetireFrame();}
    private string PreviewMode()=>_controls.IsImageArea?BlobRoiInheritance.Local:_inputs.ReadUnpresentedInput(BlobRoiInheritance.ModeId) switch
    {UnsetSetting=>BlobRoiInheritance.Local,ConstantSetting{Value:string mode}=>BlobRoiInheritance.ValidateMode(mode),_=>throw new InvalidOperationException("ROI继承方式无效。")};
    private void QueuePreview()
    {
        if(_disposed)return;
        // Retire only this read-only layer. Mask session revision, raw inputs and gestures are untouched.
        ClearPreview();
        if(_previewing){_previewAgain=true;return;}
        var request=_previewRequest;
        _previewRefresh=_controls.Dispatcher.BeginInvoke(DispatcherPriority.Background,new Action(()=>
        {
            if(_disposed||request!=_previewRequest)return;_previewRefresh=null;RefreshPreview(request);
        }));
    }
    private void RefreshPreview(long request)
    {
        if(_disposed||_previewing||!_controls.IsLoaded||!_controls.IsVisible)return;
        _previewing=true;NativeRoiInputLease? inputs=null;NativeResultViewport.ReadOnlyRoiLayer? layer=null;
        try
        {
            var mode=PreviewMode();var maskInherited=_controls.IsInheritedMask;
            if(mode==BlobRoiInheritance.Local&&!maskInherited)return;
            if(!FrameCurrent(_generation)){AcquireFrame();if(request!=_previewRequest){if(FrameCurrent(_generation))QueuePreview();return;}}
            var frame=_frame;var generation=_generation;
            bool OwnerCurrent(){var valid=!_disposed&&request==_previewRequest&&generation==_generation&&ReferenceEquals(frame,_frame)&&frame is not null&&FrameCurrent(generation)&&_controls.IsLoaded&&_controls.IsVisible;return valid&&!_disposed&&request==_previewRequest&&generation==_generation&&ReferenceEquals(frame,_frame);}
            if(!OwnerCurrent())throw new InvalidOperationException("请选择与输入一致的已发布图像后预览。");
            var ids=(mode switch
            {
                BlobRoiInheritance.Rectangle=>new[]{BlobRoiInheritance.RectangleSource},
                BlobRoiInheritance.Annulus=>new[]{BlobRoiInheritance.AnnulusSource},
                BlobRoiInheritance.Local=>Array.Empty<string>(),
                BlobRoiInheritance.RectangleParameters=>BlobRoiInheritance.RectangleMembers.Select(member=>BlobRoiInheritance.Leaf(mode,member)).ToArray(),
                _=>BlobRoiInheritance.AnnulusMembers.Select(member=>BlobRoiInheritance.Leaf(mode,member)).ToArray()
            }).Concat(new[]{BlobTool.ImageInput,BlobRoiInheritance.ModeId})
                .Concat(maskInherited?new[]{BlobMaskInheritance.ModeId,BlobMaskInheritance.SourceId}:Array.Empty<string>()).ToArray();
            inputs=_source?.CaptureInputs(ids)??throw new InvalidOperationException("继承来源预览尚未就绪。");
            if(!OwnerCurrent()||!inputs.IsCurrent||!OwnerCurrent())return;
            var image=inputs.Inputs.Get<IReadOnlyImage>(BlobTool.ImageInput);
            if(image.Frame!=frame!.Image.Frame||image.EffectiveDomain.Frame!=frame.Image.EffectiveDomain.Frame)
                throw new InvalidOperationException("继承输入与可见图像不是同一物理帧和坐标域。");
            var root=BlobMaskInheritance.Materialize(inputs.Inputs,BlobRoiInheritance.Materialize(inputs.Inputs,Read()))!;
            var rows=(mode==BlobRoiInheritance.Local?Enumerable.Empty<RoiEditItem>():Items(BlobLocalRoi.Inspection))
                .Concat(maskInherited?Items(BlobLocalRoi.Mask):Enumerable.Empty<RoiEditItem>()).ToArray();
            IEnumerable<RoiEditItem> Items(string group)=>BlobLocalRoi.Items(root,group)
                .Select(item=>new RoiEditItem(group,Guid.ParseExact(item.Get<string>("id"),"N"),item.Get<string>("name"),
                    BlobLocalRoiProjection.ToNative(item.Get<RecordValue>("shape")),Enabled:false,Visible:true));
            if(!OwnerCurrent()||!inputs.IsCurrent||!OwnerCurrent())return;
            var captured=inputs;layer=frame.CreateReadOnlyLayer();
            bool Current()=>OwnerCurrent()&&captured.IsCurrent&&OwnerCurrent();
            if(!layer.Present(frame.Receipt,++_previewRevision,rows,Current)||!Current())return;
            _previewInputs=inputs;inputs=null;_previewLayer=layer;layer=null;
            if(_interaction is null&&_controls.IsLocalDrawing(BlobLocalRoi.Mask)&&BlobLocalRoi.Items(Read(),BlobLocalRoi.Mask).Any(item=>item.Get<bool>("enabled")&&item.Get<bool>("visible")))Activate(BlobLocalRoi.Mask);
        }
        catch(Exception failure)
        {
            if(!_disposed&&request==_previewRequest){PreviewError=failure.Message;_controls.ToolTip=failure.Message;}
        }
        finally
        {
            _previewing=false;
            try{Run(new Action[]{()=>layer?.Dispose(),()=>inputs?.Dispose()});}
            finally{if(_previewAgain&&!_disposed){_previewAgain=false;QueuePreview();}}
        }
    }
    private void ClearPreview()
    {
        ++_previewRequest;var pending=_previewRefresh;_previewRefresh=null;
        var layer=_previewLayer;_previewLayer=null;var inputs=_previewInputs;_previewInputs=null;
        if (PreviewError.Length != 0)
        {
            PreviewError = "";
            _controls.ClearValue(FrameworkElement.ToolTipProperty);
        }
        Run(new Action[]{()=>pending?.Abort(),()=>layer?.Dispose(),()=>inputs?.Dispose()});
    }
    private void DisposePreview()
    {
        _controls.Loaded-=PreviewLoaded;_controls.Unloaded-=PreviewUnloaded;_controls.IsVisibleChanged-=PreviewVisibilityChanged;
        foreach(var field in _previewFields)field.Changed-=QueuePreview;_previewFields.Clear();
        if(_source is not null)_source.Changed-=QueuePreview;ClearPreview();
    }
}
