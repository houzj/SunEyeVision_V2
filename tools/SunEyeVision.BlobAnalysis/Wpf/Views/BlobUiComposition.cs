using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Tool.BlobAnalysis.Runtime;
using System.Diagnostics;
using System.Windows.Threading;

namespace SunEyeVision.Tool.BlobAnalysis.Wpf.Views;

/// <summary>The same real three-page composition for runtime and the XAML designer.</summary>
internal sealed class BlobUiComposition : IDisposable
{
    private readonly DeclaredSettingsView _editor;
    internal IToolSettingsDraft Draft {get;}
    internal ToolSettingsViewOptions BaseOptions {get;}
    private readonly BlobUiPreviewScope _preview;
    private NativeAssistUiDraftControls? _assistUi;
    private readonly FrameworkElement _assistPointVisibility=new(){Visibility=Visibility.Collapsed};
    private BlobSupportedWindowState? _supportedState;
    internal BlobLocalRoiOwner? _roiOwner;
    private NativeRoiUiDraftControls? _roiUi;
    internal NativeDisplayUiDraftRows? _displayUi;
    private NativeJudgmentUiDraftControls? _judgmentUi;
    internal BlobRunUiState? RunUi {get;private set;}
    private readonly ToolSettingsViewOptions _composed;
    private bool _disposed,_bound;
    private ToolSettingsViewOptions? _runOptions,_displayOptions;
    private DeclaredFieldsView? _pageHost;
    private readonly List<IDisposable> _pageSubscriptions=[];
    private readonly List<FrameworkElement> _deferredContents=[];
    private readonly HashSet<string> _pendingPages=new(StringComparer.Ordinal);
    private readonly HashSet<string> _composedPages=new(StringComparer.Ordinal);
    private string? _activePage;
    private string? _compositionError;
    private readonly Dictionary<string,long> _stageMs=new(StringComparer.Ordinal);
    private bool _pagePrewarmQueued;
    internal IReadOnlyDictionary<string,long> StageMs=>_stageMs;
    internal bool IsReady=>!_disposed&&_pendingPages.Count==0&&_compositionError is null;
    internal string? CompositionError=>_compositionError;
    internal event Action? Changed;
    private static readonly System.ComponentModel.DependencyPropertyDescriptor PageDescriptor=
        System.ComponentModel.DependencyPropertyDescriptor.FromProperty(DeclaredSettingsView.SelectedPageIndexProperty,typeof(DeclaredSettingsView));
    internal bool IsDisposed=>_disposed;
    internal bool HasUiChanges=>_preview.HasUiChanges;
    internal bool HasPending=>HasUiChanges||_roiOwner?.HasPending==true;
    internal long RoiRevision=>_roiOwner?.Revision??0;
    internal BlobUiComposition(DeclaredSettingsView editor,IToolSettingsDraft draft,ToolSettingsViewOptions options)
    {
        _editor=editor;Draft=draft;
        const string position="field:c95f3a26edf55097863a7ec47d17cce5";
        options=options with{HeaderlessSectionAnchors=new HashSet<string>{position}};
        BaseOptions=options;
        _preview=new(()=>!_disposed&&ReferenceEquals(_editor.Draft,Draft));
        using var sharedResourceScope=SunEyeVision.Plugin.Wpf.EditorResources.BeginSharedResourceScope();
        try
        {
            _composed=BlobDisplayUi.Configure(BaseOptions,draft.Schema) with {ValidateUiState=ValidateUiState,PageBuilt=BuildPage};
            RefreshSupported();editor.Bind(draft,_composed);_bound=true;
            _preview.Observe(()=>false,handler=>_editor.Changed+=handler,handler=>_editor.Changed-=handler,unsupported:UnsupportedSavedRunSelection);
            if(_roiOwner is not null)_roiOwner.Changed+=_preview.Notify;
            _preview.Changed+=Notify;editor.Changed+=RefreshSupported;
            PageDescriptor.AddValueChanged(editor,OnPageChanged);editor.IsVisibleChanged+=OnVisibility;editor.Unloaded+=OnUnloaded;
        }
        catch(Exception failure){try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    private void Notify(){if(!_disposed)Changed?.Invoke();}
    private void RefreshSupported()
    {
        if(_disposed)return;
        if(_supportedState is {} state)state.Refresh();
        else BlobSupportedWindowState.ProjectAssist(Draft,BaseOptions.InputContent as DeclaredInputFieldsView,_assistUi,_assistPointVisibility,()=>!_disposed);
    }
    private bool AssistIsActive(NativeAssistUiDraftControls assist)=>!_disposed&&ReferenceEquals(_assistUi,assist)
        &&assist.GraphicVisibilitySource?.Visibility is not (Visibility.Collapsed or Visibility.Hidden)
        &&Draft.CaptureFields().Read("field:c95f3a26edf55097863a7ec47d17cce5") is not ConstantSetting{Value:false};
    private bool UnsupportedSavedRunSelection()
    {
        if(RunUi is { } active)return active.IsUnsupportedSelection;
        var fields=Draft.CaptureFields();
        var mode=fields.Read("field:8ca20000000040008000000000000001") as ConstantSetting;
        var sort=fields.Read("field:8ca20000000040008000000000000004") as ConstantSetting;
        return !BlobThresholdCapabilities.IsSupported(mode?.Value as string)||sort?.Value is not string key
            ||key is not ("SortFeatureArea" or "SortFeaturePerimeter" or "SortFeatureCircularity" or "SortFeatureRect" or "SortFeatureCentroidX" or "SortFeatureCentroidY" or "SortFeatureRectX" or "SortFeatureRectY");
    }
    private string? ValidateUiState()=>_preview.Validate()??BlobSupportedWindowState.ValidateSettings(Draft)??ValidateOrderedRanges()
        ??(_supportedState is not null?_supportedState.Validate():BaseOptions.InputContent is DeclaredInputFieldsView inputs?BlobSupportedWindowState.ValidateInputs(inputs)
            :BaseOptions.InputContentFactory is not null?"基本参数输入页尚未建立，无法核验原有输入保护；请先打开基本参数页再确认或执行。":null)
        ??(UnsupportedSavedRunSelection()?"当前阈值或排序仅为界面预览，尚未接入执行；请恢复已支持选择。":null);
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
    private bool IsCurrentPage(string page)
    {
        var pages=Draft.Schema.Presentation?.Fields.Select(field=>field.Layout?.Page??field.Group).Distinct(StringComparer.Ordinal).ToArray()??[];
        var index=_editor.SelectedPageIndex;
        return index>=0&&index<pages.Length&&StringComparer.Ordinal.Equals(pages[index],page);
    }
    private void BuildPage(string page,DeclaredFieldsView fields,FrameworkElement? pageInput)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(!ReferenceEquals(_pageHost,fields))ResetPageHost(fields);
        _activePage=page;
        if(_pendingPages.Add(page))
        {
            // The selected page must be ready before the host's normal ApplicationIdle
            // pump returns. Other pages remain background-prewarmed below, so this
            // does not reintroduce full-window eager composition.
            _editor.Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle,new Action(() =>
            {
                if(_disposed||!ReferenceEquals(_pageHost,fields)||!StringComparer.Ordinal.Equals(_activePage,page)||!IsCurrentPage(page))
                {
                    _pendingPages.Remove(page);return;
                }
                _pendingPages.Remove(page);
                try{BuildPageCore(page,fields,pageInput);Changed?.Invoke();}
                catch(Exception error){_compositionError=error.Message;SunEyeVision.Foundation.Logging.PluginLogger.Error(error.Message,nameof(BlobUiComposition));Changed?.Invoke();}
            }));
        }
    }
    private void BuildPageCore(string page,DeclaredFieldsView fields,FrameworkElement? pageInput)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(!_composedPages.Add(page))return;
        void Measure(string stage,Action action)
        {
            var clock=Stopwatch.StartNew();
            try{action();}
            finally{_stageMs[$"{page}:{stage}"]=clock.ElapsedMilliseconds;}
        }
        if(!ReferenceEquals(_pageHost,fields))ResetPageHost(fields);
        var basicPage=Draft.Schema.Inputs.Count==0?null:BaseOptions.InputSection;
        if(StringComparer.Ordinal.Equals(page,basicPage)&&_roiUi is null)
        {
            var input=pageInput??BaseOptions.InputContent;
            if(input is DeclaredInputFieldsView inputs)
            {
                Measure("ROI组合",() =>
                {
                    var pose=BlobRoiUi.Add(inputs,supportedWindow:true,observe:value=>_roiUi=value);
                    _roiOwner=new BlobLocalRoiOwner(inputs,_roiUi!,BaseOptions.RoiFrames);
                    _supportedState=new BlobSupportedWindowState(Draft,inputs,_assistPointVisibility){InformationHost=pose};_supportedState.Refresh();
                    _assistUi=pose as NativeAssistUiDraftControls;
                    fields.AddAfterField("field:c95f3a26edf55097863a7ec47d17cce5",new System.Windows.Controls.Decorator{Child=pose});
                    if(_assistUi?.GraphicVisibilitySource is {} source)fields.BindFieldVisibilityTo("field:c95f3a26edf55097863a7ec47d17cce5",source);
                    if(_roiUi is not null)_pageSubscriptions.Add(_preview.Observe(()=>_roiUi.HasUiChanges,handler=>_roiUi.Changed+=handler,handler=>_roiUi.Changed-=handler));
                    if(_assistUi is {} assist)_pageSubscriptions.Add(_preview.Observe(()=>AssistIsActive(assist)&&assist.HasUiChanges,
                        handler=>assist.Changed+=handler,handler=>assist.Changed-=handler,assist.RetireInteractions));
                    if(_roiUi is not null)_roiUi.Changed+=RefreshSupported;
                    if(_roiOwner is not null)_roiOwner.Changed+=_preview.Notify;
                });
            }
        }
        var runPage=Draft.Schema.Presentation!.Fields.First(field=>field.FieldId=="field:8ca20000000040008000000000000001").Layout?.Page;
        var resultPage=Draft.Schema.Presentation.Fields.First(field=>field.FieldId==SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Field(SunEyeVision.Tool.BlobAnalysis.Runtime.BlobResultDisplay.Text,"content")).Layout?.Page;
        if(StringComparer.Ordinal.Equals(page,runPage)&&_runOptions is null)
        {
            var baseline=PageOptions();ToolSettingsViewOptions? run=null;
            Measure("运行参数控件",() => run=BlobRunUi.AddRows(baseline,Draft,_editor,ValidateUiState,state=>RunUi=state));
            _runOptions=run!;
            Measure("运行参数挂接",() => InstallOptionsRows(fields,baseline,run!));
            Measure("运行参数状态",() => RunUi!.Attach(fields));
            _pageSubscriptions.Add(_preview.Observe(()=>RunUi.HasUiChanges,handler=>RunUi.Changed+=handler,handler=>RunUi.Changed-=handler,
                ()=>RunUi.RetireInteractions(),()=>RunUi.IsUnsupportedSelection));
        }
        if(StringComparer.Ordinal.Equals(page,resultPage)&&_displayOptions is null)
        {
            var baseline=PageOptions();ToolSettingsViewOptions? display=null;
            Measure("结果显示控件",() => display=BlobDisplayUi.AddRows(baseline,Draft.Schema,_assistPointVisibility,observe:value=>_displayUi=value,observeJudgment:value=>_judgmentUi=value));
            _displayOptions=display!;
            Measure("结果显示挂接",() => InstallOptionsRows(fields,baseline,display!));
            if(_displayUi is not null)_pageSubscriptions.Add(_preview.Observe(()=>_displayUi.HasUiChanges,handler=>_displayUi.Changed+=handler,handler=>_displayUi.Changed-=handler,()=>_displayUi.RetireInteractions()));
            if(_judgmentUi is not null)_pageSubscriptions.Add(_preview.Observe(()=>_judgmentUi.HasUiChanges,handler=>_judgmentUi.Changed+=handler,handler=>_judgmentUi.Changed-=handler,()=>_judgmentUi.RetireInteractions()));
        }
        BaseOptions.PageBuilt?.Invoke(page,fields,pageInput);
        QueuePagePrewarm(fields,page);
    }

    private void QueuePagePrewarm(DeclaredFieldsView fields,string currentPage)
    {
        if(_pagePrewarmQueued||_disposed)return;
        _pagePrewarmQueued=true;
        var pages=fields.DeclaredPageKeys.Where(page=>!StringComparer.Ordinal.Equals(page,currentPage)).ToArray();
        void Queue(int index)
        {
            if(_disposed||!ReferenceEquals(_pageHost,fields)||index>=pages.Length)return;
            // VM's first frame is assembled from the selected page only.  The
            // remaining pages use the same registered templates, but their
            // first materialization must wait until the first idle turn.  A
            // Background callback still runs before the ApplicationIdle pump
            // used by the host's first-frame boundary and therefore makes the
            // window pay for every page before it is visible.
            _editor.Dispatcher.BeginInvoke(DispatcherPriority.SystemIdle,new Action(() =>
            {
                if(_disposed||!ReferenceEquals(_pageHost,fields))return;
                var page=pages[index];
                try
                {
                    fields.RealizePage(page);
                    var input=StringComparer.Ordinal.Equals(page,BaseOptions.InputSection)?BaseOptions.InputContent:null;
                    BuildPageCore(page,fields,input);
                }
                catch(Exception error)
                {
                    _compositionError=error.Message;
                    SunEyeVision.Foundation.Logging.PluginLogger.Error(error.Message,nameof(BlobUiComposition));
                    Changed?.Invoke();
                    return;
                }
                Queue(index+1);
            }));
        }
        Queue(0);
    }
    private ToolSettingsViewOptions PageOptions()=>_assistUi is null?_composed:_composed with
        {AdditionalAfterContent=_assistUi,AdditionalAfterField="field:c95f3a26edf55097863a7ec47d17cce5"};
    private void ResetPageHost(DeclaredFieldsView next)
    {
        var previous=_pageHost;
        foreach(var subscription in _pageSubscriptions.ToArray())subscription.Dispose();_pageSubscriptions.Clear();
        var oldRun=RunUi;RunUi=null;
        var oldDisplay=_displayUi;_displayUi=null;
        var oldJudgment=_judgmentUi;_judgmentUi=null;
        var oldOwner=_roiOwner;_roiOwner=null;
        var oldSupported=_supportedState;_supportedState=null;
        var oldAssist=_assistUi;_assistUi=null;
        var oldRoi=_roiUi;_roiUi=null;
        if(oldOwner is not null)oldOwner.Changed-=_preview.Notify;
        if(oldRoi is not null)oldRoi.Changed-=RefreshSupported;
        var errors=new List<Exception>();
        foreach(var action in new Action[]{()=>oldRun?.Dispose(),()=>oldDisplay?.Dispose(),()=>oldJudgment?.Dispose(),()=>oldOwner?.Dispose(),()=>oldSupported?.Dispose(),()=>oldAssist?.Dispose(),()=>oldRoi?.Dispose(),DisposeDeferredContents})
            try{action();}catch(Exception error){errors.Add(error);}
        _runOptions=null;_displayOptions=null;_pageHost=next;RefreshSupported();
        if(errors.Count!=0)throw new AggregateException("参数页宿主切换期间旧页清理失败。",errors);
    }
    private void InstallOptionsRows(DeclaredFieldsView fields,ToolSettingsViewOptions baseline,ToolSettingsViewOptions pageOptions)
    {
        var existing=baseline.AdditionalAnchoredContents?.Select(item=>item.Content).ToHashSet<FrameworkElement>(ReferenceEqualityComparer.Instance)
            ??new HashSet<FrameworkElement>(ReferenceEqualityComparer.Instance);
        foreach(var item in pageOptions.AdditionalAnchoredContents??Array.Empty<AnchoredSettingsContent>())
            if(!existing.Contains(item.Content)){fields.AddAfterField(item.FieldId,item.Content,item.Condition,item.FollowAnchorVisibility,item.InsertBefore);_deferredContents.Add(item.Content);}
        var baselineSuppressed=baseline.SuppressedFieldRows is {} suppressed? suppressed:new HashSet<string>(StringComparer.Ordinal);
        IEnumerable<string> pageSuppressed=pageOptions.SuppressedFieldRows is {} declaredSuppressed?declaredSuppressed:Array.Empty<string>();
        var newSuppressed=pageSuppressed.Where(id=>!baselineSuppressed.Contains(id)).ToArray();
        if(newSuppressed.Length!=0)fields.SuppressFieldRows(newSuppressed);
        var baselineSources=baseline.FieldVisibilitySources??new Dictionary<string,FrameworkElement>();
        foreach(var item in pageOptions.FieldVisibilitySources??new Dictionary<string,FrameworkElement>())
            if(!baselineSources.TryGetValue(item.Key,out var source)||!ReferenceEquals(source,item.Value))fields.BindFieldVisibilityTo(item.Key,item.Value);
        if(pageOptions.AdditionalAfterContent is {} after&&!ReferenceEquals(after,baseline.AdditionalAfterContent))_deferredContents.Add(after);
        foreach(var item in pageOptions.AdditionalPageContents?.Values??Array.Empty<FrameworkElement>())
            if(!(baseline.AdditionalPageContents?.Values.Contains(item,ReferenceEqualityComparer.Instance)??false))_deferredContents.Add(item);
    }
    private void DisposeDeferredContents()
    {
        var errors=new List<Exception>();
        foreach(var item in _deferredContents.Distinct(ReferenceEqualityComparer.Instance).OfType<IDisposable>())
            try{item.Dispose();}catch(Exception error){errors.Add(error);}
        _deferredContents.Clear();
        if(errors.Count!=0)throw new AggregateException("按页控件清理失败。",errors);
    }
    internal void RebindSameContext(){ObjectDisposedException.ThrowIf(_disposed,this);_preview.RetireInteractions();_editor.Bind(Draft,_composed);RunUi?.Attach(_pageHost!);}
    internal bool TryCapture(out FieldSettings fields,out string error)
    {
        fields=null!;
        if(!IsReady){error="参数组合仍在组装，请稍候。";return false;}
        if(_preview.Validate() is {} preview){error=preview;return false;}
        if(_roiOwner is not null&&!_roiOwner.TryCapture(out error))return false;
        if(_assistUi is {IsUnimplementedMode:true} assist&&AssistIsActive(assist))
        {error="按点和按坐标的位置修正尚未接入，请切回按信息。";return false;}
        return _editor.TryCapture(out fields,out error);
    }
    private void OnPageChanged(object? sender,EventArgs args)=>_preview.RetireInteractions();
    private void OnVisibility(object sender,DependencyPropertyChangedEventArgs args){if(!_editor.IsVisible)_preview.RetireInteractions();}
    private void OnUnloaded(object sender,RoutedEventArgs args)=>_preview.RetireInteractions();
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;
        _pendingPages.Clear();_activePage=null;
        _editor.Changed-=RefreshSupported;PageDescriptor.RemoveValueChanged(_editor,OnPageChanged);_editor.IsVisibleChanged-=OnVisibility;_editor.Unloaded-=OnUnloaded;
        if(_roiOwner is not null)_roiOwner.Changed-=_preview.Notify;
        if(_roiUi is not null)_roiUi.Changed-=RefreshSupported;
        _preview.Changed-=Notify;
        var errors=new List<Exception>();foreach(var action in new Action[]{()=>{foreach(var subscription in _pageSubscriptions.ToArray())subscription.Dispose();_pageSubscriptions.Clear();},_preview.Dispose,()=>RunUi?.Dispose(),()=>_roiOwner?.Dispose(),()=>_supportedState?.Dispose(),()=>_displayUi?.Dispose(),()=>_judgmentUi?.Dispose(),()=>_assistUi?.Dispose(),()=>_roiUi?.Dispose(),DisposeDeferredContents,()=>{if(!_bound&&(BaseOptions.InputContent is IDisposable input))input.Dispose();}})
            try{action();}catch(Exception error){errors.Add(error);}
        if(errors.Count!=0)throw new AggregateException("Blob界面作用域清理失败。",errors);
    }
}
