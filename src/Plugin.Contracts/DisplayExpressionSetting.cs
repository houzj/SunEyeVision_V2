using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Plugin.Contracts;

/// <summary>A presentation expression keeps its editable spelling and ordered, stable source identities.
/// It is never an invocation parameter or an evaluated current value.</summary>
public sealed record DisplayExpressionSetting : ParameterSetting
{
    public DisplayExpressionSetting(string text,IEnumerable<DisplayExpressionSlot> slots)
    {
        ArgumentNullException.ThrowIfNull(text);ArgumentNullException.ThrowIfNull(slots);
        Text=text;Slots=Array.AsReadOnly(slots.Select(slot=>slot?.Copy()??throw new ArgumentException("Expression slot cannot be null.")).ToArray());
        if(Slots.Any(slot=>string.IsNullOrWhiteSpace(slot.Token)||slot.Token.Contains('{')||slot.Token.Contains('}')))
            throw new ArgumentException("Expression tokens must be nonempty brace-free text.");
        var groups=Slots.GroupBy(slot=>slot.Token,StringComparer.Ordinal);
        if(groups.Any(group=>group.Skip(1).Any(slot=>!slot.SameSource(group.First()))))
            throw new ArgumentException("One expression spelling cannot identify different sources.");
    }
    public string Text {get;}
    public IReadOnlyList<DisplayExpressionSlot> Slots {get;}
    public DisplayExpressionSetting Copy()=>new(Text,Slots);
    public bool Equals(DisplayExpressionSetting? other)=>other is not null&&SameExpression(other);
    public override int GetHashCode()=>StringComparer.Ordinal.GetHashCode(Text);
    public DisplayExpressionSetting MapReferences(Func<OutputReference,OutputReference> map)
        =>new(Text,Slots.Select(slot=>slot.Reference is {} reference?new DisplayExpressionSlot(slot.Token,map(reference),slot.Format):slot));
    public bool SameExpression(DisplayExpressionSetting other)=>Text==other.Text&&Slots.Count==other.Slots.Count
        &&Slots.Zip(other.Slots).All(pair=>pair.First.Token==pair.Second.Token&&pair.First.SameSource(pair.Second));
}

/// <summary>Exactly one relative self path or explicitly scoped output reference. No resolved values or leases.</summary>
public sealed record DisplayExpressionSlot
{
    public DisplayExpressionSlot(string token,OutputReference reference,string? format=null)
    {Token=token;Reference=reference??throw new ArgumentNullException(nameof(reference));Format=ValidateFormat(format);}
    public DisplayExpressionSlot(string token,ResultFieldPath self,string? format=null)
    {Token=token;Self=self is null?throw new ArgumentNullException(nameof(self)):new(self.PortId,self.MemberFieldPath);Format=ValidateFormat(format);}
    private static string? ValidateFormat(string? format)
    {if(format is not null&&!System.Text.RegularExpressions.Regex.IsMatch(format,@"^[FfNnGgRr][0-9]?$") )throw new ArgumentException("Unsupported display numeric format.");return format;}
    public string? Format {get;}
    public string Token {get;}
    public OutputReference? Reference {get;}
    public ResultFieldPath? Self {get;}
    public DisplayExpressionSlot Copy()=>Reference is {} reference?new(Token,reference,Format):new(Token,Self!,Format);
    private static bool SameReference(OutputReference a,OutputReference b)=>a.NodeId==b.NodeId&&a.ScopeId==b.ScopeId
        &&a.PortId==b.PortId&&a.Type==b.Type&&a.Storage==b.Storage&&a.MemberFieldPath.SequenceEqual(b.MemberFieldPath)
        &&SameSelection(a.Selection,b.Selection);
    private static bool SameSelection(BindingSelection a,BindingSelection b)=>(a,b) switch
    {
        (BindingSelection.AllItems,BindingSelection.AllItems)=>true,
        (BindingSelection.AtConstant x,BindingSelection.AtConstant y)=>x.Index==y.Index,
        (BindingSelection.AtField x,BindingSelection.AtField y)=>x.ConstructionBehavior==y.ConstructionBehavior&&SameReference(x.Reference,y.Reference),
        (BindingSelection.AtVariable x,BindingSelection.AtVariable y)=>x.Reference.SolutionId==y.Reference.SolutionId&&x.Reference.VariableId==y.Reference.VariableId
            &&x.Reference.Type==y.Reference.Type&&x.Reference.Storage==y.Reference.Storage,
        _=>false
    };
    internal bool SameSource(DisplayExpressionSlot other)=>Format==other.Format&&(Reference is {} reference
        ?other.Reference is {} right&&SameReference(reference,right)
        :other.Self is {} self&&Self!.PortId==self.PortId&&Self.MemberFieldPath.SequenceEqual(self.MemberFieldPath));
}
