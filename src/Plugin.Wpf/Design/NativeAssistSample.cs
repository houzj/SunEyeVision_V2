using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Design;

public partial class NativeAssistPreview : UserControl,IDisposable
{
    public NativeAssistPreview()=>InitializeComponent();
    public void Dispose(){var content=Content;Content=null;(content as IDisposable)?.Dispose();}
}

/// <summary>Presentation-only declarations over the actual field composition. No algorithm,
/// Runtime settings defaults or native FIXTURE encoding are implemented by this example.</summary>
public sealed class NativeAssistSample : Decorator,IDisposable
{
    public static readonly DependencyProperty PreviewModeProperty=DependencyProperty.Register(nameof(PreviewMode),typeof(int),
        typeof(NativeAssistSample),new PropertyMetadata(-1,OnMode),value=>(int)value is >=-1 and <=2);
    public int PreviewMode {get=>(int)GetValue(PreviewModeProperty);set=>SetValue(PreviewModeProperty,value);}
    public DeclaredFieldsView Fields {get;}
    public NativeAssistOptions Assist {get;}
    public IReadOnlyList<Port> Ports {get;}
    public IReadOnlyDictionary<string,ParameterSetting>? LastProposal {get;private set;}
    public int Writes {get;private set;}
    public bool RejectNext {get;set;}
    private bool _disposed;
    public NativeAssistSample()
    {
        // The design caller supplies an opaque declaration, not a claimed VM ABI or affine matrix.
        var fixture=new Port("assist.fixture",TypeRef.RecordOf("design-only.assist-info",Array.Empty<Port>()),
            Required:false,EditModes:FieldEditModes.Reference);
        Assist=new("assist.enabled","assist.mode",fixture,"assist.base-point","assist.run-point",
            new("assist.base-x","assist.base-y","assist.base-angle","assist.base-scale-x","assist.base-scale-y"),
            new("assist.run-x","assist.run-y","assist.run-angle","assist.run-scale-x","assist.run-scale-y"));
        var point=TypeRef.GeometryOf(GeometryKind.Point,new("design-assist-image",CoordinateDomain.Image,CoordinateUnit.Pixel));
        var rows=new List<(Port Port,FieldPresentation Presentation,ParameterSetting Value)>();
        void Add(Port port,string label,ParameterSetting initial,int? digits=2)
        {rows.Add((port,new FieldPresentation(port.Id,label,"位置修正",rows.Count,FieldEditorKind.Scalar)
            {Layout=new("基本参数","位置修正",DecimalPlaces:digits)},initial));}
        Add(new(Assist.EnabledFieldId,DataType.Boolean,EditModes:FieldEditModes.Constant),"位置修正",new ConstantSetting(false));
        Add(new(Assist.ModeFieldId,DataType.Integer,EditModes:FieldEditModes.Constant),"选择方式",new ConstantSetting(0L));
        Add(fixture,"修正信息",new UnsetSetting());
        void AddPoint(string id,string label)=>Add(new(id,point,Required:false,EditModes:FieldEditModes.Reference),label,new UnsetSetting());
        void AddNumbers(NativeAssistNumbers numbers,string prefix)
        {
            foreach(var (id,label) in new[]{(numbers.XFieldId,"点X"),(numbers.YFieldId,"点Y"),
                (numbers.AngleFieldId,"角度"),(numbers.ScaleXFieldId,"尺度X"),(numbers.ScaleYFieldId,"尺度Y")})
                Add(new(id,DataType.Number,Required:false,Storage:NumericStorage.Float32,
                    EditModes:FieldEditModes.Constant|FieldEditModes.Reference),prefix+label,new UnsetSetting(),null);
        }
        AddPoint(Assist.BasePointFieldId,"基准点");AddNumbers(Assist.Base,"基准");
        AddPoint(Assist.RunPointFieldId,"运行点");AddNumbers(Assist.Run,"运行");
        Ports=Array.AsReadOnly(rows.Select(row=>row.Port).ToArray());
        var sources=rows.Skip(2).Select(row=>new DeclaredReferenceCandidate(row.Presentation.DisplayName,"design-producer","design-scope",
            row.Port with{Id="out."+row.Port.Id,Required=true,EditModes=FieldEditModes.None},Array.Empty<string>())).ToArray();
        var index=new DeclaredReferenceCandidate("索引","design-producer","design-scope",new("index",DataType.Integer),Array.Empty<string>());
        var candidates=new ReferenceCandidates(sources,new[]{index});
        Fields=new(rows,proposal=>
        {
            if(RejectNext){RejectNext=false;throw new ArgumentException("示例宿主拒绝本组修改。");}
            LastProposal=new System.Collections.ObjectModel.ReadOnlyDictionary<string,ParameterSetting>(
                proposal.ToDictionary(entry=>entry.Key,entry=>entry.Value,StringComparer.Ordinal));Writes++;
        },new ReferenceFieldOptions(_=>candidates,EditableIndex:true),assistCompositions:new[]{Assist});
        Child=Fields;
    }
    private static void OnMode(DependencyObject target,DependencyPropertyChangedEventArgs args)
    {
        var sample=(NativeAssistSample)target;if(sample._disposed)return;
        var value=(int)args.NewValue;
        sample.Fields.TrySetFields(value<0?new Dictionary<string,ParameterSetting>{{sample.Assist.EnabledFieldId,new ConstantSetting(false)}}
            :new Dictionary<string,ParameterSetting>{{sample.Assist.EnabledFieldId,new ConstantSetting(true)},
                {sample.Assist.ModeFieldId,new ConstantSetting((long)value)}});
    }
    public void Dispose(){if(_disposed)return;_disposed=true;Fields.Dispose();Child=null;}
}
