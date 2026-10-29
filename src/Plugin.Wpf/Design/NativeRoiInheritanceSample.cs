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
public sealed partial class NativeRoiInheritanceSample : Grid,IDisposable
{
    private sealed record Root(long Revision,IReadOnlyDictionary<string,ParameterSetting> Fields,IReadOnlyList<RoiEditItem> LocalItems,IReadOnlyList<RoiGroupState> LocalGroups,
        RoiEditItem? Detection=null,string DetectionError="",Func<bool>? DetectionCurrent=null);
    private sealed record SourceValue(Guid Id,string Node,string Group,RecordValue Geometry,ImageFrameAddress Frame);
    private readonly NativeRoiSamples.Image _image=new();
    private readonly Guid _scope=Guid.NewGuid();
    private readonly SourceValue[] _sources;
    private Root _root;
    private RoiEditTarget? _lastLocalSelection;
    private long _projectedRevision=-1;
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
    public bool GraphicMode=>_root.Fields["area.mode"] is ConstantSetting{Value:0L};
    public NativeRoiInheritanceSample():this(null){}
    public NativeRoiInheritanceSample(NativeRoiComponentBindings? bindings)
    {
        if(bindings is not null)
        {
            ArgumentNullException.ThrowIfNull(bindings.Candidates);ArgumentNullException.ThrowIfNull(bindings.Resolve);ArgumentNullException.ThrowIfNull(bindings.IsCurrent);
            _componentBindings=bindings with{Candidates=new(
                bindings.Candidates.Sources.Where(source=>source.SelectedStorage==NumericStorage.Float32
                    &&(source.SelectedType.Shape==ValueShape.Sequence?source.SelectedType.Element:source.SelectedType)==DataType.Number),
                bindings.Candidates.Indices.Where(index=>index.SelectedStorage==NumericStorage.Int32))};
        }
        var items=NativeRoiSamples.CreateItems().Where(x=>x.Geometry.Get<string>("kind") is "rectangle" or "annulus").ToArray();
        var groups=new[]{"detect","mask"};
        _root=new(0,Freeze(groups.SelectMany(group=>new[]{
            new KeyValuePair<string,ParameterSetting>(Mode(group),new ConstantSetting(0L)),
            new KeyValuePair<string,ParameterSetting>(Source(group),new UnsetSetting())})
            .Concat(new[]{new KeyValuePair<string,ParameterSetting>("area.mode",new ConstantSetting(0L)),
                new KeyValuePair<string,ParameterSetting>("mask.image",new UnsetSetting())}).Concat(ComponentInitial())),Array.AsReadOnly(items),Array.AsReadOnly(groups.Select(group=>new RoiGroupState(group,RoiCoverage.Explicit,group=="detect"?RoiCoverage.FullDomain:RoiCoverage.None)).ToArray()));
        _sources=items.SelectMany(row=>new[]{new SourceValue(Guid.NewGuid(),row.Group+"-source",row.Group,row.Geometry,_image.Frame),
            new SourceValue(Guid.NewGuid(),row.Group+"-foreign",row.Group,row.Geometry,new(Guid.NewGuid(),"different-physical-frame"))}).ToArray();
        var partial=RoiGeometryEditing.Values(items.Single(row=>row.Group=="mask").Geometry);
        partial["start-radians"]=170*Math.PI/180;partial["end-radians"]=380*Math.PI/180;
        _sources=_sources.Append(new SourceValue(Guid.NewGuid(),"mask-partial","mask",RoiGeometryEditing.Checked(partial),_image.Frame)).ToArray();
        _sources=_sources.Append(new SourceValue(Guid.NewGuid(),"detect-annulus-source","detect",RoiGeometryEditing.Checked(partial),_image.Frame)).ToArray();
        InitializeComponentSources(items);
        Session=new(_image,Read,AcceptGeometry,()=>!_disposed,()=>new(_undo.Count>0,_redo.Count>0),"detect",canDraw:group=>!_disposed&&GraphicMode&&Local(group),canSetCoverage:group=>!_disposed&&GraphicMode&&Local(group));
        var declarations=groups.SelectMany((group,i)=>new[]{
            (new Port(Mode(group),DataType.Integer,EditModes:FieldEditModes.Constant),
             new FieldPresentation(Mode(group),group=="detect"?"ROI创建":"屏蔽区创建","来源组合",i*2,FieldEditorKind.Scalar),_root.Fields[Mode(group)]),
            (new Port(Source(group),RoiDeclarations.Shape,Required:false,EditModes:FieldEditModes.Reference),
             new FieldPresentation(Source(group),group=="detect"?"检测区域来源":"屏蔽区域来源","来源组合",i*2+1,FieldEditorKind.Record){Layout=new("来源组合","来源组合",VisibleWhen:group=="detect"?new("detect.inherit-way",0L):null)},_root.Fields[Source(group)])})
            .Concat(new[]{
                (new Port("area.mode",DataType.Integer),new FieldPresentation("area.mode","ROI区域类型","来源组合",-2,FieldEditorKind.Scalar),_root.Fields["area.mode"]),
                (new Port("mask.image",TypeRef.ResourceOf(ResourceKind.Image),Required:false,EditModes:FieldEditModes.Reference),
                    new FieldPresentation("mask.image","屏蔽图像","来源组合",-1,FieldEditorKind.Image){Layout=new("来源组合","来源组合",VisibleWhen:new("area.mode",1L))},_root.Fields["mask.image"])
            }).Concat(ComponentDeclarations()).ToArray();
        var options=groups.ToDictionary(Mode,_=>new NativeIntegerRadioOptions(new[]{new NativeIntegerRadioChoice(0,"绘制"),new NativeIntegerRadioChoice(1,"继承")}),StringComparer.Ordinal);
        options.Add("area.mode",new(new[]{new NativeIntegerRadioChoice(0,"图形类型"),new NativeIntegerRadioChoice(1,"图像类型")}));
        options.Add("detect.inherit-way",InheritanceOptions());
        try
        {
            Fields=new(declarations,AcceptFields,new ReferenceFieldOptions(Candidates,EditableIndex:true),
                integerRadios:options,validateComposition:ValidateFields,isCurrent:()=>!_disposed,
                imageSources:new Dictionary<string,Func<IReadOnlyList<DeclaredReferenceCandidate>>>{["mask.image"]=()=>new[]{
                    new DeclaredReferenceCandidate("同帧屏蔽图像","mask-image",null,new Port("image",TypeRef.ResourceOf(ResourceKind.Image)),Array.Empty<string>())}},
                parentConditions:ParentConditions(),inlineConstants:Components.Select(ComponentId).ToHashSet(StringComparer.Ordinal));
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
        var value=_sources.Single(x=>x.Node==group+(foreignFrame?"-foreign":"-source"));
        return new(value.Node,"geometry",RoiDeclarations.Shape);
    }
    public OutputReference PartialAnnulusReference=>new("mask-partial","geometry",RoiDeclarations.Shape);
    private SourceValue Resolve(string group,ReferenceSetting reference,string? expectedKind=null)
    {
        var value=_sources.SingleOrDefault(x=>x.Group==group&&x.Node==reference.Reference.NodeId);
        if(value is null||reference.Reference.PortId!="geometry"||reference.Reference.Type!=RoiDeclarations.Shape
            ||reference.Reference.ScopeId is not null||reference.Reference.MemberFieldPath.Count!=0||reference.Reference.Selection is not BindingSelection.AllItems)
            throw new ArgumentException("整体ROI来源必须是明确的结构化几何单项。");
        if(value.Frame!=_image.Frame)throw new ArgumentException("继承ROI与当前图像不是同一物理帧。");
        var kind=value.Geometry.Get<string>("kind");
        var expected=expectedKind??(group=="detect"?"rectangle":"annulus");
        if(kind!=expected)throw new ArgumentException("整体ROI来源形状不匹配。");
        return value;
    }
    private string? ValidateFields(FieldGroupCandidate candidate)
    {
        return _restoring?"正在恢复宿主历史。":ValidateInheritance(candidate);
    }
    private void AcceptFields(IReadOnlyDictionary<string,ParameterSetting> changes)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_restoring)throw new ArgumentException("历史恢复期间不能接纳另一字段提案。");
        var current=_root;var fields=current.Fields.ToDictionary(x=>x.Key,x=>x.Value,StringComparer.Ordinal);
        var prepared=_preparedComponents;_preparedComponents=null;
        if(prepared is null||!ReferenceEquals(prepared.Root,current)||!ComponentsCurrent()||!PreparedCurrent(prepared)||!ReferenceEquals(current,_root)
            ||changes.Count!=prepared.Changes.Count||changes.Any(change=>!prepared.Changes.TryGetValue(change.Key,out var value)||!SameSetting(change.Value,value)))
            throw new ArgumentException("参数解析回执已失效，不能安装旧组合。");
        if(Session.CurROIData is {Enabled:true} selected&&current.LocalItems.Any(item=>item.Group==selected.Group&&item.Id==selected.Id))
            _lastLocalSelection=new(selected.Group,selected.Id);
        var history=CaptureHistory();
        foreach(var change in changes)fields[change.Key]=change.Value;
        _root=current with{Revision=current.Revision+1,Fields=Freeze(fields),Detection=prepared.Detection,DetectionError=prepared.Error,DetectionCurrent=prepared.IsCurrent};
        _undo.Push(history);_redo.Clear();
        // No Session callbacks here: fields install accepted states before Changed refreshes the scene.
    }
    private void OnFieldsChanged()
    {
        if(_disposed)return;
        var root=_root;if(_projectedRevision==root.Revision)return;_projectedRevision=root.Revision;
        bool Current()=>!_disposed&&ReferenceEquals(root,_root);
        NativeRoiCallbacks.Run(new Action[]{
            ()=>{if(Current())Session.Refresh();},
            ()=>{if(Current())Scene.ParameterHost.Visibility=GraphicMode?Visibility.Visible:Visibility.Collapsed;},
            ()=>{if(Current()&&GraphicMode&&Session.CurROIData is null&&_lastLocalSelection is {} selection&&Local(selection.Group))Session.Select(selection.Group,selection.Id);}});
    }
    private RoiEditSnapshot Read()
    {
        var root=_root;
        var rows=new List<RoiEditItem>();
        if(!GraphicMode)return new(new(_scope,root.Revision,_image.Frame),Array.AsReadOnly(rows.ToArray()),Array.AsReadOnly(new[]{new RoiGroupState("detect",RoiCoverage.None,RoiCoverage.FullDomain),new RoiGroupState("mask",RoiCoverage.None,RoiCoverage.None)}));
        foreach(var group in new[]{"detect","mask"})
            if(Local(group))rows.AddRange(root.LocalItems.Where(row=>row.Group==group));
            else
            {
                if(group=="detect")
                {if(root.Detection is {} detected)rows.Add(detected);}
                else if(root.Fields[Source(group)] is ReferenceSetting reference)
                {var source=Resolve(group,reference);rows.Add(new(group,source.Id,source.Node,source.Geometry,Enabled:false));}
            }
        return new(new(_scope,root.Revision,_image.Frame),Array.AsReadOnly(rows.ToArray()),Array.AsReadOnly(new[]{"detect","mask"}.Select(group=>Local(group)?root.LocalGroups.Single(state=>state.Group==group):new RoiGroupState(group,rows.Any(row=>row.Group==group)?RoiCoverage.Explicit:RoiCoverage.None,group=="detect"?RoiCoverage.FullDomain:RoiCoverage.None)).ToArray()));
    }
    private RoiEditAcceptance AcceptGeometry(RoiEditStamp expected,RoiEditProposal proposal)
    {
        var current=Read();if(_disposed||_restoring||current.Stamp!=expected)return RoiEditAcceptance.Reject(current,"区域范围或修订已改变。");
        if(proposal is RoiEditProposal.Undo or RoiEditProposal.Redo)return RestoreHistory(current,proposal is RoiEditProposal.Redo);
        var groups=proposal switch
        {
            RoiEditProposal.SetCoverage coverage=>new[]{coverage.Group},RoiEditProposal.Create create=>new[]{create.Group},RoiEditProposal.Replace replace=>new[]{replace.Target.Group},
            RoiEditProposal.Delete delete=>delete.Targets.Select(target=>target.Group).Distinct().ToArray(),
            _=>throw new NotSupportedException("未知区域提案。")
        };
        if(!GraphicMode||groups.Any(group=>group is not ("detect" or "mask")||!Local(group)))return RoiEditAcceptance.Reject(current,"继承或图像模式不可修改本地集合。");
        var before=CaptureHistory();var rows=_root.LocalItems.ToList();var states=_root.LocalGroups.ToArray();IReadOnlyList<RoiEditTarget> affected;RoiEditSelection selection;
        switch(proposal)
        {
            case RoiEditProposal.Create create:
                states=states.Select(state=>state.Group==create.Group?state with{Coverage=RoiCoverage.Explicit}:state).ToArray();
                var id=Guid.NewGuid();rows.Add(new(create.Group,id,"新区域",create.Geometry));affected=new[]{new RoiEditTarget(create.Group,id)};
                selection=new RoiEditSelection.Select(affected[0]);break;
            case RoiEditProposal.Replace replace:
                var index=rows.FindIndex(row=>row.Group==replace.Target.Group&&row.Id==replace.Target.Id&&row.Enabled&&row.Visible);
                if(index<0)return RoiEditAcceptance.Reject(current,"本地对象不存在。");
                rows[index]=rows[index] with{Geometry=replace.Geometry};affected=new[]{replace.Target};selection=new RoiEditSelection.Keep();break;
            case RoiEditProposal.Delete delete:
                if(delete.Targets.Any(target=>!rows.Any(row=>row.Group==target.Group&&row.Id==target.Id&&row.Enabled&&row.Visible)))
                    return RoiEditAcceptance.Reject(current,"删除对象不存在或不可编辑。");
                rows.RemoveAll(row=>delete.Targets.Contains(new(row.Group,row.Id)));affected=delete.Targets;
                states=states.Select(state=>delete.Targets.Any(target=>target.Group==state.Group)&&!rows.Any(row=>row.Group==state.Group)?state with{Coverage=state.EmptyCoverage}:state).ToArray();
                selection=before.Selection is {} target&&delete.Targets.Contains(target)?new RoiEditSelection.Clear():new RoiEditSelection.Keep();break;
            case RoiEditProposal.SetCoverage coverage:
                affected=Array.AsReadOnly(rows.Where(row=>row.Group==coverage.Group).Select(row=>new RoiEditTarget(row.Group,row.Id)).ToArray());
                rows.RemoveAll(row=>row.Group==coverage.Group);
                states=states.Select(state=>state.Group==coverage.Group?state with{Coverage=coverage.Coverage}:state).ToArray();
                selection=before.Selection?.Group==coverage.Group?new RoiEditSelection.Clear():new RoiEditSelection.Keep();break;
            default:throw new NotSupportedException("未知区域提案。");
        }
        _root=_root with{Revision=_root.Revision+1,LocalItems=Array.AsReadOnly(rows.ToArray()),LocalGroups=Array.AsReadOnly(states)};_undo.Push(before);_redo.Clear();
        return new(true,Read(),affected,selection);
    }
    public void Dispose()
    {
        if(_disposed)return;_disposed=true;
        if(Fields is not null)foreach(var field in Fields.Fields)field.Changed-=OnFieldsChanged;
        NativeRoiCallbacks.Run(new Action[]{()=>Scene.Session=null,()=>Fields?.Dispose(),Session.Dispose,Scene.Dispose,_image.Dispose,()=>Children.Clear()});
    }
}
