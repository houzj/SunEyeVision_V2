using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// 参数值的**旧格式兼容**读取器：数组属性遇到**标量**时，自动包成长度 1 的数组。
/// </summary>
/// <remarks>
/// <para><b>为什么需要</b>（数组口子的兼容配套）：点参数由 <c>ParamValue&lt;Point2d&gt;</c> 改为
/// <c>ParamValue&lt;Point2d[]&gt;</c> 后，**老工程**里 <c>P1Point.Value</c> 仍是**标量对象</c>，
/// 默认数组转换器会直接抛 "The JSON value could not be converted to Point2d[]" ⇒ **老工程打不开**。</para>
/// <para><b>兼容原则</b>（决策 5：单值 = 长度 1 的数组）：</para>
/// <list type="bullet">
/// <item>读：数组 → 原样；标量 → 包成长度 1 数组（老工程零迁移）；null → null；</item>
/// <item>写：**始终写数组**（新格式统一，不再产出标量）。</item>
/// </list>
/// <para>工厂对本属性声明的**任意类型**都要能被创建（属性级 <c>[JsonConverter]</c> 会无条件使用本工厂），
/// 故非数组类型一律**委托给默认转换器**（<c>options.GetConverter</c>），保证 <c>ParamValue&lt;double&gt;</c>
/// 等标量参数行为完全不变。</para>
/// </remarks>
public sealed class LenientScalarOrArrayConverterFactory : JsonConverterFactory
{
    /// <inheritdoc />
    public override bool CanConvert(Type typeToConvert) => true;

    /// <inheritdoc />
    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
    {
        if (!typeToConvert.IsArray)
        {
            // 非数组：完全交回默认实现（行为零变化）
            return (JsonConverter)options.GetConverter(typeToConvert);
        }

        var elementType = typeToConvert.GetElementType()
            ?? throw new InvalidOperationException($"无法取得 '{typeToConvert}' 的元素类型。");

        var converterType = typeof(LenientScalarOrArrayConverter<>).MakeGenericType(elementType);
        return (JsonConverter)Activator.CreateInstance(converterType)!;
    }
}

/// <summary>
/// 数组读取兼容标量（写始终为数组）。仅由 <see cref="LenientScalarOrArrayConverterFactory"/> 创建。
/// </summary>
internal sealed class LenientScalarOrArrayConverter<TElement> : JsonConverter<TElement[]>
{
    /// <inheritdoc />
    public override TElement[]? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        if (reader.TokenType == JsonTokenType.Null)
            return null;

        if (reader.TokenType == JsonTokenType.StartArray)
            return JsonSerializer.Deserialize<TElement[]>(ref reader, options);

        // 旧工程：标量 → 长度 1 的数组（决策 5）
        var single = JsonSerializer.Deserialize<TElement>(ref reader, options);
        return single is null ? Array.Empty<TElement>() : new[] { single };
    }

    /// <inheritdoc />
    public override void Write(Utf8JsonWriter writer, TElement[] value, JsonSerializerOptions options)
        => JsonSerializer.Serialize(writer, value, options);
}
