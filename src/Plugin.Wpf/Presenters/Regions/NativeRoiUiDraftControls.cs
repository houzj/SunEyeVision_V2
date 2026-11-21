using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;

namespace SunEyeVision.Plugin.Wpf.Presenters.Regions;

/// <summary>Original ROI groups over the formal source editors. Input view owns the field rows.</summary>
public sealed class NativeRoiUiDraftControls : StackPanel, INativeRoiControls, IDisposable
{
    private readonly List<DeclaredFieldState> _states = [];
    private readonly List<DeclaredFieldRow> _radios = [];
    private readonly List<DeclaredFieldRow> _sources = [];
    private readonly Dictionary<DeclaredFieldRow,bool> _interactionVisibility = new();
    private readonly List<DeclaredFieldRow> _rectangleParameters = [], _circleParameters = [];
    private readonly ContentControl _detectionShapes = new(), _maskShapes = new();
    private readonly DeclaredFieldState _detection, _inherit, _mask;
    private readonly FrameworkElement _detectionRadio, _inheritRadio, _maskRadio;
    private readonly DeclaredFieldRow? _area, _outputMask, _maskImage, _maskSource;
    private readonly NativeRoiInheritanceBinding _inheritance;
    private readonly Func<bool> _readMaskMode;
    private readonly bool _showMaskGroup, _showAreaType, _showMaskImage, _showOutputMask, _showAnnulusSource;
    private readonly IReadOnlyList<string> _inheritanceLabels;
    private bool _rectangleParametersRealized, _circleParametersRealized, _disposed;
    private bool _positionCorrectionEnabled;
    private bool _projectingModes;
    private long _interactionRevision;
    public event Action? Changed;
    /// <summary>容器视觉根，供宿主做可见性与加载状态联动。</summary>
    public FrameworkElement Element => this;
    public FrameworkElement GraphicVisibilitySource => _detectionRadio;
    public bool IsImageArea => !_disposed && _showAreaType && _area?.Setting is ConstantSetting { Value:1L };
    public bool IsInheritedMask => _showMaskGroup && !_disposed && !IsImageArea && Value(_mask) == 1;
    public void SetPositionCorrectionEnabled(bool enabled)
    {
        if (_disposed) return;
        var changed = _positionCorrectionEnabled != enabled;
        _positionCorrectionEnabled = enabled;
        // Re-project even when unchanged because AssistGroup can attach after
        // the first state projection.
        ProjectVisibility();
        if (changed) Notify();
    }
    public NativeRoiUiDraftControls(NativeRoiInheritanceBinding inheritance, DeclaredFieldRow? maskImage,
        DeclaredFieldRow? area, DeclaredFieldRow? outputMask,
        DeclaredFieldRow? maskSource, Func<bool> readMaskMode, Action<bool> acceptMaskMode,
        bool showMaskGroup = true, bool showAreaType = true, bool showMaskImage = true,
        bool showOutputMask = true, bool showAnnulusSource = true,
        IReadOnlyList<string>? inheritanceLabels = null)
    {
        _inheritance = inheritance;
        _showMaskGroup = showMaskGroup;
        _showAreaType = showAreaType;
        _showMaskImage = showMaskImage;
        _showOutputMask = showOutputMask;
        _showAnnulusSource = showAnnulusSource;
        _inheritanceLabels = inheritanceLabels?.ToArray() ?? ["By rectangle area", "By rectangle parameter", "By circle area", "By circle parameter"];
        _area = area;
        _outputMask = outputMask;
        _maskImage = maskImage;
        _maskSource = maskSource;
        _readMaskMode=readMaskMode;
        var initialMode=inheritance.ReadMode();
        ValidateMode(initialMode);
        if (_showOutputMask && outputMask is not null) AddSource(Children, outputMask);
        if (_showAreaType && area is not null) Children.Add(area);
        if (_showMaskImage && _maskImage is not null) AddSource(Children, _maskImage);
        (_detection,_detectionRadio) = AddRadio("roi-creation","ROI Creation",["RunParam_Draw","RunParam_Inherit"],initialMode is null?0:1,
            value=>inheritance.AcceptMode(value==0?null:(int)Value(_inherit)));
        Children.Add(_detectionShapes);
        (_inherit,_inheritRadio) = AddRadio("roi-inheritance","Inheritance Mode",_inheritanceLabels,initialMode??0,
            value=>{if(Value(_detection)==1)inheritance.AcceptMode((int)value);});
        AddSource(Children,inheritance.RectangleSource);
        if (_showAnnulusSource) AddSource(Children, inheritance.AnnulusSource);
        (_mask,_maskRadio) = AddRadio("roi-mask-creation","ExternROI Creation",["RunParam_Draw","RunParam_Inherit"],readMaskMode()?1:0,value=>acceptMaskMode(value==1));
        Children.Add(_maskShapes);
        if (_showMaskGroup && _maskSource is not null) AddSource(Children,maskSource);
        foreach (var state in _states) state.Changed += OnModeChanged;
        if (_area is not null) _area.Changed += OnModeChanged;
        IsVisibleChanged += OnVisibilityChanged;
        ProjectVisibility();
    }

