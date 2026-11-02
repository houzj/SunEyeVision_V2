using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Six original threshold UI branches. No algorithm configuration is written.</summary>
public sealed class NativeThresholdUiDraftControls : StackPanel,IDisposable,IParameterVisibility
{
    private readonly List<DeclaredFieldEditor> _editors=[];
    private readonly List<(FrameworkElement View,string[] Modes)> _branches=[];
    private readonly DeclaredFieldEditor _mode;
    private readonly DeclaredFieldState _softLow,_softHigh;
    private readonly NativeRangePresenter _softRange;
    private bool _disposed,_seeding;
    private readonly bool _externalSingleFields;
    private readonly DeclaredFieldEditor _polarity,_low;
    private readonly Dictionary<DeclaredFieldEditor,ParameterSetting> _baseline=[];
    private Func<(ParameterSetting Polarity,ParameterSetting Low,string? LowPendingRaw)>? _readSingle;
    private string? _seedRaw;
    private bool _projectionSeeded,_seedRawApplied;
    private string _lastMode="SingleThreshold";
    public event Action? Changed;
    public string SelectedMode=>((ConstantSetting)_mode.Setting).Value as string??"SingleThreshold";
    public bool IsSingleMode=>SelectedMode=="SingleThreshold";
    public FrameworkElement SingleVisibilitySource {get;}=new Border();
    private bool SeedPending(DeclaredFieldEditor e)=>ReferenceEquals(e,_low)&&_seedRaw is not null&&e.InputElement is TextBox text&&text.Text==_seedRaw;
    public bool HasPendingEdits=>!_disposed&&(_editors.Any(e=>e.HasPendingEdits&&!SeedPending(e))||_softRange.HasPendingEdits(0)||_softRange.HasPendingEdits(1));
    public bool HasInvalidInput=>!_disposed&&(_editors.Any(e=>e.HasInvalidText&&!SeedPending(e))||_softLow.HasInvalidInput||_softHigh.HasInvalidInput);
    public bool HasUiChanges=>!_disposed&&(HasPendingEdits||HasInvalidInput||_baseline.Any(p=>!DeclaredFieldState.SameSetting(p.Key.Setting,p.Value))
        ||!DeclaredFieldState.SameSetting(_softLow.Accepted,new ConstantSetting(100L))||!DeclaredFieldState.SameSetting(_softHigh.Accepted,new ConstantSetting(150L)));
    private void OnChanged(){if(!_disposed&&!_seeding)Changed?.Invoke();}
    private void OnRawChanged(object sender,TextChangedEventArgs args)=>OnChanged();
    internal IEnumerable<FrameworkElement> Rows=>_editors.Cast<FrameworkElement>().Append(_softRange);
    public NativeThresholdUiDraftControls(bool externalSingleFields=false)
    {
        _externalSingleFields=externalSingleFields;
        var labels=new Dictionary<string,string>{{"BinaryImage","不进行二值化"},{"SingleThreshold","单阈值"},{"DoubleThreshold","双阈值"},
            {"AutoThreshold","自动阈值"},{"FixedSoftThreshold","软阈值（固定）"},{"RelativeSoftThreshold","软阈值（相对）"}};
        _mode=Choice("mode","阈值方式",labels,"SingleThreshold");
        _mode.Changed+=Project;
        var polarity=_polarity=Choice("polarity","极性",new Dictionary<string,string>{{"bright","亮于背景"},{"dark","暗于背景"}},"bright");
        Branch(polarity,"BinaryImage","SingleThreshold","AutoThreshold","FixedSoftThreshold","RelativeSoftThreshold");
        Branch(_low=Number("low","低阈值",100,0,255),"SingleThreshold","DoubleThreshold");
        Branch(Number("high","高阈值",150,0,255),"DoubleThreshold");
        var lowPort=new Port("ui-only-threshold.soft-low",DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
        var highPort=lowPort with{Id="ui-only-threshold.soft-high"};
        _softLow=new(lowPort,new ConstantSetting(100L),_=>{});_softHigh=new(highPort,new ConstantSetting(150L),_=>{});
        var layout=new FieldLayout("运行参数","运行参数",Minimum:0,Maximum:255,DecimalPlaces:0,Increment:1);
        var lower=new FieldPresentation(lowPort.Id,"阈值范围","运行参数",0,FieldEditorKind.Scalar){Layout=layout with{Minimum=1}};
        var upper=new FieldPresentation(highPort.Id,"阈值范围","运行参数",1,FieldEditorKind.Scalar){Layout=layout};
        _softRange=new(_softLow,_softHigh,lower,upper,_=>{},ReferenceFieldOptions.Empty,null);
        Children.Add(_softRange);Branch(_softRange,"FixedSoftThreshold");
        Branch(Number("softness","软阈值柔和度",254,0,254),"FixedSoftThreshold","RelativeSoftThreshold");
        foreach(var (id,label,value) in new[]{("left","低尾部比例",5L),("right","高尾部比例",5L),("bottom","低阈值比例",40L),("top","高阈值比例",60L)})
            Branch(Number(id,label,value,0,100),"RelativeSoftThreshold");
        foreach(var editor in _editors){_baseline.Add(editor,editor.Setting);editor.Changed+=OnChanged;}
        _softLow.Changed+=OnChanged;_softHigh.Changed+=OnChanged;
        AddHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnRawChanged));
        IsVisibleChanged+=OnVisible;_low.Loaded+=OnLowLoaded;Project();
    }
    private DeclaredFieldEditor Choice(string id,string label,Dictionary<string,string> labels,string initial)
    {
        var port=new Port("ui-only-threshold."+id,DataType.Text,EditModes:FieldEditModes.Constant,Choices:new TextChoiceConstraint(labels.Keys));
        var presentation=new FieldPresentation(port.Id,label,"运行参数",0,FieldEditorKind.Choice,labels.Keys,labels){Layout=new("运行参数","运行参数")};
        return Add(new(port,label,new ConstantSetting(initial),_=>{},choiceLabels:labels,presentation:presentation));
    }
    private DeclaredFieldEditor Number(string id,string label,long initial,double minimum,double maximum)
    {
        var port=new Port("ui-only-threshold."+id,DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
        var presentation=new FieldPresentation(port.Id,label,"运行参数",0,FieldEditorKind.Scalar)
            {Layout=new("运行参数","运行参数",Minimum:minimum,Maximum:maximum,DecimalPlaces:0,Increment:1)};
        return Add(new(port,label,new ConstantSetting(initial),_=>{},ReferenceFieldOptions.Empty,presentation:presentation));
    }
    private DeclaredFieldEditor Add(DeclaredFieldEditor editor){_editors.Add(editor);Children.Add(editor);return editor;}
    private void Branch(FrameworkElement view,params string[] modes)=>_branches.Add((view,modes));
    public void ConfigureSingleProjection(Func<(ParameterSetting Polarity,ParameterSetting Low,string? LowPendingRaw)> read)
    {ObjectDisposedException.ThrowIf(_disposed,this);_readSingle=read??throw new ArgumentNullException(nameof(read));SeedSingle();}
    private void SeedSingle()
    {
        if(_readSingle is null||_disposed||(_projectionSeeded&&(HasPendingEdits||HasInvalidInput||_baseline.Any(p=>!ReferenceEquals(p.Key,_mode)&&!DeclaredFieldState.SameSetting(p.Key.Setting,p.Value))
            ||!DeclaredFieldState.SameSetting(_softLow.Accepted,new ConstantSetting(100L))||!DeclaredFieldState.SameSetting(_softHigh.Accepted,new ConstantSetting(150L)))))return;
        var seed=_readSingle();_seeding=true;
        try
        {
            _polarity.TryApplySetting(seed.Polarity);_low.TryApplySetting(seed.Low);
            _baseline[_polarity]=_polarity.Setting;_baseline[_low]=_low.Setting;
            _seedRaw=seed.LowPendingRaw;_seedRawApplied=false;_projectionSeeded=true;ApplySeedRaw();
        }
        finally{_seeding=false;}
    }
    private void OnLowLoaded(object sender,RoutedEventArgs args)=>ApplySeedRaw();
    private void ApplySeedRaw()
    {if(!_seedRawApplied&&_seedRaw is not null&&_low.InputElement is TextBox text){_seedRawApplied=true;var prior=_seeding;_seeding=true;try{text.Text=_seedRaw;}finally{_seeding=prior;}}}
    public bool TrySelectMode(string mode)
    {if(_disposed)return false;if(IsSingleMode)SeedSingle();return _mode.TrySetConstant(mode);}
    private void Project()
    {
        if(_disposed)return;
        var mode=SelectedMode;
        if(_lastMode=="SingleThreshold"&&mode!=_lastMode)SeedSingle();
        _lastMode=mode;
        ParameterCleanup.Run(_branches.Select(branch=>(Action)(()=>
        {
            var visible=branch.Modes.Contains(mode)&&!(_externalSingleFields&&IsSingleMode&&(ReferenceEquals(branch.View,_low)||ReferenceEquals(branch.View,_polarity)));
            (branch.View as IParameterVisibility)?.PrepareForVisibility(visible&&IsVisible);
            branch.View.Visibility=visible?Visibility.Visible:Visibility.Collapsed;
        })));
        SingleVisibilitySource.Visibility=IsSingleMode?Visibility.Visible:Visibility.Collapsed;
    }
    private void OnVisible(object sender,DependencyPropertyChangedEventArgs args)=>((IParameterVisibility)this).PrepareForVisibility(IsVisible);
    void IParameterVisibility.PrepareForVisibility(bool visible)
    {ParameterCleanup.Run(_editors.Cast<FrameworkElement>().Append(_softRange).Select(view=>(Action)(()=> (view as IParameterVisibility)?.PrepareForVisibility(visible&&view.Visibility==Visibility.Visible))));}
    public void RetireInteractions(){if(_disposed)return;((IParameterVisibility)this).PrepareForVisibility(false);if(IsVisible)((IParameterVisibility)this).PrepareForVisibility(true);}
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;Changed=null;_readSingle=null;
        _mode.Changed-=Project;_low.Loaded-=OnLowLoaded;IsVisibleChanged-=OnVisible;
        RemoveHandler(TextBoxBase.TextChangedEvent,new TextChangedEventHandler(OnRawChanged));
        foreach(var editor in _editors)editor.Changed-=OnChanged;
        _softLow.Changed-=OnChanged;_softHigh.Changed-=OnChanged;
        ParameterCleanup.Run(new Action[]{()=>Children.Clear()}.Concat(_editors.Select(editor=>(Action)editor.Dispose))
            .Concat(new Action[]{_softRange.Dispose,_softLow.Dispose,_softHigh.Dispose}));
    }
}
