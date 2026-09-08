using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

namespace SunEyeVision.Workflow;

/// <summary>
/// 解决方案文件仓库
/// </summary>
/// <remarks>
/// 职责：解决方案文件的加载、保存、删除操作
///
/// 特性：
/// - 文件系统操作
/// - JSON 序列化/反序列化
/// - 批量操作支持
/// - 错误处理和日志记录
/// - 确保插件在反序列化前加载（Fail Fast 原则）
///
/// 设计原则（rule-002）：
/// - 命名符合视觉软件行业标准
/// - 方法使用 PascalCase，动词开头
///
/// 日志规范（rule-003）：
/// - 使用 VisionLogger 记录日志
/// - 使用适当的日志级别（Info/Success/Warning/Error）
///
/// 架构原则（rule-004）：
/// - 反序列化前确保插件已加载（方案2）
/// - 使用 PluginLoader 全局状态管理
/// </remarks>
public class SolutionRepository
{
    private readonly ILogger _logger;

    /// <summary>
    /// 构造函数
    /// </summary>
    public SolutionRepository()
    {
        _logger = VisionLogger.Instance;
        _logger.Log(LogLevel.Info, "解决方案文件仓库初始化完成", "SolutionRepository");
    }

    /// <summary>
    /// 确保插件已加载（使用全局状态管理）
    /// </summary>
    /// <remarks>
    /// Fail Fast 原则：在反序列化前确保插件已加载，避免运行时失败。
    /// 使用 PluginLoader 全局状态管理，确保插件只加载一次。
    ///
    /// 架构改进（rule-004）：
    /// - 使用 PluginLoader 全局状态替代本地静态字段
    /// - 与 App.OnStartup() 的插件加载逻辑解耦
    /// - 避免重复加载插件
    /// </remarks>
    /// <returns>是否成功加载</returns>
    private bool EnsurePluginsLoaded()
    {
        // 检查全局状态
        if (PluginLoader.IsLoaded)
        {
            _logger.Log(LogLevel.Info, "插件已加载（全局状态），跳过重复检查", "SolutionRepository");
            return true;
        }

        // 如果未加载，触发全局加载
        _logger.Log(LogLevel.Info, "插件未加载，触发全局加载", "SolutionRepository");
        return PluginLoader.EnsureLoaded();
    }


    /// <summary>
    /// 加载完整的解决方案对象
    /// </summary>
    /// <param name="filePath">解决方案文件路径</param>
    /// <returns>解决方案对象，失败返回 null</returns>
    /// <remarks>
    /// Fail Fast 原则：在反序列化前确保插件已加载。
    /// 确保所有工具参数类型都能正确解析。
    /// </remarks>
    public Solution? Load(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.Log(LogLevel.Warning, "加载解决方案失败：文件路径为空", "SolutionRepository");
            return null;
        }

        if (!File.Exists(filePath))
        {
            _logger.Log(LogLevel.Warning, $"加载解决方案失败：文件不存在: {filePath}", "SolutionRepository");
            return null;
        }

        // ✅ Fail Fast：确保插件在反序列化前加载
        if (!EnsurePluginsLoaded())
        {
            _logger.Log(LogLevel.Error, "加载解决方案失败：插件加载未完成", "SolutionRepository");
            return null;
        }

