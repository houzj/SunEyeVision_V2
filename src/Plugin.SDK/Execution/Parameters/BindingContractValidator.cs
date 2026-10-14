using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

public enum BindingCompatibility { Compatible, Incompatible, PendingValidation }
public sealed record BindingValidation(BindingCompatibility State, string Reason, Type? ValueType = null);

/// <summary>Formal-path checking; previous runtime contents never decide compatibility.</summary>
public static class BindingContractValidator
{
    public static BindingValidation Validate(IReadOnlyList<OutputVariable>? contract, string path, IReadOnlyList<Type> allowed)
    {
        BindingValidation Bad(string message) => new(BindingCompatibility.Incompatible, message);
        BindingValidation Pending(string message) => new(BindingCompatibility.PendingValidation, message);
        if (contract == null) return Pending("上游输出契约暂不可用，等待校验");
        var parts = path.Split('.');
        if (parts.Length == 0 || parts.Any(p => !Regex.IsMatch(p, @"^[A-Za-z_\p{L}][\w]*(?:\[Type=[A-Za-z]+\])?(?:\[(?:[0-9]+)?\])?$")))
            return Bad("绑定元素路径格式无效，请重新绑定");
        var parsed = PropertyPathParser.ParsePropertyPath(path);
        var output = contract.FirstOrDefault(o => string.Equals(o.Name, parsed[0].propertyName, StringComparison.OrdinalIgnoreCase));
        if (output == null || output.Kind != OutputVariableKind.Data) return Bad("上游输出字段已删除或不是数据输出，请重新绑定");
        var type = output.DeclaredType ?? SdkTypeVocabulary.ToSystemType(output.VariableType);
        if (type == null || type == typeof(object)) return Pending("来源类型未声明完整，等待校验");
        if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
            return Pending("集合载体缺少统一元素输出声明，等待校验");
        var collection = output.IsCollection ?? type.IsArray;
        if (type.IsArray) type = type.GetElementType()!;
        int? count = output.FixedCount ?? (collection ? null : 1);
        string? filter = null;
        for (var i = 0; i < parsed.Count; i++)
        {
            var segment = parsed[i];
            if (i > 0)
            {
                if (collection && (type.IsPrimitive || type == typeof(string))) return Pending("标量集合成员需明确元素索引，等待校验");
                if (collection && segment.arrayIndex != null) return Pending("嵌套集合的索引语义未声明完整，等待校验");
                if (type == typeof(object)) return Pending("多态成员缺少形状声明，等待校验");
                if (type == typeof(RoiShape) && segment.propertyName == "Geometry" && filter != null)
                    type = GeometryType(filter)!;
                else
                {
                    var property = type.GetProperty(segment.propertyName);
                    var field = type.GetField(segment.propertyName);
                    if (property == null && field == null) return Bad("上游输出成员已不存在，请重新绑定");
                    type = property?.PropertyType ?? field!.FieldType;
                }
                if (type.IsArray) { type = type.GetElementType()!; collection = true; count = null; }
            }
            if (segment.typeFilter != null)
            {
                if (type != typeof(RoiShape)) return Bad("该输出不支持图形类型筛选，请重新绑定");
                if (GeometryType(segment.typeFilter) == null) return Bad("图形类型筛选无效，请重新绑定");
                filter = segment.typeFilter; collection = true; count = null;
            }
            if (segment.arrayIndex is >= 0)
            {
                if (count.HasValue && segment.arrayIndex >= count.Value) return Bad("绑定元素索引超出声明的固定数量，请重新绑定");
                collection = false; count = 1;
            }
            else if (segment.arrayIndex == -1) collection = true;
        }
        var actual = collection ? type.MakeArrayType() : type;
        return Accept(actual, allowed) switch
        {
            true => new(BindingCompatibility.Compatible, "适配", actual),
            false => Bad("来源类型或集合形状不符合当前输入要求，请重新绑定"),
            null => Pending("输入类型要求未声明完整，等待校验")
        };
    }

    public static bool? Accept(Type? type, IReadOnlyList<Type> allowed)
    {
        if (type == null || type == typeof(object) || allowed.Count == 0) return null;
        Type Normalize(Type t)
        {
            var array = t.IsArray; var item = array ? t.GetElementType()! : t;
            var canonical = SdkTypeVocabulary.ToSystemType(SdkTypeVocabulary.ToTypeVocabulary(item)) ?? item;
            if (canonical.IsArray) canonical = canonical.GetElementType()!;
            return array ? canonical.MakeArrayType() : canonical;
        }
        type = Normalize(type);
        return allowed.Any(t => t == typeof(object) || Normalize(t).IsAssignableFrom(type) ||
            Normalize(t).IsArray && !type.IsArray && Normalize(t).GetElementType()!.IsAssignableFrom(type));
    }

    private static Type? GeometryType(string token) => token switch
    {
        "Point" => typeof(Point2d), "Line" => typeof(Line2d), "Circle" => typeof(Circle2d),
        "RotatedRectangle" => typeof(RotatedRect2d), "Polygon" => typeof(Polygon2d),
        "Annulus" => typeof(Annulus2d), "Arc" => typeof(Arc2d), _ => null
    };
}
