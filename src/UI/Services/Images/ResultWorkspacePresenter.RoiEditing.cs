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
    internal NativeRoiFrameLease? BorrowRoiFrame(string key,bool interactive,Func<bool> current)
    {
        _shell.Dispatcher.VerifyAccess();var request=++_roiRequest;var epoch=_epoch;var plan=_plan;var input=_shell.IsDoubleImage;
        bool PlanCurrent()=>!_disposed&&!_refreshing&&epoch==_epoch&&ReferenceEquals(plan,_plan)&&plan is not null&&input==_shell.IsDoubleImage;
        bool RequestCurrent()=>request==_roiRequest&&PlanCurrent();
        if(!RequestCurrent()||!current()||!RequestCurrent())
        {
            return null;
        }
        var source=(input?_shell.SelectedInputItem:_shell.SelectedOutputItem) as ResultWorkspaceImageSource;
        // P1（原件 IsCanDrawROI+RoiDrawTips 语义）：绘制请求时显示图必须是模块的输入图，
        // 否则阻断借帧并给出原因（不自动切换显示——原厂由用户自行切换，OnRelatedIOChanged
        // 只随输入绑定变化切换）；预览等非交互借帧不受影响。
        var displayed=(input?_shell.SelectedInputItem:_shell.SelectedOutputItem) as ResultWorkspaceImageSource;
        if(interactive&&displayed?.Key!=key)
        {
            _main.RoiDrawBlockReason="请先将单图显示切换到该模块的输入图像后再绘制区域。";
            return null;
        }
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
            // P2（原件 UpdateROI 语义，ModuleResultView.xaml.cs.pending:604-608）：租约货币
            // 锚定"同一回执仍是该视口的呈现帧"，与变量路径一致——回执全等含 Owner/Epoch/
            // Frame/Coordinates，视口重绑定（执行/刷新后 BindImage epoch+1）即失效，
            // 由 BlobLocalRoiOwner.RefreshFrame 自动重建会话把既有形状重投影到新呈现，
            // 不再锚定刷新代次（epoch），避免绘制中途会话反复死亡（roibtn.log 实锤）。
            // 不能只比 Frame 身份：BeginRoiEditing 校验完整回执 Current(receipt)，
            // 只比帧会让"活租约撞失效回执"在点击绘制时抛不一致异常。
            var lease=new NativeRoiFrameLease(accepted.Image,accepted.Viewport,accepted.Receipt,
                ()=>!_disposed&&pane.CurrentFrame==accepted.Receipt,()=>
                {try{accepted.Dispose();}finally{DisposePrivate();if(ReferenceEquals(_roiPresentation,presentation))_roiPresentation=null;}});
            borrowed=null;transferred=true;_main.RoiDrawBlockReason=null;return lease;
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