        try
        {
            _logger.Log(LogLevel.Info, $"开始反序列化解决方案: {filePath}", "SolutionRepository");
            var json = File.ReadAllText(filePath);
            var solution = JsonSerializer.Deserialize<Solution>(json, WorkflowSerializationOptions.Default);

            if (solution != null)
            {
                solution.FilePath = filePath;
                _logger.Log(LogLevel.Success, $"加载解决方案成功: Id={solution.Id} -> {filePath}", "SolutionRepository");
            }
            else
            {
                _logger.Log(LogLevel.Warning, $"加载解决方案失败：反序列化结果为null: {filePath}", "SolutionRepository");
            }

            return solution;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"加载解决方案失败: {filePath}, 错误: {ex.Message}", "SolutionRepository", ex);
            return null;
        }
    }

    /// <summary>
    /// 仅加载解决方案元数据（快速读取）
    /// </summary>
    /// <param name="filePath">解决方案文件路径</param>
    /// <returns>元数据对象，失败返回 null</returns>
    public SolutionMetadata? LoadMetadata(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.Log(LogLevel.Warning, "加载元数据失败：文件路径为空", "SolutionRepository");
            return null;
        }

        if (!File.Exists(filePath))
        {
            _logger.Log(LogLevel.Warning, $"加载元数据失败：文件不存在: {filePath}", "SolutionRepository");
            return null;
        }

        try
        {
            var json = File.ReadAllText(filePath);
            var jsonDoc = JsonDocument.Parse(json);
            var root = jsonDoc.RootElement;

            var metadata = SolutionMetadata.Create(filePath);

            // ✅ 从 JSON 读取 ID（在 Solution 中）
            metadata.Id = root.TryGetProperty("Id", out var idElement) ? idElement.GetString() ?? "" : "";

            // ✅ 从 JSON 读取 Name（最小元数据，在 Solution 中）
            metadata.Name = root.TryGetProperty("Name", out var nameElement)
                ? nameElement.GetString() ?? Path.GetFileNameWithoutExtension(filePath)
                : Path.GetFileNameWithoutExtension(filePath);

            // ✅ 从 JSON 读取 Description（最小元数据，在 Solution 中）
            metadata.Description = root.TryGetProperty("Description", out var descElement)
                ? descElement.GetString() ?? ""
                : "";

            // ✅ Version 从 JSON 读取（在 Solution 中）
            metadata.Version = root.TryGetProperty("Version", out var verElement)
                ? verElement.GetString() ?? "1.0"
                : "1.0";

            // ✅ 时间信息从文件系统读取
            var fileInfo = new FileInfo(filePath);
            metadata.CreatedTime = fileInfo.CreationTime;
            metadata.ModifiedTime = fileInfo.LastWriteTime;

            // ✅ 读取统计数据（在 Solution 中）
            if (root.TryGetProperty("Workflows", out var workflowsElement) && workflowsElement.ValueKind == JsonValueKind.Array)
            {
                metadata.WorkflowCount = workflowsElement.GetArrayLength();
            }

            if (root.TryGetProperty("GlobalVariables", out var varsElement) && varsElement.ValueKind == JsonValueKind.Array)
            {
                metadata.GlobalVariableCount = varsElement.GetArrayLength();
            }

            _logger.Log(LogLevel.Info, $"加载元数据成功: {metadata.Name} (Id={metadata.Id}) <- {filePath}", "SolutionRepository");
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"加载元数据失败: {filePath}, 错误: {ex.Message}", "SolutionRepository", ex);
            return null;
        }
    }

    /// <summary>
    /// 保存解决方案到文件
    /// </summary>
    /// <param name="solution">解决方案对象</param>
    /// <param name="filePath">目标文件路径</param>
    /// <returns>是否成功</returns>
    public bool Save(Solution solution, string filePath)
    {
        // ✅ 日志监控：生成唯一调用ID
        long callId = System.Threading.Interlocked.Increment(ref _saveCallCount);

        if (solution == null)
        {
            _logger.Log(LogLevel.Warning, $"[Save #{callId}] 保存解决方案失败：解决方案对象为空\n堆栈: {GetShortStackTrace(3)}", "SolutionRepository");
            return false;
        }

        if (string.IsNullOrEmpty(filePath))
        {
            _logger.Log(LogLevel.Warning, $"[Save #{callId}] 保存解决方案失败：文件路径为空\n堆栈: {GetShortStackTrace(3)}", "SolutionRepository");
            return false;
        }

        try
        {
            solution.FilePath = filePath;

            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.Log(LogLevel.Info, $"[Save #{callId}] 创建目录: {directory}", "SolutionRepository");
            }

            var json = JsonSerializer.Serialize(solution, WorkflowSerializationOptions.Default);

            _logger.Log(LogLevel.Info, $"[Save #{callId}] 序列化完成，开始写入文件", "SolutionRepository");

            File.WriteAllText(filePath, json);

            var fileInfo = new FileInfo(filePath);
            _logger.Log(LogLevel.Success, $"[Save #{callId}] 保存解决方案成功: Id={solution.Id} -> {filePath}, 文件大小: {fileInfo.Length} 字节\n堆栈: {GetShortStackTrace(3)}", "SolutionRepository");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"[Save #{callId}] 保存解决方案失败: {filePath}, 错误: {ex.Message}\n堆栈: {GetShortStackTrace(3)}", "SolutionRepository", ex);
            return false;
        }
    }

    /// <summary>
    /// 保存操作调用计数器（用于日志监控）
    /// </summary>
    private static long _saveCallCount = 0;

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
    /// 删除解决方案文件
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否成功</returns>
    public bool Delete(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.Log(LogLevel.Warning, "删除解决方案失败：文件路径为空", "SolutionRepository");
            return false;
        }

        if (!File.Exists(filePath))
        {
            _logger.Log(LogLevel.Warning, $"删除解决方案失败：文件不存在: {filePath}", "SolutionRepository");
            return false;
        }

        try
        {
            File.Delete(filePath);
            _logger.Log(LogLevel.Success, $"删除解决方案成功: {filePath}", "SolutionRepository");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"删除解决方案失败: {filePath}, 错误: {ex.Message}", "SolutionRepository", ex);
            return false;
        }
    }

    /// <summary>
    /// 检查文件是否存在
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>是否存在</returns>
    public bool Exists(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
            return false;

        return File.Exists(filePath);
    }

    /// <summary>
    /// 批量加载元数据
    /// </summary>
    /// <param name="filePaths">文件路径列表</param>
    /// <returns>元数据列表</returns>
    public List<SolutionMetadata> LoadMetadataBatch(IEnumerable<string> filePaths)
    {
        if (filePaths == null)
        {
            _logger.Log(LogLevel.Warning, "批量加载元数据失败：路径列表为空", "SolutionRepository");
            return new List<SolutionMetadata>();
        }

        var results = new List<SolutionMetadata>();
        int successCount = 0;
        int failCount = 0;

        foreach (var filePath in filePaths)
        {
            var metadata = LoadMetadata(filePath);
            if (metadata != null)
            {
                results.Add(metadata);
                successCount++;
            }
            else
            {
                failCount++;
            }
        }

        _logger.Log(LogLevel.Success, $"批量加载元数据完成: 成功 {successCount}, 失败 {failCount}, 总计 {results.Count}", "SolutionRepository");
        return results;
    }

    /// <summary>
    /// 扫描目录并加载所有解决方案元数据
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    /// <param name="searchPattern">搜索模式（默认 *.solution）</param>
    /// <returns>元数据列表</returns>
    public List<SolutionMetadata> ScanDirectory(string directoryPath, string searchPattern = "*.solution")
    {
        if (string.IsNullOrEmpty(directoryPath))
        {
            _logger.Log(LogLevel.Warning, "扫描目录失败：目录路径为空", "SolutionRepository");
            return new List<SolutionMetadata>();
        }

        if (!Directory.Exists(directoryPath))
        {
            _logger.Log(LogLevel.Warning, $"扫描目录失败：目录不存在: {directoryPath}", "SolutionRepository");
            return new List<SolutionMetadata>();
        }

        try
        {
            var files = Directory.GetFiles(directoryPath, searchPattern, SearchOption.TopDirectoryOnly);
            var metadataList = LoadMetadataBatch(files);

            _logger.Log(LogLevel.Success, $"扫描目录完成: {directoryPath}, 找到 {metadataList.Count} 个解决方案", "SolutionRepository");
            return metadataList;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"扫描目录失败: {directoryPath}, 错误: {ex.Message}", "SolutionRepository", ex);
            return new List<SolutionMetadata>();
        }
    }

    /// <summary>
    /// 获取解决方案文件信息
    /// </summary>
    /// <param name="filePath">文件路径</param>
    /// <returns>文件信息，文件不存在返回 null</returns>
    public FileInfo? GetFileInfo(string filePath)
    {
        if (string.IsNullOrEmpty(filePath) || !File.Exists(filePath))
            return null;

        try
        {
            return new FileInfo(filePath);
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"获取文件信息失败: {filePath}, 错误: {ex.Message}", "SolutionRepository", ex);
            return null;
        }
    }

    /// <summary>
    /// 复制解决方案文件
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="destPath">目标文件路径</param>
    /// <param name="overwrite">是否覆盖（默认 false）</param>
    /// <returns>是否成功</returns>
    public bool Copy(string sourcePath, string destPath, bool overwrite = false)
    {
        // ✅ 日志监控：生成唯一调用ID
        long callId = System.Threading.Interlocked.Increment(ref _copyCallCount);

        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destPath))
        {
            _logger.Log(LogLevel.Warning, $"[Copy #{callId}] 复制解决方案失败：路径为空\n堆栈: {GetShortStackTrace(3)}", "SolutionRepository");
            return false;
        }

        if (!File.Exists(sourcePath))
        {
            _logger.Log(LogLevel.Warning, $"[Copy #{callId}] 复制解决方案失败：源文件不存在: {sourcePath}\n堆栈: {GetShortStackTrace(3)}", "SolutionRepository");
            return false;
        }

        if (!overwrite && File.Exists(destPath))
        {
            _logger.Log(LogLevel.Warning, $"[Copy #{callId}] 复制解决方案失败：目标文件已存在: {destPath}\n堆栈: {GetShortStackTrace(3)}", "SolutionRepository");
            return false;
        }

        try
        {
            // 确保目标目录存在
            var directory = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }

            File.Copy(sourcePath, destPath, overwrite);
            _logger.Log(LogLevel.Success, $"[Copy #{callId}] 复制解决方案成功: {sourcePath} -> {destPath}, 覆盖={overwrite}\n堆栈: {GetShortStackTrace(3)}", "SolutionRepository");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"[Copy #{callId}] 复制解决方案失败: {sourcePath} -> {destPath}, 错误: {ex.Message}\n堆栈: {GetShortStackTrace(3)}", "SolutionRepository", ex);
            return false;
        }
    }

    /// <summary>
    /// 复制操作调用计数器（用于日志监控）
    /// </summary>
    private static long _copyCallCount = 0;

    /// <summary>
    /// 移动/重命名解决方案文件
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="destPath">目标文件路径</param>
    /// <param name="overwrite">是否覆盖（默认 false）</param>
    /// <returns>是否成功</returns>
    public bool Move(string sourcePath, string destPath, bool overwrite = false)
    {
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(destPath))
        {
            _logger.Log(LogLevel.Warning, "移动解决方案失败：路径为空", "SolutionRepository");
            return false;
        }

        if (!File.Exists(sourcePath))
        {
            _logger.Log(LogLevel.Warning, $"移动解决方案失败：源文件不存在: {sourcePath}", "SolutionRepository");
            return false;
        }

        if (!overwrite && File.Exists(destPath))
        {
            _logger.Log(LogLevel.Warning, $"移动解决方案失败：目标文件已存在: {destPath}", "SolutionRepository");
            return false;
        }

        try
        {
            // 确保目标目录存在
            var directory = Path.GetDirectoryName(destPath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.Log(LogLevel.Info, $"创建目录: {directory}", "SolutionRepository");
            }

            // 如果目标文件存在且允许覆盖，先删除
            if (overwrite && File.Exists(destPath))
            {
                File.Delete(destPath);
                _logger.Log(LogLevel.Info, $"删除已存在的目标文件: {destPath}", "SolutionRepository");
            }

            // 移动文件
            File.Move(sourcePath, destPath);
            _logger.Log(LogLevel.Success, $"移动解决方案成功: {sourcePath} -> {destPath}", "SolutionRepository");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"移动解决方案失败: {sourcePath} -> {destPath}, 错误: {ex.Message}", "SolutionRepository", ex);
            return false;
        }
    }
}
