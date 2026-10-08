using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace SunEyeVision.Workflow;

public sealed record CommunicationSendValue(Guid FieldId, string DataType, object Value)
{
    public string Display => Convert.ToString(Value, CultureInfo.InvariantCulture) ?? string.Empty;
}

public sealed record CommunicationSendMessage(
    IReadOnlyList<CommunicationSendValue> Inputs, IReadOnlyList<CommunicationSendValue> Outputs,
    string Text, byte[] Utf8Bytes);

/// <summary>Formats an execution snapshot without accessing UI or sending to a device.</summary>
public static class CommunicationSendEventFormatter
{
    public static CommunicationSendMessage Build(CommunicationSendEventDefinition definition,
        IReadOnlyDictionary<Guid, object?>? parameterSnapshot = null)
    {
        ArgumentNullException.ThrowIfNull(definition);
        if (definition.Mode is not ("Direct" or "Assembly"))
            throw new FormatException("不支持的发送事件处理方式。");
        if (definition.Mode == "Direct" && definition.Parameters.Count == 0) throw new FormatException("请至少配置一个发送参数。");
        var inputs = new List<CommunicationSendValue>();
        var byId = new Dictionary<Guid, CommunicationSendValue>();
        var names = new HashSet<string>(StringComparer.Ordinal);
        foreach (var field in definition.Parameters)
        {
            if (field.FieldId == Guid.Empty || byId.ContainsKey(field.FieldId))
                throw new FormatException("发送参数的字段 ID 无效或重复。");
            if (string.IsNullOrWhiteSpace(field.Name) || !names.Add(field.Name))
                throw new FormatException("发送参数名称不能为空或重复。");
            object value;
            if (parameterSnapshot == null) value = ParseLiteral(field.Content, field.DataType);
            else
            {
                if (!parameterSnapshot.TryGetValue(field.FieldId, out var supplied) || supplied == null)
                    throw new FormatException($"发送参数 {field.Name} 缺少输入值。");
                if (!MatchesType(supplied, field.DataType))
                    throw new FormatException($"发送参数 {field.Name} 类型不匹配：需要 {field.DataType}，实际 {supplied.GetType().Name}。");
                value = supplied;
            }
            var input = new CommunicationSendValue(field.FieldId, field.DataType, value);
            inputs.Add(input);
            byId.Add(field.FieldId, input);
        }
        var outputs = new List<CommunicationSendValue>();
        if (definition.Mode == "Direct") outputs.AddRange(inputs);
        else
        {
            if (definition.AssemblyOutputs.Count == 0) throw new FormatException("请至少配置一条组装输出规则。");
            var outputIds = new HashSet<Guid>();
            foreach (var field in definition.AssemblyOutputs)
            {
                if (field.FieldId == Guid.Empty || !outputIds.Add(field.FieldId))
                    throw new FormatException("组装输出的字段 ID 无效或重复。");
                object value;
                if (field.IsContentBound)
                {
                    if (!field.SourceFieldId.HasValue || !byId.TryGetValue(field.SourceFieldId.Value, out var source))
                        throw new FormatException($"组装输出 {field.Name} 的绑定参数不存在。");
                    if (!MatchesType(source.Value, field.DataType))
                        throw new FormatException($"组装输出 {field.Name} 与绑定参数的类型不匹配。");
                    value = source.Value;
                }
                else value = ParseLiteral(field.Content, field.DataType);
                outputs.Add(new CommunicationSendValue(field.FieldId, field.DataType, value));
            }
        }
        var text = string.Join(definition.Separator ?? string.Empty, outputs.Select(item => item.Display)) +
                   (definition.Terminator ?? string.Empty);
        var bytes = new UTF8Encoding(false, true).GetBytes(text);
        return new CommunicationSendMessage(inputs.AsReadOnly(), outputs.AsReadOnly(), text, bytes);
    }

    public static bool MatchesType(object value, string type) => type.ToLowerInvariant() switch
    {
        "string" => value is string,
        "int" or "int32" => value is int,
        "int16" => value is short,
        "uint16" => value is ushort,
        "uint32" => value is uint,
        "float" or "float32" => value is float single && float.IsFinite(single),
        "double" => value is double number && double.IsFinite(number),
        "boolean" or "bool" => value is bool,
        _ => false
    };

    public static object ParseLiteral(string content, string type)
    {
        var culture = CultureInfo.InvariantCulture;
        return type.ToLowerInvariant() switch
        {
            "string" => content,
            "int" or "int32" when int.TryParse(content, NumberStyles.Integer, culture, out var integer) => integer,
            "int16" when short.TryParse(content, NumberStyles.Integer, culture, out var shortValue) => shortValue,
            "uint16" when ushort.TryParse(content, NumberStyles.Integer, culture, out var word) => word,
            "uint32" when uint.TryParse(content, NumberStyles.Integer, culture, out var unsigned) => unsigned,
            "float" or "float32" when float.TryParse(content, NumberStyles.Float, culture, out var single) && float.IsFinite(single) => single,
            "double" when double.TryParse(content, NumberStyles.Float, culture, out var number) && double.IsFinite(number) => number,
            "boolean" or "bool" when bool.TryParse(content, out var boolean) => boolean,
            _ => throw new FormatException($"内容“{content}”不是有效的 {type} 配置值。")
        };
    }
}
