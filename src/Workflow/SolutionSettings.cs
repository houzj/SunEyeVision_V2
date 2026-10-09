using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Workflow.Persistence;

namespace SunEyeVision.Workflow;

/// <summary>
/// 解决方案用户设置
/// </summary>
/// <remarks>
/// 职责：用户偏好设置和已知解决方案列表
///
/// 特性：
/// - 持久化到 JSON 文件
/// - 继承 ObservableObject（支持 UI 绑定）
/// - 线程安全集合（Dictionary + Lock）
/// - 已知解决方案列表（持久化存储）
///
/// 设计原则（rule-002）：
/// - 命名符合视觉软件行业标准
/// - 替代 RuntimeConfig（更符合语义）
///
/// 日志规范（rule-003）：
/// - 使用 VisionLogger 记录日志
/// - 使用适当的日志级别（Info/Success/Warning/Error）
///
/// 属性通知（rule-001）：
/// - 继承 ObservableObject
/// - 使用 SetProperty 方法
/// - 为用户可见属性提供 displayName
/// </remarks>
public class SolutionSettings : ObservableObject
{
    private string _currentSolutionId = "";
    private string _defaultSolutionId = "";
    private string _basePath = "";  // 基准路径（用于相对路径转换）

    // ✅ 使用公共属性进行序列化，避免私有字段序列化问题
    // 内部使用锁保证线程安全，序列化时直接访问字典
    private Dictionary<string, SolutionMetadata> _knownSolutions = new();

    private readonly object _knownSolutionsLock = new();
    private readonly ILogger _logger;

    /// <summary>
    /// 当前解决方案ID
    /// </summary>
    public string CurrentSolutionId
    {
        get => _currentSolutionId;
        set => SetProperty(ref _currentSolutionId, value);
    }

    /// <summary>
    /// 默认解决方案ID（启动时自动打开）
    /// </summary>
    public string DefaultSolutionId
    {
        get => _defaultSolutionId;
        set => SetProperty(ref _defaultSolutionId, value);
    }

    /// <summary>
    /// 已知解决方案列表（持久化存储）
    /// </summary>
    /// <remarks>
    /// 序列化说明：
    /// - 使用 [JsonPropertyName] 指定 JSON 属性名
    /// - Getter 返回线程安全的克隆副本
    /// - Setter 仅用于反序列化，直接替换内部字典
    /// </remarks>
    [JsonPropertyName("knownSolutions")]
    public Dictionary<string, SolutionMetadata> KnownSolutions
    {
        get
        {
            lock (_knownSolutionsLock)
            {
                return _knownSolutions.ToDictionary(kvp => kvp.Key, kvp => kvp.Value.Clone());
            }
        }
        set
        {
            // 仅用于反序列化
            if (value != null)
            {
                lock (_knownSolutionsLock)
                {
                    _knownSolutions = value;
                }
            }
        }
    }

    /// <summary>
    /// 用户偏好设置
    /// </summary>
    public Dictionary<string, object> UserPreferences { get; set; }

    /// <summary>
    /// 启动时跳过配置界面
    /// </summary>
    public bool SkipStartupConfig
    {
        get => GetPreference<bool>("SkipStartupConfig", false);
        set => SetPreference("SkipStartupConfig", value);
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public string StartupMode
    {
        get => GetPreference<string>("StartupMode", SkipStartupConfig ? "Default" : "Choose");
        set { if (value is not ("Choose" or "Last" or "Default")) throw new ArgumentException("启动方式无效。"); SetPreference("StartupMode", value); }
    }

    public SolutionSettings()
    {
        _currentSolutionId = "";
        UserPreferences = new Dictionary<string, object>();
        _logger = VisionLogger.Instance;

        _logger.Log(LogLevel.Info, "解决方案用户设置初始化完成", "SolutionSettings");
    }

    /// <summary>
    /// 设置基准路径（用于计算相对路径）
    /// </summary>
    /// <param name="basePath">基准路径</param>
    public void SetBasePath(string basePath)
    {
        if (string.IsNullOrEmpty(basePath))
        {
            _logger.Log(LogLevel.Warning, "设置基准路径失败：路径为空", "SolutionSettings");
            return;
        }

        _basePath = Path.GetFullPath(basePath);
        _logger.Log(LogLevel.Info, $"设置基准路径: {_basePath}", "SolutionSettings");
    }

    /// <summary>
    /// 将绝对路径转换为相对路径
    /// </summary>
    /// <param name="absolutePath">绝对路径</param>
    /// <returns>相对路径</returns>
    private string ToRelativePath(string absolutePath)
    {
        if (string.IsNullOrEmpty(absolutePath))
            return "";

        if (string.IsNullOrEmpty(_basePath))
            return absolutePath;  // 未设置基准路径，返回原路径

        try
        {
            var result = Path.GetRelativePath(_basePath, absolutePath);
            return result;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Warning, $"转换为相对路径失败: {absolutePath}, 错误: {ex.Message}", "SolutionSettings");
            return absolutePath;
        }
    }

