using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Controls.References.Variables;

/// <summary>Frozen display row. Value is display text only; selection returns Reference, never this text as a constant.</summary>
public sealed record VariableSelectionItem(string Name,string Group,int Index,string RemarkInfo,string Value,VariableReference Reference);

public sealed class VariableGroup
{
    public string DisplayNameA {get;}
    public string FullName {get;}
    public IReadOnlyList<VariableGroup> EnumEntrysA {get;}
    internal IReadOnlyList<VariableSelectionItem> Items {get;}
    internal VariableGroup(string name,string fullName,IEnumerable<VariableGroup> children,IEnumerable<VariableSelectionItem> items)
    {DisplayNameA=name;FullName=fullName;EnumEntrysA=Array.AsReadOnly(children.ToArray());Items=Array.AsReadOnly(items.ToArray());}
}
