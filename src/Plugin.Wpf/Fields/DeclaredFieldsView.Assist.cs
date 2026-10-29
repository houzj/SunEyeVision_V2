using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Fields;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Plugin.Wpf.Native.Common.Localization;

namespace SunEyeVision.Plugin.Wpf.Fields;

public sealed partial class DeclaredFieldsView
{
    private NativeAssistOptions[] _assists=[];
    private static string[] AssistNumbers(NativeAssistNumbers numbers)=>
        [numbers.XFieldId,numbers.YFieldId,numbers.AngleFieldId,numbers.ScaleXFieldId,numbers.ScaleYFieldId];
    private static string[] AssistSources(NativeAssistOptions option)=>
        [option.FixturePort.Id,option.BasePointFieldId,..AssistNumbers(option.Base),option.RunPointFieldId,..AssistNumbers(option.Run)];
    private static string[] AssistCleared(NativeAssistOptions option)=>
        [option.FixturePort.Id,option.RunPointFieldId,..AssistNumbers(option.Base),..AssistNumbers(option.Run)];

    private (IReadOnlyDictionary<string,IReadOnlyList<FieldCondition>> Parents,IReadOnlySet<string> Inline)
        ConfigureAssists(IReadOnlyDictionary<string,(Port Port,FieldPresentation Presentation,ParameterSetting Value)> declarations,
            IReadOnlyList<NativeAssistOptions>? options,Dictionary<string,NativeIntegerRadioOptions> radios,
            IReadOnlyDictionary<string,IReadOnlyList<FieldCondition>>? parents,IReadOnlySet<string>? inline,
            IReadOnlyDictionary<string,Func<IReadOnlyList<SunEyeVision.Plugin.Wpf.Adapters.References.DeclaredReferenceCandidate>>>? images,
            IReadOnlyList<NativeGraphicDisplayOptions>? graphics,IReadOnlyList<NativeTextDisplayOptions>? texts,
            IReadOnlyDictionary<string,SunEyeVision.Plugin.Wpf.Adapters.Text.TextExpressionOptions>? expressions)
    {
        var gates=parents?.ToDictionary(entry=>entry.Key,entry=>entry.Value,StringComparer.Ordinal)??new(StringComparer.Ordinal);
        var sameSlot=inline?.ToHashSet(StringComparer.Ordinal)??new(StringComparer.Ordinal);
        var consumed=new HashSet<string>(StringComparer.Ordinal);
        var values=options?.ToArray()??[];
        if(values.Length==0){_assists=[];return(gates,sameSlot);}
        if(gates.Any(entry=>entry.Value is null)||graphics?.Any(option=>option is null)==true||texts?.Any(option=>option is null)==true)
            throw new ArgumentException("Assist附属条件或显示配置不能为空。");
        var displayIds=(graphics??[]).SelectMany(GraphicIds).Concat((texts??[]).SelectMany(TextIds)).ToHashSet(StringComparer.Ordinal);
        foreach(var option in values)
        {
            if(option is null||option.FixturePort is null||option.Base is null||option.Run is null)
                throw new ArgumentException("Assist组合必须完整声明十五个角色。");
            var ids=new[]{option.EnabledFieldId,option.ModeFieldId}.Concat(AssistSources(option)).ToArray();
            if(ids.Length!=15||ids.Any(string.IsNullOrWhiteSpace)||ids.Distinct(StringComparer.Ordinal).Count()!=15)
                throw new ArgumentException("Assist必须引用十五个不同的稳定字段。");
            foreach(var id in ids)
            {
                if(!declarations.TryGetValue(id,out var field)||!consumed.Add(id))throw new ArgumentException("Assist字段未知或重复消费："+id);
                if(field.Presentation.Layout is not {} layout||layout.PairWith is not null||layout.DialogGroup is not null
                    ||layout.DialogPrimary||radios.ContainsKey(id)||images?.ContainsKey(id)==true||displayIds.Contains(id)
                    ||expressions?.ContainsKey(id)==true)
                    throw new ArgumentException("Assist角色与其他组合冲突："+id);
            }
            var first=declarations[option.EnabledFieldId].Presentation;
            foreach(var id in ids)
            {
                var field=declarations[id].Presentation;
                if(field.Layout!.Page!=first.Layout!.Page||field.Layout.Section!=first.Layout.Section
                    ||field.Layout.Advanced!=first.Layout.Advanced||field.Group!=first.Group)
                    throw new ArgumentException("Assist十五角色必须属于同一页面和组合区。");
            }
            var enabled=declarations[option.EnabledFieldId].Port;
            if(enabled.Type!=DataType.Boolean||!enabled.Required||enabled.Nullable
                ||enabled.EditModes is not (FieldEditModes.None or FieldEditModes.Constant))
                throw new ArgumentException("Assist启用角色要求必填常量Boolean。");
            var mode=declarations[option.ModeFieldId].Port;
            var radio=new NativeIntegerRadioOptions(new[]{
                new NativeIntegerRadioChoice(0,AppLocalizationService.GetLang("RunParam_By Info","按信息")),
                new NativeIntegerRadioChoice(1,AppLocalizationService.GetLang("RunParam_By Point","按点")),
                new NativeIntegerRadioChoice(2,AppLocalizationService.GetLang("RunParam_By Coordinate","按坐标"))},true);
            radio.ValidatePort(mode);radios.Add(mode.Id,radio);
            var fixture=declarations[option.FixturePort.Id].Port;
            if(fixture!=option.FixturePort||fixture.Required||fixture.Nullable||fixture.EditModes!=FieldEditModes.Reference)
                throw new ArgumentException("Assist修正信息必须使用调用者提供的可清空正式引用声明。");
            var basePoint=declarations[option.BasePointFieldId].Port;var runPoint=declarations[option.RunPointFieldId].Port;
            foreach(var point in new[]{basePoint,runPoint})
                if(point.Type.Shape!=ValueShape.Geometry||point.Type.Geometry!=GeometryKind.Point||point.Required
                    ||point.Nullable||point.EditModes!=FieldEditModes.Reference||point.Type!=basePoint.Type)
                    throw new ArgumentException("Assist两点要求同坐标声明、可清空的Point引用。");
            foreach(var id in AssistNumbers(option.Base).Concat(AssistNumbers(option.Run)))
            {
                var field=declarations[id];var port=field.Port;
                if(port.Type!=DataType.Number||port.Storage!=NumericStorage.Float32||port.Required||port.Nullable
                    ||port.EditModes is not (FieldEditModes.Reference or (FieldEditModes.Constant|FieldEditModes.Reference)))
                    throw new ArgumentException("Assist数值要求可清空Float32来源及显式编辑能力："+id);
                if(port.EditModes==(FieldEditModes.Constant|FieldEditModes.Reference))sameSlot.Add(id);
            }
            Gate(option.ModeFieldId,new(option.EnabledFieldId,true));
            foreach(var id in AssistSources(option))Gate(id,new(option.EnabledFieldId,true));
            Gate(fixture.Id,new(mode.Id,0L));
            foreach(var id in new[]{option.BasePointFieldId,option.RunPointFieldId})Gate(id,new(mode.Id,1L));
            foreach(var numbers in new[]{option.Base,option.Run})
            {
                foreach(var id in new[]{numbers.XFieldId,numbers.YFieldId})Gate(id,new(mode.Id,2L));
                foreach(var id in new[]{numbers.AngleFieldId,numbers.ScaleXFieldId,numbers.ScaleYFieldId})Gate(id,new(mode.Id,0L,true));
            }
        }
        _assists=values;return(gates,sameSlot);
        void Gate(string id,FieldCondition condition)
        {
            var previous=gates.GetValueOrDefault(id)??Array.Empty<FieldCondition>();
            var declared=declarations[id].Presentation.Layout!.VisibleWhen;
            var constraints=previous.Concat(declared is null?[]:new[]{declared}).Append(condition)
                .Where(existing=>existing.FieldId==condition.FieldId)
                .Select(existing=>existing with{Value=ValueRules.Freeze(declarations[condition.FieldId].Port,existing.Value)}).ToArray();
            object[] domain=declarations[condition.FieldId].Port.Type==DataType.Boolean?[false,true]:[0L,1L,2L];
            if(!domain.Any(value=>constraints.All(existing=>Equals(value,existing.Value)!=existing.Negate)))
                throw new ArgumentException("Assist角色条件与既有条件冲突："+id);
            gates[id]=previous.Contains(condition)?previous:Array.AsReadOnly(previous.Append(condition).ToArray());
        }
    }

