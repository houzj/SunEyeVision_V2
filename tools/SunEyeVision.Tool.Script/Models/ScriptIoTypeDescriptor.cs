using System;
using System.Globalization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.Script.Models;

/// <summary>脚本字段、绑定和结果声明共同使用SDK词目的实际CLR形状。</summary>
internal sealed record ScriptIoTypeDescriptor(string Vocabulary, Type ClrType)
{
    public bool IsCollection => ClrType.IsArray;
    public string CsTypeName => Format(ClrType);
    public string DefaultExpression => ClrType.IsArray
        ? $"= global::System.Array.Empty<{Format(ClrType.GetElementType()!)}>()"
        : ClrType == typeof(string) ? "= string.Empty" : ClrType.IsValueType ? "= default" : "= null!";
    public object? DefaultValue => ClrType.IsArray ? Array.CreateInstance(ClrType.GetElementType()!, 0)
        : ClrType == typeof(string) ? string.Empty : ClrType.IsValueType ? Activator.CreateInstance(ClrType) : null;

    public static ScriptIoTypeDescriptor From(string vocabulary)
    {
        var type = SdkTypeVocabulary.ToSystemType(vocabulary);
        // 几何词目已是数组；显式[]表示同一集合形状，不能再加一层。
        if (type == null && vocabulary.EndsWith("[]", StringComparison.Ordinal))
        {
            var element = SdkTypeVocabulary.ToSystemType(vocabulary[..^2]);
            type = element?.IsArray == true ? element : element?.MakeArrayType();
        }
        return type != null ? new(vocabulary, type)
            : throw new ArgumentOutOfRangeException(nameof(vocabulary), $"未知的脚本类型: '{vocabulary}'。");
    }

    private static string Format(Type type) => type.IsArray ? Format(type.GetElementType()!) + "[]"
        : "global::" + (type.FullName ?? throw new InvalidOperationException("脚本类型缺少完整名称")).Replace('+', '.');

    public object? ConvertValue(object? value) => ConvertTo(value, ClrType);
    private static object? ConvertTo(object? value, Type target)
    {
        if (value == null) return target.IsArray ? Array.CreateInstance(target.GetElementType()!, 0)
            : target.IsValueType ? Activator.CreateInstance(target) : null;
        if (target.IsInstanceOfType(value)) return value;
        if (target.IsArray && value is Array source && source.Rank == 1)
        {
            var converted = Array.CreateInstance(target.GetElementType()!, source.Length);
            for (var i = 0; i < source.Length; i++) converted.SetValue(ConvertTo(source.GetValue(i), target.GetElementType()!), i);
            return converted;
        }
        if (value is Array) throw new InvalidOperationException("数组不能直接赋给单值字段，请在绑定路径使用[i]索引。");
        if (value is IConvertible && typeof(IConvertible).IsAssignableFrom(target))
            return Convert.ChangeType(value, target, CultureInfo.InvariantCulture);
        throw new InvalidOperationException($"无法把 {value.GetType().Name} 转换为 {target.Name}。");
    }
}
