using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Text;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Design;

public partial class NativeDisplayPreview : UserControl,IDisposable
{
    public NativeDisplayPreview()=>InitializeComponent();
    public void Dispose(){var view=Content;Content=null;(view as IDisposable)?.Dispose();}
}

/// <summary>Independent fixture data over the same real settings, original rows and templates.</summary>
public sealed class NativeDisplaySample : Decorator,IDisposable
{
    public static readonly DependencyProperty SelectedPageIndexProperty=DependencyProperty.Register(nameof(SelectedPageIndex),typeof(int),typeof(NativeDisplaySample),
        new FrameworkPropertyMetadata(0,FrameworkPropertyMetadataOptions.BindsTwoWayByDefault),value=>(int)value>=0);
    public int SelectedPageIndex {get=>(int)GetValue(SelectedPageIndexProperty);set=>SetValue(SelectedPageIndexProperty,value);}
    private bool _disposed;
    private readonly NativeDisplayDraft _draft=new();
    private readonly NativeRoiSamples.Image _image=new();
    private long _drawRevision;
    private bool _isOk=true;
    public DeclaredSettingsView SettingsView {get;}
    public NativeTextViewport TextViewport {get;}=new(){Height=320,VerticalAlignment=VerticalAlignment.Top};
    public DeclaredFieldsView Fields=>(DeclaredFieldsView)SettingsView.Content;
    public int Writes=>_draft.Writes;
    public NativeDisplaySample()
    {
        var a=new DeclaredReferenceCandidate("同名数值","source-a",null,new Port("value",DataType.Number),Array.Empty<string>());
        var b=new DeclaredReferenceCandidate("同名数值","source-b",null,new Port("value",DataType.Integer),Array.Empty<string>());
        var tokens=new Dictionary<DeclaredReferenceCandidate,string>(ReferenceEqualityComparer.Instance){{a,"source-a.value"},{b,"source-b.value"}};
        var options=new ToolSettingsViewOptions(
            ReferenceOptions:new(_=>new ReferenceCandidates(new[]{a,b},Array.Empty<DeclaredReferenceCandidate>()),EditableIndex:true),
            TextExpressions:new Dictionary<string,TextExpressionOptions>{{"text.content",new(()=>new(new[]{a,b},candidate=>tokens[candidate],()=>!_disposed))}},
            GraphicDisplays:new[]{new NativeGraphicDisplayOptions("outline","outline.visible","outline.ok","outline.ng","outline.opacity","outline.width"),
                new NativeGraphicDisplayOptions("region","region.visible","region.ok","region.ng","region.opacity")},
            TextDisplays:new[]{new NativeTextDisplayOptions("text.content","text.ok","text.ng","text.size","text.opacity","text.x","text.y")});
        SettingsView=new(_draft,options);
        SettingsView.SetBinding(DeclaredSettingsView.SelectedPageIndexProperty,new Binding(nameof(SelectedPageIndex)){Source=this,Mode=BindingMode.TwoWay});
        var layout=new Grid();layout.ColumnDefinitions.Add(new(){Width=new GridLength(600)});layout.ColumnDefinitions.Add(new());
        layout.Children.Add(SettingsView);Grid.SetColumn(TextViewport,1);layout.Children.Add(TextViewport);Child=layout;
        SettingsView.Changed+=OnChanged;
        try
        {
            if(!TextViewport.BindImage(_image,()=>!_disposed))throw new InvalidOperationException("独立显示帧未接纳。");
            OnChanged();
        }
        catch(Exception failure){try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;}
    }
    public void OpenGraphicStyle(string visibleFieldId)
        =>((NativeGraphicStylePresenter)Fields.Fields.Single(field=>field.FieldId==visibleFieldId).View).Open();
    public void SetResultStatus(bool isOk){ObjectDisposedException.ThrowIf(_disposed,this);_isOk=isOk;OnChanged();}
    private void OnChanged()
    {
        if(_disposed)return;var revision=++_drawRevision;var values=_draft.CaptureFields();
        var x=Fields.Fields.Single(field=>field.FieldId=="text.x").Setting;
        var y=Fields.Fields.Single(field=>field.FieldId=="text.y").Setting;
        if(x is not ConstantSetting {Value:double px}||y is not ConstantSetting {Value:double py})
        {
            // This fixture has declarations, not a resolver. Never paint stale constant coordinates for a reference.
            TextViewport.Present(_image.Frame,revision,Array.Empty<NativeTextOverlay>(),()=>!_disposed&&revision==_drawRevision);return;
        }
        var color=values.Get<RecordValue>(_isOk?"text.ok":"text.ng");
        var rgba=Color.FromArgb((byte)color.Get<long>("a"),(byte)color.Get<long>("r"),(byte)color.Get<long>("g"),(byte)color.Get<long>("b"));
        // Host fixture policy: original IText requires an integer font size. The editor retains its real number.
        var font=checked((int)Math.Round(values.Get<double>("text.size"),MidpointRounding.AwayFromZero));
        TextViewport.Present(_image.Frame,revision,new[]{new NativeTextOverlay(values.Get<string>("text.content"),new(px,py),font,rgba,values.Get<double>("text.opacity"))},
            ()=>!_disposed&&revision==_drawRevision);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;_drawRevision++;
        ParameterCleanup.Run(new Action[]{()=>SettingsView.Changed-=OnChanged,SettingsView.Dispose,TextViewport.Dispose,_image.Dispose,()=>Child=null});
    }

