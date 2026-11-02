using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Adapters.References;
using SunEyeVision.Plugin.Wpf.Adapters.Text;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;

namespace SunEyeVision.Plugin.Wpf.Fields;

public sealed partial class DeclaredFieldsView
{
    private static string[] TextIds(NativeTextDisplayOptions option)=>[option.ContentFieldId,option.OkColorFieldId,
        option.NgColorFieldId,option.FontSizeFieldId,option.OpacityFieldId,option.PositionXFieldId,option.PositionYFieldId];
    private void ValidateTextDisplays(
        IReadOnlyDictionary<string,(Port Port,FieldPresentation Presentation,ParameterSetting Value)> declarations,
        IReadOnlyList<NativeTextDisplayOptions> texts,IReadOnlySet<string> graphicIds,IReadOnlySet<string> paired,
        IReadOnlyDictionary<string,NativeIntegerRadioOptions> radios,
        IReadOnlyDictionary<string,Func<IReadOnlyList<DeclaredReferenceCandidate>>> images,IReadOnlySet<string>? externalInline,
        IReadOnlyDictionary<string,TextExpressionOptions> expressions)
    {
        var consumed=new HashSet<string>(graphicIds,StringComparer.Ordinal);
        foreach(var option in texts)
        {
            var ids=TextIds(option);
            if(ids.Any(string.IsNullOrWhiteSpace)||ids.Distinct(StringComparer.Ordinal).Count()!=7)
                throw new ArgumentException("文本七行必须声明七个不同的稳定字段。");
            foreach(var id in ids)
            {
                if(!declarations.ContainsKey(id)||!consumed.Add(id))throw new ArgumentException("文本角色未知或重复消费："+id);
                if(paired.Contains(id)||radios.ContainsKey(id)||images.ContainsKey(id)||externalInline?.Contains(id)==true
                    ||id!=option.ContentFieldId&&expressions.ContainsKey(id))throw new ArgumentException("文本角色与其他组合冲突："+id);
            }
            var first=declarations[ids[0]].Presentation;var a=first.Layout??throw new ArgumentException("文本显示需要完整布局。");
            foreach(var id in ids)
            {
                var p=declarations[id].Presentation;var b=p.Layout??throw new ArgumentException("文本显示需要完整布局。");
                if(b.DialogGroup is not null||b.DialogPrimary||a.Page!=b.Page||a.Section!=b.Section||a.Advanced!=b.Advanced
                    ||a.VisibleWhen!=b.VisibleWhen||first.Group!=p.Group||!Gates(ids[0]).SequenceEqual(Gates(id)))
                    throw new ArgumentException("文本七行必须共享组装区及活动条件。");
            }
            var content=declarations[option.ContentFieldId];
            if(content.Port.Type!=DataType.Text||content.Port.Nullable||!content.Port.Required||content.Port.Choices is not null
                ||(content.Port.EditModes&FieldEditModes.Constant)==0||(content.Port.EditModes&~(FieldEditModes.Constant|FieldEditModes.DisplayExpression))!=0)
                throw new ArgumentException("文本内容必须具有正式字符串及表达式能力。");
            _=FieldSettings.Freeze(content.Port,content.Value);
            Constant(option.OkColorFieldId,PresentationDeclarations.Rgba);Constant(option.NgColorFieldId,PresentationDeclarations.Rgba);
            Number(option.FontSizeFieldId,6,72,1);Number(option.OpacityFieldId,0,1,.1);
            foreach(var id in new[]{option.PositionXFieldId,option.PositionYFieldId})
            {
                var field=declarations[id];var port=field.Port;
                if(port.Nullable||port.Type.Shape!=ValueShape.Scalar||port.Type.Scalar is not (ScalarKind.Int64 or ScalarKind.Float64)
                    ||port.EditModes is not (FieldEditModes.Constant or FieldEditModes.Reference or (FieldEditModes.Constant|FieldEditModes.Reference)))
                    throw new ArgumentException("文本坐标要求原数值订阅及明确的常量能力："+id);
                if(field.Value is ConstantSetting value)
                {
                    if(port.EditModes==FieldEditModes.Reference)throw new ArgumentException("引用坐标不能提供常量。");
                    InlineConstantRules.Validate(port,field.Presentation,value);
                }
            }
        }
        IReadOnlyList<FieldCondition> Gates(string id)=>_parentConditions.GetValueOrDefault(id)??Array.Empty<FieldCondition>();
        void Constant(string id,TypeRef type)
        {
            var field=declarations[id];var port=field.Port;
            if(port.Type!=type||port.Nullable||!port.Required||port.EditModes is not (FieldEditModes.None or FieldEditModes.Constant)
                ||port.Choices is not null||field.Value is not ConstantSetting value)
                throw new ArgumentException("文本角色类型或常量能力错误："+id);
            ValueRules.Freeze(port,value.Value);
        }
        void Number(string id,double minimum,double maximum,double increment)
        {
            Constant(id,TypeRef.ScalarOf(ScalarKind.Float64));var field=declarations[id];var layout=field.Presentation.Layout!;
            if(layout.Minimum!=minimum||layout.Maximum!=maximum||layout.Increment!=increment||layout.DecimalPlaces is null or <0 or >9)
                throw new ArgumentException("文本原数值范围、步长或精度错误："+id);
            var value=(double)ValueRules.Freeze(field.Port,((ConstantSetting)field.Value).Value)!;
            if(value<minimum||value>maximum||Math.Round(value,layout.DecimalPlaces.Value,MidpointRounding.ToEven)!=value)
                throw new ArgumentException("文本原数值初值超出范围或精度："+id);
        }
    }
    private static string[] GraphicIds(NativeGraphicDisplayOptions option)=>option.ThicknessFieldId is {} thickness
        ?[option.VisibleFieldId,option.OkColorFieldId,option.NgColorFieldId,option.OpacityFieldId,thickness]
        :[option.VisibleFieldId,option.OkColorFieldId,option.NgColorFieldId,option.OpacityFieldId];
    private Dictionary<string,NativeGraphicDisplayOptions> ValidateGraphics(
        IReadOnlyDictionary<string,(Port Port,FieldPresentation Presentation,ParameterSetting Value)> declarations,
        IReadOnlyList<NativeGraphicDisplayOptions>? graphics,IReadOnlySet<string> paired,
        IReadOnlyDictionary<string,NativeIntegerRadioOptions> radios,
        IReadOnlyDictionary<string,Func<IReadOnlyList<DeclaredReferenceCandidate>>> images,
        IReadOnlyDictionary<string,TextExpressionOptions> expressions)
    {
        var result=new Dictionary<string,NativeGraphicDisplayOptions>(StringComparer.Ordinal);
        var consumed=new HashSet<string>(StringComparer.Ordinal);var groups=new HashSet<string>(StringComparer.Ordinal);
        foreach(var option in graphics?.ToArray()??[])
        {
            if(option is null||string.IsNullOrWhiteSpace(option.GroupId)||!groups.Add(option.GroupId))
                throw new ArgumentException("图形组合必须声明唯一非空分组。");
            var ids=GraphicIds(option);
            if(ids.Any(string.IsNullOrWhiteSpace)||ids.Distinct(StringComparer.Ordinal).Count()!=ids.Length)
                throw new ArgumentException("图形角色必须非空且互不重复。");
            foreach(var id in ids)
            {
                if(!declarations.ContainsKey(id)||!consumed.Add(id))throw new ArgumentException("图形字段未知或重复消费："+id);
                if(paired.Contains(id)||radios.ContainsKey(id)||images.ContainsKey(id)||expressions.ContainsKey(id)||_inlineConstants.Contains(id))
                    throw new ArgumentException("图形样式与其他输入组合冲突："+id);
            }
            var members=declarations.Values.Where(field=>field.Presentation.Layout?.DialogGroup==option.GroupId).ToArray();
            if(!ids.ToHashSet(StringComparer.Ordinal).SetEquals(members.Select(field=>field.Port.Id)))
                throw new ArgumentException("图形角色必须完整消费分组，不能遗漏或隐藏额外字段。");
            var primary=members.Where(field=>field.Presentation.Layout!.DialogPrimary).ToArray();
            if(primary.Length!=1||primary[0].Port.Id!=option.VisibleFieldId)throw new ArgumentException("图形显示主字段错误。");
            var first=primary[0].Presentation;var a=first.Layout!;
            foreach(var member in members)
            {
                var p=member.Presentation;var b=p.Layout!;
                if(a.Page!=b.Page||a.Section!=b.Section||a.Advanced!=b.Advanced||a.VisibleWhen!=b.VisibleWhen||first.Group!=p.Group
                    ||!Gates(first.FieldId).SequenceEqual(Gates(p.FieldId)))throw new ArgumentException("图形角色必须共享布局和活动条件。");
            }
            Constant(option.VisibleFieldId,TypeRef.ScalarOf(ScalarKind.Boolean));
            Constant(option.OkColorFieldId,PresentationDeclarations.Rgba);Constant(option.NgColorFieldId,PresentationDeclarations.Rgba);
            Number(option.OpacityFieldId,0,1,.01);
            if(option.ThicknessFieldId is {} thickness)Number(thickness,1,10,1);
            result.Add(option.VisibleFieldId,option);
        }
        if(!groups.SetEquals(declarations.Values.Where(field=>field.Presentation.Layout?.DialogGroup is not null)
            .Select(field=>field.Presentation.Layout!.DialogGroup!)))throw new ArgumentException("所有图形分组都需要显式完整角色配置。");
        return result;

        IReadOnlyList<FieldCondition> Gates(string id)=>_parentConditions.GetValueOrDefault(id)??Array.Empty<FieldCondition>();
        void Constant(string id,TypeRef type)
        {
            var field=declarations[id];var port=field.Port;
            if(port.Type!=type||port.Nullable||!port.Required||port.EditModes is not (FieldEditModes.None or FieldEditModes.Constant)
                ||field.Value is not ConstantSetting value)throw new ArgumentException("图形角色需要非空常量声明："+id);
            ValueRules.Freeze(port,value.Value);
        }
        void Number(string id,double minimum,double maximum,double increment)
        {
            Constant(id,TypeRef.ScalarOf(ScalarKind.Float64));var field=declarations[id];var layout=field.Presentation.Layout!;
            if(layout.Minimum!=minimum||layout.Maximum!=maximum||layout.Increment!=increment||layout.DecimalPlaces!=2)
                throw new ArgumentException("原图形数值范围、步长或精度不符："+id);
            var value=(ConstantSetting)field.Value;NativeNumericCommit.EnsureRepresentable(value);
            var number=(double)ValueRules.Freeze(field.Port,value.Value)!;
            if(number<minimum||number>maximum||Math.Round(number,layout.DecimalPlaces.Value,MidpointRounding.ToEven)!=number)
                throw new ArgumentException("原图形数值初值超出范围或精度："+id);
        }
    }
}
