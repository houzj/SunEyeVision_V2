using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.Fields;

/// <summary>Display identity is never a parameter value. Only a declared formal key may be accepted.</summary>
public sealed record DeclaredChoiceDisplayItem
{
    public string DisplayId {get;}
    public string Label {get;}
    public string? FormalKey {get;}
    public bool IsAvailable {get;}
    public string? UnavailableReason {get;}
    public DeclaredChoiceDisplayItem(string displayId,string label,string? formalKey,bool isAvailable,string? unavailableReason=null)
    {
        if(string.IsNullOrWhiteSpace(displayId)||string.IsNullOrWhiteSpace(label)
            ||formalKey is not null&&string.IsNullOrWhiteSpace(formalKey))
            throw new ArgumentException("选项展示身份、标签或正式键无效。");
        if(isAvailable&&(formalKey is null||unavailableReason is not null)
            ||!isAvailable&&string.IsNullOrWhiteSpace(unavailableReason))
            throw new ArgumentException("可用项必须有正式键，不可用项必须说明原因。");
        DisplayId=displayId;Label=label;FormalKey=formalKey;IsAvailable=isAvailable;UnavailableReason=unavailableReason;
    }
}

/// <summary>Frozen presentation over a closed constant-choice port; it cannot extend that port's values.</summary>
public sealed class DeclaredChoiceDisplayOptions
{
    public IReadOnlyList<DeclaredChoiceDisplayItem> Items {get;}
    public DeclaredChoiceDisplayOptions(IEnumerable<DeclaredChoiceDisplayItem> items)
    {
        ArgumentNullException.ThrowIfNull(items);var rows=items.ToArray();
        if(rows.Length==0||rows.Any(item=>item is null)
            ||rows.Select(item=>item.DisplayId).Distinct(StringComparer.Ordinal).Count()!=rows.Length
            ||rows.Where(item=>item.FormalKey is not null).Select(item=>item.FormalKey).Distinct(StringComparer.Ordinal).Count()!=rows.Count(item=>item.FormalKey is not null))
            throw new ArgumentException("选项展示清单不能为空，且展示身份和正式键必须唯一。");
        Items=Array.AsReadOnly(rows);
    }
    internal void ValidatePort(Port port)
    {
        if(port.Type!=DataType.Text||port.Choices is null||(port.EditModes&FieldEditModes.Reference)!=0
            ||!port.Choices.Keys.ToHashSet(StringComparer.Ordinal).SetEquals(Items.Where(item=>item.FormalKey is not null).Select(item=>item.FormalKey!)))
            throw new ArgumentException("展示清单必须完整对应同一常量枚举字段的全部正式键，不能增减声明值。");
    }
    internal string? ValidateProposal(ParameterSetting setting)
    {
        if(setting is UnsetSetting)return null;
        if(setting is not ConstantSetting {Value:string key})return "枚举展示字段只接受正式常量键。";
        var item=Items.SingleOrDefault(item=>item.FormalKey==key);
        return item is null?"该展示项没有可接受的正式键。":item.IsAvailable?null:item.UnavailableReason;
    }
}