    private IReadOnlyList<(DeclaredFieldState State,ParameterSetting Proposal)> ExpandAssistProposals(
        IReadOnlyList<(DeclaredFieldState State,ParameterSetting Proposal)> proposals)
    {
        if(_assists.Length==0)return proposals;
        var changes=proposals.ToDictionary(entry=>entry.State.Port.Id,entry=>entry,StringComparer.Ordinal);
        var states=_fieldStates.ToDictionary(state=>state.Port.Id,StringComparer.Ordinal);
        foreach(var option in _assists)
        {
            var hasEnabled=changes.TryGetValue(option.EnabledFieldId,out var enabled);
            var effectiveEnabled=hasEnabled?enabled.Proposal:states[option.EnabledFieldId].Accepted;
            if(hasEnabled&&effectiveEnabled is ConstantSetting{Value:false})
            {
                if(AssistCleared(option).Any(id=>changes.TryGetValue(id,out var change)&&change.Proposal is not UnsetSetting))
                    throw new ArgumentException("关闭修正与安装其来源不能属于同一提案。");
                foreach(var id in AssistCleared(option))changes[id]=(states[id],new UnsetSetting());
            }
            else if(effectiveEnabled is ConstantSetting{Value:false}
                &&AssistCleared(option).Any(id=>changes.TryGetValue(id,out var change)&&change.Proposal is not UnsetSetting))
                throw new ArgumentException("关闭的位置修正不能安装隐藏来源。");
        }
        return changes.Values.ToArray();
    }
    private string? AssistCompletenessError()
    {
        foreach(var option in _assists)
        {
            var states=_fieldStates.ToDictionary(state=>state.Port.Id,StringComparer.Ordinal);
            if(!IsFieldActive(option.EnabledFieldId)||states[option.EnabledFieldId].Accepted is not ConstantSetting{Value:true})continue;
            if(states[option.ModeFieldId].Accepted is not ConstantSetting{Value:long mode}||mode is <0 or >2)
                return "请选择位置修正的三种原方式之一。";
            foreach(var id in AssistSources(option).Where(IsFieldActive))
                if(states[id].Accepted is UnsetSetting)return "请选择位置修正来源："+_groupPresentations[id].DisplayName;
        }
        return null;
    }
}
