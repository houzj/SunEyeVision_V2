using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

internal static class CanonicalCases
{
    internal static readonly TypeRef Numbers=TypeRef.SequenceOf(DataType.Integer,CountConstraint.Any);
    internal static readonly OutputReference Index=new("节点<一>","序号.\"",DataType.Integer,scopeId:"流程/一",storage:NumericStorage.Int64);
    internal static readonly VariableReference VariableIndex=new("方案<1>","索引&一",DataType.Integer);
    internal static readonly VariableReference Variable=new("方案一","变量\"x",Numbers,new BindingSelection.AtField(Index));
    internal static readonly OutputReference Nested=new("node一","端口/序列",Numbers,new[]{"member.1"},new BindingSelection.AtVariable(VariableIndex),"scope&<");
    internal static readonly OutputReference ConstantIndex=new("节点B","数组",Numbers,selection:new BindingSelection.AtConstant(2),scopeId:"流程B");
    internal static readonly OutputReference Resource=new("src","image",TypeRef.ResourceOf(ResourceKind.Image),scopeId:"scope");
    internal static readonly TypeRef Record=TypeRef.RecordOf("R-类型",new[]{new Port("文本",DataType.Text),new Port("整数",DataType.Integer)});
    internal static IReadOnlyDictionary<string,ParameterSetting> Fields()=>new Dictionary<string,ParameterSetting>{
        ["z.bool"]=new ConstantSetting(true),["a.long"]=new ConstantSetting(-42L),["n.real"]=new ConstantSetting(1.25d),["text"]=new ConstantSetting("中文<>&\"\\"),
        ["null"]=new ConstantSetting(null),["array"]=new ConstantSetting(new double[]{-0d,1.5,123456.25}),
        ["sequence"]=new ConstantSetting(new SequenceValue(Numbers,new object?[]{1L,2L})),
        ["record"]=new ConstantSetting(new RecordValue(Record,new Dictionary<string,object?>{{"文本","字<段"},{"整数",7L}})),
        ["reference"]=new ReferenceSetting(Nested,BindingConversion.Automatic,"connection:<1>","重选"),
        ["variable"]=new VariableSetting(Variable,BindingConversion.Automatic,"变量缺失"),
        ["expression"]=new DisplayExpressionSetting("值:{宽度}/{本地}",new[]{new DisplayExpressionSlot("宽度",Resource),new DisplayExpressionSlot("本地",new ResultFieldPath("端口",new[]{"值"}),"F2")})
    };
}
