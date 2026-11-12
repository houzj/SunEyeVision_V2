using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Controls.Native.Composition.Results.ResultShell;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Results;
using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Services.Images;

/// <summary>Owns the two original Shell image views; result tables retain their independent publication.</summary>
internal sealed partial class ResultWorkspacePresenter:IDisposable
{
    private sealed class Pane:IDisposable
    {
        internal long Generation;
        internal readonly ResultImagePane Image;
        internal readonly NativeResultViewport.GeometryLayer Selection;
        internal DisplayScene? Display;
        internal void ClearDisplay(){var previous=Display;Display=null;previous?.Dispose();}
        private NativeResultViewport.CrosshairLayer? _cross;
        internal NativeResultViewport.CrosshairLayer Cross=>_cross??=Image.Viewport.CreateCrosshairLayer();
        internal Pane(FrameworkElement view)
        {
            Image=new(view);
            try{Selection=Image.Viewport.CreateSelectionLayer();}
            catch{Image.Dispose();throw;}
        }
        internal void ClearGraphics()=>Selection.Clear();
        public void Dispose()=>Run(new Action[]{ClearDisplay,ClearGraphics,()=>_cross?.Dispose(),Selection.Dispose,Image.Dispose});
    }
    private readonly MainWindowViewModel _main;
    private readonly ResultWorkspaceShell _shell;
    private readonly Pane _input,_output;
    private readonly DependencyPropertyDescriptor _inputSelection,_outputSelection,_doubleImage;
    private ResultWorkspaceImagePlan? _plan;
    private CancellationTokenSource? _previewCancellation;
    private ResultViewportFullscreenScope? _fullscreen;
    private long _epoch,_layerRevision,_selectionGeneration,_outputChoiceRevision;
    private bool _disposed,_refreshing,_refreshAgain,_refreshQueued,_restoringInput,_restoringOutput;
    private string? _inputKey,_outputKey;
    private string? _editorInputDisplayKey;
    private string? _previewPath;
    private string? _variableOutputKey;
    private ResultWorkspaceImageSource? _inputSource,_outputSource;
    private object? _selectionOwner;
    private ResultSelection? _sceneSelection;
    private IResultPublication? _displayDefinitionNotice;
    internal ResultImagePane InputPane=>_input.Image;
    internal ResultImagePane OutputPane=>_output.Image;
    internal ResultWorkspacePresenter(MainWindowViewModel main,ResultWorkspaceShell shell)
    {
        _main=main;_shell=shell;
        _input=new(shell.InputViewport);
        try{_output=new(shell.OutputViewport);}catch{_input.Dispose();throw;}
        _inputSelection=DependencyPropertyDescriptor.FromProperty(ResultWorkspaceShell.SelectedInputItemProperty,typeof(ResultWorkspaceShell));
        _outputSelection=DependencyPropertyDescriptor.FromProperty(ResultWorkspaceShell.SelectedOutputItemProperty,typeof(ResultWorkspaceShell));
        _doubleImage=DependencyPropertyDescriptor.FromProperty(ResultWorkspaceShell.IsDoubleImageProperty,typeof(ResultWorkspaceShell));
        try
        {
        _inputSelection.AddValueChanged(shell,InputSelected);_outputSelection.AddValueChanged(shell,OutputSelected);
        _doubleImage.AddValueChanged(shell,DoubleImageChanged);
        main.ResultWorkspaceChanged+=Refresh;main.ResultWorkspaceSelectionChanged+=Highlight;
        main.ResultDisplayChanged+=ResultDisplayChanged;
        main.ResultWorkspaceGraphicsCleared+=ClearGraphics;
        main.SourceDisplayPreservationPreparing+=PrepareSourceDisplayPreservation;
        main.ResultWorkspaceRootSettled+=RootSettled;
        shell.FullscreenCommand=new SunEyeVision.UI.Core.RelayCommand(value=>
        {
            if(value is FrameworkElement view&&Window.GetWindow(shell) is {} owner)
            {
                _fullscreen?.Dispose();_fullscreen=ResultViewportFullscreenScope.Open(view,owner,shell.ImageNavigationControls);
            }
        },value=>!_disposed&&value is FrameworkElement view&&(ReferenceEquals(view,shell.InputViewport)||ReferenceEquals(view,shell.OutputViewport)));
        shell.SelectOverlayCommand=new SunEyeVision.UI.Core.RelayCommand(ToggleOverlay,CanToggleOverlay);
        shell.SelectBackgroundCommand=new SunEyeVision.UI.Core.RelayCommand(SwapBackground,CanSwapBackground);
        ((SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.ImageViewBox)_output.Image.Viewport.View).OnFreshExtendContent+=OnOverlayViewportChanged;
        shell.CrossLinesCommand=new SunEyeVision.UI.Core.RelayCommand(()=>
        {
            if(Window.GetWindow(shell) is {} owner)_output.Cross.OpenSettings(owner);
        },()=>!_disposed&&Window.GetWindow(shell)?.IsVisible==true);
        Refresh();
        }
        catch(Exception failure)
        {
            try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    private void DoubleImageChanged(object? sender,EventArgs args)
    {
        try
        {
        if(_disposed)return;
        RetireRoiPresentation();
        // Original OnIsShowDoubleImageChanged clears the old input then selects the first input on entry.
        _shell.SelectedInputItem=null;
        if(!_disposed&&_shell.IsDoubleImage)
            _shell.SelectedInputItem=_plan?.InputSources.FirstOrDefault();

        }
        finally{if(!_disposed)_main.NotifyRoiFrameAvailabilityChanged();}
    }
    private void InputSelected(object? sender,EventArgs args)
    {
        var accepted=false;
        try
        {
        if(_disposed||_restoringInput)return;
        if(_refreshing&&_plan is null)return;
        var source=_shell.SelectedInputItem as ResultWorkspaceImageSource;
        if(_shell.SelectedInputItem is not null&&(source is null||!OwnsChoice(source,true)))
        {RestoreChoice(true);return;}
        accepted=true;
        if(_roiPresentation?.Input==true)RetireRoiPresentation();
        _inputSource=source;_inputKey=source?.Key;
        if(_refreshing)return;
        Present(_input,source);ReportDisplayDefinitionGap();

        }
        finally{if(accepted&&!_disposed&&_shell.IsDoubleImage)_main.NotifyRoiFrameAvailabilityChanged();}
    }
    private void OutputSelected(object? sender,EventArgs args)
    {
        var accepted=false;
        try
        {
        if(_disposed||_restoringOutput)return;
        if(_refreshing&&_plan is null)return;
        var source=_shell.SelectedOutputItem as ResultWorkspaceImageSource;
        if(_shell.SelectedOutputItem is not null&&(source is null||!OwnsChoice(source,false)))
        {RestoreChoice(false);return;}
        accepted=true;
        if(_roiPresentation?.Input==false)RetireRoiPresentation();
        _outputSource=source;_outputKey=source?.Key;var choiceRevision=++_outputChoiceRevision;
        var choicePlan=_plan;var choiceEpoch=_epoch;
        if(choicePlan is not null)
            foreach(var item in choicePlan.Sources.ToArray())
            {
                if(_disposed||choiceEpoch!=_epoch||!ReferenceEquals(choicePlan,_plan)||choiceRevision!=_outputChoiceRevision)return;
                item.IsCheck=IsCheckedOutput(item,source);
                if(_disposed||choiceEpoch!=_epoch||!ReferenceEquals(choicePlan,_plan)||choiceRevision!=_outputChoiceRevision)return;
                item.IsBackgroundImage=item.Key==_backgroundKey;
            }
        if(_disposed||choiceEpoch!=_epoch||!ReferenceEquals(choicePlan,_plan)||choiceRevision!=_outputChoiceRevision)return;
        if(_refreshing)return;
        if(sender is not null)
        {
            _editorInputDisplayKey=null;
            _variableOutputKey=source?.VariableImage is not null?source.Key:null;
            if(_variableOutputKey is null&&_main.SelectedNode is {} selected)
                _main.SetFormalDisplaySelection(selected,source?.Key);
        }
        if(_disposed||choiceEpoch!=_epoch||!ReferenceEquals(choicePlan,_plan)||choiceRevision!=_outputChoiceRevision)return;
        var epoch=_epoch;var plan=_plan;
        _previewCancellation?.Cancel();
        Present(_output,source);RefreshOverlayLayers();ReportDisplayDefinitionGap();

        }
        finally{if(accepted&&!_disposed&&!_shell.IsDoubleImage)_main.NotifyRoiFrameAvailabilityChanged();}
    }
    private bool OwnsChoice(ResultWorkspaceImageSource source,bool input)
    {
        if(_disposed||_plan is null||_plan.IsDisposed)return false;
        foreach(var candidate in input?_plan.InputSources:_plan.Sources)if(ReferenceEquals(candidate,source))return true;
        return false;
    }
    private void RestoreChoice(bool input)
    {
        var epoch=_epoch;var plan=_plan;
        var accepted=input?_inputSource:_outputSource;
        if(input)_restoringInput=true;else _restoringOutput=true;
        try
        {
            _shell.SetCurrentValue(input?ResultWorkspaceShell.SelectedInputItemProperty:ResultWorkspaceShell.SelectedOutputItemProperty,accepted);
        }
        finally{if(input)_restoringInput=false;else _restoringOutput=false;}
        if(_disposed||epoch!=_epoch||!ReferenceEquals(plan,_plan))return;
        // A synchronous observer can select a genuinely new current item while the
        // obsolete value is restored. Honour that intent, never replay the old item.
        var current=input?_shell.SelectedInputItem:_shell.SelectedOutputItem;
        if(!ReferenceEquals(current,accepted)&&(current is null||current is ResultWorkspaceImageSource source&&OwnsChoice(source,input)))
        {if(input)InputSelected(null,EventArgs.Empty);else OutputSelected(_shell,EventArgs.Empty);}
    }
    private void Present(Pane pane,ResultWorkspaceImageSource? source)
    {
        var plan=_plan;var epoch=_epoch;var paneGeneration=++pane.Generation;
        ++_selectionGeneration;
        bool Current()=>!_disposed&&epoch==_epoch&&ReferenceEquals(_plan,plan)&&pane.Generation==paneGeneration;
        if(!Current())return;
        pane.ClearDisplay();
        if(!Current())return;
        if(source is null||source.Publication is null&&source.VariableImage is null){pane.Image.ClearUnlessRetained();return;}
        if(!source.HasImageValue){pane.Image.Clear();return;}
        bool SourceCurrent()=>Current()&&source.Current()&&Current();
        var preserveShapeLayers=ReferenceEquals(pane,_output);
        var presented=source.VariableImage is {} variable
            ? pane.Image.TryPresentPreview(variable,SourceCurrent,out var error,filePreview:false,preserveShapeLayers:preserveShapeLayers)
            : pane.Image.TryPresent(source.Publication!,source.Reference!,source.Reference!.ScopeId!,SourceCurrent,out error,preserveShapeLayers);
        if(!presented)
            _main.AddLog("图像来源未显示："+source.DisplayName+"，"+error);
        else if(Current())RefreshImageName(pane,source);
        if(Current())QueueResultDisplay();
    }
    private static void SetImageName(Pane pane,string? name)
        =>pane.Image.Viewport.View.SetCurrentValue(
            SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.ImageViewBox.ImageNameProperty,name);
    private void RefreshImageName(Pane pane,ResultWorkspaceImageSource? source)
    {
        if(pane.Image.CurrentFrame is null)return;
        var name=_main.ShowLocalImageName
            ?pane.Image.IsFilePreview?System.IO.Path.GetFileName(_main.NativePreviewPath)
                :source is {IsUpstream:false}?source.FileName:null
            :null;
        SetImageName(pane,string.IsNullOrEmpty(name)?null:name);
    }
    private void ResultDisplayChanged()
    {
        if(_disposed)return;
        RefreshImageName(_input,_inputSource);
        if(_disposed)return;
        RefreshImageName(_output,_outputSource);
        QueueResultDisplay();
    }
    private void PrepareSourceDisplayPreservation(SunEyeVision.UI.Shared.ImageInputSource source,SourceDisplayPreservationScope scope)
    {
        if(_disposed||!ReferenceEquals(_main.ActiveInputSource,source))return;
        bool Current()=>!_disposed&&scope.IsCurrent;
        if(_input.Image.PrepareDisplayRetention(Current) is {} input)scope.Enlist(input);
        if(_output.Image.PrepareDisplayRetention(Current) is {} output)scope.Enlist(output);
    }
    private void RootSettled(string workflowId)
    {
        if(_disposed||_main.WorkflowTabViewModel.SelectedTab?.RootWorkflowId!=workflowId)return;
        Run(new Action[]{_input.Image.ClearRetained,_output.Image.ClearRetained});
    }
    internal void Refresh()
    {
        if(_disposed)return;
        RetireRoiPresentation();
        if(_refreshing){_refreshAgain=true;return;}
        _refreshing=true;
        try
        {
            do
            {
                _refreshAgain=false;
                var previous=_plan;
                // 捕获的 currency 只挂 _disposed：重入由 _refreshing 排除，_epoch 仅由本方法重建
                // 路径与 Dispose 触碰；plan 的来源闭包必须跨过重建路径的 epoch 自增仍然有效。
                var plan=_main.CaptureNativeImageSources(()=>!_disposed);
                if(_disposed){plan.Dispose();continue;}
                // 原件按数据变化更新（VMControls.WPF.ModuleResultView：图像仅在对象变化时经
                // ImageDataSource 守卫重设，参数窗口开合只切 ROI 可见性，证据见
                // docs/collaboration/evidence/VM调试窗口结果视口更新路径-20261112.json）：
                // 重建计划与当前显示完全一致时不动视口，避免“清空→重设同一张图”的一帧闪烁
                // （如双击打开调试窗口附加编辑上下文）。
                if(DisplayUnchanged(previous,plan)){plan.Dispose();continue;}
                var epoch=++_epoch;
                ++_input.Generation;++_output.Generation;++_selectionGeneration;
                _previewCancellation?.Cancel();
                var old=previous;_plan=null;_inputSource=null;_outputSource=null;_previewPath=null;
                Run(new Action[]{_input.Image.ClearUnlessRetained,_output.Image.ClearUnlessRetained,ClearGraphics,()=>old?.Dispose()});
                if(_disposed)return;
                if(!ReferenceEquals(_selectionOwner,_main.SelectedNode))
                {_selectionOwner=_main.SelectedNode;_inputKey=null;_outputKey=_main.SelectedNode?.SelectedDisplayReference;
                    _variableOutputKey=null;ResetOverlayChoice();}
                ReadOverlayChoice(_main.SelectedNode?.DisplayOverlays);
                _plan=plan;
                var inputs=plan.InputSources.ToArray();
                _shell.InputItems=inputs;_shell.OutputItems=plan.Sources;
                var sameEditor=plan.IsPrivateDebug&&old is not null&&old.EditorScopeId==plan.EditorScopeId;
                var relatedImageChanged=sameEditor&&old!.PrimaryInputKey!=plan.PrimaryInputKey;
                var inputKey=relatedImageChanged?plan.PrimaryInputKey:_inputKey;
                var input=_shell.IsDoubleImage?inputs.FirstOrDefault(source=>source.Key==inputKey)??inputs.FirstOrDefault():null;
                var byKey=plan.Sources.FirstOrDefault(source=>source.Key==_outputKey);
                if (!sameEditor)
                    _editorInputDisplayKey = null;
                else if (!_shell.IsDoubleImage && relatedImageChanged)
                    _editorInputDisplayKey = plan.PrimaryInputKey;
                var editedInput = _editorInputDisplayKey is { } editedKey
                    ? plan.Sources.FirstOrDefault(source => source.Key == editedKey) : null;
                if(old?.EditorScopeId!=plan.EditorScopeId)_variableOutputKey=null;
                var variableOutput=_variableOutputKey is {} variableKey
                    ?plan.Sources.FirstOrDefault(source=>source.Key==variableKey):null;
                // A persisted explicit identity must match exactly, including scope and members.
                // Missing or stale declarations never silently select a different image.
                var explicitChoice=_main.SelectedNode?.SelectedDisplayReference;
                var useDefault=plan.IsPrivateDebug||old?.IsPrivateDebug==true;
                var output=editedInput??variableOutput??(explicitChoice is not null
                    ?plan.Sources.FirstOrDefault(source=>source.Key==explicitChoice)
                    :useDefault?plan.DefaultOutput:byKey??plan.DefaultOutput);
                if(explicitChoice is not null&&editedInput is null&&output is null)plan.Diagnostics.Add("已选择的图像来源不存在或声明已变更，请重新选择。");
                _inputSource=input;_outputSource=output;
                var outputChoiceRevision=_outputChoiceRevision;
                _shell.SelectedInputItem=input;
                // An input observer may already have accepted a newer output choice.
                if(outputChoiceRevision==_outputChoiceRevision)_shell.SelectedOutputItem=output;
                // Setters can synchronously replace a choice. Reconcile the actual
                // values rather than replaying the pre-setter local candidates.
                InputSelected(null,EventArgs.Empty);OutputSelected(null,EventArgs.Empty);
                if(plan.Diagnostics.Count>0)_main.AddLog(string.Join("；",plan.Diagnostics));
            }while(_refreshAgain&&!_disposed);
        }
        catch(Exception error){_main.AddLog("结果工作区刷新失败："+error.Message);}
        finally
        {
            _refreshing=false;
            try
            {
            if(!_disposed&&!_refreshAgain&&_plan is not null)
            {
                var epoch=_epoch;var plan=_plan;
                InputSelected(null,EventArgs.Empty);
                if(!_disposed&&epoch==_epoch&&ReferenceEquals(plan,_plan))
                {
                    OutputSelected(null,EventArgs.Empty);
                    if(!_disposed&&epoch==_epoch&&ReferenceEquals(plan,_plan)
                        &&_main.NativePreviewPath is {} path
                        &&!string.Equals(path,_previewPath,StringComparison.Ordinal))_=PresentPreview(path,epoch);
                }
            }
            }
            catch(Exception error){_main.AddLog("结果工作区选择刷新失败："+error.Message);}
            finally
            {
            // An exception must not discard a newer notification received while the
            // previous plan was clearing/capturing. Retry only a genuinely queued intent.
            if(_refreshAgain&&!_disposed&&!_refreshQueued)
            {
                _refreshQueued=true;
                _shell.Dispatcher.BeginInvoke(new Action(()=>
                {
                    _refreshQueued=false;
                    if(!_disposed)Refresh();
                }));
            }
            }
        }
    }
    /// <summary>判定重建计划与当前显示是否完全一致：一致时不动视口（原件按数据变化更新，不清空重画）。</summary>
    private bool DisplayUnchanged(ResultWorkspaceImagePlan? previous,ResultWorkspaceImagePlan plan)
    {
        if(previous is null||previous.IsDisposed)return false;
        if(!ReferenceEquals(_selectionOwner,_main.SelectedNode))return false;
        if(!OverlayChoiceMatches(_main.SelectedNode?.DisplayOverlays))return false;
        if(!SourcesUnchanged(previous.InputSources,plan.InputSources))return false;
        if(!SourcesUnchanged(previous.Sources,plan.Sources))return false;
        // 输入/输出选择按与重建路径完全相同的规则只读解析，再与当前显示比较；
        // 状态重置（编辑作用域、主输入变化）只有在会改变解析结果时才触发重建。
        var sameEditor=plan.IsPrivateDebug&&previous.EditorScopeId==plan.EditorScopeId;
        var relatedImageChanged=sameEditor&&previous.PrimaryInputKey!=plan.PrimaryInputKey;
        var inputKey=relatedImageChanged?plan.PrimaryInputKey:_inputKey;
        var input=_shell.IsDoubleImage?plan.InputSources.FirstOrDefault(source=>source.Key==inputKey)??plan.InputSources.FirstOrDefault():null;
        var editedKey=!sameEditor?null
            :!_shell.IsDoubleImage&&relatedImageChanged?plan.PrimaryInputKey
            :_editorInputDisplayKey;
        var editedInput=editedKey is { } edited?plan.Sources.FirstOrDefault(source=>source.Key==edited):null;
        var variableOutput=previous.EditorScopeId==plan.EditorScopeId&&_variableOutputKey is { } variableKey
            ?plan.Sources.FirstOrDefault(source=>source.Key==variableKey):null;
        var explicitChoice=_main.SelectedNode?.SelectedDisplayReference;
        var useDefault=plan.IsPrivateDebug||previous.IsPrivateDebug;
        var byKey=plan.Sources.FirstOrDefault(source=>source.Key==_outputKey);
        var output=editedInput??variableOutput??(explicitChoice is not null
            ?plan.Sources.FirstOrDefault(source=>source.Key==explicitChoice)
            :useDefault?plan.DefaultOutput:byKey??plan.DefaultOutput);
        return SameChoice(_inputSource,input)&&SameChoice(_outputSource,output)
            &&SameChoiceHasImage(_inputSource,input)&&SameChoiceHasImage(_outputSource,output);
    }
    private static bool SameChoice(ResultWorkspaceImageSource? presented,ResultWorkspaceImageSource? candidate)
        =>presented is null?candidate is null:candidate is not null&&presented.Key==candidate.Key;
    private static bool SameChoiceHasImage(ResultWorkspaceImageSource? presented,ResultWorkspaceImageSource? candidate)
        =>presented is null||candidate is null?presented is null&&candidate is null
            :presented.HasImageValue==candidate.HasImageValue;
    private static bool SourcesUnchanged(System.Collections.Generic.IReadOnlyList<ResultWorkspaceImageSource> previous,
        System.Collections.Generic.IReadOnlyList<ResultWorkspaceImageSource> current)
    {
        if(previous.Count!=current.Count)return false;
        for(var index=0;index<previous.Count;index++)
        {
            if(!string.Equals(previous[index].Key,current[index].Key,StringComparison.Ordinal))return false;
            if(!string.Equals(previous[index].DisplayName,current[index].DisplayName,StringComparison.Ordinal))return false;
            // publication 每次捕获都重建实例，用快照运行身份判定“同一份结果”。
            if(!Nullable.Equals(previous[index].Publication?.Stamp.Invocation,current[index].Publication?.Stamp.Invocation))return false;
            if(!ReferenceEquals(previous[index].VariableImage,current[index].VariableImage))return false;
            // 同一运行身份下图像值有无变化（如掩膜清空/恢复）必须照常重建。
            if(previous[index].HasImageValue!=current[index].HasImageValue)return false;
        }
        return true;
    }
    private async Task PresentPreview(string path,long epoch)
    {
        var cancellation=new CancellationTokenSource();var token=cancellation.Token;_previewCancellation=cancellation;
        var paneGeneration=++_output.Generation;++_selectionGeneration;
        var openingStatus=_main.StatusText;
        try
        {
            using var decoded=await LocalResultImagePreview.LoadAsync(path,token);
            bool Current()=>!_disposed&&!token.IsCancellationRequested&&epoch==_epoch&&_output.Generation==paneGeneration&&_main.NativePreviewPath==path;
            if(!Current())return;
            if(!_output.Image.TryPresentPreview(decoded.Image,Current,out var error))_main.AddLog("文件预览未显示："+error);
            else if(Current())
            {
                _previewPath=path;
                RefreshImageName(_output,null);
                if(_main.StatusText==openingStatus&&!openingStatus.StartsWith("图像源配置已接纳，",StringComparison.Ordinal))
                    _main.StatusText="文件预览："+decoded.Image.Width+" × "+decoded.Image.Height;
            }
        }
        catch(OperationCanceledException){}
        catch(Exception error){if(!_disposed&&epoch==_epoch)_main.AddLog("文件预览失败："+error.Message);}
        finally
        {
            if(ReferenceEquals(_previewCancellation,cancellation))_previewCancellation=null;
            cancellation.Dispose();
        }
    }
    internal void ClearGraphics()
    {
        var generation=++_selectionGeneration;var epoch=_epoch;
        bool Current()=>!_disposed&&generation==_selectionGeneration&&epoch==_epoch;
        Run(new Action[]{()=>{if(Current())_input.ClearGraphics();},()=>{if(Current())_output.ClearGraphics();}});
    }
    private void ReportDisplayDefinitionGap()
    {
        var publication=_main.CurrentResultPublication;
        if(publication is not null&&_main.CaptureCurrentResultDisplay(publication) is not null)return;
        if(publication is null||!publication.IsCurrent()||ReferenceEquals(publication,_displayDefinitionNotice))return;
        _displayDefinitionNotice=publication;
        _main.AddLog("当前结果没有正式图形或文字显示声明。");
    }
    private void Highlight(ResultSelection? selection)
    {
        var publication=_main.CurrentResultPublication;var epoch=_epoch;
        if(selection is not null&&(publication is null||selection.Publication!=publication.Stamp||!publication.IsCurrent()))return;
        var generation=++_selectionGeneration;
        bool LocalCurrent()=>!_disposed&&epoch==_epoch&&generation==_selectionGeneration
            &&ReferenceEquals(_main.CurrentResultPublication,publication);
        bool Current()=>LocalCurrent()&&(publication?.IsCurrent()!=false)&&LocalCurrent();
        bool ClearCurrent()=>selection is null?LocalCurrent():Current();
        Run(new Action[]{()=>{if(ClearCurrent())_input.Selection.Clear();},()=>{if(ClearCurrent())_output.Selection.Clear();}});
        if(!ClearCurrent())return;
        var sceneSelection = selection?.ElementIdentity is long && publication is not null &&
            _main.CaptureCurrentResultDisplay(publication)?.Descriptor.SelectedRectangleColor is not null;
        if(!ClearCurrent())return;
        if(sceneSelection || _sceneSelection is not null)
        {
            _sceneSelection = sceneSelection ? selection : null;
            QueueResultDisplay();
        }
        if(sceneSelection)return;
        if(selection is null||publication is null||!Current())return;
        var displayed=false;
        foreach(var pane in new[]{_input,_output})
        {
            if(!Current())return;
            if(pane.Image.CurrentFrame is not {} receipt)continue;
            if(!Current())return;
            var paneGeneration=pane.Generation;
            bool LayerCurrent()=>Current()&&pane.Generation==paneGeneration;
            var values=selection.Targets.Where(target=>target.ImageFrame==receipt.Frame&&target.Coordinates==receipt.Coordinates)
                .SelectMany(target=>target.Geometry.Select(geometry=>new NativeGeometryOverlay(target.ImageFrame,geometry,Colors.Orange,2))).ToArray();
            if(values.Length==0)continue;
            if(!LayerCurrent())return;
            try
            {
                displayed|=pane.Selection.Present(receipt,++_layerRevision,values,LayerCurrent);
                if(!LayerCurrent())return;
            }
            catch(Exception error){if(Current())_main.AddLog("选择高亮未显示："+error.Message);}
        }
        if(Current()&&selection.Targets.Count>0&&!displayed)_main.AddLog("选择来源与当前双图物理Frame/坐标不一致，已拒绝高亮。");
    }
    private static void Run(IEnumerable<Action> actions)
    {
        var errors=new List<Exception>();foreach(var action in actions)try{action();}catch(Exception error){errors.Add(error);}
        if(errors.Count>0)throw new AggregateException("结果工作区清理失败。",errors);
    }
    public void Dispose()
    {
        if(_disposed)return;RetireRoiPresentation();_disposed=true;++_epoch;++_selectionGeneration;++_input.Generation;++_output.Generation;_displayDefinitionNotice=null;
        var plan=_plan;_plan=null;_inputSource=null;_outputSource=null;_sceneSelection=null;var fullscreen=_fullscreen;_fullscreen=null;
        var cancellation=_previewCancellation;_previewCancellation=null;
        Run(new Action[]{
            ()=>_main.ResultWorkspaceChanged-=Refresh,
            ()=>_main.ResultDisplayChanged-=ResultDisplayChanged,()=>{_displayRefresh?.Abort();_displayRefresh=null;},
            ()=>_main.ResultWorkspaceSelectionChanged-=Highlight,
            ()=>_main.ResultWorkspaceGraphicsCleared-=ClearGraphics,
            ()=>_main.SourceDisplayPreservationPreparing-=PrepareSourceDisplayPreservation,
            ()=>_main.ResultWorkspaceRootSettled-=RootSettled,
            ()=>_inputSelection.RemoveValueChanged(_shell,InputSelected),
            ()=>_outputSelection.RemoveValueChanged(_shell,OutputSelected),
            ()=>_doubleImage.RemoveValueChanged(_shell,DoubleImageChanged),
            ()=>((SunEyeVision.Plugin.Wpf.Native.Display.ImageViewport.ImageViewBox)_output.Image.Viewport.View).OnFreshExtendContent-=OnOverlayViewportChanged,
            ()=>_shell.FullscreenCommand=null,()=>_shell.SelectOverlayCommand=null,()=>_shell.SelectBackgroundCommand=null,()=>_shell.CrossLinesCommand=null,
            ()=>_shell.SelectedInputItem=null,()=>_shell.SelectedOutputItem=null,
            ()=>_shell.InputItems=null,()=>_shell.OutputItems=null,
            ()=>cancellation?.Cancel(),()=>cancellation?.Dispose(),
            ()=>fullscreen?.Dispose(),ResetOverlayChoice,_input.Dispose,_output.Dispose,()=>plan?.Dispose(),_main.NotifyRoiFrameAvailabilityChanged});
    }
}
