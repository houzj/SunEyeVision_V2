using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;

namespace SunEyeVision.UI.Services.Images;

internal sealed partial class ResultWorkspacePresenter
{
    private sealed record RoiPresentation(long Epoch,bool Input,ResultWorkspaceImageSource Source);
    private RoiPresentation? _roiPresentation;
    private long _roiRequest;
    /// <summary>Borrow only the actually selected visible input. ROI never selects, restores or publishes images.</summary>
    internal NativeRoiFrameLease? BorrowRoiFrame(OutputReference reference,Func<bool> current)
    {
        _shell.Dispatcher.VerifyAccess();var request=++_roiRequest;var epoch=_epoch;var plan=_plan;var input=_shell.IsDoubleImage;
        bool PlanCurrent()=>!_disposed&&!_refreshing&&epoch==_epoch&&ReferenceEquals(plan,_plan)&&plan is not null&&input==_shell.IsDoubleImage;
        bool RequestCurrent()=>request==_roiRequest&&PlanCurrent();
        if(!RequestCurrent()||!current()||!RequestCurrent())return null;
        var key=SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Serialize(reference);
        var source=(input?_shell.SelectedInputItem:_shell.SelectedOutputItem) as ResultWorkspaceImageSource;
        if(source is null||source.Key!=key||!plan!.InputSources.Any(candidate=>candidate.Key==key)
            ||!ReferenceEquals(source,input?_inputSource:_outputSource)||!OwnsChoice(source,input)||source.Publication is not {} publication)return null;
        bool Selected()=>ReferenceEquals(source,input?_shell.SelectedInputItem:_shell.SelectedOutputItem)&&ReferenceEquals(source,input?_inputSource:_outputSource);
        bool ActiveProof()=>PlanCurrent()&&Selected()&&current()&&PlanCurrent()&&Selected()&&source.Current()&&PlanCurrent()&&Selected();
        bool Proof()=>RequestCurrent()&&ActiveProof()&&RequestCurrent();
        if(!Proof())return null;
        var presentation=new RoiPresentation(epoch,input,source);
        bool ActiveOwner()=>ReferenceEquals(_roiPresentation,presentation)&&ActiveProof()&&ReferenceEquals(_roiPresentation,presentation);
        bool Owns()=>RequestCurrent()&&ActiveOwner()&&RequestCurrent();
        var previous=_roiPresentation;var transferred=false;
        ResultViewportImageLease? borrowed=null;
        try
        {
            _roiPresentation=presentation;
            if(!Owns())return null;
            borrowed=(input?_input:_output).Image.BorrowCurrentImage();
            if(borrowed is null||!Owns()||borrowed.Stamp!=publication.Stamp||!Owns())return null;
            var accepted=borrowed;
            var lease=new NativeRoiFrameLease(accepted.Image,accepted.Viewport,accepted.Receipt,()=>ActiveOwner()&&accepted.IsCurrent&&ActiveOwner(),()=>
            {try{accepted.Dispose();}finally{if(ReferenceEquals(_roiPresentation,presentation))_roiPresentation=null;}});
            borrowed=null;transferred=true;return lease;
        }
        catch(Exception failure)
        {
            var failedBorrow=borrowed;borrowed=null;
            try{failedBorrow?.Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;
        }
        finally
        {
            // Only the failed request's independent borrow is released. No old cleanup can clear a new owner or pane.
            try{borrowed?.Dispose();}finally{if(!transferred&&ReferenceEquals(_roiPresentation,presentation))_roiPresentation=previous;}
        }
    }
    private void RetireRoiPresentation()=>_roiPresentation=null;
}
