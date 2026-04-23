using System;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Workflow;

/// <summary>
/// 解决方案元数据
/// </summary>
/// <remarks>
/// 统一的解决方案元数据模型，替代原有的 SolutionMetadata 和 SolutionInfo。
/// 用于懒加载场景，避免加载完整的 Solution 对象。
///
/// 特性：
/// - 继承 ObservableObject（属性变化通知）
/// - 支持 JSON 序列化（用于 SolutionSettings）
/// - 包含所有必要的元数据字段
///
/// 设计原则（rule-002）：
/// - 命名符合视觉软件行业标准
/// - 属性使用 PascalCase
/// - 布尔值使用 Is/Has 前缀
///
/// 日志规范（rule-003）：
/// - 属性变化通过 displayName 参数自动记录日志
/// - 内部属性不记录日志
/// </remarks>
public class SolutionMetadata : ObservableObject
{
    private string _id = Guid.NewGuid().ToString();
    private string _name = "新建解决方案";
    private string _description = "";
    private string _version = "1.0";
    private string _filePath = "";
    private string _directoryPath = "";
    private DateTime _createdTime = DateTime.Now;
    private DateTime _modifiedTime = DateTime.Now;
    private DateTime _lastAccessTime = DateTime.Now;
    private int _workflowCount;
    private int _globalVariableCount;
    private bool _isDefault;

    /// <summary>
    /// 解决方案ID
    /// </summary>
    public string Id
    {
        get => _id;
        set => SetProperty(ref _id, value);
    }

    /// <summary>
    /// 解决方案名称
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }

    /// <summary>
    /// 解决方案描述
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value);
    }

    /// <summary>
    /// 解决方案版本
    /// </summary>
    public string Version
    {
        get => _version;
        set => SetProperty(ref _version, value);
    }

    /// <summary>
    /// 解决方案文件完整路径
    /// </summary>
    public string FilePath
    {
        get => _filePath;
        set => SetProperty(ref _filePath, value);
    }

    /// <summary>
    /// 目录路径
    /// </summary>
    public string DirectoryPath
    {
        get => _directoryPath;
        set => SetProperty(ref _directoryPath, value);
    }

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime
    {
        get => _createdTime;
        set => SetProperty(ref _createdTime, value);
    }

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime ModifiedTime
    {
        get => _modifiedTime;
        set => SetProperty(ref _modifiedTime, value);
    }

    /// <summary>
    /// 最后访问时间
    /// </summary>
    public DateTime LastAccessTime
    {
        get => _lastAccessTime;
        set => SetProperty(ref _lastAccessTime, value);
    }

    /// <summary>
    /// 工作流数量
    /// </summary>
    public int WorkflowCount
    {
        get => _workflowCount;
        set => SetProperty(ref _workflowCount, value);
    }

    /// <summary>
    /// 全局变量数量
    /// </summary>
    public int GlobalVariableCount
    {
        get => _globalVariableCount;
        set => SetProperty(ref _globalVariableCount, value);
    }

    /// <summary>
    /// 是否为默认解决方案
    /// </summary>
    public bool IsDefault
    {
        get => _isDefault;
        set => SetProperty(ref _isDefault, value);
    }

    /// <summary>
    /// 判断是否已保存到文件
    /// </summary>
    [JsonIgnore]
    public bool IsSaved => !string.IsNullOrEmpty(_filePath) && System.IO.File.Exists(_filePath);

    /// <summary>
    /// 判断是否有描述信息
    /// </summary>
    [JsonIgnore]
    public bool HasDescription => !string.IsNullOrEmpty(_description);

    /// <summary>
    /// 更新最后访问时间
    /// </summary>
    public void UpdateLastAccessTime()
    {
        LastAccessTime = DateTime.Now;
    }

    /// <summary>
    /// 更新修改时间
    /// </summary>
    public void UpdateModifiedTime()
    {
        ModifiedTime = DateTime.Now;
    }

    /// <summary>
    /// 更新统计数据（在加载 Solution 后调用）
    /// </summary>
    /// <param name="solution">解决方案对象</param>
    public void UpdateStatistics(Solution solution)
    {
        if (solution == null)
            throw new ArgumentNullException(nameof(solution));

        WorkflowCount = solution.Workflows?.Count ?? 0;
        GlobalVariableCount = solution.GlobalVariables?.Count ?? 0;
    }

    /// <summary>
    /// 创建轻量级元数据实例
    /// </summary>
    /// <param name="filePath">解决方案文件路径</param>
    /// <returns>元数据实例</returns>
    public static SolutionMetadata Create(string filePath)
    {
        var metadata = new SolutionMetadata
        {
            FilePath = filePath,
            DirectoryPath = System.IO.Path.GetDirectoryName(filePath) ?? "",
            CreatedTime = System.IO.File.GetCreationTime(filePath),
            ModifiedTime = System.IO.File.GetLastWriteTime(filePath),
            LastAccessTime = DateTime.Now
        };
        return metadata;
    }

    /// <summary>
    /// 克隆元数据
    /// </summary>
    /// <returns>元数据副本</returns>
    public SolutionMetadata Clone()
    {
        return new SolutionMetadata
        {
            Id = _id,
            Name = _name,
            Description = _description,
            Version = _version,
            FilePath = _filePath,
            DirectoryPath = _directoryPath,
            CreatedTime = _createdTime,
            ModifiedTime = _modifiedTime,
            LastAccessTime = _lastAccessTime,
            WorkflowCount = _workflowCount,
            GlobalVariableCount = _globalVariableCount,
            IsDefault = _isDefault
        };
    }

    /// <summary>
    /// 获取显示名称（用于UI）
    /// </summary>
    /// <returns>显示名称</returns>
    public string GetDisplayName()
    {
        if (string.IsNullOrEmpty(_description))
            return _name;
        return $"{_name} - {_description}";
    }

}
