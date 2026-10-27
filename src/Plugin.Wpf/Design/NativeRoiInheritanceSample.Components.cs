using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Regions;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;

namespace SunEyeVision.Plugin.Wpf.Design;

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
            yield return(new(id,DataType.Number,Required:false,EditModes:FieldEditModes.Reference),
                new FieldPresentation(id,AppLocalizationService.GetLang(component.Label),"来源组合",20+Array.IndexOf(Components,component),FieldEditorKind.Scalar){Layout=new("来源组合","来源组合",VisibleWhen:new("detect.inherit-way",(long)component.Way))},_root.Fields[id]);
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
                _numbers.Add(node,new(node,value,foreign?new(Guid.NewGuid(),"different-component-frame"):_image.Frame));
            }
        }
    }
    private static DeclaredReferenceCandidate NumberCandidate(NumberSource source)=>new(source.Node,source.Node,null,
        new Port("value",DataType.Number,Storage:NumericStorage.Float64),Array.Empty<string>());
    private IReadOnlyList<DeclaredReferenceCandidate> Candidates(Port port)
    {
        if(Components.Any(component=>ComponentId(component)==port.Id))return _numbers.Values.Select(NumberCandidate).ToArray();
        var group=port.Id=="mask.source"?"mask":"detect";
        var kind=port.Id=="detect.source"?"rectangle":"annulus";
        return _sources.Where(source=>source.Group==group&&source.Geometry.Get<string>("kind")==kind)
            .Select(source=>new DeclaredReferenceCandidate(source.Node,source.Node,null,new Port("geometry",RoiDeclarations.Shape),Array.Empty<string>())).ToArray();
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
            ||reference.Storage!=NumericStorage.Float64||reference.ScopeId is not null||reference.MemberFieldPath.Count!=0||reference.Selection is not BindingSelection.AllItems)
            throw new ArgumentException("参数来源必须是明确声明的浮点单项。");
        if(source.Frame!=_image.Frame)throw new ArgumentException("参数来源与当前图像不是同一物理帧。");
        if(!double.IsFinite(source.Value))throw new ArgumentException("参数来源不是有限数值。");
        return source.Value;
    }
    private string? ValidateInheritance(Fields.FieldGroupCandidate candidate)
    {
        foreach(var (id,setting) in candidate.Changes)
        {
            if(setting is not ReferenceSetting reference)continue;
            if(Components.Any(component=>ComponentId(component)==id))ResolveNumber(reference);
            else if(id is "detect.source" or "mask.source")Resolve(id=="detect.source"?"detect":"mask",reference);
            else if(id=="detect.annulus-source")Resolve("detect",reference,"annulus");
            else if(id=="mask.image")ValidateMaskImage(reference);
        }
        if(candidate.Proposed["area.mode"] is ConstantSetting{Value:0L}&&candidate.Proposed["detect.mode"] is ConstantSetting{Value:1L})
            TryDetect(candidate.Proposed,out _,out _); // Incomplete is a valid edit state; invalid complete geometry throws.
        return null;
    }
    private bool TryDetect(IReadOnlyDictionary<string,ParameterSetting> fields,out RoiEditItem? item,out string error)
    {
        item=null;error="";
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
        var values=new Dictionary<string,double>(StringComparer.Ordinal);
        foreach(var component in Components.Where(component=>component.Way==way))
        {
            var id=ComponentId(component);
            if(fields[id] is not ReferenceSetting reference){error="请选择来源："+id;return false;}
            values.Add(component.GeometryField,ResolveNumber(reference));
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
        if(GraphicMode)
        {
            if(!Local("detect")&&!TryDetect(root.Fields,out _,out error))return false;
            if(!Local("mask")&&root.Fields["mask.source"] is not ReferenceSetting){error="请选择屏蔽区域来源。";return false;}
        }
        else if(root.Fields["mask.image"] is not ReferenceSetting image){error="请选择屏蔽图像来源。";return false;}
        else ValidateMaskImage(image);
        snapshot=Read();return true;
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
