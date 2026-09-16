using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Workflow;

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

    /// <summary>参数中文显示名称</summary>
    public string ParameterDisplayName { get; set; } = string.Empty;

    /// <summary>显示路径："工作流名.节点名.参数显示名"</summary>
    [JsonIgnore]
    public string DisplayPath => $"{WorkflowName}.{NodeName}.{ParameterDisplayName}";
}

/// <summary>
/// 全局变量的唯一生产者：某工作流节点的一个结果字段。
/// 一个变量只允许一个生产者，避免并发写入时出现“最后写入者获胜”的不确定结果。
/// </summary>
public class ProducerBinding
{
    /// <summary>生产者所属工作流 ID。</summary>
    public string WorkflowId { get; set; } = string.Empty;

    /// <summary>生产者所属工作流名称，仅用于显示与旧方案可读性。</summary>
    public string WorkflowName { get; set; } = string.Empty;

    /// <summary>生产者节点 ID。</summary>
    public string NodeId { get; set; } = string.Empty;

    /// <summary>生产者节点名称。</summary>
    public string NodeName { get; set; } = string.Empty;

    /// <summary>节点结果中的字段名。</summary>
    public string ResultName { get; set; } = string.Empty;

    /// <summary>结果字段显示名。</summary>
    public string ResultDisplayName { get; set; } = string.Empty;

    [JsonIgnore]
    public string DisplayPath => $"{WorkflowName}.{NodeName}.{ResultDisplayName}";
}

/// <summary>
/// 全局变量
/// </summary>
/// <remarks>
/// 用于存储解决方案级别的全局变量，可以在所有工作流和节点中访问。
/// 
/// 设计原则（rule-010: 方案系统实现规范）：
/// - 弱类型 + 类型字符串（视觉软件行业标准）
/// - 使用 JsonConverter 保留类型信息
/// - 支持分组管理（多工位/多相机场景）
/// 
/// 支持的数据类型：
/// - 基础类型：Integer, String, Boolean, Double, Byte
/// - OpenCvSharp类型：Point, Rectangle, Size
/// - 图像类型：Image
/// - 几何类型：Circle, Line, Annulus, RotatedRect, Polygon
/// - 区域/图形：Region（仅运行时）
/// 
/// JSON格式示例：
/// {
///   "Id": "var_001",
///   "Name": "阈值",
///   "Group": "参数设置",
///   "Index": 1,
///   "Type": "Integer",
///   "Value": { "Type": "Integer", "Value": 128 },
///   "Description": "二值化阈值"
/// }
/// </remarks>
public class GlobalVariable : ObservableObject
{
    private string _name = "";
    private string _group = "默认分组";
    private int _index = 0;
    private object? _value = null;
    private string _type = "String";
    private string _description = "";
    private bool _isInitialized = false;

