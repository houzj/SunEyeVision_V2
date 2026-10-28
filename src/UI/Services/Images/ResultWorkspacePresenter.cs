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
internal sealed class ResultWorkspacePresenter:IDisposable
{
    private sealed class Pane:IDisposable
    {
        internal long Generation;
        internal readonly ResultImagePane Image;
        internal readonly NativeResultViewport.GeometryLayer Selection;
        internal Pane(FrameworkElement view){Image=new(view);Selection=Image.Viewport.CreateSelectionLayer();}
        internal void ClearGraphics()=>Selection.Clear();
        public void Dispose()=>Run(new Action[]{ClearGraphics,Selection.Dispose,Image.Dispose});
    }
    private readonly MainWindowViewModel _main;
    private readonly ResultWorkspaceShell _shell;
    private readonly Pane _input,_output;
    private readonly DependencyPropertyDescriptor _inputSelection,_outputSelection;
    private ResultWorkspaceImagePlan? _plan;
    private CancellationTokenSource? _previewCancellation;
    private ResultViewportFullscreenScope? _fullscreen;
    private long _epoch,_layerRevision,_selectionGeneration;
    private int _navigationIndex=int.MinValue;
    private bool _disposed,_refreshing,_refreshAgain,_refreshQueued;
    private string? _inputKey,_outputKey;
    private object? _selectionOwner;
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
        try
        {
        _inputSelection.AddValueChanged(shell,InputSelected);_outputSelection.AddValueChanged(shell,OutputSelected);
        main.ResultWorkspaceChanged+=Refresh;main.ResultWorkspaceSelectionChanged+=Highlight;
        main.ResultWorkspaceGraphicsCleared+=ClearGraphics;
        shell.FullscreenCommand=new SunEyeVision.UI.Core.RelayCommand(value=>
        {
            if(value is FrameworkElement view&&Window.GetWindow(shell) is {} owner)
            {
                _fullscreen?.Dispose();_fullscreen=ResultViewportFullscreenScope.Open(view,owner);
            }
        },value=>!_disposed&&value is FrameworkElement view&&(ReferenceEquals(view,shell.InputViewport)||ReferenceEquals(view,shell.OutputViewport)));
        // The original background command exchanges an established output background
        // with a checked overlay. It is not an alias of ordinary source selection.
        // Keep it unavailable until the one-owner multi-image layer chain is present.
        shell.SelectBackgroundCommand=null;
        Refresh();
        }
        catch(Exception failure)
        {
            try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}
            throw;
        }
    }
    private void InputSelected(object? sender,EventArgs args)
    {
        if(_disposed||_refreshing)return;
        _inputKey=(_shell.SelectedInputItem as ResultWorkspaceImageSource)?.Key;
        Present(_input,_shell.SelectedInputItem as ResultWorkspaceImageSource);ReportDisplayDefinitionGap();
    }
    private void OutputSelected(object? sender,EventArgs args)
    {
        if(_disposed||_refreshing)return;
        var source=_shell.SelectedOutputItem as ResultWorkspaceImageSource;_outputKey=source?.Key;
        _previewCancellation?.Cancel();
        if(source is {NavigationIndex:>=0}&&_main.SelectedDisplayImageSourceIndex!=source.NavigationIndex)
        {_main.SelectedDisplayImageSourceIndex=source.NavigationIndex;return;}
        Present(_output,source);ReportDisplayDefinitionGap();
    }
    private void Present(Pane pane,ResultWorkspaceImageSource? source)
    {
        var plan=_plan;var epoch=_epoch;var paneGeneration=++pane.Generation;
        ++_selectionGeneration;
        bool Current()=>!_disposed&&epoch==_epoch&&ReferenceEquals(_plan,plan)&&pane.Generation==paneGeneration;
        if(!Current())return;
        pane.ClearGraphics();
        if(!Current())return;
        if(source?.Publication is not {} publication){pane.Image.Clear();return;}
        if(!pane.Image.TryPresent(publication,source.Reference,source.Reference.ScopeId!,
            ()=>Current()&&source.Current()&&Current(),out var error))
            _main.AddLog("图像来源未显示："+source.DisplayName+"，"+error);
    }
    internal void Refresh()
    {
        if(_disposed)return;
        if(_refreshing){_refreshAgain=true;return;}
        _refreshing=true;
        try
        {
            do
            {
                _refreshAgain=false;var epoch=++_epoch;
                ++_input.Generation;++_output.Generation;++_selectionGeneration;
                _previewCancellation?.Cancel();
                var old=_plan;_plan=null;
                Run(new Action[]{_input.Image.Clear,_output.Image.Clear,ClearGraphics,()=>old?.Dispose()});
                if(_disposed)return;
                if(!ReferenceEquals(_selectionOwner,_main.SelectedNode))
                {_selectionOwner=_main.SelectedNode;_inputKey=null;_outputKey=null;}
                var plan=_main.CaptureNativeImageSources(()=>!_disposed&&epoch==_epoch);
                if(_disposed||epoch!=_epoch){plan.Dispose();continue;}
                _plan=plan;
                var inputs=plan.Sources.Where(source=>source.IsUpstream).ToArray();
                _shell.InputItems=inputs;_shell.OutputItems=plan.Sources;
                var input=inputs.FirstOrDefault(source=>source.Key==_inputKey)??inputs.FirstOrDefault();
                var navigation=_main.SelectedDisplayImageSourceIndex;
                var byKey=plan.Sources.FirstOrDefault(source=>source.Key==_outputKey);
                var byNavigation=plan.Sources.FirstOrDefault(source=>source.NavigationIndex==navigation);
                var output=(_navigationIndex!=navigation?byNavigation??byKey:byKey??byNavigation)??plan.Sources.FirstOrDefault();
                _navigationIndex=navigation;
                _shell.SelectedInputItem=input;_shell.SelectedOutputItem=output;
                _inputKey=input?.Key;_outputKey=output?.Key;
                Present(_input,input);
                if(_main.NativePreviewPath is {} path)_=PresentPreview(path,epoch);
                else Present(_output,output);
                ReportDisplayDefinitionGap();
                if(plan.Diagnostics.Count>0)_main.AddLog(string.Join("；",plan.Diagnostics));
            }while(_refreshAgain&&!_disposed);
        }
        catch(Exception error){_main.AddLog("结果工作区刷新失败："+error.Message);}
        finally
        {
            _refreshing=false;
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
    private async Task PresentPreview(string path,long epoch)
    {
        var cancellation=new CancellationTokenSource();var token=cancellation.Token;_previewCancellation=cancellation;
        var paneGeneration=++_output.Generation;++_selectionGeneration;
        try
        {
            using var decoded=await LocalResultImagePreview.LoadAsync(path,token);
            bool Current()=>!_disposed&&!token.IsCancellationRequested&&epoch==_epoch&&_output.Generation==paneGeneration&&_main.NativePreviewPath==path;
            if(!Current())return;
            if(!_output.Image.TryPresentPreview(decoded.Image,Current,out var error))_main.AddLog("文件预览未显示："+error);
            else _main.StatusText="文件预览："+decoded.Image.Width+" × "+decoded.Image.Height;
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
        if(publication is null||!publication.IsCurrent()||ReferenceEquals(publication,_displayDefinitionNotice))return;
        _displayDefinitionNotice=publication;
        // Resource values are not display declarations. Do not invent visible regions,
        // colors, text or frame provenance while the formal display contract is absent.
        _main.AddLog("结果图形/文字显示尚未接入正式声明；图像叠层与背景替换尚未接入唯一owner，不绘制推测图层。普通来源选择、树表及有同帧证明的高亮可用。");
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
        if(_disposed)return;_disposed=true;++_epoch;++_selectionGeneration;++_input.Generation;++_output.Generation;_displayDefinitionNotice=null;
        var plan=_plan;_plan=null;var fullscreen=_fullscreen;_fullscreen=null;
        var cancellation=_previewCancellation;_previewCancellation=null;
        Run(new Action[]{
            ()=>_main.ResultWorkspaceChanged-=Refresh,
            ()=>_main.ResultWorkspaceSelectionChanged-=Highlight,
            ()=>_main.ResultWorkspaceGraphicsCleared-=ClearGraphics,
            ()=>_inputSelection.RemoveValueChanged(_shell,InputSelected),
            ()=>_outputSelection.RemoveValueChanged(_shell,OutputSelected),
            ()=>_shell.FullscreenCommand=null,()=>_shell.SelectBackgroundCommand=null,
            ()=>_shell.SelectedInputItem=null,()=>_shell.SelectedOutputItem=null,
            ()=>_shell.InputItems=null,()=>_shell.OutputItems=null,
            ()=>cancellation?.Cancel(),()=>cancellation?.Dispose(),
            ()=>fullscreen?.Dispose(),_input.Dispose,_output.Dispose,()=>plan?.Dispose()});
    }
}
