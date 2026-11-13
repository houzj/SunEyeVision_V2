using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Display;

namespace SunEyeVision.UI.Services.Images;

internal sealed partial class ResultWorkspacePresenter
{
    private sealed record RoiPresentation(long Epoch,bool Input,ResultWorkspaceImageSource Source);
    private RoiPresentation? _roiPresentation;
    private long _roiRequest;
    /// <summary>Borrow only the actually selected visible input. ROI never selects, restores or publishes images.</summary>
    internal NativeRoiFrameLease? BorrowRoiFrame(string key,Func<bool> current)
    {
        _shell.Dispatcher.VerifyAccess();var request=++_roiRequest;var epoch=_epoch;var plan=_plan;var input=_shell.IsDoubleImage;
        bool PlanCurrent()=>!_disposed&&!_refreshing&&epoch==_epoch&&ReferenceEquals(plan,_plan)&&plan is not null&&input==_shell.IsDoubleImage;
        bool RequestCurrent()=>request==_roiRequest&&PlanCurrent();
        if(!RequestCurrent()||!current()||!RequestCurrent())
        {
            return null;
        }
        var source=(input?_shell.SelectedInputItem:_shell.SelectedOutputItem) as ResultWorkspaceImageSource;
        ResultImagePane? privatePane=null;
        FrozenImageResource? temporaryVariable=null;
        void DisposePrivate(){privatePane?.Dispose();temporaryVariable?.Dispose();temporaryVariable=null;}
        var temporarySource=false;
        var requested=source?.Key==key?source:plan!.Sources.FirstOrDefault(candidate=>candidate.Key==key);
        if(requested is null&&input&&_main.CaptureVariableImageForRoi(key) is {} captured)
        {
            temporaryVariable=captured.Image;
            requested=new ResultWorkspaceImageSource("变量 · "+captured.Name,key,captured.Image,captured.Current);
        }
        if(source?.Key!=key&&requested is {} requestedSource)
        {
            source=requestedSource;temporarySource=true;
            privatePane=new(new NativeResultViewport());
            // The editor owns this borrowed frame for the whole draft lifetime.
            // A later debug run may replace the formal publication; that must not
            // retire the ROI frame or rebind the draft underneath the user.
            bool SourceCurrent()=>!_disposed&&(temporarySource||current());
            var presented=source.VariableImage is {} variable
                ?privatePane.TryPresentPreview(variable,SourceCurrent,out _,filePreview:false)
                :source.Publication is {} publication&&source.Reference is {} reference
                    &&privatePane.TryPresent(publication,reference,reference.ScopeId!,SourceCurrent,out _);
            if(!presented){DisposePrivate();return null;}
        }
        if(source is not {HasImageValue:true}||source.Key!=key||(!temporarySource&&!plan!.InputSources.Any(candidate=>candidate.Key==key))
            ||(!temporarySource&&!ReferenceEquals(source,input?_inputSource:_outputSource))||(!temporarySource&&!OwnsChoice(source,input)))
        {
            DisposePrivate();return null;
        }
        bool Selected()=>temporarySource?true:ReferenceEquals(source,input?_shell.SelectedInputItem:_shell.SelectedOutputItem)&&ReferenceEquals(source,input?_inputSource:_outputSource);
        bool ActiveProof()=>temporarySource||PlanCurrent()&&Selected()&&(temporarySource||current())&&PlanCurrent()&&Selected()&&(temporarySource||source.Current())&&PlanCurrent()&&Selected();
        bool Proof()=>temporarySource||RequestCurrent()&&ActiveProof()&&RequestCurrent();
        if(!Proof())return null;
        var presentation=new RoiPresentation(epoch,input,source);
        bool ActiveOwner()=>ReferenceEquals(_roiPresentation,presentation)&&(temporarySource||ActiveProof())&&ReferenceEquals(_roiPresentation,presentation);
        bool Owns()=>temporarySource||RequestCurrent()&&ActiveOwner()&&RequestCurrent();
        var previous=_roiPresentation;var transferred=false;
        ResultViewportImageLease? borrowed=null;
        FrozenImageResource? variableBorrow=null;
        try
        {
            _roiPresentation=presentation;
            if(!Owns())return null;
            var pane=temporarySource?privatePane!:(input?_input:_output).Image;
            if(source.VariableImage is {} variable)
            {
                var receipt=pane.CurrentFrame;
                if(receipt is null||!Owns())return null;
                variableBorrow=variable.RetainSnapshot();
                if(receipt.Frame!=variableBorrow.Frame||receipt.Coordinates!=variableBorrow.EffectiveDomain.Frame
                    ||pane.CurrentFrame!=receipt||!Owns())return null;
                var acceptedVariable=variableBorrow;
                // 租约有效性锚定“同一帧仍是该视口的呈现帧”：原件在数据刷新后对既有 ROI
                // 调 UpdateROI 重投影到当前帧（ModuleResultView.xaml.cs:604-608），
                // 不因刷新销毁区域会话；图像对象变化时帧回执改变，租约自然失效。
                var variableLease=new NativeRoiFrameLease(acceptedVariable,pane.Viewport,receipt,
                    ()=>!_disposed&&pane.CurrentFrame==receipt,()=>
                    {try{acceptedVariable.Dispose();}finally{DisposePrivate();if(ReferenceEquals(_roiPresentation,presentation))_roiPresentation=null;}});
                variableBorrow=null;transferred=true;return variableLease;
            }
            if(source.Publication is not {} publication)return null;
            borrowed=pane.BorrowCurrentImage();
            if(borrowed is null)return null;
            if(!Owns()||borrowed.Stamp!=publication.Stamp)return null;
            var accepted=borrowed;
            var lease=new NativeRoiFrameLease(accepted.Image,accepted.Viewport,accepted.Receipt,()=>ActiveOwner(),()=>
            {try{accepted.Dispose();}finally{DisposePrivate();if(ReferenceEquals(_roiPresentation,presentation))_roiPresentation=null;}});
            borrowed=null;transferred=true;return lease;
        }
        catch(Exception failure)
        {
            var failedBorrow=borrowed;borrowed=null;
            try{failedBorrow?.Dispose();DisposePrivate();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;
        }
        finally
        {
            // Only the failed request's independent borrow is released. No old cleanup can clear a new owner or pane.
            try{borrowed?.Dispose();variableBorrow?.Dispose();if(!transferred)DisposePrivate();}finally{if(!transferred&&ReferenceEquals(_roiPresentation,presentation))_roiPresentation=previous;}
        }
    }
    private void RetireRoiPresentation()=>_roiPresentation=null;
}