    /// <summary>
    /// 变量ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>
    /// 变量名称
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, "变量名称");
    }

    /// <summary>
    /// 变量所属分组
    /// </summary>
    /// <remarks>
    /// 支持多工位/多相机场景的分组管理。
    /// 例如："工位1参数"、"工位2参数"、"相机1设置"、"相机2设置"
    /// </remarks>
    public string Group
    {
        get => _group;
        set => SetProperty(ref _group, value, "所属分组");
    }

    /// <summary>
    /// 变量在分组中的序号（用于UI显示）
    /// </summary>
    public int Index
    {
        get => _index;
        set => SetProperty(ref _index, value, "序号");
    }

    /// <summary>
    /// 变量值（使用转换器保留类型信息）
    /// </summary>
    /// <remarks>
    /// 使用 GlobalVariableValueConverter 保留类型信息，
    /// 序列化格式：{ "Type": "Integer", "Value": 128 }
    /// </remarks>
    [JsonConverter(typeof(GlobalVariableValueConverter))]
    public object? Value
    {
        get => _value;
        set => SetProperty(ref _value, value, "变量值");
    }

    /// <summary>
    /// 变量类型（词汇表由 SdkTypeVocabulary 定义）
    /// </summary>
    public string Type
    {
        get => _type;
        set => SetProperty(ref _type, value, "变量类型");
    }

    /// <summary>
    /// 变量描述/注释
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value, "变量描述");
    }

    /// <summary>
    /// 是否为复杂类型（图像/图形/矩阵等），此类「当前值」只读占位显示，不可编辑
    /// </summary>
    /// <remarks>
    /// 文本可编辑类型为 Integer/String/Boolean/Double，其余（Image/RoI/Transform/Circle/
    /// RotatedRect/Point/Polygon/Line/Annulus）为复杂类型，仅显示占位（如 [图像]）。
    /// </remarks>
    [JsonIgnore]
    public bool IsComplexValue
    {
        get
        {
            switch (Type)
            {
                case "Integer":
                case "String":
                case "Boolean":
                case "Double":
                    return false;
                default:
                    return true;
            }
        }
    }

    /// <summary>
    /// 消费者绑定列表：哪些参数绑定到此变量
    /// </summary>
    /// <remarks>
    /// 参数绑定至此变量时自动追加记录，解绑时移除。
    /// </remarks>
    private List<ConsumerBinding> _consumerBindings = new();
    public List<ConsumerBinding> ConsumerBindings
    {
        get => _consumerBindings;
        set => SetProperty(ref _consumerBindings, value, "消费者绑定");
    }

    /// <summary>
    /// 输入来源（可选）。为空表示变量使用方案保存的初值或宿主在运行请求中提供的值。
    /// 非空时表示该变量由一个节点结果产生，并在解决方案级依赖图中连接到所有消费者。
    /// </summary>
    public ProducerBinding? ProducerBinding { get; set; }

    /// <summary>
    /// 目标输出显示文本（用于UI）
    /// </summary>
    [JsonIgnore]
    public string OutputTargetDisplayText
    {
        get
        {
            if (ConsumerBindings.Count == 0)
                return "无";
            // 只显示第一个消费者的路径
            return ConsumerBindings[0].DisplayPath;
        }
    }

    /// <summary>
    /// 是否已初始化
    /// </summary>
    public bool IsInitialized
    {
        get => _isInitialized;
        set => SetProperty(ref _isInitialized, value, "是否已初始化");
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime ModifiedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 无参构造函数
    /// </summary>
    public GlobalVariable()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public GlobalVariable(string name, object? value, string type = "String", string group = "默认分组")
    {
        Name = name;
        Value = value;
        Type = type;
        Group = group;
    }

    /// <summary>
    /// 克隆变量
    /// </summary>
    public GlobalVariable Clone()
    {
        return new GlobalVariable
        {
            Id = Guid.NewGuid().ToString(),
            Name = Name,
            Group = Group,
            Index = Index,
            Value = Value,  // 注意：值类型会被复制，引用类型只复制引用
            Type = Type,
            Description = Description,
            ConsumerBindings = ConsumerBindings.Select(c => new ConsumerBinding
            {
                WorkflowId = c.WorkflowId,
                NodeId = c.NodeId,
                NodeName = c.NodeName,
                WorkflowName = c.WorkflowName,
                ParameterName = c.ParameterName,
                ParameterDisplayName = c.ParameterDisplayName
            }).ToList(),
            ProducerBinding = ProducerBinding == null ? null : new ProducerBinding
            {
                WorkflowId = ProducerBinding.WorkflowId,
                WorkflowName = ProducerBinding.WorkflowName,
                NodeId = ProducerBinding.NodeId,
                NodeName = ProducerBinding.NodeName,
                ResultName = ProducerBinding.ResultName,
                ResultDisplayName = ProducerBinding.ResultDisplayName
            },
            IsInitialized = IsInitialized,
            CreatedTime = CreatedTime,
            ModifiedTime = DateTime.Now
        };
    }

    /// <summary>
    /// 检查名称是否全局重复
    /// </summary>
    /// <param name="allVariables">所有全局变量</param>
    /// <param name="newName">新名称</param>
    /// <param name="excludeId">排除的变量ID（重命名时排除自身）</param>
    public static bool IsNameDuplicate(
        IEnumerable<GlobalVariable> allVariables,
        string newName,
        string? excludeId = null)
    {
        return allVariables.Any(v =>
            v.Name.Equals(newName, StringComparison.OrdinalIgnoreCase) &&
            v.Id != excludeId);
    }

    /// <summary>
    /// 验证变量
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("变量名称不能为空");
        }

        if (string.IsNullOrEmpty(Type))
        {
            errors.Add("变量类型不能为空");
        }

        // 验证类型是否有效
        if (!IsValidType(Type))
        {
            errors.Add($"不支持的变量类型: {Type}");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 检查类型是否有效
    /// </summary>
    private static bool IsValidType(string type)
    {
        // 通过 SdkTypeVocabulary 验证类型有效性（SSOT）。Region 已移出词汇表，不再需要特判。
        return SdkTypeVocabulary.ToSystemType(type) != null;
    }

    /// <summary>
    /// 通知输出显示文本已变化（供外部类调用）
    /// </summary>
    public void NotifyOutputDisplayChanged()
    {
        OnPropertyChanged(nameof(OutputTargetDisplayText));
        OnPropertyChanged(nameof(ConsumerBindings));
    }
}

/// <summary>
/// 全局变量管理器
/// </summary>
public class GlobalVariableManager
{
    private readonly Dictionary<string, GlobalVariable> _variables = new();

    /// <summary>
    /// 获取所有变量
    /// </summary>
    public List<GlobalVariable> GetAll()
    {
        return _variables.Values.ToList();
    }

    /// <summary>
    /// 获取变量
    /// </summary>
    public GlobalVariable? Get(string name)
    {
        return _variables.TryGetValue(name, out var variable) ? variable : null;
    }

    /// <summary>
    /// 添加变量
    /// </summary>
    public void Add(GlobalVariable variable)
    {
        if (string.IsNullOrWhiteSpace(variable.Name))
            throw new ArgumentException("变量名称不能为空");

        _variables[variable.Name] = variable;
    }

    /// <summary>
    /// 移除变量
    /// </summary>
    public bool Remove(string name)
    {
        return _variables.Remove(name);
    }

    /// <summary>
    /// 更新变量
    /// </summary>
    public bool Update(GlobalVariable variable)
    {
        if (string.IsNullOrWhiteSpace(variable.Name))
            return false;

        if (!_variables.ContainsKey(variable.Name))
            return false;

        _variables[variable.Name] = variable;
        return true;
    }

    /// <summary>
    /// 设置变量值
    /// </summary>
    public void SetValue(string name, object? value)
    {
        if (!_variables.TryGetValue(name, out var variable))
            return;

        variable.Value = value;
        variable.ModifiedTime = DateTime.Now;
    }

    /// <summary>
    /// 获取变量值
    /// </summary>
    public object? GetValue(string name)
    {
        return _variables.TryGetValue(name, out var variable) ? variable.Value : null;
    }

    /// <summary>
    /// 检查变量是否存在
    /// </summary>
    public bool Contains(string name)
    {
        return _variables.ContainsKey(name);
    }

    /// <summary>
    /// 清空所有变量
    /// </summary>
    public void Clear()
    {
        _variables.Clear();
    }
}
