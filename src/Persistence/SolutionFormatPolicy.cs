using System.Text.Json;
using System.Text.Json.Nodes;

namespace SunEyeVision.Persistence;

/// <summary>Solution document format, separate from plugin contract and parameter-codec versions.</summary>
public static class SolutionFormatPolicy
{
    public const int Current = 4;

    public static int ReadVersion(JsonNode root)
    {
        ArgumentNullException.ThrowIfNull(root);
        using var document = JsonDocument.Parse(root.ToJsonString());
        ValidateReadable(document.RootElement);
        return Current;
    }

    /// <summary>Reject obsolete identities before deserializing extension data or replacing a live document.</summary>
    public static void ValidateReadable(JsonElement root)
    {
        if (root.ValueKind != JsonValueKind.Object ||
            root.EnumerateObject().Count(property => string.Equals(property.Name, "SchemaVersion", StringComparison.OrdinalIgnoreCase)) != 1 ||
            !root.TryGetProperty("SchemaVersion", out var version) ||
            version.ValueKind != JsonValueKind.Number || !version.TryGetInt32(out var number) || number != Current)
            throw new JsonException("不支持该方案格式；需要 SchemaVersion 4。", "$.SchemaVersion", null, null);
        var triggerProperties=root.EnumerateObject().Where(property => string.Equals(property.Name,"CommunicationTriggers",StringComparison.OrdinalIgnoreCase)).ToArray();
        if(triggerProperties.Length>1)throw new JsonException("通讯触发规则字段不唯一。","$.CommunicationTriggers",null,null);
        if(triggerProperties.Length==1 && triggerProperties[0].Value.ValueKind!=JsonValueKind.Null)
        {
            if(triggerProperties[0].Value.ValueKind!=JsonValueKind.Array)throw new JsonException("通讯触发规则必须是数组。","$.CommunicationTriggers",null,null);
            var triggerIndex=0;
            foreach(var trigger in triggerProperties[0].Value.EnumerateArray())
            {
                if(trigger.ValueKind!=JsonValueKind.Object)throw new JsonException("通讯触发规则必须是对象。",$"$.CommunicationTriggers[{triggerIndex}]",null,null);
                foreach(var property in trigger.EnumerateObject())
                    if(string.Equals(property.Name,"FieldId",StringComparison.OrdinalIgnoreCase) || string.Equals(property.Name,"ParameterComparisonEnabled",StringComparison.OrdinalIgnoreCase))
                        throw new JsonException("旧通信触发字段不属于方案格式 4。",$"$.CommunicationTriggers[{triggerIndex}].{property.Name}",null,null);
                triggerIndex++;
            }
        }
        var variableProperties = root.EnumerateObject().Where(property =>
            string.Equals(property.Name, "GlobalVariables", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (variableProperties.Length > 1)
            throw new JsonException("全局变量字段不唯一。", "$.GlobalVariables", null, null);
        if (variableProperties.Length == 0 || variableProperties[0].Value.ValueKind == JsonValueKind.Null)
            return;
        var variables = variableProperties[0].Value;
        if (variables.ValueKind != JsonValueKind.Array)
            throw new JsonException("全局变量必须是数组。", "$.GlobalVariables", null, null);
        var index = 0;
        foreach (var variable in variables.EnumerateArray())
        {
            if (variable.ValueKind != JsonValueKind.Object)
                throw new JsonException("全局变量必须是对象。", $"$.GlobalVariables[{index}]", null, null);
            if (variable.EnumerateObject().Any(property => string.Equals(property.Name, "ConsumerBindings", StringComparison.OrdinalIgnoreCase)))
                throw new JsonException("旧消费者索引不属于方案格式 4。", $"$.GlobalVariables[{index}].ConsumerBindings", null, null);
            index++;
        }
    }
}