    private sealed class NativeDisplayDraft : IToolSettingsDraft
    {
        private FieldSettingsDraft _root;
        public int Writes {get;private set;}
        public ToolSchema Schema {get;}
        public NativeDisplayDraft()
        {
            var ports=new List<Port>();var presentations=new List<FieldPresentation>();var values=new Dictionary<string,object?>();
            Add("text.content",DataType.Text,"内容","文本显示",0,"文字样式",FieldEditorKind.Scalar,"aa{}bb");
            Add("text.ok",PresentationDeclarations.Rgba,"OK颜色","文本显示",1,"文字样式",FieldEditorKind.Record,PresentationDeclarations.Color(102,255,0));
            Add("text.ng",PresentationDeclarations.Rgba,"NG颜色","文本显示",2,"文字样式",FieldEditorKind.Record,PresentationDeclarations.Color(255,0,0));
            Add("text.size",DataType.Number,"字号","文本显示",3,"文字样式",FieldEditorKind.Scalar,16d,6,72,1);
            Add("text.opacity",DataType.Number,"透明度","文本显示",4,"文字样式",FieldEditorKind.Scalar,1d,0,1,.1);
            Add("text.x",DataType.Number,"位置X","文本显示",5,"文字样式",FieldEditorKind.Scalar,120d,modes:FieldEditModes.Constant|FieldEditModes.Reference,digits:null);
            Add("text.y",DataType.Number,"位置Y","文本显示",6,"文字样式",FieldEditorKind.Scalar,80d,modes:FieldEditModes.Constant|FieldEditModes.Reference,digits:null);
            Graphic("outline","轮廓",10,true);Graphic("region","区域",20,false);
            Schema=new(ports,Array.Empty<Port>(),new ToolPresentation(presentations));_root=new(Schema,new FieldSettings(Schema.Inputs,values.ToDictionary(pair=>pair.Key,pair=>(ParameterSetting)new ConstantSetting(pair.Value),StringComparer.Ordinal)));
            void Graphic(string group,string title,int order,bool width)
            {
                Add(group+".visible",DataType.Boolean,title,"图形显示",order,"图形样式",FieldEditorKind.Scalar,true,dialog:group,title:title,primary:true);
                Add(group+".ok",PresentationDeclarations.Rgba,"OK颜色","图形显示",order+1,"图形样式",FieldEditorKind.Record,PresentationDeclarations.Color(102,255,0),dialog:group,title:title);
                Add(group+".ng",PresentationDeclarations.Rgba,"NG颜色","图形显示",order+2,"图形样式",FieldEditorKind.Record,PresentationDeclarations.Color(255,0,0),dialog:group,title:title);
                Add(group+".opacity",DataType.Number,"透明度","图形显示",order+3,"图形样式",FieldEditorKind.Scalar,1d,0,1,.01,dialog:group,title:title);
                if(width)Add(group+".width",DataType.Number,"粗细","图形显示",order+4,"图形样式",FieldEditorKind.Scalar,1d,1,10,1,dialog:group,title:title);
            }
            void Add(string id,TypeRef type,string label,string section,int order,string page,FieldEditorKind editor,object value,
                double? min=null,double? max=null,double increment=1,FieldEditModes modes=FieldEditModes.Constant,int? digits=2,
                string? dialog=null,string? title=null,bool primary=false)
            {
                ports.Add(new(id,type,EditModes:modes));values.Add(id,value);
                presentations.Add(new(id,label,section,order,editor){Layout=new(page,section,Minimum:min,Maximum:max,DecimalPlaces:digits,
                    DialogGroup:dialog,DialogTitle:title,DialogPrimary:primary,Increment:increment)});
            }
        }
        public long Revision=>_root.Revision;
        public FieldSettings CaptureFields()=>_root.CaptureFields();
        public FieldSettings CaptureValidatedFields()=>_root.CaptureValidatedFields();
        public void ApplyFields(IReadOnlyDictionary<string,ParameterSetting> changes,Action<FieldSettings> acceptCandidate)
        {_root.ApplyFields(changes,acceptCandidate);Writes++;}

    }
}
