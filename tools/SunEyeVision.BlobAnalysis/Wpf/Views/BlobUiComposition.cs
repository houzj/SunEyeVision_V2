using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using System.Diagnostics;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>The same real three-page composition for runtime and the XAML designer.</summary>
internal sealed class BlobUiComposition : IDisposable
{
    private const string PositionCorrectionField = "field:c95f3a26edf55097863a7ec47d17cce5";
    private readonly DeclaredSettingsView _editor;
    internal IToolSettingsDraft Draft {get;}
    private readonly ToolSettingsViewOptions _sourceOptions;
    private readonly ToolSettingsViewOptions _baseOptions;
    private NativeAssistUiDraftControls? _assistUi;
    private readonly FrameworkElement _assistPointVisibility = new() { Visibility = Visibility.Collapsed };
    private BlobSupportedWindowState? _supportedState;
    internal BlobLocalRoiOwner? _roiOwner;
    private NativeRoiUiDraftControls? _roiUi;
    private readonly ToolSettingsViewOptions _composed;
    private bool _disposed,_bound;
    private int _buildingPages;
    private DeclaredFieldsView? _pageHost;
    private string? _compositionError;
    private readonly Dictionary<string,long> _stageMs=new(StringComparer.Ordinal);

    private readonly TaskCompletionSource<bool> _ready = new(TaskCreationOptions.RunContinuationsAsynchronously);
    internal IReadOnlyDictionary<string,long> StageMs=>_stageMs;
    internal Task ReadyTask=>_ready.Task;
    internal bool IsReady=>!_disposed&&_bound&&_buildingPages==0&&_compositionError is null;
    internal string? CompositionError=>_compositionError;
    internal event Action? Changed;
    internal bool HasPending=>_roiOwner?.HasPending==true||_roiUi?.HasPendingEdits==true||_assistUi is {HasPendingEdits:true,Visibility:Visibility.Visible};
    internal long RoiRevision=>_roiOwner?.Revision??0;
    internal BlobUiComposition(DeclaredSettingsView editor,IToolSettingsDraft draft,ToolSettingsViewOptions options)
    {
        _editor=editor;Draft=draft;_sourceOptions=options;
        options=options with{HeaderlessSectionAnchors=new HashSet<string>{PositionCorrectionField}};
        _baseOptions=BlobRunUi.Configure(options);
        try
        {
            _composed=BlobDisplayUi.Configure(_baseOptions,_assistPointVisibility) with {ValidateUiState=ValidateUiState,PageBuilt=BuildPage};
            RefreshSupported();editor.Bind(draft,_composed);_bound=true;
            editor.Changed+=RefreshSupported;
            editor.SelectedPageChanged+=OnPageChanged;editor.IsVisibleChanged+=OnVisibility;editor.Unloaded+=OnUnloaded;
            _ready.TrySetResult(true);
        }
        catch(Exception failure){try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    internal bool OwnsContext(IToolSettingsDraft draft,ToolSettingsViewOptions options)=>
        !_disposed&&ReferenceEquals(Draft,draft)&&ReferenceEquals(_editor.Draft,draft)&&ReferenceEquals(_sourceOptions,options);
    private void Notify(){if(!_disposed)Changed?.Invoke();}
    private void OnInputChanged(){RefreshSupported();Notify();}
    internal void RefreshAcceptedInputs()
    {
        if(_disposed||_roiUi is null||_supportedState is null)return;
        _supportedState.RefreshAcceptedInputs(_roiUi);
    }
    private void RefreshSupported()
    {
        if(_disposed)return;
        try
        {
            var mode = _baseOptions.ReadInputSetting?.Invoke(BlobImageRoi.ModeId)
                ?? (_baseOptions.InputContent as DeclaredInputFieldsView)?.ReadUnpresentedInput(BlobImageRoi.ModeId)
                ?? new UnsetSetting();
            var correction = BlobParameterDefinition.Instance.SettingsFieldId(nameof(BlobSettings.IsPositionCorrectionEnabled));
            _assistPointVisibility.Visibility = !BlobImageRoi.IsImageMode(mode) && Draft.CaptureFields().Read(correction) is ConstantSetting { Value:true }
                ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (Exception error) when (error is ArgumentException or InvalidOperationException)
        {
            _assistPointVisibility.Visibility = Visibility.Collapsed;
        }
        if(_supportedState is {} state)state.Refresh();
        else BlobSupportedWindowState.ProjectSources(Draft,_baseOptions.InputContent as DeclaredInputFieldsView,()=>!_disposed);
    }
    private string? ValidateUiState()
    {
        if(_disposed||!ReferenceEquals(_editor.Draft,Draft))return "参数窗口已失效，请重新打开。";
        var hostError=_sourceOptions.ValidateUiState?.Invoke();
        if(!string.IsNullOrEmpty(hostError))return hostError;
        return BlobSupportedWindowState.ValidateSettings(Draft)??ValidateOrderedRanges()
            ??(_supportedState is not null?_supportedState.Validate():_baseOptions.InputContent is DeclaredInputFieldsView inputs?BlobSupportedWindowState.ValidateInputs(inputs,Draft)
                :_baseOptions.InputContentFactory is not null?"基本参数输入页尚未建立，无法核验原有输入保护；请先打开基本参数页再确认或执行。":null);
    }
    private string? ValidateOrderedRanges()
    {
        var fields=Draft.CaptureFields();
        string Id(string property)=>BlobParameterDefinition.Instance.SettingsFieldId(property);
        var pairs=new List<(string Low,string High,string Name)>();
        if(fields.Read(Id(nameof(BlobSettings.SegmentationMode))) is ConstantSetting{Value:"FixedSoftThreshold"})
            pairs.Add((Id(nameof(BlobSettings.FixedSoftLowerThreshold)),Id(nameof(BlobSettings.FixedSoftUpperThreshold)),"固定软阈值"));
        if(fields.Read(Id(nameof(BlobSettings.IsAreaFilterEnabled))) is ConstantSetting{Value:true})
            pairs.Add((Id(nameof(BlobSettings.FilterAreaMin)),Id(nameof(BlobSettings.FilterAreaMax)),"面积"));
        foreach(var pair in pairs)
        {
            // Subscription values are resolved by the invocation. Last accepted current values
            // do not describe a newly selected source or index. Native model/readback checks are distinct from binding proposals.
            if(fields.Read(pair.Low) is not ConstantSetting lower||fields.Read(pair.High) is not ConstantSetting upper)continue;
            if(lower.Value is not long low||upper.Value is not long high)return pair.Name+"范围常量类型无效。";
            if(low>high)return pair.Name+"使用原有序范围控件，当前下限超过上限；原设置保持不变。";
        }
        return null;
    }
    private void BuildPage(string page,DeclaredFieldsView fields,FrameworkElement? pageInput)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(ReferenceEquals(_pageHost,fields)&&_compositionError is not null)return;
        _buildingPages++;
        try
        {
            if(!ReferenceEquals(_pageHost,fields))ResetPageHost(fields);
            BuildPageCore(page,fields,pageInput);
        }
        catch(Exception error) when (_bound)
        {
            _compositionError=error.Message;
            SunEyeVision.Foundation.Logging.PluginLogger.Error(error.Message,nameof(BlobUiComposition));
            try{ReleaseInputComposition();}
            catch(Exception cleanup)
            {
                SunEyeVision.Foundation.Logging.PluginLogger.Error(cleanup.Message,nameof(BlobUiComposition),cleanup);
            }
        }
        finally
        {
            _buildingPages--;
        }
        if(_bound)Notify();
    }
    private void BuildPageCore(string page,DeclaredFieldsView fields,FrameworkElement? pageInput)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        void Measure(string stage,Action action)
        {
            var clock=Stopwatch.StartNew();
            try{action();}
            finally{_stageMs[$"{page}:{stage}"]=clock.ElapsedMilliseconds;}
        }
        var basicPage=Draft.Schema.Inputs.Count==0?null:_baseOptions.InputSection;
        if(StringComparer.Ordinal.Equals(page,basicPage)&&_roiUi is null)
        {
            var input=pageInput??_baseOptions.InputContent;
            if(input is DeclaredInputFieldsView inputs)
            {
                Measure("ROI组合",() =>
                {
                    var pose=BlobRoiUi.Add(inputs,observe:value=>_roiUi=value);
                    _assistUi=pose;
                    _roiOwner=new BlobLocalRoiOwner(inputs,_roiUi!,_baseOptions.RoiFrames);
                    _supportedState=new BlobSupportedWindowState(Draft,inputs,pose);
                    fields.BindFieldVisibilityTo(PositionCorrectionField,_roiUi!.GraphicVisibilitySource);
                    fields.AddAfterField(PositionCorrectionField,pose,new FieldCondition(PositionCorrectionField,true));
                    pose.Changed+=OnInputChanged;
                    if(_roiUi is not null)_roiUi.Changed+=OnInputChanged;
                    _roiOwner.Changed+=Notify;
                });
            }
        }
        _baseOptions.PageBuilt?.Invoke(page,fields,pageInput);
    }

    private void ResetPageHost(DeclaredFieldsView next)
    {
        ReleaseInputComposition();
        _pageHost=next;_compositionError=null;RefreshSupported();
    }
    private void ReleaseInputComposition()
    {
        var oldOwner=_roiOwner;_roiOwner=null;
        var oldSupported=_supportedState;_supportedState=null;
        var oldAssist=_assistUi;_assistUi=null;
        var oldRoi=_roiUi;_roiUi=null;
        if(oldOwner is not null)oldOwner.Changed-=Notify;
        if(oldAssist is not null)oldAssist.Changed-=OnInputChanged;
        if(oldRoi is not null)oldRoi.Changed-=OnInputChanged;
        var errors=new List<Exception>();
        foreach(var action in new Action[]{()=>oldOwner?.Dispose(),()=>oldSupported?.Dispose(),()=>oldAssist?.Dispose(),()=>oldRoi?.Dispose()})
            try{action();}catch(Exception error){errors.Add(error);}
        if(errors.Count!=0)throw new AggregateException("基本参数交互清理失败。",errors);
    }
    internal bool TryCapture(out FieldSettings fields,out string error)
    {
        fields=null!;
        if(!IsReady){error="参数组合仍在组装，请稍候。";return false;}
        if(!ReferenceEquals(_editor.Draft,Draft)){error="参数窗口已失效，请重新打开。";return false;}
        if(_roiUi is not null&&!_roiUi.TryCaptureActiveSources(out error))return false;
        if(_roiOwner is not null&&!_roiOwner.TryCapture(out error))return false;
        return _editor.TryCapture(out fields,out error);
    }
    private void OnPageChanged(object? sender,EventArgs args)=>_assistUi?.RetireInteractions();
    private void OnVisibility(object sender,DependencyPropertyChangedEventArgs args){if(!_editor.IsVisible)_assistUi?.RetireInteractions();}
    private void OnUnloaded(object sender,RoutedEventArgs args)=>_assistUi?.RetireInteractions();
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;
        if(!_ready.Task.IsCompleted)_ready.TrySetCanceled();
        _editor.Changed-=RefreshSupported;_editor.SelectedPageChanged-=OnPageChanged;_editor.IsVisibleChanged-=OnVisibility;_editor.Unloaded-=OnUnloaded;
        var errors=new List<Exception>();foreach(var action in new Action[]{ReleaseInputComposition,()=>{if(!_bound&&_baseOptions.InputContent is IDisposable input)input.Dispose();}})
            try{action();}catch(Exception error){errors.Add(error);}
        if(errors.Count!=0)throw new AggregateException("Blob界面作用域清理失败。",errors);
    }
}
