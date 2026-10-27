using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Presenters.Regions;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.Plugin.Wpf.Design;

public partial class NativeRoiInheritancePreview : UserControl,IDisposable
{
    public NativeRoiInheritancePreview()=>InitializeComponent();
    public void Dispose(){var content=Content;Content=null;(content as IDisposable)?.Dispose();}
}

/// <summary>One preview-only owner for fields, local geometry and revision. No tool defaults or execution.</summary>
public sealed class NativeRoiInheritanceSample : Grid,IDisposable
{
    private sealed record Root(long Revision,IReadOnlyDictionary<string,ParameterSetting> Fields,IReadOnlyList<RoiEditItem> LocalItems);
    private sealed record SourceValue(Guid Id,string Node,string Group,RecordValue Geometry,ImageFrameAddress Frame);
    private readonly NativeRoiSamples.Image _image=new();
    private readonly Guid _scope=Guid.NewGuid();
    private readonly SourceValue[] _sources;
    private Root _root;
    private bool _disposed;
    public DeclaredFieldsView Fields {get;}
    public NativeRoiScene Scene {get;}=new();
    public NativeRoiSession Session {get;}
    public long Revision=>_root.Revision;
    public IReadOnlyList<RoiEditItem> LocalItems=>_root.LocalItems;
    public ImageFrameAddress Frame=>_image.Frame;
    private static string Mode(string group)=>group+".mode";
    private static string Source(string group)=>group+".source";
    private bool Local(string group)=>_root.Fields[Mode(group)] is ConstantSetting{Value:0L};
    public NativeRoiInheritanceSample()
    {
        var items=NativeRoiSamples.CreateItems().Where(x=>x.Geometry.Get<string>("kind") is "rectangle" or "annulus").ToArray();
        var groups=new[]{"detect","mask"};
        _root=new(0,Freeze(groups.SelectMany(group=>new[]{
            new KeyValuePair<string,ParameterSetting>(Mode(group),new ConstantSetting(0L)),
            new KeyValuePair<string,ParameterSetting>(Source(group),new UnsetSetting())})),Array.AsReadOnly(items));
        _sources=items.SelectMany(row=>new[]{new SourceValue(Guid.NewGuid(),row.Group+"-source",row.Group,row.Geometry,_image.Frame),
            new SourceValue(Guid.NewGuid(),row.Group+"-foreign",row.Group,row.Geometry,new(Guid.NewGuid(),"different-physical-frame"))}).ToArray();
        Session=new(_image,Read,AcceptGeometry,()=>!_disposed,"detect",canDraw:group=>!_disposed&&Local(group));
        var declarations=groups.SelectMany((group,i)=>new[]{
            (new Port(Mode(group),DataType.Integer,EditModes:FieldEditModes.Constant),
             new FieldPresentation(Mode(group),group=="detect"?"ROI创建":"屏蔽区创建","来源组合",i*2,FieldEditorKind.Scalar),_root.Fields[Mode(group)]),
            (new Port(Source(group),RoiDeclarations.Shape,Required:false,EditModes:FieldEditModes.Reference),
             new FieldPresentation(Source(group),group=="detect"?"检测区域来源":"屏蔽区域来源","来源组合",i*2+1,FieldEditorKind.Record),_root.Fields[Source(group)])}).ToArray();
        var options=groups.ToDictionary(Mode,_=>new NativeIntegerRadioOptions(new[]{new NativeIntegerRadioChoice(0,"绘制"),new NativeIntegerRadioChoice(1,"继承")}),StringComparer.Ordinal);
        try
        {
            Fields=new(declarations,AcceptFields,new ReferenceFieldOptions(port=>_sources.Where(x=>Source(x.Group)==port.Id)
                .Select(x=>new DeclaredReferenceCandidate(x.Node,x.Node,null,new Port("geometry",RoiDeclarations.Shape),Array.Empty<string>())).ToArray()),
                integerRadios:options,validateComposition:ValidateFields,isCurrent:()=>!_disposed);
            foreach(var field in Fields.Fields)field.Changed+=OnFieldsChanged;
            RowDefinitions.Add(new(){Height=GridLength.Auto});RowDefinitions.Add(new(){Height=new GridLength(1,GridUnitType.Star)});
            Fields.Width=380;Fields.HorizontalAlignment=HorizontalAlignment.Left;Children.Add(Fields);
            Scene.Session=Session;SetRow(Scene,1);Children.Add(Scene);
            Session.Select(items[0].Group,items[0].Id);
        }
        catch(Exception failure)
        {
            try{Dispose();}catch(Exception cleanup){throw new AggregateException(failure,cleanup);}throw;
        }
    }
    private static IReadOnlyDictionary<string,ParameterSetting> Freeze(IEnumerable<KeyValuePair<string,ParameterSetting>> values)
        =>new ReadOnlyDictionary<string,ParameterSetting>(values.ToDictionary(x=>x.Key,x=>DeclaredFieldState.CopySetting(x.Value),StringComparer.Ordinal));
    public OutputReference Reference(string group,bool foreignFrame=false)
    {
        var value=_sources.Single(x=>x.Group==group&&x.Node.EndsWith(foreignFrame?"-foreign":"-source",StringComparison.Ordinal));
        return new(value.Node,"geometry",RoiDeclarations.Shape);
    }
    private SourceValue Resolve(string group,ReferenceSetting reference)
    {
        var value=_sources.SingleOrDefault(x=>x.Group==group&&x.Node==reference.Reference.NodeId);
        if(value is null||reference.Reference.PortId!="geometry"||reference.Reference.Type!=RoiDeclarations.Shape
            ||reference.Reference.ScopeId is not null||reference.Reference.MemberFieldPath.Count!=0||reference.Reference.Selection is not BindingSelection.AllItems)
            throw new ArgumentException("整体ROI来源必须是明确的结构化几何单项。");
        if(value.Frame!=_image.Frame)throw new ArgumentException("继承ROI与当前图像不是同一物理帧。");
        var kind=value.Geometry.Get<string>("kind");
        if(kind!=(group=="detect"?"rectangle":"annulus"))throw new ArgumentException("整体ROI来源形状不匹配。");
        return value;
    }
    private string? ValidateFields(FieldGroupCandidate candidate)
    {
        foreach(var group in new[]{"detect","mask"})
        {
            if(candidate.Proposed[Source(group)] is ReferenceSetting reference)Resolve(group,reference);
            if(candidate.Proposed[Mode(group)] is ConstantSetting{Value:1L}&&candidate.Proposed[Source(group)] is not ReferenceSetting)
                return "继承模式必须与有效来源一起接纳。";
        }
        return null;
    }
    private void AcceptFields(IReadOnlyDictionary<string,ParameterSetting> changes)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        var current=_root;var fields=current.Fields.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
        foreach(var change in changes)fields[change.Key]=change.Value;
        _root=current with{Revision=current.Revision+1,Fields=Freeze(fields)};
        // No Session callbacks here: fields install accepted states before Changed refreshes the scene.
    }
    private void OnFieldsChanged(){if(!_disposed)Session.Refresh();}
    private RoiEditSnapshot Read()
    {
        var root=_root;
        var rows=new List<RoiEditItem>();
        foreach(var group in new[]{"detect","mask"})
            if(Local(group))rows.AddRange(root.LocalItems.Where(row=>row.Group==group));
            else
            {
                var source=Resolve(group,(ReferenceSetting)root.Fields[Source(group)]);
                rows.Add(new(group,source.Id,source.Node,source.Geometry,Enabled:false));
            }
        return new(new(_scope,root.Revision,_image.Frame),Array.AsReadOnly(rows.ToArray()));
    }
    private RoiEditAcceptance AcceptGeometry(RoiEditStamp expected,RoiEditProposal proposal)
    {
        var current=Read();if(_disposed||current.Stamp!=expected||!Local(proposal.Group))return new(false,current,Error:"区域来源或修订已改变。");
        var rows=_root.LocalItems.ToList();Guid id;
        if(proposal.Operation==RoiEditOperation.Create){id=Guid.NewGuid();rows.Add(new(proposal.Group,id,"新区域",proposal.Geometry));}
        else
        {
            var index=rows.FindIndex(x=>x.Group==proposal.Group&&x.Id==proposal.TargetId);
            if(index<0)return new(false,current,Error:"本地对象不存在。");
            id=rows[index].Id;rows[index]=rows[index] with{Geometry=proposal.Geometry};
        }
        _root=_root with{Revision=_root.Revision+1,LocalItems=Array.AsReadOnly(rows.ToArray())};
        return new(true,Read(),new(proposal.Group,id));
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        if(Fields is not null)foreach(var field in Fields.Fields)field.Changed-=OnFieldsChanged;
        NativeRoiCallbacks.Run(new Action[]{()=>Scene.Session=null,()=>Fields?.Dispose(),Session.Dispose,Scene.Dispose,_image.Dispose,()=>Children.Clear()});
    }
}
