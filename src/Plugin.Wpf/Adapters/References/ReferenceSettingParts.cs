using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

/// <summary>Operations on the existing source union; no display text is parsed into source identity.</summary>
internal static class ReferenceSettingParts
{
    internal static bool IsReference(ParameterSetting value)=>value is ReferenceSetting or VariableSetting;
    internal static BindingConversion Conversion(ParameterSetting value)=>value switch {
        ReferenceSetting module=>module.Conversion,VariableSetting variable=>variable.Conversion,_=>BindingConversion.Automatic};
    internal static BindingSelection Selection(ParameterSetting value)=>value switch {
        ReferenceSetting module=>module.Reference.Selection,VariableSetting variable=>variable.Reference.Selection,_=>throw new ArgumentException("字段没有引用来源。")};
    internal static TypeRef Type(ParameterSetting value)=>value switch {
        ReferenceSetting module=>DeclaredReferenceCandidate.ProjectType(module.Reference.Type,module.Reference.MemberFieldPath),
        VariableSetting variable=>variable.Reference.Type,_=>throw new ArgumentException("字段没有引用声明。")};
    internal static ParameterSetting WithSelection(ParameterSetting value,BindingSelection selection)=>value switch {
        ReferenceSetting module=>module with {Reference=new OutputReference(module.Reference.NodeId,module.Reference.PortId,module.Reference.Type,
            module.Reference.MemberFieldPath,selection,module.Reference.ScopeId,module.Reference.Storage)},
        VariableSetting variable=>variable with {Reference=new VariableReference(variable.Reference.SolutionId,variable.Reference.VariableId,variable.Reference.Type,selection,variable.Reference.Storage)},
        _=>throw new ArgumentException("字段没有引用来源。")};
    internal static bool SameSource(VariableReference a,VariableReference b)=>a.SolutionId==b.SolutionId&&a.VariableId==b.VariableId&&a.Type==b.Type&&a.Storage==b.Storage;
}
