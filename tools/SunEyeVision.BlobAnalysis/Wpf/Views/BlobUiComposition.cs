using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Display;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>The same real three-page composition for runtime and the XAML designer.</summary>
internal sealed class BlobUiComposition : IDisposable
{
    private readonly DeclaredSettingsView _editor;
    internal IToolSettingsDraft Draft {get;}
    internal ToolSettingsViewOptions BaseOptions {get;}
    private readonly BlobUiPreviewScope _preview;
    private readonly NativeAssistUiDraftControls? _assistUi;
    private readonly BlobSupportedWindowState? _supportedState;
    internal readonly BlobLocalRoiOwner? _roiOwner;
    private NativeRoiUiDraftControls? _roiUi;
    internal NativeDisplayUiDraftRows? _displayUi;
    private NativeJudgmentUiDraftControls? _judgmentUi;
    internal BlobRunUiState? RunUi {get;private set;}
    private readonly ToolSettingsViewOptions _composed;
    private bool _disposed,_bound;
    internal event Action? Changed;
    private static readonly System.ComponentModel.DependencyPropertyDescriptor PageDescriptor=
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(DeclaredSettingsView.SelectedPageIndexProperty,typeof(DeclaredSettingsView));
    internal bool IsDisposed=>_disposed;
    internal bool HasUiChanges=>_preview.HasUiChanges;
    internal bool HasPending=>HasUiChanges||_roiOwner?.HasPending==true;
    internal long RoiRevision=>_roiOwner?.Revision??0;
    internal BlobUiComposition(DeclaredSettingsView editor,IToolSettingsDraft draft,ToolSettingsViewOptions options)
    {
        _editor=editor;Draft=draft;BaseOptions=options;
        _preview=new(()=>!_disposed&&ReferenceEquals(_editor.Draft,Draft));
        try
        {
            if(options.InputContent is DeclaredInputFieldsView inputs)
            {
                var pose=BlobRoiUi.Add(inputs,supportedWindow:true,observe:value=>_roiUi=value);
                _roiOwner=new BlobLocalRoiOwner(inputs,_roiUi!,options.RoiFrames);
                _supportedState=new BlobSupportedWindowState(draft,inputs){InformationHost=pose};_supportedState.Refresh();
                _assistUi=pose as NativeAssistUiDraftControls;
                const string position="field:c95f3a26edf55097863a7ec47d17cce5";
                options=options with{AdditionalAfterField=position,AdditionalAfterContent=pose,HeaderlessSectionAnchors=new HashSet<string>{position}};
                if(_assistUi?.GraphicVisibilitySource is {} source)
                {
                    var gates=options.FieldVisibilitySources?.ToDictionary(item=>item.Key,item=>item.Value,StringComparer.Ordinal)??new Dictionary<string,FrameworkElement>(StringComparer.Ordinal);
                    gates[position]=source;options=options with{FieldVisibilitySources=gates};
                }
            }
            options=BlobRunUi.AddRows(options,draft,editor,ValidateUiState,state=>RunUi=state);
            _composed=BlobDisplayUi.AddRows(options,draft.Schema,observe:value=>_displayUi=value,observeJudgment:value=>_judgmentUi=value);
            editor.Bind(draft,_composed);_bound=true;RunUi!.Attach();
            _preview.Observe(()=>RunUi.HasUiChanges,handler=>RunUi.Changed+=handler,handler=>RunUi.Changed-=handler,
                ()=>RunUi.RetireInteractions(),()=>RunUi.IsUnsupportedSelection);
            if(_roiUi is not null)_preview.Observe(()=>_roiUi.HasUiChanges,handler=>_roiUi.Changed+=handler,handler=>_roiUi.Changed-=handler);
            if(_assistUi is not null)_preview.Observe(()=>_assistUi.HasUiChanges,handler=>_assistUi.Changed+=handler,handler=>_assistUi.Changed-=handler);
            if(_displayUi is not null)_preview.Observe(()=>_displayUi.HasUiChanges,handler=>_displayUi.Changed+=handler,handler=>_displayUi.Changed-=handler,()=>_displayUi.RetireInteractions());
            if(_judgmentUi is not null)_preview.Observe(()=>_judgmentUi.HasUiChanges,handler=>_judgmentUi.Changed+=handler,handler=>_judgmentUi.Changed-=handler,()=>_judgmentUi.RetireInteractions());
            if(_roiOwner is not null)_roiOwner.Changed+=_preview.Notify;
            _preview.Changed+=Notify;editor.Changed+=RefreshSupported;
            PageDescriptor.AddValueChanged(editor,OnPageChanged);editor.IsVisibleChanged+=OnVisibility;editor.Unloaded+=OnUnloaded;
        }
        catch(Exception failure){try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    private void Notify(){if(!_disposed)Changed?.Invoke();}
    private void RefreshSupported()=>_supportedState?.Refresh();
    private string? ValidateUiState()=>_preview.Validate()??_supportedState?.Validate();
    internal void RebindSameContext(){ObjectDisposedException.ThrowIf(_disposed,this);_preview.RetireInteractions();_editor.Bind(Draft,_composed);RunUi?.Attach();}
    internal bool TryCapture(out FieldSettings fields,out string error)
    {
        fields=null!;
        if(_preview.Validate() is {} preview){error=preview;return false;}
        if(_roiOwner is not null&&!_roiOwner.TryCapture(out error))return false;
        if(_assistUi is {Visibility:Visibility.Visible,IsUnimplementedMode:true})
        {error="按点和按坐标的位置修正尚未接入，请切回按信息。";return false;}
        return _editor.TryCapture(out fields,out error);
    }
    private void OnPageChanged(object? sender,EventArgs args)=>_preview.RetireInteractions();
    private void OnVisibility(object sender,DependencyPropertyChangedEventArgs args){if(!_editor.IsVisible)_preview.RetireInteractions();}
    private void OnUnloaded(object sender,RoutedEventArgs args)=>_preview.RetireInteractions();
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;
        _editor.Changed-=RefreshSupported;PageDescriptor.RemoveValueChanged(_editor,OnPageChanged);_editor.IsVisibleChanged-=OnVisibility;_editor.Unloaded-=OnUnloaded;
        if(_roiOwner is not null)_roiOwner.Changed-=_preview.Notify;
        _preview.Changed-=Notify;
        var errors=new List<Exception>();foreach(var action in new Action[]{_preview.Dispose,()=>RunUi?.Dispose(),()=>_roiOwner?.Dispose(),()=>_supportedState?.Dispose(),()=>_displayUi?.Dispose(),()=>_judgmentUi?.Dispose(),()=>_assistUi?.Dispose(),()=>_roiUi?.Dispose(),()=>{if(!_bound&&(BaseOptions.InputContent is IDisposable input))input.Dispose();}})
            try{action();}catch(Exception error){errors.Add(error);}
        if(errors.Count!=0)throw new AggregateException("Blob界面作用域清理失败。",errors);
    }
}
