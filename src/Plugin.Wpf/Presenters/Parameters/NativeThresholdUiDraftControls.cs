using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Presenters.Numeric;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Six original threshold UI branches. No algorithm configuration is written.</summary>
public sealed class NativeThresholdUiDraftControls : StackPanel,IDisposable
{
    private readonly List<DeclaredFieldEditor> _editors=[];
    private readonly List<(FrameworkElement View,string[] Modes)> _branches=[];
    private readonly DeclaredFieldEditor _mode;
    private readonly DeclaredFieldState _softLow,_softHigh;
    private readonly NativeRangePresenter _softRange;
    private bool _disposed;
    internal IEnumerable<FrameworkElement> Rows=>_editors.Cast<FrameworkElement>().Append(_softRange);
    public NativeThresholdUiDraftControls()
    {
        var labels=new Dictionary<string,string>{{"binary","不进行二值化"},{"single","单阈值"},{"double","双阈值"},
            {"auto","自动阈值"},{"fixed","软阈值（固定）"},{"relative","软阈值（相对）"}};
        _mode=Choice("mode","阈值方式",labels,"single");
        _mode.Changed+=Project;
        var polarity=Choice("polarity","极性",new Dictionary<string,string>{{"bright","亮于背景"},{"dark","暗于背景"}},"bright");
        Branch(polarity,"binary","single","auto","fixed","relative");
        Branch(Number("low","低阈值",100,0,255),"single","double");
        Branch(Number("high","高阈值",150,0,255),"double");
        var lowPort=new Port("ui-only-threshold.soft-low",DataType.Integer,Storage:NumericStorage.Int32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference);
        var highPort=lowPort with{Id="ui-only-threshold.soft-high"};
        _softLow=new(lowPort,new ConstantSetting(100L),_=>{});_softHigh=new(highPort,new ConstantSetting(150L),_=>{});
        var layout=new FieldLayout("运行参数","运行参数",Minimum:0,Maximum:255,DecimalPlaces:0,Increment:1);
        var lower=new FieldPresentation(lowPort.Id,"阈值范围","运行参数",0,FieldEditorKind.Scalar){Layout=layout with{Minimum=1}};
        var upper=new FieldPresentation(highPort.Id,"阈值范围","运行参数",1,FieldEditorKind.Scalar){Layout=layout};
        _softRange=new(_softLow,_softHigh,lower,upper,_=>{},ReferenceFieldOptions.Empty,null);
        Children.Add(_softRange);Branch(_softRange,"fixed");
        Branch(Number("softness","软阈值柔和度",254,0,254),"fixed","relative");
        foreach(var (id,label,value) in new[]{("left","低尾部比例",5L),("right","高尾部比例",5L),("bottom","低阈值比例",40L),("top","高阈值比例",60L)})
            Branch(Number(id,label,value,0,100),"relative");
        ToolTip="纯UI试用，阈值功能尚未接入。";Project();
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
    public bool TrySelectMode(string mode)=>_mode.TrySetConstant(mode);
    private void Project()
    {
        if(_disposed)return;
        var mode=((ConstantSetting)_mode.Setting).Value as string;
        foreach(var (view,modes) in _branches)view.Visibility=modes.Contains(mode)?Visibility.Visible:Visibility.Collapsed;
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_mode.Changed-=Project;Children.Clear();
        foreach(var editor in _editors)editor.Dispose();_softRange.Dispose();_softLow.Dispose();_softHigh.Dispose();
    }
}
