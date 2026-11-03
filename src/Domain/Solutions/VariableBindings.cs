using System.Text.Json.Serialization;

namespace SunEyeVision.Domain.Solutions;

/// <summary>
/// 全局变量的唯一生产者：某工作流节点的一个结果字段。
/// 一个变量只允许一个生产者，避免并发写入时出现“最后写入者获胜”的不确定结果。
/// </summary>
[JsonUnmappedMemberHandling(JsonUnmappedMemberHandling.Disallow)]
public class ProducerBinding
{
    public string ReferenceExpression { get; set; } = string.Empty;
    [JsonIgnore]
    public SunEyeVision.Plugin.Contracts.OutputReference SourceReference =>
        SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Deserialize(ReferenceExpression);
    public string WorkflowName { get; set; } = string.Empty;
    public string NodeName { get; set; } = string.Empty;
    public string ResultDisplayName { get; set; } = string.Empty;
    [JsonIgnore]
    public string DisplayPath => $"{WorkflowName}.{NodeName}.{ResultDisplayName}";
}


