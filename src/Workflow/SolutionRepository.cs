using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Workflow.Persistence;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

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
    private readonly string _recoveryDirectory;
    public string? LastError { get; private set; }

    /// <summary>
    /// 构造函数
    /// </summary>
    public SolutionRepository(string? recoveryDirectory = null)
    {
        _recoveryDirectory = recoveryDirectory ?? Path.Combine(Path.GetTempPath(), "SunEyeVision", "recovery");
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
    public string GetRecoveryPath(string path) => Path.Combine(_recoveryDirectory,
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Path.GetFullPath(path).ToUpperInvariant()))) + ".previous.solution");

    private static void ValidateSchema(JsonElement root)
    {
        if (!root.TryGetProperty("Id", out var id) || id.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(id.GetString()))
            throw new JsonException("方案缺少有效身份。", "$.Id", null, null);
        SolutionFormatPolicy.ValidateReadable(root);
    }

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
    public Solution? Load(string filePath, string? resourceBaseDirectory = null)
        => LoadCandidate(filePath, resourceBaseDirectory).Candidate;

    public SolutionLoadResult LoadCandidate(string filePath, string? resourceBaseDirectory = null)
    {
        LastError = null;
        var diagnostics = new List<PersistenceDiagnostic>();
        string? sha = null, directory = null;
        JsonNode? root = null;
        try
        {
            var path = Path.GetFullPath(filePath);
            directory = Path.GetFullPath(resourceBaseDirectory ?? Path.GetDirectoryName(path)!);
            var bytes = File.ReadAllBytes(path);
            sha = Convert.ToHexString(SHA256.HashData(bytes));
            var json = new UTF8Encoding(false, true).GetString(bytes).TrimStart('\uFEFF');
            SolutionCompatibilityAdapter.CheckDuplicateKeys(json);
            root = JsonNode.Parse(json) ?? throw new JsonException("Empty document.");
            var legacyDocument = SolutionFormatPolicy.ReadVersion(root) == SolutionFormatPolicy.Legacy;
            SolutionCompatibilityAdapter.Prepare(root, diagnostics, deferLegacyBindingOwner: legacyDocument);
            new SolutionResourceResolver(directory).Inspect(root, diagnostics);
            var solution = ReadFrozenCandidate(root, diagnostics, legacyDocument);
            if (legacyDocument)
            {
                diagnostics.Add(new("COMPAT_FORMAT_1_TO_2", "Compatibility", "Warning", "$.SchemaVersion",
                    "Format 1 candidate and its known references validated in memory; the source file is unchanged."));
            }
            solution.FilePath = path;
            solution.ResourceBaseDirectory = directory;
            if (string.IsNullOrWhiteSpace(solution.Name)) solution.Name = Path.GetFileNameWithoutExtension(path);
            return new(solution, sha, directory, diagnostics.AsReadOnly());
        }
        catch (Exception ex)
        {
            var jsonError = ex as JsonException;
            var path = jsonError?.Path ?? "$";
            if (ex.Data["WorkflowId"] is string workflowId && root?["Workflows"] is JsonArray workflows)
                for (int i = 0; i < workflows.Count; i++)
                    if (workflows[i]?["Id"]?.GetValue<string>() == workflowId)
                    { path = "$.Workflows[" + i + "]" + path.TrimStart('$'); break; }
            LastError = path + ": " + ex.Message;
            var code = path.EndsWith(".ToolType", StringComparison.Ordinal) ? "PLUGIN_CONTRACT_REJECTED"
                : path.StartsWith("$.Devices", StringComparison.Ordinal) ? "DEVICE_STRUCTURE_INVALID"
                : path.StartsWith("$.GlobalVariables", StringComparison.Ordinal) ? "VARIABLE_VALUE_INVALID"
                : path.Contains(".Connections", StringComparison.Ordinal) ? "CONNECTION_INVALID"
                : path == "$.SchemaVersion" ? "SCHEMA_UNSUPPORTED" : "LOAD_REJECTED";
            diagnostics.Add(new(code, "Read", "Error", path, ex.Message));
            _logger.Log(LogLevel.Error, "方案候选读取失败：" + LastError, "SolutionRepository", ex);
            return new(null, sha, directory, diagnostics.AsReadOnly());
        }
    }

    /// <summary>Read only the supplied frozen candidate. Both disk reads and save preparation use
    /// the same device restoration, registered declarations and owning-workflow binding validation.</summary>
    internal Solution ReadFrozenCandidate(JsonNode root, List<PersistenceDiagnostic> diagnostics, bool legacyDocument)
    {
        var hasTools = root["Workflows"] is JsonArray flowArray && flowArray.OfType<JsonObject>().Any(w => w["Nodes"] is JsonArray nodes && nodes.Count > 0);
        if (hasTools && !EnsurePluginsLoaded()) throw new InvalidDataException("插件元数据加载未完成。");
        using var document = JsonDocument.Parse(root.ToJsonString());
        ValidateTools(document.RootElement);
        var devices = ReadPersistedDevices(document.RootElement, Stopwatch.StartNew());
        var solution = JsonSerializer.Deserialize<Solution>(RemoveDevicesProperty(root.ToJsonString()),
            new JsonSerializerOptions(WorkflowSerializationOptions.Default) { UnmappedMemberHandling = System.Text.Json.Serialization.JsonUnmappedMemberHandling.Disallow })
            ?? throw new JsonException("Null solution.");
        solution.Devices = devices;
        SolutionLegacyBindingMigration.Prepare(solution, diagnostics, legacyDocument);
        return solution;
    }

    private static void ValidateTools(JsonElement root)
    {
        if (!root.TryGetProperty("Workflows", out var workflows)) return;
        int w = 0;
        foreach (var workflow in workflows.EnumerateArray())
        {
            if (workflow.TryGetProperty("Nodes", out var nodes))
            {
                int n = 0;
                foreach (var node in nodes.EnumerateArray())
                {
                    var path = "$.Workflows[" + w + "].Nodes[" + n++ + "].ToolType";
                    if (!node.TryGetProperty("ToolType", out var tool) || tool.ValueKind != JsonValueKind.String || string.IsNullOrWhiteSpace(tool.GetString()))
                        throw new JsonException("Node ToolType is required.", path, null, null);
                    if (ToolRegistry.GetToolMetadata(tool.GetString()!) == null)
                        throw new JsonException("缺少兼容工具插件：" + tool.GetString(), path, null, null);
                }
            }
            w++;
        }
    }

    private string RemoveDevicesProperty(string json)
    {
        using var document = JsonDocument.Parse(json);
        var properties = new Dictionary<string, JsonElement>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (!string.Equals(property.Name, "Devices", StringComparison.OrdinalIgnoreCase))
                properties[property.Name] = property.Value.Clone();
        }

        return JsonSerializer.Serialize(properties);
    }

    private List<Device> ReadPersistedDevices(
        JsonElement root,
        Stopwatch stopwatch)
    {
        var result = new List<Device>();
        if (!root.TryGetProperty("Devices", out var devices)
            || devices.ValueKind != JsonValueKind.Array)
        {
            return result;
        }

        _logger.Log(LogLevel.Info,
            $"[LoadTrace] Persisted device read begin | count={devices.GetArrayLength()}, elapsedMs={stopwatch.ElapsedMilliseconds}",
            "SolutionRepository");

        var index = 0;
        foreach (var device in devices.EnumerateArray())
        {
            var deviceStopwatch = Stopwatch.StartNew();
            _logger.Log(LogLevel.Info,
                $"[LoadTrace] Persisted device read item begin | index={index}",
                "SolutionRepository");

            var restored = new Device
            {
                Id = ReadString(device, "Id", string.Empty),
                Name = ReadString(device, "Name", string.Empty),
                Type = ReadEnum(device, "Type", DeviceType.Camera),
                Manufacturer = ReadString(device, "Manufacturer", string.Empty),
                Model = ReadString(device, "Model", string.Empty),
                SerialNumber = ReadString(device, "SerialNumber", string.Empty),
                IpAddress = ReadString(device, "IpAddress", string.Empty),
                Port = ReadInt32(device, "Port", 0),
                Status = ReadEnum(device, "Status", DeviceStatus.Unknown),
                IsEnabled = ReadBoolean(device, "IsEnabled", true),
                WorkflowRef = ReadNullableString(device, "WorkflowRef"),
                Description = ReadString(device, "Description", string.Empty),
                Settings = ReadSettings(device),
                CreatedTime = ReadDateTime(device, "CreatedTime", DateTime.Now),
                ModifiedTime = ReadDateTime(device, "ModifiedTime", DateTime.Now)
            };

            var known = typeof(Device).GetProperties().Select(p => p.Name).ToHashSet(StringComparer.Ordinal);
            restored.AdditionalData = device.EnumerateObject().Where(p => !known.Contains(p.Name)).ToDictionary(p => p.Name, p => p.Value.Clone());
            result.Add(restored);

            _logger.Log(LogLevel.Info,
                $"[LoadTrace] Persisted device read item completed | index={index}, id={restored.Id}, settings={restored.Settings.Count}, elapsedMs={deviceStopwatch.ElapsedMilliseconds}",
                "SolutionRepository");
            index++;
        }

        _logger.Log(LogLevel.Info,
            $"[LoadTrace] Persisted device read completed | count={result.Count}, elapsedMs={stopwatch.ElapsedMilliseconds}",
            "SolutionRepository");
        return result;
    }

    private static string ReadString(JsonElement element, string propertyName, string fallback)
    {
        return element.TryGetProperty(propertyName, out var property)
            && property.ValueKind == JsonValueKind.String
            ? property.GetString() ?? fallback
            : fallback;
    }

    private static string? ReadNullableString(JsonElement element, string propertyName)
        => element.TryGetProperty(propertyName, out var property)
           && property.ValueKind == JsonValueKind.String
            ? property.GetString()
            : null;

    private static int ReadInt32(JsonElement element, string propertyName, int fallback)
        => element.TryGetProperty(propertyName, out var property)
           && property.ValueKind == JsonValueKind.Number
           && property.TryGetInt32(out var value)
            ? value
            : fallback;

    private static bool ReadBoolean(JsonElement element, string propertyName, bool fallback)
        => element.TryGetProperty(propertyName, out var property)
           && (property.ValueKind == JsonValueKind.True || property.ValueKind == JsonValueKind.False)
            ? property.GetBoolean()
            : fallback;

    private static TEnum ReadEnum<TEnum>(JsonElement element, string propertyName, TEnum fallback)
        where TEnum : struct, Enum
    {
        if (!element.TryGetProperty(propertyName, out var property))
            return fallback;

        if (property.ValueKind == JsonValueKind.Number
            && property.TryGetInt32(out var numericValue)
            && Enum.IsDefined(typeof(TEnum), numericValue))
        {
            return (TEnum)Enum.ToObject(typeof(TEnum), numericValue);
        }

        if (property.ValueKind == JsonValueKind.String
            && Enum.TryParse<TEnum>(property.GetString(), true, out var parsed))
        {
            return parsed;
        }

        return fallback;
    }

    private static DateTime ReadDateTime(JsonElement element, string propertyName, DateTime fallback)
    {
        if (!element.TryGetProperty(propertyName, out var property)
            || property.ValueKind != JsonValueKind.String)
            return fallback;

        return DateTime.TryParse(property.GetString(), out var value) ? value : fallback;
    }

    private static Dictionary<string, object> ReadSettings(JsonElement device)
    {
        var settings = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
        if (!device.TryGetProperty("Settings", out var element)
            || element.ValueKind != JsonValueKind.Object)
            return settings;

        foreach (var property in element.EnumerateObject())
        {
            settings[property.Name] = ReadJsonValue(property.Value);
        }

        return settings;
    }

    private static object ReadJsonValue(JsonElement element)
    {
        return element.ValueKind switch
        {
            JsonValueKind.String => element.GetString() ?? string.Empty,
            JsonValueKind.True or JsonValueKind.False => element.GetBoolean(),
            JsonValueKind.Number when element.TryGetInt64(out var integer) => integer,
            JsonValueKind.Number when element.TryGetDouble(out var number) => number,
            JsonValueKind.Array or JsonValueKind.Object => element.Clone(),
            JsonValueKind.Null => null!,
            _ => throw new JsonException("Unsupported device setting value.")
        };
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
            ValidateSchema(root);

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
    public bool Save(Solution solution, string filePath, bool overwrite = true)
    {
        if (solution == null) { LastError = "方案为空。"; return false; }
        try
        {
            var success = SaveSnapshot(solution, SolutionSession.Serialize(solution), filePath, overwrite);
            // Preserve the repository API's location update, but only after the file exists.
            if (success) solution.FilePath = Path.GetFullPath(filePath);
            return success;
        }
        catch (Exception ex) when (ex is not SolutionSaveRecoveryRequiredException) { LastError = ex.Message; return false; }
    }

    internal bool SaveSnapshot(Solution solution, string serializedSnapshot, string filePath, bool overwrite = true)
        => WriteSnapshot(SolutionSnapshot.PrepareJson(serializedSnapshot, filePath, solution.FilePath == null), overwrite).FileWritten;

    public SolutionSaveResult WriteSnapshot(SolutionSnapshot prepared, bool overwrite = true)
    {
        LastError = null;
        var filePath = prepared.TargetPath;
        long callId = System.Threading.Interlocked.Increment(ref _saveCallCount);
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.Log(LogLevel.Warning, $"[Save #{callId}] 保存解决方案失败：文件路径为空\n堆栈: {GetShortStackTrace(3)}", "SolutionRepository");
            return new SolutionSaveResult(false, LastError ?? "保存失败：路径为空。");
        }

        bool committed = false;
        try
        {
            // The public entry point has the same owner/schema gate as Prepare and Save.
            // Revalidation consumes only the immutable captured JSON, never the live document.
            prepared = SolutionSnapshot.PrepareJson(prepared.Json, prepared.TargetPath, untitled: false);
            // Communication validation consumes the same frozen JSON that will be written.
            var snapshot = JsonNode.Parse(prepared.Json)!;
            if (snapshot["Communications"] is JsonArray communications)
            {
                var frozen = new Solution();
                frozen.Communications = JsonSerializer.Deserialize<List<Communication>>(communications.ToJsonString(), WorkflowSerializationOptions.Default)!;
                ValidateCommunicationConfigurations(frozen);
            }
            // 确保目录存在
            var directory = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
                _logger.Log(LogLevel.Info, $"[Save #{callId}] 创建目录: {directory}", "SolutionRepository");
            }

            var json = prepared.Json;

            _logger.Log(LogLevel.Info, $"[Save #{callId}] 序列化完成，开始写入文件", "SolutionRepository");

            var destination = Path.GetFullPath(filePath);
            var warning = SolutionFileCommit.Write(destination, GetRecoveryPath(destination), json, overwrite);
            committed = true;
            // After the two-file commit, diagnostics may warn but cannot turn a completed save into false.
            try { _logger.Log(LogLevel.Success, $"[Save #{callId}] 保存解决方案成功: -> {filePath}", "SolutionRepository"); }
            catch (Exception notification) { warning = "保存已成功，日志通知失败：" + notification.Message; }
            return new SolutionSaveResult(true, Warning: warning) { WrittenSnapshot = prepared };
        }
        catch (SolutionSaveRecoveryRequiredException recovery)
        {
            recovery.AttemptedSnapshot = prepared;
            LastError = recovery.Message;
            throw;
        }
        catch (Exception ex)
        {
            if (committed) return new SolutionSaveResult(true, Warning: ex.Message) { WrittenSnapshot = prepared };
            LastError = ex.Message;
            _logger.Log(LogLevel.Error, $"[Save #{callId}] 保存解决方案失败: {filePath}, 错误: {ex.Message}\n堆栈: {GetShortStackTrace(3)}", "SolutionRepository", ex);
            return new SolutionSaveResult(false, LastError ?? "保存失败：路径为空。");
        }
    }

    private static void ValidateCommunicationConfigurations(Solution solution)
    {
        var deviceIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var device in solution.Communications)
        {
            if (string.IsNullOrWhiteSpace(device.Id) || !deviceIds.Add(device.Id))
                throw new InvalidDataException($"通讯设备 ID 为空或重复：{device.Id}");
            var addressIds = new HashSet<string>(StringComparer.Ordinal);
            var previousEnd = -1;
            foreach (var point in device.ModbusAddressPoints.OrderBy(point => point.StartAddress))
            {
                if (string.IsNullOrWhiteSpace(point.Id) || !addressIds.Add(point.Id))
                    throw new InvalidDataException($"通讯设备“{device.Name}”地址块 ID 为空或重复。");
                if (point.Count < 1 || point.StartAddress < 0 ||
                    (long)point.StartAddress + point.Count - 1 > ushort.MaxValue ||
                    point.StartAddress <= previousEnd)
                    throw new InvalidDataException($"通讯设备“{device.Name}”地址块“{point.Name}”范围无效或重叠。");
                previousEnd = point.EndAddress;
            }
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
