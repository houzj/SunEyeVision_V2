using BindingCompatibility = SunEyeVision.Plugin.Contracts.BindingCompatibility;
using BindingConversion = SunEyeVision.Plugin.Contracts.BindingConversion;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;


public sealed record BindingValidation(BindingCompatibility State, string Reason, Type? ValueType = null);

/// <summary>Formal-path checking; previous runtime contents never decide compatibility.</summary>
public static partial class BindingContractValidator
{
    public static BindingValidation Validate(IReadOnlyList<OutputVariable>? contract, string path, IReadOnlyList<Type> allowed,BindingConversion conversion=BindingConversion.Automatic)
    {
        BindingValidation Bad(string message) => new(BindingCompatibility.Incompatible, message);
        BindingValidation Pending(string message) => new(BindingCompatibility.Unresolved, message);
        if(!Enum.IsDefined(conversion))return Bad("Unknown conversion operation.");
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
        int? selectedIndex = null;
        int? selectedSourceCount = null;
        string? filter = null;
        for (var i = 0; i < parsed.Count; i++)
        {
            var segment = parsed[i];
            if (i > 0)
            {
                if (collection && (type.IsPrimitive || type == typeof(string))) return Pending("标量集合成员需明确元素索引，等待校验");
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
                if (type.IsArray)
                {
                    if (collection && segment.arrayIndex != null)
                        return Pending("嵌套集合的索引语义未声明完整，等待校验");
                    type = type.GetElementType()!; collection = true; count = null;
                }
            }
            if (segment.typeFilter != null)
            {
                if (type != typeof(RoiShape)) return Bad("该输出不支持图形类型筛选，请重新绑定");
                if (GeometryType(segment.typeFilter) == null) return Bad("图形类型筛选无效，请重新绑定");
                filter = segment.typeFilter; collection = true; count = null;
            }
            if (segment.arrayIndex is >= 0)
            {
                if (!collection) return Bad("元素索引只适用于声明为集合的来源");
                if (count.HasValue && segment.arrayIndex >= count.Value) return Bad("绑定元素索引超出声明的固定数量，请重新绑定");
                selectedIndex = segment.arrayIndex; selectedSourceCount = count;
                collection = false; count = 1;
            }
            else if (segment.arrayIndex == -1 && !collection) return Bad("All集合表达式要求声明为集合的来源");
        }
        var actual = collection ? type.MakeArrayType() : type;
        var canonical = CompileDeclaredAdaptation(type, collection, count, selectedIndex, selectedSourceCount, allowed,conversion);
        if (canonical != null) return canonical;
        // Geometry/transform adapters still use their existing declared CLR element contract,
        // but quantity compatibility comes from the same contract rule as the pure runtime.
        if (Accept(actual, allowed,conversion) == true)
        {
            var sourceCount = collection ? count.HasValue ? SunEyeVision.Plugin.Contracts.CountConstraint.Exactly(count.Value) : SunEyeVision.Plugin.Contracts.CountConstraint.Any
                : SunEyeVision.Plugin.Contracts.CountConstraint.Exactly(1);
            var states = allowed.Where(target => Accept(actual, new[] { target },conversion) == true)
                .Select(target => sourceCount.CompatibilityWith(target.IsArray ? SunEyeVision.Plugin.Contracts.CountConstraint.Any : SunEyeVision.Plugin.Contracts.CountConstraint.Exactly(1))).ToArray();
            if (states.Contains(BindingCompatibility.Compatible) && !(selectedIndex.HasValue && !selectedSourceCount.HasValue))
                return new(BindingCompatibility.Compatible, "适配", actual);
            if (states.Any(state => state is BindingCompatibility.Compatible or BindingCompatibility.Conditional))
                return new(BindingCompatibility.Conditional, "可绑定；本轮数量或索引须满足声明", actual);
            return Bad("来源声明数量不满足当前输入要求");
        }
        return Accept(actual, allowed,conversion) switch
        {
            true => new(BindingCompatibility.Compatible, "适配", actual),
            false => Bad("来源类型或集合形状不符合当前输入要求，请重新绑定"),
            null => Pending("输入类型要求未声明完整，等待校验")
        };
    }

    public static bool? Accept(Type? type, IReadOnlyList<Type> allowed,BindingConversion conversion=BindingConversion.Automatic)
    {
        if(!Enum.IsDefined(conversion))return false;
        if (type == null || type == typeof(object) || allowed.Count == 0) return null;
        var element=type.IsArray?type.GetElementType()!:type;
        var declared=CompileDeclaredAdaptation(element,type.IsArray,null,null,null,allowed,conversion);
        if(declared!=null)return declared.State==BindingCompatibility.Unresolved?null:declared.State is BindingCompatibility.Compatible or BindingCompatibility.Conditional;
        if(IsNumeric(element)||allowed.Any(t=>IsNumeric(t.IsArray?t.GetElementType()!:t)))return false;
        Type Normalize(Type t)
        {
            var array = t.IsArray; var item = array ? t.GetElementType()! : t;
            var canonical = SdkTypeVocabulary.ToSystemType(SdkTypeVocabulary.ToTypeVocabulary(item)) ?? item;
            if (canonical.IsArray) canonical = canonical.GetElementType()!;
            return array ? canonical.MakeArrayType() : canonical;
        }
        type = Normalize(type);
        return allowed.Any(t => t == typeof(object) || Normalize(t).IsAssignableFrom(type) ||
            type.IsArray && Normalize(t).IsArray &&
                Accept(type.GetElementType(), new[] { Normalize(t).GetElementType()! },conversion) == true ||
            type.IsArray && !Normalize(t).IsArray &&
                Accept(type.GetElementType(), new[] { t },conversion) == true ||
            !type.IsArray && !Normalize(t).IsArray && IsNumeric(type) && IsNumeric(Normalize(t)) ||
            Normalize(t).IsArray && !type.IsArray && Normalize(t).GetElementType()!.IsAssignableFrom(type));
    }

    private static bool IsNumeric(Type type) => Type.GetTypeCode(type) is
        TypeCode.Byte or TypeCode.SByte or TypeCode.Int16 or TypeCode.UInt16 or
        TypeCode.Int32 or TypeCode.UInt32 or TypeCode.Int64 or TypeCode.UInt64 or
        TypeCode.Single or TypeCode.Double or TypeCode.Decimal;

    private static Type? GeometryType(string token) => token switch
    {
        "Point" => typeof(Point2d), "Line" => typeof(Line2d), "Circle" => typeof(Circle2d),
        "RotatedRectangle" => typeof(RotatedRect2d), "Polygon" => typeof(Polygon2d),
        "Annulus" => typeof(Annulus2d), "Arc" => typeof(Arc2d), _ => null
    };
}
