using System.Text.Json.Serialization;

namespace SunEyeVision.Domain.Solutions;

/// <summary>
/// 消费者绑定：标记哪个工具的哪个参数绑定到此全局变量
/// </summary>
public class ConsumerBinding
{
    /// <summary>消费者所属工作流 ID。旧方案没有该字段，加载旧方案时由编译器按节点 ID 兼容解析。</summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>消费者节点ID</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>消费者节点名称</summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>消费者所属工作流名称</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>绑定的参数名称（属性名）</summary>
    public string ParameterName { get; set; } = string.Empty;
    public string? InputKey { get; set; }

    /// <summary>参数中文显示名称</summary>
    public string ParameterDisplayName { get; set; } = string.Empty;

    /// <summary>消费者稳定键，包含工作流作用域，避免跨工作流节点 ID 冲突。</summary>
    [JsonIgnore]
    public string BindingKey => $"{WorkflowId}|{NodeId}|{InputKey ?? ParameterName}";

    /// <summary>显示路径："工作流名.节点名.参数显示名"</summary>
    [JsonIgnore]
    public string DisplayPath => $"{WorkflowName}.{NodeName}.{ParameterDisplayName}";
}


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