    /// <summary>
    /// 将相对路径转换为绝对路径
    /// </summary>
    /// <param name="relativePath">相对路径</param>
    /// <returns>绝对路径</returns>
    private string ToAbsolutePath(string relativePath)
    {
        if (string.IsNullOrEmpty(relativePath))
            return "";

        if (string.IsNullOrEmpty(_basePath))
            return relativePath;  // 未设置基准路径，返回原路径

        try
        {
            var result = Path.Combine(_basePath, relativePath);
            return Path.GetFullPath(result);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Warning, $"转换为绝对路径失败: {relativePath}, 错误: {ex.Message}", "SolutionSettings");
            return relativePath;
        }
    }

    /// <summary>
    /// 元数据序列化辅助类（存储相对路径）
    /// </summary>
    private class SerializableSolutionMetadata
    {
        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Description { get; set; } = "";
        public string Version { get; set; } = "";
        public string RelativePath { get; set; } = "";  // 相对路径
        public string RelativeDirectoryPath { get; set; } = "";  // 相对目录路径
        public DateTime CreatedTime { get; set; }
        public DateTime ModifiedTime { get; set; }
        public DateTime LastAccessTime { get; set; }
        public int WorkflowCount { get; set; }
        public int GlobalVariableCount { get; set; }
    }

    /// <summary>
    /// 将元数据转换为可序列化对象（相对路径）
    /// </summary>
    private SerializableSolutionMetadata ToSerializable(SolutionMetadata metadata)
    {
        return new SerializableSolutionMetadata
        {
            Id = metadata.Id,
            Name = metadata.Name,
            Description = metadata.Description,
            Version = metadata.Version,
            RelativePath = ToRelativePath(metadata.FilePath),
            RelativeDirectoryPath = ToRelativePath(metadata.DirectoryPath),
            CreatedTime = metadata.CreatedTime,
            ModifiedTime = metadata.ModifiedTime,
            LastAccessTime = metadata.LastAccessTime,
            WorkflowCount = metadata.WorkflowCount,
            GlobalVariableCount = metadata.GlobalVariableCount
        };
    }

    /// <summary>
    /// 从可序列化对象还原元数据（绝对路径）
    /// </summary>
    private SolutionMetadata FromSerializable(SerializableSolutionMetadata serializable)
    {
        return new SolutionMetadata
        {
            Id = serializable.Id,
            Name = serializable.Name,
            Description = serializable.Description,
            Version = serializable.Version,
            FilePath = ToAbsolutePath(serializable.RelativePath),
            DirectoryPath = ToAbsolutePath(serializable.RelativeDirectoryPath),
            CreatedTime = serializable.CreatedTime,
            ModifiedTime = serializable.ModifiedTime,
            LastAccessTime = serializable.LastAccessTime,
            WorkflowCount = serializable.WorkflowCount,
            GlobalVariableCount = serializable.GlobalVariableCount
        };
    }

    /// <summary>
    /// 添加到已知解决方案列表
    /// </summary>
    /// <param name="metadata">元数据对象</param>
    public void AddKnownSolution(SolutionMetadata metadata)
    {
        // ✅ 日志监控：生成唯一调用ID
        long callId = System.Threading.Interlocked.Increment(ref _addKnownSolutionCallCount);

        if (metadata == null)
        {
            _logger.Log(LogLevel.Warning, $"[AddKnownSolution #{callId}] 添加到已知解决方案失败：元数据为空\n堆栈: {GetShortStackTrace(3)}", "SolutionSettings");
            return;
        }

        // 防御性检查：Id不能为空
        if (string.IsNullOrEmpty(metadata.Id))
        {
            _logger.Log(LogLevel.Warning, $"[AddKnownSolution #{callId}] 添加到已知解决方案失败：元数据Id为空（Name={metadata.Name}）\n堆栈: {GetShortStackTrace(3)}", "SolutionSettings");
            return;
        }

        lock (_knownSolutionsLock)
        {
            bool alreadyExists = _knownSolutions.ContainsKey(metadata.Id);
            int beforeCount = _knownSolutions.Count;

            // 最近打开只由实际打开成功的操作更新，注册/编辑不改变访问时间。

            // 添加或更新到字典
            _knownSolutions[metadata.Id] = metadata.Clone();

            int afterCount = _knownSolutions.Count;

            _logger.Log(LogLevel.Info, $"[AddKnownSolution #{callId}] 添加到已知解决方案: {metadata.Name} (Id={metadata.Id}, FilePath={metadata.FilePath})\n已存在={alreadyExists}, 添加前数量={beforeCount}, 添加后数量={afterCount}\n堆栈: {GetShortStackTrace(4)}", "SolutionSettings");
        }
    }

    /// <summary>
    /// 添加到已知解决方案操作调用计数器（用于日志监控）
    /// </summary>
    private static long _addKnownSolutionCallCount = 0;

    /// <summary>
    /// 获取简短的调用堆栈（只显示前几层）
    /// </summary>
    private string GetShortStackTrace(int skipFrames)
    {
        try
        {
            var stackTrace = new System.Diagnostics.StackTrace(skipFrames);
            var frames = stackTrace.GetFrames();
            if (frames == null || frames.Length == 0)
                return "";

            var result = new System.Text.StringBuilder();
            int maxFrames = Math.Min(5, frames.Length); // 最多显示5层
            for (int i = 0; i < maxFrames; i++)
            {
                var method = frames[i].GetMethod();
                if (method != null)
                {
                    result.AppendLine($"  at {method.DeclaringType?.Name}.{method.Name}");
                }
            }
            return result.ToString();
        }
        catch
        {
            return "[堆栈获取失败]";
        }
    }

    /// <summary>
    /// 从已知解决方案列表中移除
    /// </summary>
    /// <param name="solutionId">解决方案ID</param>
    public void RemoveKnownSolution(string solutionId)
    {
        if (string.IsNullOrEmpty(solutionId))
        {
            _logger.Log(LogLevel.Warning, "从已知解决方案移除失败：解决方案ID为空", "SolutionSettings");
            return;
        }

        lock (_knownSolutionsLock)
        {
            if (_knownSolutions.TryGetValue(solutionId, out var existing))
            {
                _knownSolutions.Remove(solutionId);
                _logger.Log(LogLevel.Info, $"从已知解决方案移除: {existing.Name} (Id={solutionId})", "SolutionSettings");
            }
        }
    }

    /// <summary>
    /// 获取已知解决方案列表（线程安全）
    /// </summary>
    /// <returns>元数据列表</returns>
    public List<SolutionMetadata> GetKnownSolutions()
    {
        lock (_knownSolutionsLock)
        {
            return _knownSolutions.Values.Select(m => m.Clone()).ToList();
        }
    }

    /// <summary>
    /// 检查是否包含指定解决方案
    /// </summary>
    /// <param name="solutionId">解决方案ID</param>
    /// <returns>是否包含</returns>
    public bool ContainsKnownSolution(string solutionId)
    {
        if (string.IsNullOrEmpty(solutionId))
            return false;

        lock (_knownSolutionsLock)
        {
            return _knownSolutions.ContainsKey(solutionId);
        }
    }

    /// <summary>
    /// 获取用户偏好设置
    /// </summary>
    /// <typeparam name="T">值类型</typeparam>
    /// <param name="key">键</param>
    /// <param name="defaultValue">默认值</param>
    /// <returns>偏好设置值</returns>
    public T? GetPreference<T>(string key, T? defaultValue = default)
    {
        if (string.IsNullOrEmpty(key))
        {
            _logger.Log(LogLevel.Warning, "获取偏好设置失败：键为空", "SolutionSettings");
            return defaultValue;
        }

        if (UserPreferences.TryGetValue(key, out var value))
        {
            // 类型匹配，直接返回
            if (value is T typedValue)
                return typedValue;

            // 尝试类型转换
            try
            {
                if (value is JsonElement json)
                {
                    if (json.ValueKind == JsonValueKind.String && typeof(T) != typeof(string))
                        return (T?)Convert.ChangeType(json.GetString(), typeof(T));
                    return json.Deserialize<T>();
                }
                return (T?)Convert.ChangeType(value, typeof(T));
            }
            catch (Exception ex)
            {
                _logger.Log(LogLevel.Warning, $"类型转换失败: key={key}, targetType={typeof(T)}, valueType={value?.GetType()}, error={ex.Message}", "SolutionSettings");
                return defaultValue;
            }
        }

        return defaultValue;
    }

    /// <summary>
    /// 设置用户偏好设置
    /// </summary>
    /// <param name="key">键</param>
    /// <param name="value">值</param>
    public void SetPreference(string key, object? value)
    {
        if (string.IsNullOrEmpty(key))
        {
            _logger.Log(LogLevel.Warning, "设置偏好设置失败：键为空", "SolutionSettings");
            return;
        }

        UserPreferences[key] = value;
        _logger.Log(LogLevel.Info, $"设置偏好: {key} = {value}", "SolutionSettings");
        
        // ✅ 新增：映射并触发属性通知
        // 将偏好键映射到对应的属性名
        string propertyName = key switch
        {
            "SkipStartupConfig" => nameof(SkipStartupConfig),
            _ => key  // 其他键直接使用原名称（如果有对应的属性）
        };
        
        // 触发属性变更通知
        OnPropertyChanged(propertyName);
    }

    /// <summary>
    /// 移除用户偏好设置
    /// </summary>
    /// <param name="key">键</param>
    /// <returns>是否成功移除</returns>
    public bool RemovePreference(string key)
    {
        if (string.IsNullOrEmpty(key))
            return false;

        bool removed = UserPreferences.Remove(key);
        if (removed)
        {
            _logger.Log(LogLevel.Info, $"移除偏好: {key}", "SolutionSettings");
        }
        return removed;
    }

    /// <summary>
    /// 从文件加载设置
    /// </summary>
    /// <param name="filePath">配置文件路径</param>
    /// <returns>是否成功</returns>
    public bool Load(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.Log(LogLevel.Warning, "加载设置失败：文件路径为空", "SolutionSettings");
            return false;
        }

        if (!File.Exists(filePath))
        {
            _logger.Log(LogLevel.Info, $"配置文件不存在，使用默认设置: {filePath}", "SolutionSettings");
            return false;
        }

        try
        {
            var json = File.ReadAllText(filePath);

            // 临时反序列化对象（使用手动解析支持相对路径）
            using var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;
            if (root.ValueKind != JsonValueKind.Object || (root.TryGetProperty("SchemaVersion", out var schema) && schema.GetInt32() != 1))
                throw new InvalidDataException("不支持或损坏的用户配置，禁止覆盖。");

            // 加载基本属性
            if (root.TryGetProperty("CurrentSolutionId", out var currentSolutionIdElement))
            {
                _currentSolutionId = currentSolutionIdElement.GetString() ?? "";
            }

            // 加载默认解决方案ID
            if (root.TryGetProperty("DefaultSolutionId", out var defaultSolutionIdElement))
            {
                _defaultSolutionId = defaultSolutionIdElement.GetString() ?? "";
            }

            // 加载用户偏好
            if (root.TryGetProperty("UserPreferences", out var userPreferencesElement) && userPreferencesElement.ValueKind == JsonValueKind.Object)
            {
                UserPreferences = new Dictionary<string, object>();
                foreach (var prop in userPreferencesElement.EnumerateObject())
                {
                    UserPreferences[prop.Name] = prop.Value.Clone();
                }
            }

            if (root.TryGetProperty("StartupMode", out var startup)) StartupMode = startup.GetString() ?? "Choose";

            // 加载已知解决方案列表（使用相对路径）
            if (!root.TryGetProperty("KnownSolutions", out var knownRecords) || knownRecords.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("用户配置缺少有效方案目录，不能用空记录覆盖。");
            if (root.TryGetProperty("KnownSolutions", out var knownSolutionsElement) && knownSolutionsElement.ValueKind == JsonValueKind.Object)
            {
                lock (_knownSolutionsLock)
                {
                    _knownSolutions.Clear();
                    foreach (var prop in knownSolutionsElement.EnumerateObject())
                    {
                        var serializableMetadata = JsonSerializer.Deserialize<SerializableSolutionMetadata>(prop.Value.GetRawText());
                        var metadata = prop.Value.TryGetProperty("RelativePath", out _) && serializableMetadata != null
                            ? FromSerializable(serializableMetadata)
                            : JsonSerializer.Deserialize<SolutionMetadata>(prop.Value.GetRawText());
                        if (metadata == null || string.IsNullOrWhiteSpace(metadata.Id) || string.IsNullOrWhiteSpace(metadata.FilePath) ||
                            _knownSolutions.ContainsKey(metadata.Id))
                            throw new InvalidDataException("方案管理记录损坏或身份重复，不能丢弃记录后覆盖原配置。");
                        _knownSolutions.Add(metadata.Id, metadata);
                    }
                }

                _logger.Log(LogLevel.Success, $"加载已知解决方案列表成功: {_knownSolutions.Count} 条记录", "SolutionSettings");
            }

            _logger.Log(LogLevel.Success, $"加载设置成功: {filePath}", "SolutionSettings");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"加载设置失败: {filePath}, 错误: {ex.Message}", "SolutionSettings", ex);
            return false;
        }
    }

