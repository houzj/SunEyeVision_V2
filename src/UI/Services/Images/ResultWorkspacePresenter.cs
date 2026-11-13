using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
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
        // 原件两段式（形状按钮只选中，图像落笔才检查并弹阻断提示）：视口落笔仅在
        // 存在阻断意图（RoiDrawBlockReason 非空）时上报给工具侧 owner 重试激活。
        System.Windows.Input.MouseButtonEventHandler onRoiDrawDown=(sender,args)=>
        {
            if(!_disposed&&!string.IsNullOrEmpty(_main.RoiDrawBlockReason))_main.NotifyRoiInputViewportClicked();
        };
        _output.Image.Viewport.View.PreviewMouseLeftButtonDown+=onRoiDrawDown;
        _input.Image.Viewport.View.PreviewMouseLeftButtonDown+=onRoiDrawDown;
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
        // 原件双图入口语义（ModuleDisplayDecorator.cs.pending:1453-1463 RefreshInputImageList）：
        // 先选与关联 IO 匹配的输入项，否则取第一个输入项；退出双图清空输入选择
        // （OnIsShowDoubleImageChanged，:2228-2260）。
        if(!_disposed&&_shell.IsDoubleImage)
        {
            var related=_main.EditorRelatedImageState;
            var selected=related is null?null
                :_plan?.InputSources.FirstOrDefault(candidate=>candidate.Reference?.PortId==related.PortId||candidate.Key==related.Key);
            selected??=_plan?.InputSources.FirstOrDefault();
            if(!ReferenceEquals(_shell.SelectedInputItem,selected))_shell.SelectedInputItem=selected;
        }
        else _shell.SelectedInputItem=null;

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
    /// <summary>编辑器相关输入的单图临时显示态（VM ModuleResultView.OnRelatedIOChanged）：
    /// 只改写视图选择，不写正式 SelectedDisplayReference。</summary>
    private ResultWorkspaceImageSource? EditorRelatedDisplay(ResultWorkspaceImagePlan plan)
    {
        if(_shell.IsDoubleImage)return null;
        if(_main.EditorRelatedImageState is not {} related)return null;
        return plan.Sources.FirstOrDefault(source=>source.Reference?.PortId==related.PortId)
            ??plan.Sources.FirstOrDefault(source=>source.Key==related.Key);
    }
    private bool IsEditorRelatedDisplay(ResultWorkspaceImageSource? source)
        =>source is not null&&_main.EditorRelatedImageState is {} related
            &&(source.Reference?.PortId==related.PortId||source.Key==related.Key);
    /// <summary>单图选择的唯一解析规则；重建与不变判定共用，避免两条路径漂移。</summary>
    private ResultWorkspaceImageSource? ResolveOutputChoice(ResultWorkspaceImagePlan? previous,ResultWorkspaceImagePlan plan)
    {
        var editorRelated=EditorRelatedDisplay(plan);
        var variableOutput=previous is not null&&previous.EditorScopeId==plan.EditorScopeId&&_variableOutputKey is {} variableKey
            ?plan.Sources.FirstOrDefault(source=>source.Key==variableKey):null;
        var explicitChoice=_main.SelectedNode?.SelectedDisplayReference;
        var useDefault=plan.IsPrivateDebug||previous?.IsPrivateDebug==true;
        var byKey=plan.Sources.FirstOrDefault(source=>source.Key==_outputKey);
        // 双图模式下原件不改单图选择（OnIsShowDoubleImageChanged 只动 SelectedInputImage，
        // OnRelatedIOChanged 在双图时不切 SelectedImage），因此编辑器会话存续期间保持当前来源；
        // 会话退役后回到正式保存的选择。
        if(_shell.IsDoubleImage&&byKey is not null&&_main.EditorRelatedImageState is not null)return byKey;
        return editorRelated??variableOutput??(explicitChoice is not null
            ?plan.Sources.FirstOrDefault(source=>source.Key==explicitChoice)
            :useDefault?plan.DefaultOutput:byKey??plan.DefaultOutput);
    }
    /// <summary>双图输入项选择：关联输入变化时跟随新输入，否则保持当前合法输入。</summary>
    private ResultWorkspaceImageSource? ResolveInputChoice(ResultWorkspaceImagePlan? previous,ResultWorkspaceImagePlan plan)
    {
        if(!_shell.IsDoubleImage)return null;
        var relatedImageChanged=previous is not null&&previous.PrimaryInputKey!=plan.PrimaryInputKey;
        var inputKey=relatedImageChanged?plan.PrimaryInputKey:_inputKey;
        return plan.InputSources.FirstOrDefault(source=>source.Key==inputKey)??plan.InputSources.FirstOrDefault();
    }
    private void SetOutputSelection(ResultWorkspaceImageSource? source)
    {
        if(_disposed||ReferenceEquals(_shell.SelectedOutputItem,source))return;
        _restoringOutput=true;
        try{_shell.SetCurrentValue(ResultWorkspaceShell.SelectedOutputItemProperty,source);}
        finally{_restoringOutput=false;}
    }
    private void OutputSelected(object? sender,EventArgs args)
    {
        var accepted=false;
        try
        {
        if(_disposed||_restoringOutput)return;
        if(_refreshing&&_plan is null)return;
        // WPF 在 ItemsSource 被替换时会短暂把双向 SelectedItem 置空。清空不是用户意图：
        // 原件 SelectedImage 只由显式改选或所属模块重建改变。刷新期内的置空是中间态，
        // 由 Refresh 安装解析结果；刷新之外的迟到置空恢复原选择。
        if(_shell.SelectedOutputItem is null)
        {
            if(_refreshing)return;
            if(_outputSource is not null){RestoreChoice(false);return;}
        }
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
            // 用户在单图下拉里的显式改选属于正式选择边界：编辑器临时显示态在此退役，
            // 正式 SelectedDisplayReference 只允许在该边界或提交时写入。
            var transientEditorChoice=IsEditorRelatedDisplay(source);
            if(!transientEditorChoice)_main.SetEditorRelatedImage(null);
            _variableOutputKey=source?.VariableImage is not null?source.Key:null;
            if(_variableOutputKey is null&&!transientEditorChoice&&_main.SelectedNode is {} selected)
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
        // ROI 展示不随“数据未变”的刷新退役：原件在同一 ImageDataSource 下保留区域编辑，
        // 只在真正重建（相机/结果/作用域变化，epoch 自增）时失效。
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
                RetireRoiPresentation();
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
                // A parameter rebind can recreate the private draft scope while keeping
                // the same editor session. The input reference is still the authoritative
                // input-pane choice in that case, so no extra state is carried here.
                var input=ResolveInputChoice(old,plan);
                if(old?.EditorScopeId!=plan.EditorScopeId)_variableOutputKey=null;
                // A persisted explicit identity must match exactly, including scope and members.
                // Missing or stale declarations never silently select a different image.
                var explicitChoice=_main.SelectedNode?.SelectedDisplayReference;
                var output=ResolveOutputChoice(old,plan);
                if(explicitChoice is not null&&output is null)plan.Diagnostics.Add("已选择的图像来源不存在或声明已变更，请重新选择。");
                _inputSource=input;_outputSource=output;
                _shell.SelectedInputItem=input;
                // 本轮解析结果是单图显示的权威值：绕过 OutputSelected 的正式选择写入。
                SetOutputSelection(output);
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
                    // 单图选择已由本轮解析安装；这里只做呈现对账，不再改写选择。
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
        // 会话/选中/工作流短暂无效时捕获到的空计划不是数据变化：原件在无新数据时保留
        // 当前 ImageDataSource（ModuleResultView 图像守卫），不得清空已呈现的图像与选择。
        if(plan.Sources.Count==0&&previous.Sources.Count>0&&ReferenceEquals(_plan,previous)
            &&ReferenceEquals(_selectionOwner,_main.SelectedNode))return true;
        // 草稿瞬时不可读会捕获到“有来源但无输入”的中间计划；这不是输入选择变化，
        // 原件在同一草稿作用域内仍保留已呈现的输入帧（ROI 借用依赖同一帧身份）。
        if(plan.InputSources.Count==0&&previous.InputSources.Count>0&&ReferenceEquals(_plan,previous)
            &&ReferenceEquals(_selectionOwner,_main.SelectedNode))return true;
        // Opening or closing the private editor changes the result-source
        // ownership scope even when the published image collection is equal.
        // Reuse across that boundary leaves ROI bound to the formal Blob output.
        if(previous.EditorScopeId!=plan.EditorScopeId
            &&(ContainsVariableImage(previous.InputSources)||ContainsVariableImage(previous.Sources)
                ||ContainsVariableImage(plan.InputSources)||ContainsVariableImage(plan.Sources)))return false;
        // A WPF parameter editor can publish one intermediate refresh while a
        // pending image-variable selection is being materialized. Keep the
        // already-owned private input lease across that transient empty plan;
        // the next refresh with the committed draft replaces it normally.
        if(previous.IsPrivateDebug&&plan.IsPrivateDebug&&previous.InputSources.Count>0&&plan.InputSources.Count==0)
            return true;
        if(!ReferenceEquals(_selectionOwner,_main.SelectedNode))return false;
        if(!OverlayChoiceMatches(_main.SelectedNode?.DisplayOverlays))return false;
        // Check this boundary before publication identity comparison: the formal
        // refresh may legitimately carry a new invocation while the editor's
        // already-presented input frame must remain selected for ROI interaction.
        if(previous.IsPrivateDebug&&!plan.IsPrivateDebug
            &&previous.PrimaryInputKey is {} primaryKey
            &&previous.PrimaryInputKey!=plan.PrimaryInputKey
            &&_outputSource?.Key==primaryKey)
            return true;
        if(!SourcesUnchanged(previous.InputSources,plan.InputSources))return false;
        if(!SourcesUnchanged(previous.Sources,plan.Sources))return false;
        // 输入/输出选择按与重建路径完全相同的规则只读解析，再与当前显示比较；
        // 状态重置（编辑作用域、主输入变化）只有在会改变解析结果时才触发重建。
        var input=ResolveInputChoice(previous,plan);
        var output=ResolveOutputChoice(previous,plan);
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
            // 变量图像每次捕获都是新的包装对象，按实际帧/坐标域比较（原件 ImageDataSource 守卫
            // 比较的是图像数据本身），否则每次刷新都会误判为变化并清空视口。
            if(!SameVariableImage(previous[index].VariableImage,current[index].VariableImage))return false;
            // 同一运行身份下图像值有无变化（如掩膜清空/恢复）必须照常重建。
            if(previous[index].HasImageValue!=current[index].HasImageValue)return false;
        }
        return true;
    }
    private static bool SameVariableImage(FrozenImageResource? previous,FrozenImageResource? current)
        =>previous is null?current is null
            :current is not null&&previous.Frame==current.Frame&&previous.EffectiveDomain.Frame==current.EffectiveDomain.Frame;
    private static bool ContainsVariableImage(System.Collections.Generic.IReadOnlyList<ResultWorkspaceImageSource> sources)
        =>sources.Any(source=>source.VariableImage is not null);
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