    private static void ValidateMode(int? mode)
    {
        if (mode is <0 or >3) throw new ArgumentException("继承组合需要四种模式。");
    }
    public void RefreshAcceptedModes(Action refreshFormalRows)
    {
        if (_disposed||_projectingModes) return;
        _projectingModes=true;
        try
        {
            refreshFormalRows();
            if (_disposed) return;
            var inheritedMode=_inheritance.ReadMode();
            ValidateMode(inheritedMode);
            var mask=_readMaskMode();
            if (inheritedMode is int mode) _inherit.ProjectAcceptedInput(new ConstantSetting((long)mode));
            _detection.ProjectAcceptedInput(new ConstantSetting(inheritedMode is null?0L:1L));
            _mask.ProjectAcceptedInput(new ConstantSetting(mask?1L:0L));
        }
        finally { _projectingModes=false; }
        OnModeChanged();
    }

    private IEnumerable<DeclaredFieldRow> ActiveSources()
    {
        if (_disposed) yield break;
        if (_showOutputMask && _outputMask is not null) yield return _outputMask;
        if (IsImageArea)
        {
            if (_maskImage is not null) yield return _maskImage;
            yield break;
        }
        // VM declares ROI sources independently from the fixture input. The
        // position-correction switch must not remove an ROI source from capture.
        if (Value(_detection)==1) switch ((int)Value(_inherit))
        {
            case 0: yield return _inheritance.RectangleSource; break;
            case 1:
                foreach (var row in _rectangleParameters) yield return row;
                break;
            case 2: yield return _inheritance.AnnulusSource; break;
            case 3:
                foreach (var row in _circleParameters) yield return row;
                break;
        }
        if (IsInheritedMask && _maskSource is not null) yield return _maskSource;
    }
    public bool HasPendingEdits => ActiveSources().Any(row => row.HasPendingEdits);
    public bool TryCaptureActiveSources(out string error)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var sources=ActiveSources().ToArray();
        foreach (var row in sources)
        {
            if (_disposed || !ActiveSources().Contains(row))
            {
                error="捕获期间 ROI 来源已切换，请重试。";
                return false;
            }
            if (row.HasPendingEdits && !row.TryCommitPending())
            {
                error=string.IsNullOrEmpty(row.Error)?"ROI 来源尚未接纳。":row.Error;
                return false;
            }
        }
        if (_disposed || !sources.SequenceEqual(ActiveSources()))
        {
            error="捕获期间 ROI 来源已切换，请重试。";
            return false;
        }
        error="";
        return true;
    }

    public bool IsLocalDrawing(string group) => !_disposed && !IsImageArea && Value(group=="inspection"?_detection:_mask)==0;
    public void BindLocalEditors(NativeRoiEditor detection, NativeRoiEditor mask)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if (_detectionShapes.Content is not null || detection.Parent is not null || mask.Parent is not null)
            throw new InvalidOperationException("区域编辑器已经归属视图。");
        _detectionShapes.Content=detection;
        _maskShapes.Content=mask;
    }
    private void AddSource(UIElementCollection target, DeclaredFieldRow field)
    {
        target.Add(field);
        _sources.Add(field);
        field.Changed += Notify;
    }
    /// <summary>
    /// 参数行必须紧跟继承方式（原厂 Tab_ROI Area：继承方式 → 参数行 → 屏蔽区创建 → 屏蔽区）。
    /// 不能用 AddSource(Children,row) 追加到末尾，否则参数行会落到屏蔽区/位置修正之后。
    /// </summary>
    private void InsertParameterSource(DeclaredFieldRow field)
    {
        var index = _maskRadio is null ? Children.Count : Children.IndexOf(_maskRadio);
        if (index < 0) index = Children.Count;
        Children.Insert(index, field);
        _sources.Add(field);
        field.Changed += Notify;
    }
    private void EnsureParameterEditors(int mode)
    {
        if (mode==1 && !_rectangleParametersRealized)
        {
            foreach (var row in _inheritance.RectangleParametersFactory()) { _rectangleParameters.Add(row); InsertParameterSource(row); }
            _rectangleParametersRealized=true;
        }
        if (mode==3 && !_circleParametersRealized)
        {
            foreach (var row in _inheritance.AnnulusParametersFactory()) { _circleParameters.Add(row); InsertParameterSource(row); }
            _circleParametersRealized=true;
        }
    }
    private (DeclaredFieldState,FrameworkElement) AddRadio(string id,string label,IReadOnlyList<string> choices,long initial,Action<long> accept)
    {
        var state = new DeclaredFieldState(new Port(id,DataType.Integer),new ConstantSetting(initial),
            proposal=>accept((long)((ConstantSetting)proposal).Value!));
        _states.Add(state);
        var radio = new DeclaredFieldRow(state,label,
            integerRadioOptions:new NativeIntegerRadioOptions(choices.Select((value,index)=>new NativeIntegerRadioChoice(index,AppLocalizationService.GetLang(value)??value)),useFiveSlotTemplate:choices.Count>2));
        _radios.Add(radio);
        Children.Add(radio);
        return (state,radio);
    }
    private static long Value(DeclaredFieldState state) => (long)((ConstantSetting)state.Accepted).Value!;
    private void Notify(){if(!_disposed&&!_projectingModes)Changed?.Invoke();}
    private void OnModeChanged(){if(!_disposed&&!_projectingModes){ProjectVisibility();Notify();}}
    private void ProjectVisibility()
    {
        var graphic=!IsImageArea;
        var draw=Value(_detection)==0;
        var mode=(int)Value(_inherit);
        if (graphic&&!draw) EnsureParameterEditors(mode);
        if (_maskImage is not null) _maskImage.Visibility=Shown(_showMaskImage&&_showMaskGroup&&!graphic);
        // ROI 继承是 RoiSelectGroup/InheritWayGroup 的状态，位置修正属于
        // AssistGroup。原厂两组独立，位置修正关闭不能隐藏已选择的 ROI 来源。
        _inheritance.RectangleSource.Visibility=Shown(graphic&&!draw&&mode==0);
        _inheritance.AnnulusSource.Visibility=Shown(_showAnnulusSource&&graphic&&!draw&&mode==2);
        foreach (var row in _rectangleParameters) row.Visibility=Shown(graphic&&!draw&&mode==1);
        foreach (var row in _circleParameters) row.Visibility=Shown(graphic&&!draw&&mode==3);
        if (_maskSource is not null) _maskSource.Visibility=Shown(_showMaskGroup&&IsInheritedMask);
        _detectionRadio.Visibility=Shown(graphic);
        _detectionShapes.Visibility=Shown(graphic&&draw);
        _inheritRadio.Visibility=Shown(graphic&&!draw&&_inheritanceLabels.Count>1);
        _maskRadio.Visibility=Shown(_showMaskGroup&&graphic);
        _maskShapes.Visibility=Shown(_showMaskGroup&&graphic&&!IsInheritedMask);
        PrepareSourceInteractions();
    }
    private void OnVisibilityChanged(object sender,DependencyPropertyChangedEventArgs args)=>PrepareSourceInteractions();
    private void PrepareSourceInteractions()
    {
        if (_disposed) return;
        var revision=++_interactionRevision;
        var active=IsVisible?ActiveSources().ToHashSet():null;
        ParameterCleanup.Run(_sources.ToArray().Select(row=>(Action)(()=>
        {
            if (_disposed || revision!=_interactionRevision) return;
            var visible=active?.Contains(row)==true;
            if (_interactionVisibility.TryGetValue(row,out var previous)&&previous==visible) return;
            _interactionVisibility[row]=visible;
            ((IParameterVisibility)row).PrepareForVisibility(visible);
        })));
    }
    private static Visibility Shown(bool show) => show?Visibility.Visible:Visibility.Collapsed;
    public void Dispose()
    {
        if (_disposed) return;
        _disposed=true;
        Changed=null;
        IsVisibleChanged-=OnVisibilityChanged;
        if (_area is not null) _area.Changed-=OnModeChanged;
        _interactionVisibility.Clear();
        foreach (var field in _sources) field.Changed-=Notify;
        foreach (var state in _states) state.Changed-=OnModeChanged;
        _detectionShapes.Content=null;
        _maskShapes.Content=null;
        Children.Clear();
        _rectangleParameters.Clear();
        _circleParameters.Clear();
        ParameterCleanup.Run(_sources.Select(row=>(Action)(()=>((IParameterVisibility)row).PrepareForVisibility(false)))
            .Concat(_radios.Select(radio=>(Action)radio.Dispose)).Concat(_states.Select(state=>(Action)state.Dispose)));
    }
}