/// <summary>
/// 保存设置到文件
/// </summary>
/// <param name="filePath">配置文件路径</param>
/// <returns>是否成功</returns>
public bool Save(string filePath)
{
    if (string.IsNullOrEmpty(filePath))
    {
        _logger.Log(LogLevel.Warning, "保存设置失败：文件路径为空", "SolutionSettings");
        return false;
    }

    try
    {
        // 确保目录存在
        var directory = Path.GetDirectoryName(filePath);
        if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
        {
            Directory.CreateDirectory(directory);
        }

        // 构建可序列化对象（使用相对路径）
        var serializableKnownSolutions = new Dictionary<string, SerializableSolutionMetadata>();
        lock (_knownSolutionsLock)
        {
            foreach (var kvp in _knownSolutions)
            {
                serializableKnownSolutions[kvp.Key] = ToSerializable(kvp.Value);
            }
        }

        var dataToSerialize = new
        {
            SchemaVersion = 1,
            StartupMode = StartupMode,
            CurrentSolutionId = _currentSolutionId,
            DefaultSolutionId = _defaultSolutionId,
            UserPreferences = UserPreferences,
            KnownSolutions = serializableKnownSolutions
        };

        // 使用独立的序列化选项，避免触发 WorkflowSerializationOptions.Default 的初始化
        // 这是因为 SolutionSettings.Save() 不涉及 ToolParameters 类型
        var json = JsonSerializer.Serialize(dataToSerialize, SettingsSerializationOptions);
        AtomicJsonFile.Write(filePath, json);

        var fileInfo = new FileInfo(filePath);
        _logger.Log(LogLevel.Success, $"保存设置成功: {filePath}, 文件大小: {fileInfo.Length} 字节", "SolutionSettings");
        return true;
    }
    catch (Exception ex)
    {
        _logger.Log(LogLevel.Error, $"保存设置失败: {filePath}, 错误: {ex.Message}", "SolutionSettings", ex);
        return false;
    }
}

/// <summary>
/// 设置文件的独立序列化选项
/// </summary>
/// <remarks>
/// 为什么使用独立选项：
/// - SolutionSettings.Save() 不涉及 ToolParameters 类型
/// - 避免 WorkflowSerializationOptions.Default 的初始化触发 ParameterTypeRegistry
/// - 确保在任何时候都可以安全保存设置文件
/// </remarks>
private static readonly JsonSerializerOptions SettingsSerializationOptions = new()
{
    WriteIndented = true,
    PropertyNamingPolicy = null,  // 保持 PascalCase
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
    PropertyNameCaseInsensitive = true
};

    /// <summary>
    /// 获取设置统计信息
    /// </summary>
    /// <returns>统计信息字符串</returns>
    public string GetStatistics()
    {
        lock (_knownSolutionsLock)
        {
            return $"设置统计: 当前解决方案={_currentSolutionId}, 已知解决方案={_knownSolutions.Count}, 偏好设置={UserPreferences.Count}";
        }
    }
}
