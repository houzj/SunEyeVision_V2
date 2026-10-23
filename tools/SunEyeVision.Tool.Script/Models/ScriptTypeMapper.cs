namespace SunEyeVision.Tool.Script.Models;

/// <summary>兼容既有调用点，类型规则集中于ScriptIoTypeDescriptor。</summary>
internal static class ScriptTypeMapper
{
    public static string ToCsTypeName(string typeName) => ScriptIoTypeDescriptor.From(typeName).CsTypeName;
    public static string GetFieldDefault(string typeName) => ScriptIoTypeDescriptor.From(typeName).DefaultExpression;
    public static object? GetDefaultValue(string typeName) => ScriptIoTypeDescriptor.From(typeName).DefaultValue;
}
