using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Plugin.Wpf.Design;

/// <summary>Explicit host-provided component declarations and one borrowed formal-resolution batch.
/// The parameterless designer uses fixed scalar fixture receipts, never a second sequence resolver.</summary>
public sealed record NativeRoiComponentBindings(ReferenceCandidates Candidates,
    Func<IReadOnlyList<(Port Target,ParameterSetting Setting)>,ReferenceBindingBatchResult> Resolve,Func<bool> IsCurrent);

public sealed partial class NativeRoiInheritanceSample
{
    private sealed record Component(string Id,string Label,int Way,string GeometryField);
    private sealed record NumberSource(string Node,double Value,ImageFrameAddress Frame);
    private static readonly Component[] Components={
        new("rect.x","中心点X",1,"x"),new("rect.y","中心点Y",1,"y"),new("rect.width","宽",1,"width"),
        new("rect.height","高",1,"height"),new("rect.angle","角度",1,"angle-degrees"),
        new("annulus.x","中心点X",3,"x"),new("annulus.y","中心点Y",3,"y"),new("annulus.inner","CircleInnerRadius",3,"inner-radius"),
        new("annulus.outer","roiAnnulusOuterRadius",3,"outer-radius"),new("annulus.start","起始角度",3,"start-radians"),new("annulus.sweep","角度范围",3,"sweep")};
    private readonly Dictionary<string,NumberSource> _numbers=new(StringComparer.Ordinal);
    private readonly NativeRoiComponentBindings? _componentBindings;
    private sealed record PreparedComponents(Root Root,IReadOnlyDictionary<string,ParameterSetting> Changes,RoiEditItem? Detection,string Error,Func<bool>? IsCurrent=null);
    private PreparedComponents? _preparedComponents;
    private bool _resolvingComponents,_refreshingComponents;
    private bool ComponentsCurrent()=>!_disposed&&(_componentBindings?.IsCurrent()!=false)&&!_disposed;
    private bool PreparedCurrent(PreparedComponents prepared)=>!_disposed&&(prepared.IsCurrent?.Invoke()!=false)&&!_disposed;
    private static bool SameSetting(ParameterSetting a,ParameterSetting b)=>a is ReferenceSetting ar&&b is ReferenceSetting br
        ?DeclaredFieldState.SameReference(ar.Reference,br.Reference):Equals(a,b);
    /// <summary>Host explicitly announces new source receipts. Reads never resolve or silently change geometry at the old stamp.</summary>
    public bool RefreshComponentReferences()
    {
        ObjectDisposedException.ThrowIf(_disposed,this);
        if(_resolvingComponents||_refreshingComponents)return false;
        var root=_root;
        _refreshingComponents=true;
        try
        {
            Session.CancelPending();
            if(_disposed||!ReferenceEquals(root,_root))return false;
            string? error;PreparedComponents? prepared;
            try
            {
                error=ValidateInheritance(new FieldGroupCandidate(root.Fields,new Dictionary<string,ParameterSetting>()));
                prepared=_preparedComponents;
                if(error is null&&prepared is not null&&!PreparedCurrent(prepared))error="来源批次回执已过期。";
            }
            catch(Exception failure)when(failure is ArgumentException or OverflowException){error=failure.Message;prepared=null;}
            finally{_preparedComponents=null;}
            if(_disposed||!ReferenceEquals(root,_root))return false;
            _root=root with{Revision=root.Revision+1,Detection=error is null?prepared?.Detection:null,DetectionError=error??prepared?.Error??"来源解析尚未完成。",DetectionCurrent=error is null?prepared?.IsCurrent:null};
            OnFieldsChanged();return error is null&&_root.DetectionError.Length==0;
        }
        finally{_refreshingComponents=false;}
    }
    private readonly Guid _rectComponentsId=Guid.NewGuid(),_annulusComponentsId=Guid.NewGuid();
    private static string ComponentId(Component component)=>"detect."+component.Id;
    private static IEnumerable<KeyValuePair<string,ParameterSetting>> ComponentInitial()
        =>Components.Select(component=>new KeyValuePair<string,ParameterSetting>(ComponentId(component),new UnsetSetting()))
            .Concat(new[]{new KeyValuePair<string,ParameterSetting>("detect.inherit-way",new ConstantSetting(0L)),
                new KeyValuePair<string,ParameterSetting>("detect.annulus-source",new UnsetSetting())});
    private IEnumerable<(Port Port,FieldPresentation Presentation,ParameterSetting Value)> ComponentDeclarations()
    {
        yield return(new("detect.inherit-way",DataType.Integer),new("detect.inherit-way","继承方式","来源组合",10,FieldEditorKind.Scalar),_root.Fields["detect.inherit-way"]);
        yield return(new("detect.annulus-source",RoiDeclarations.Shape,Required:false,EditModes:FieldEditModes.Reference),
            new FieldPresentation("detect.annulus-source","圆环区域来源","来源组合",11,FieldEditorKind.Record){Layout=new("来源组合","来源组合",VisibleWhen:new("detect.inherit-way",2L))},_root.Fields["detect.annulus-source"]);
        foreach(var component in Components)
        {
            var id=ComponentId(component);
            yield return(new(id,DataType.Number,Required:false,Storage:NumericStorage.Float32,EditModes:FieldEditModes.Constant|FieldEditModes.Reference),
                new FieldPresentation(id,AppLocalizationService.GetLang(component.Label),"来源组合",20+Array.IndexOf(Components,component),FieldEditorKind.Scalar){Layout=new("来源组合","来源组合",VisibleWhen:new("detect.inherit-way",(long)component.Way),DecimalPlaces:null)},_root.Fields[id]);
        }
    }
    private static NativeIntegerRadioOptions InheritanceOptions()=>new(new[]{
        new NativeIntegerRadioChoice(0,AppLocalizationService.GetLang("By rectangle area")),
        new NativeIntegerRadioChoice(1,AppLocalizationService.GetLang("By rectangle parameter")),
        new NativeIntegerRadioChoice(2,AppLocalizationService.GetLang("By circle area")),
        new NativeIntegerRadioChoice(3,AppLocalizationService.GetLang("By circle parameter"))},useFiveSlotTemplate:true,unselectedValue:-1);
    private IReadOnlyDictionary<string,IReadOnlyList<FieldCondition>> ParentConditions()
    {
        var result=new Dictionary<string,IReadOnlyList<FieldCondition>>(StringComparer.Ordinal);
        foreach(var id in new[]{"detect.mode","detect.source","mask.mode","mask.source"})result[id]=new[]{new FieldCondition("area.mode",0L)};
        result["detect.source"]=new[]{new FieldCondition("area.mode",0L),new FieldCondition("detect.mode",1L)};
        result["mask.source"]=new[]{new FieldCondition("area.mode",0L),new FieldCondition("mask.mode",1L)};
        foreach(var id in Components.Select(ComponentId).Concat(new[]{"detect.inherit-way","detect.annulus-source"}))
            result[id]=new[]{new FieldCondition("area.mode",0L),new FieldCondition("detect.mode",1L)};
        return result;
    }
    private void InitializeComponentSources(IReadOnlyList<RoiEditItem> items)
    {
        // Fixture receipts use this library's explicit geometry units: rectangle degrees, annulus radians.
        // They are not claims about the VM native algorithm's input angle convention.
        foreach(var component in Components)
        {
            var geometry=component.Way==1?items.Single(item=>item.Group=="detect").Geometry:_sources.Single(source=>source.Node=="mask-partial").Geometry;
            var center=RoiGeometryEditing.Center(geometry);
            var value=component.GeometryField switch{"x"=>center.X,"y"=>center.Y,"sweep"=>geometry.Get<double>("end-radians")-geometry.Get<double>("start-radians"),_=>geometry.Get<double>(component.GeometryField)};
            foreach(var foreign in new[]{false,true})
            {
                var node="component-"+component.Id+(foreign?"-foreign":"");
                _numbers.Add(node,new(node,(double)(float)value,foreign?new(Guid.NewGuid(),"different-component-frame"):_image.Frame));
            }
        }
    }
    private static DeclaredReferenceCandidate NumberCandidate(NumberSource source)=>new(source.Node,source.Node,null,
        new Port("value",DataType.Number,Storage:NumericStorage.Float32),Array.Empty<string>());
    private ReferenceCandidates Candidates(Port port)
    {
        if(Components.Any(component=>ComponentId(component)==port.Id))return _componentBindings?.Candidates??new(_numbers.Values.Select(NumberCandidate),Array.Empty<DeclaredReferenceCandidate>());
        var group=port.Id=="mask.source"?"mask":"detect";
        var kind=port.Id=="detect.source"?"rectangle":"annulus";
        return new(_sources.Where(source=>source.Group==group&&source.Geometry.Get<string>("kind")==kind)
            .Select(source=>new DeclaredReferenceCandidate(source.Node,source.Node,null,new Port("geometry",RoiDeclarations.Shape),Array.Empty<string>())),Array.Empty<DeclaredReferenceCandidate>());
    }
    public OutputReference ComponentReference(string componentId,bool foreignFrame=false)
    {
        var component=Components.Single(item=>ComponentId(item)==componentId);
        return NumberCandidate(_numbers["component-"+component.Id+(foreignFrame?"-foreign":"")]).Reference(BindingSelection.All);
    }
    public OutputReference DetectionAnnulusReference=>new("detect-annulus-source","geometry",RoiDeclarations.Shape);
    private double ResolveNumber(ReferenceSetting setting)
    {
        var reference=setting.Reference;
        if(!_numbers.TryGetValue(reference.NodeId,out var source)||reference.PortId!="value"||reference.Type!=DataType.Number
            ||reference.Storage!=NumericStorage.Float32||reference.ScopeId is not null||reference.MemberFieldPath.Count!=0||reference.Selection is not BindingSelection.AllItems)
            throw new ArgumentException("参数来源必须是明确声明的浮点单项。");
        if(source.Frame!=_image.Frame)throw new ArgumentException("参数来源与当前图像不是同一物理帧。");
        if(!double.IsFinite(source.Value))throw new ArgumentException("参数来源不是有限数值。");
        return source.Value;
    }
    private string? ValidateInheritance(Fields.FieldGroupCandidate candidate)
    {
        _preparedComponents=null;
        if(!ComponentsCurrent()||_resolvingComponents)return "来源解析宿主已失效或正在接纳另一组合。";
        var root=_root;
        if(!_refreshingComponents&&candidate.Changes.All(change=>SameSetting(candidate.Current[change.Key],change.Value)))
        {_preparedComponents=new(root,candidate.Changes,root.Detection,root.DetectionError,root.DetectionCurrent);return null;}
        foreach(var (id,setting) in candidate.Changes)
        {
            if(setting is not ReferenceSetting reference)continue;
            if(Components.Any(component=>ComponentId(component)==id))
            {if(_componentBindings is null)ResolveNumber(reference);else ValidateComponentReference(reference.Reference);}
            else if(id is "detect.source" or "mask.source")Resolve(id=="detect.source"?"detect":"mask",reference);
            else if(id=="detect.annulus-source")Resolve("detect",reference,"annulus");
            else if(id=="mask.image")ValidateMaskImage(reference);
        }
        RoiEditItem? detection=null;var error="";Func<bool>? current=null;
        if(candidate.Proposed["area.mode"] is ConstantSetting{Value:0L}&&candidate.Proposed["detect.mode"] is ConstantSetting{Value:1L})
            TryDetect(candidate.Proposed,out detection,out error,out current); // Incomplete is a valid edit state; invalid complete geometry throws.
        if(current?.Invoke()==false)return "来源批次回执已过期。";
        if(!ComponentsCurrent()||!ReferenceEquals(root,_root))return "解析期间编辑根或来源已改变。";
        _preparedComponents=new(root,candidate.Changes,detection,error,current);
        return null;
    }
    private void ValidateComponentReference(OutputReference reference)
    {
        var source=_componentBindings!.Candidates.Sources.SingleOrDefault(item=>DeclaredFieldState.SameSource(item.Reference(BindingSelection.All),reference));
        if(source is null||source.SelectedStorage!=NumericStorage.Float32||
            (source.SelectedType.Shape==ValueShape.Sequence?source.SelectedType.Element:source.SelectedType)!=DataType.Number)
            throw new ArgumentException("参数来源须为已声明的Float32数值或序列成员。");
        if(source.SelectedType.Shape==ValueShape.Sequence&&source.SelectedType.Count.Maximum==0)
            throw new ArgumentException("声明为恒空的序列不能提供区域参数。");
        if(reference.Selection is not BindingSelection.AllItems&&reference.Type.Shape!=ValueShape.Sequence)
            throw new ArgumentException("只有序列来源可以选择索引。");
        if(reference.Selection is BindingSelection.AtConstant index&&reference.Type.Count.Maximum is int maximum&&index.Index>=maximum)
            throw new ArgumentException("索引超出来源声明范围。");
        if(reference.Selection is BindingSelection.AtField dynamicIndex)
        {
            var item=_componentBindings.Candidates.Indices.SingleOrDefault(candidate=>DeclaredFieldState.SameReference(candidate.Reference(BindingSelection.All),dynamicIndex.Reference));
            if(item is null||item.SelectedStorage!=NumericStorage.Int32)throw new ArgumentException("动态索引须为已声明的Int32单项。");
        }
    }
    private double ResolveComponent(ParameterSetting setting)=>setting switch
    {
        ConstantSetting {Value:double number} when double.IsFinite(number)=>number,
        ReferenceSetting reference=>ResolveNumber(reference),
        _=>throw new ArgumentException("参数必须是规范浮点常量或明确来源。")
    };
    private bool TryDetect(IReadOnlyDictionary<string,ParameterSetting> fields,out RoiEditItem? item,out string error,out Func<bool>? current)
    {
        item=null;error="";current=null;
        var way=(long)((ConstantSetting)fields["detect.inherit-way"]).Value!;
        if(way==-1){error="请选择继承方式。";return false;}
        if(way is 0 or 2)
        {
            var id=way==0?"detect.source":"detect.annulus-source";
            if(fields[id] is not ReferenceSetting reference){error="请选择来源："+id;return false;}
            var source=Resolve("detect",reference,way==0?"rectangle":"annulus");
            item=new("detect",source.Id,source.Node,source.Geometry,Enabled:false);return true;
        }
        if(way is not (1 or 3))throw new ArgumentException("未知继承方式。");
        var components=Components.Where(component=>component.Way==way).ToArray();
        foreach(var component in components)
            if(fields[ComponentId(component)] is UnsetSetting){error="请选择来源或输入常量："+ComponentId(component);return false;}
        InputValues? resolved=null;
        if(_componentBindings is {} binding)
        {
            var root=_root;
            _resolvingComponents=true;
            try
            {
                var declarations=components.Select(component=>(new Port(ComponentId(component),DataType.Number,Storage:NumericStorage.Float32),fields[ComponentId(component)])).ToArray();
                var result=binding.Resolve(Array.AsReadOnly(declarations));
                if(!ComponentsCurrent()||!ReferenceEquals(root,_root))throw new ArgumentException("解析期间来源、帧或编辑根已改变。");
                if(!result.Success)
                {
                    if(result.Diagnostic?.Code is "binding-stale" or "binding-reentrant" or "document-changed" or "schema-changed" or "run-mismatch" or "frame-mismatch")
                        throw new ArgumentException(result.Diagnostic.Message);
                    error=result.Diagnostic?.Message??"来源尚未产生完整参数。";return false;
                }
                resolved=result.Values!;
                if(result.Frame!=Frame||result.IsCurrent is null||!result.IsCurrent())throw new ArgumentException("解析回执帧不符或批次已过期。");
                current=result.IsCurrent;
                if(declarations.Any(field=>!resolved.Contains(field.Item1.Id)))throw new ArgumentException("解析回执缺少活动分量。");
            }
            finally{_resolvingComponents=false;}
        }
        var values=new Dictionary<string,double>(StringComparer.Ordinal);
        foreach(var component in components)
        {
            var id=ComponentId(component);
            var number=resolved is null?ResolveComponent(fields[id]):(double)ValueRules.Freeze(new Port(id,DataType.Number,Storage:NumericStorage.Float32),resolved.Get<double>(id))!;
            values.Add(component.GeometryField,number);
        }
        var geometry=new Dictionary<string,object?>{{"kind",way==1?"rectangle":"annulus"},{"center",RoiGeometryEditing.Point(new(values["x"],values["y"]))}};
        foreach(var value in values.Where(value=>value.Key is not ("x" or "y" or "sweep")))geometry.Add(value.Key,value.Value);
        if(way==3)geometry.Add("end-radians",values["start-radians"]+values["sweep"]);
        var shape=new RecordValue(RoiDeclarations.Shape,geometry);RoiGeometryEditing.Validate(shape);
        item=new("detect",way==1?_rectComponentsId:_annulusComponentsId,"参数继承",shape,Enabled:false);return true;
    }
    public bool TryCapture(out RoiEditSnapshot? snapshot,out string error)
    {
        ObjectDisposedException.ThrowIf(_disposed,this);snapshot=null;
        if(!Fields.TryValidate(out error))return false;
        if(_disposed){error="来源组合已释放。";return false;}
        var root=_root;
        if(!ComponentsCurrent()||!ReferenceEquals(root,_root)){error="来源或编辑根已改变。";return false;}
        if(GraphicMode)
        {
            if(root.DetectionCurrent?.Invoke()==false||_disposed||!ReferenceEquals(root,_root)){error="来源解析回执已过期。";return false;}
            if(!Local("detect")&&root.Detection is null){error=root.DetectionError;return false;}
            if(!Local("mask")&&root.Fields["mask.source"] is not ReferenceSetting){error="请选择屏蔽区域来源。";return false;}
        }
        else if(root.Fields["mask.image"] is not ReferenceSetting image){error="请选择屏蔽图像来源。";return false;}
        else ValidateMaskImage(image);
        snapshot=Read();
        if(_disposed||!ReferenceEquals(root,_root)){snapshot=null;error="捕获期间编辑根已改变。";return false;}
        return true;
    }
    private void ValidateMaskImage(ReferenceSetting image)
    {
        var reference=image.Reference;
        if(reference.NodeId!="mask-image"||reference.PortId!="image"||reference.Type!=TypeRef.ResourceOf(ResourceKind.Image)
            ||reference.Storage is not null||reference.ScopeId is not null||reference.MemberFieldPath.Count!=0||reference.Selection is not BindingSelection.AllItems)
            throw new ArgumentException("屏蔽图像必须来自本示例同帧图像声明。");
        ResourceContractRules.ValidateImage(_image);
    }
}
