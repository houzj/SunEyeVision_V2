using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Services.Serialization;

namespace SunEyeVision.Workflow;

/// <summary>
/// 解决方案管理器（重构版本）
/// </summary>
/// <remarks>
/// 职责：高层协调，整合所有组件
///
/// 特性：
/// - 组合所有组件（Registry、Cache、Repository、Settings）
/// - 提供统一的高层API
/// - 触发事件通知UI层
/// - 管理当前解决方案
/// - 懒加载优化（通过元数据缓存）
///
/// 架构原则（rule-004）：
/// - 职责清晰：仅负责高层协调
/// - 单一职责：不直接操作文件系统（委托给Repository）
/// - 依赖注入：通过构造函数注入依赖
/// - 事件驱动：通过事件通知UI层
///
/// 设计原则（rule-002）：
/// - 命名符合视觉软件行业标准
/// - 方法使用 PascalCase，动词开头
///
/// 日志规范（rule-003）：
/// - 使用 VisionLogger 记录日志
/// - 使用适当的日志级别（Info/Success/Warning/Error）
/// </remarks>
public class SolutionManager
{
    private readonly string _solutionsDirectory;
    private readonly string _configFilePath;
    private readonly ILogger _logger;

    private readonly SolutionRegistry _registry;
    private readonly SolutionCache _cache;
    private readonly SolutionRepository _repository;
    private readonly SolutionSettings _settings;

    private bool _isSettingsDirty = false;  // 脏标记：设置是否需要保存
    private long _solutionAddedEventCount = 0;  // SolutionAdded事件计数（用于日志监控）

    /// <summary>
    /// 当前解决方案
    /// </summary>
    public Solution? CurrentSolution { get; private set; }

    /// <summary>
    /// 当前文件路径
    /// </summary>
    public string? CurrentFilePath { get; private set; }

    /// <summary>
    /// 解决方案目录
    /// </summary>
    public string SolutionsDirectory => _solutionsDirectory;

    /// <summary>
    /// 用户设置
    /// </summary>
    public SolutionSettings Settings => _settings;

    /// <summary>
    /// 解决方案打开事件
    /// </summary>
    public event EventHandler<Solution>? SolutionOpened;

    /// <summary>
    /// 解决方案保存事件
    /// </summary>
    public event EventHandler<Solution>? SolutionSaved;

    /// <summary>
    /// 解决方案关闭事件
    /// </summary>
    public event EventHandler? SolutionClosed;

    /// <summary>
    /// 解决方案添加事件
    /// </summary>
    public event EventHandler<SolutionMetadataEventArgs>? SolutionAdded;

    /// <summary>
    /// 解决方案删除事件
    /// </summary>
    public event EventHandler<SolutionMetadataEventArgs>? SolutionRemoved;

    /// <summary>
    /// 解决方案重命名事件
    /// </summary>
    public event EventHandler<SolutionMetadataEventArgs>? SolutionRenamed;

    /// <summary>
    /// 解决方案更新事件
    /// </summary>
    public event EventHandler<SolutionMetadataEventArgs>? SolutionUpdated;

    /// <summary>
    /// 元数据刷新事件（全量）
    /// </summary>
    public event EventHandler? MetadataRefreshed;

    /// <summary>
    /// 当前解决方案变更事件
    /// </summary>
    public event EventHandler<SolutionMetadataEventArgs>? CurrentSolutionChanged;

    /// <summary>
    /// 工作流添加事件
    /// </summary>
    public event EventHandler<Workflow>? WorkflowAdded;

    /// <summary>
    /// 工作流移除事件
    /// </summary>
    public event EventHandler<Workflow>? WorkflowRemoved;

    /// <summary>
    /// 全局变量添加事件
    /// </summary>
    public event EventHandler<GlobalVariable>? GlobalVariableAdded;

    /// <summary>
    /// 设备添加事件
    /// </summary>
    public event EventHandler<Device>? DeviceAdded;

    /// <summary>
    /// 通讯添加事件
    /// </summary>
    public event EventHandler<Communication>? CommunicationAdded;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="solutionsDirectory">解决方案目录</param>
    public SolutionManager(string solutionsDirectory)
    {
        _solutionsDirectory = solutionsDirectory;
        _configFilePath = Path.Combine(solutionsDirectory, "solution_settings.json");
        _logger = VisionLogger.Instance;

        // 初始化组件
        _registry = new SolutionRegistry();
        _cache = new SolutionCache(maxSize: 100, expirationTime: TimeSpan.FromMinutes(30));
        _repository = new SolutionRepository();
        _settings = new SolutionSettings();

        // ✅ 设置基准路径（用于相对路径转换）
        _settings.SetBasePath(solutionsDirectory);

        // 创建目录
        Directory.CreateDirectory(solutionsDirectory);

        _logger.Log(LogLevel.Info, $"解决方案管理器初始化: 目录={_solutionsDirectory}, 配置文件={_configFilePath}", "SolutionManager");

        // 加载设置（轻量级配置，不涉及Solution对象）
        LoadSettings();

        // 扫描文件系统，注册所有元数据（轻量级，200-300字节/个）
        RefreshMetadata();
    }

    /// <summary>
    /// 创建新解决方案（文件级操作）
    /// </summary>
    /// <param name="metadata">解决方案元数据（从对话框获取）</param>
    /// <returns>创建的元数据对象</returns>
    public SolutionMetadata CreateNewSolution(SolutionMetadata metadata)
    {
        CloseSolution();

        // 创建新的 Solution 对象（纯数据模型，自动创建默认工作流）
        var solution = Solution.Create();
        solution.Version = metadata.Version;
        // Name 和 Description 由 SolutionMetadata 统一管理，不再同步到 Solution

        // 添加默认工作流
        var defaultWorkflow = solution.AddWorkflow("工作流1");

        // 构建文件路径
        var filePath = string.IsNullOrEmpty(metadata.FilePath)
            ? Path.Combine(metadata.DirectoryPath, $"{metadata.Name}.solution")
            : metadata.FilePath;

        solution.FilePath = filePath;

        // ✅ 文件级操作：保存到文件
        bool success = _repository.Save(solution, filePath);
        if (!success)
        {
            throw new InvalidOperationException($"保存解决方案文件失败: {filePath}");
        }

        // 创建元数据（基于保存的文件）
        var newMetadata = metadata.Clone();
        newMetadata.Id = solution.Id;
        newMetadata.FilePath = filePath;
        newMetadata.DirectoryPath = Path.GetDirectoryName(filePath) ?? "";
        newMetadata.CreatedTime = DateTime.Now;
        newMetadata.ModifiedTime = DateTime.Now;
        newMetadata.UpdateStatistics(solution);

        // 注册元数据
        _registry.Register(newMetadata);
        _cache.Set(newMetadata.Id, newMetadata);

        // 添加到已知解决方案
        _settings.AddKnownSolution(newMetadata);
        // ❌ 不自动设置 CurrentSolutionId，需要用户明确启动才设置
        // _settings.CurrentSolutionId = newMetadata.Id;
        ForceSaveSettings();

        // ❌ 不自动设置当前解决方案，需要用户明确启动才设置
        // CurrentSolution = solution;
        // CurrentFilePath = filePath;

        // ==================== 触发事件 ====================
        var addedArgs = new SolutionMetadataEventArgs
        {
            Metadata = newMetadata,
            SolutionId = newMetadata.Id
        };
        _logger.Log(LogLevel.Info, $"[SolutionAdded #{_solutionAddedEventCount}] 准备触发 SolutionAdded 事件: {newMetadata.Name} (Id={newMetadata.Id}, FilePath={newMetadata.FilePath})", "SolutionManager");
        SolutionAdded?.Invoke(this, addedArgs);
        _logger.Log(LogLevel.Success, $"[SolutionAdded #{_solutionAddedEventCount}] SolutionAdded 事件已触发", "SolutionManager");
        _solutionAddedEventCount++;

        _logger.Log(LogLevel.Success, $"创建新解决方案: {newMetadata.Name} -> {filePath}", "SolutionManager");
        return newMetadata;
    }

    /// <summary>
    /// 创建新解决方案（向后兼容方法）
    /// </summary>
    /// <param name="name">解决方案名称</param>
    /// <param name="description">描述</param>
    /// <param name="solutionPath">保存路径（可选）</param>
    /// <returns>创建的元数据对象</returns>
    public SolutionMetadata CreateNewSolution(string name, string description = "", string? solutionPath = null)
    {
        var metadata = new SolutionMetadata
        {
            Name = name,
            Description = description,
            DirectoryPath = solutionPath ?? _solutionsDirectory
        };
        return CreateNewSolution(metadata);
    }

    /// <summary>
    /// 打开解决方案（懒加载）
    /// </summary>
    /// <param name="filePath">解决方案文件路径</param>
    /// <returns>解决方案对象，失败返回 null</returns>
    public Solution? OpenSolution(string filePath)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.Log(LogLevel.Warning, "打开解决方案失败：文件路径为空", "SolutionManager");
            return null;
        }

        // 检查文件是否存在
        if (!_repository.Exists(filePath))
        {
            _logger.Log(LogLevel.Error, $"打开解决方案失败：文件不存在: {filePath}", "SolutionManager");
            return null;
        }

        CloseSolution();

        // 加载解决方案
        var solution = _repository.Load(filePath);
        if (solution == null)
        {
            _logger.Log(LogLevel.Error, $"打开解决方案失败：加载失败: {filePath}", "SolutionManager");
            return null;
        }

        CurrentSolution = solution;
        CurrentFilePath = filePath;

        // ✅ 获取元数据（名称和描述由 SolutionMetadata 统一管理）
        SolutionMetadata? metadata = _settings.KnownSolutions.Values
            .FirstOrDefault(m => m.FilePath == filePath);

        if (metadata == null)
        {
            // 如果 Settings 中没有，从文件加载元数据
            metadata = _repository.LoadMetadata(filePath);
            if (metadata == null)
            {
                // 如果加载失败，创建一个基本的元数据（使用文件名作为名称）
                metadata = new SolutionMetadata
                {
                    Id = solution.Id,
                    Name = Path.GetFileNameWithoutExtension(filePath),
                    Description = "",
                    Version = solution.Version,
                    FilePath = filePath,
                    DirectoryPath = Path.GetDirectoryName(filePath) ?? "",
                    CreatedTime = File.GetCreationTime(filePath),
                    ModifiedTime = File.GetLastWriteTime(filePath)
                };
            }
            metadata.UpdateStatistics(solution);
        }
        else
        {
            // 如果 Settings 中已有，更新文件路径和时间戳
            metadata.FilePath = filePath;
            metadata.DirectoryPath = Path.GetDirectoryName(filePath) ?? "";
            metadata.ModifiedTime = File.GetLastWriteTime(filePath);
            metadata.UpdateStatistics(solution);
        }

        // 注册到注册表和缓存
        _registry.Register(metadata);
        _cache.Set(metadata.Id, metadata);

        // 添加到已知解决方案（AddKnownSolution 会自动更新现有条目）
        _settings.AddKnownSolution(metadata);
        _settings.CurrentSolutionId = metadata.Id;
        MarkSettingsDirty();

        // 触发事件
        SolutionOpened?.Invoke(this, solution);

        // ✅ 使用细粒度事件
        var currentSolutionArgs = new SolutionMetadataEventArgs
        {
            Metadata = metadata,
            SolutionId = metadata.Id
        };
        CurrentSolutionChanged?.Invoke(this, currentSolutionArgs);

        _logger.Log(LogLevel.Success, $"打开解决方案: {metadata.Name} -> {filePath}", "SolutionManager");
        return CurrentSolution;
    }

    /// <summary>
    /// 从路径加载解决方案（别名方法）
    /// </summary>
    /// <param name="filePath">解决方案文件路径</param>
    /// <returns>解决方案对象，失败返回 null</returns>
    public Solution? LoadSolutionFromPath(string filePath)
    {
        return OpenSolution(filePath);
    }

    /// <summary>
    /// 仅加载解决方案对象（不设置为当前解决方案）
    /// </summary>
    /// <param name="filePath">解决方案文件路径</param>
    /// <returns>解决方案对象，失败返回 null</returns>
    public Solution? LoadSolutionOnly(string filePath)
    {
        _logger.Log(LogLevel.Info,
            $"[LoadSolutionOnly] 开始加载解决方案: FilePath={filePath}", "SolutionManager");

        if (string.IsNullOrEmpty(filePath))
        {
            _logger.Log(LogLevel.Warning, "[LoadSolutionOnly] 加载失败：文件路径为空", "SolutionManager");
            return null;
        }

        if (!File.Exists(filePath))
        {
            _logger.Log(LogLevel.Error, $"[LoadSolutionOnly] 加载失败：文件不存在: {filePath}", "SolutionManager");
            return null;
        }

        try
        {
            var solution = _repository.Load(filePath);

            if (solution == null)
            {
                _logger.Log(LogLevel.Error, $"[LoadSolutionOnly] 加载失败：Repository.Load 返回 null: {filePath}", "SolutionManager");
                return null;
            }

            _logger.Log(LogLevel.Success,
                $"[LoadSolutionOnly] 加载成功: Solution.Id={solution.Id}, Workflows={solution.Workflows.Count}, GlobalVariables={solution.GlobalVariables?.Count ?? 0}, Devices={solution.Devices?.Count ?? 0}",
                "SolutionManager");

            return solution;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error,
                $"[LoadSolutionOnly] 加载异常: FilePath={filePath}, Message={ex.Message}\n堆栈: {ex.StackTrace}",
                "SolutionManager", ex);
            return null;
        }
    }

    /// <summary>
    /// 静默设置当前解决方案（不触发事件）
    /// </summary>
    /// <param name="solution">解决方案对象</param>
    /// <param name="filePath">解决方案文件路径</param>
    /// <remarks>
    /// 此方法用于特定场景（如保存新解决方案）需要设置当前解决方案但不触发事件的场合
    /// </remarks>
    public void SetCurrentSolutionSilently(Solution solution, string filePath)
    {
        if (solution == null || string.IsNullOrEmpty(filePath))
        {
            _logger.Log(LogLevel.Warning, "静默设置当前解决方案失败：参数无效", "SolutionManager");
            return;
        }

        CurrentSolution = solution;
        CurrentFilePath = filePath;
        _settings.CurrentSolutionId = solution.Id;
        MarkSettingsDirty();

        // 获取元数据以获取Name
        var metadata = _cache.Get(solution.Id) ?? _registry.Get(solution.Id);
        var solutionName = metadata?.Name ?? solution.Id;
        _logger.Log(LogLevel.Info, $"静默设置当前解决方案: {solutionName} -> {filePath}", "SolutionManager");
    }

    /// <summary>
    /// 直接保存解决方案对象（用于不依赖CurrentSolution的场景）
    /// </summary>
    /// <param name="solution">要保存的Solution对象</param>
    /// <param name="filePath">保存路径</param>
    /// <param name="metadata">元数据对象（用于更新注册表和缓存）</param>
    /// <returns>是否成功</returns>
    /// <remarks>
    /// 此方法直接保存Solution对象，不依赖CurrentSolution。
    /// 用于UI层直接创建Solution对象并保存的场景（如关闭软件时创建新解决方案）。
    /// </remarks>
    public bool SaveSolutionDirect(Solution solution, string filePath, SolutionMetadata? metadata = null)
    {
        try
        {
            _logger.Log(LogLevel.Info, $"[SaveSolutionDirect] 开始保存解决方案: Id={solution.Id} -> {filePath}", "SolutionManager");

            // 保存到文件
            bool success = _repository.Save(solution, filePath);
            if (!success)
            {
                _logger.Log(LogLevel.Error, $"[SaveSolutionDirect] 保存解决方案文件失败: {filePath}", "SolutionManager");
                return false;
            }

            // 更新元数据
            if (metadata != null)
            {
                metadata.FilePath = filePath;
                metadata.DirectoryPath = Path.GetDirectoryName(filePath) ?? "";
                metadata.ModifiedTime = DateTime.Now;
                metadata.UpdateStatistics(solution);

                // 注册元数据
                _registry.Register(metadata);
                _cache.Set(metadata.Id, metadata);

                // 添加到已知解决方案
                _settings.AddKnownSolution(metadata);
                ForceSaveSettings();
            }

            _logger.Log(LogLevel.Success, $"[SaveSolutionDirect] 解决方案保存成功: Id={solution.Id}", "SolutionManager");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"[SaveSolutionDirect] 保存解决方案失败: {ex.Message}", "SolutionManager", ex);
            return false;
        }
    }

    /// <summary>
    /// 保存当前解决方案
    /// </summary>
    /// <param name="filePath">文件路径（可选，默认使用当前路径）</param>
    /// <returns>是否成功</returns>
    public bool SaveSolution(string? filePath = null)
    {
        if (CurrentSolution == null)
        {
            _logger.Log(LogLevel.Warning, "保存解决方案失败：没有当前解决方案", "SolutionManager");
            return false;
        }

        var savePath = filePath ?? CurrentFilePath;
        if (string.IsNullOrEmpty(savePath))
        {
            _logger.Log(LogLevel.Warning, "保存解决方案失败：未指定保存路径", "SolutionManager");
            return false;
        }

        // ✅ 获取元数据（名称和描述由 SolutionMetadata 统一管理）
        var metadata = _settings.KnownSolutions.Values
            .FirstOrDefault(m => m.Id == CurrentSolution.Id);

        // 保存文件
        bool success = _repository.Save(CurrentSolution, savePath);
        if (!success)
        {
            return false;
        }

        CurrentFilePath = savePath;

        // ✅ 更新元数据
        if (metadata == null)
        {
            // 如果元数据不存在（不应该发生），从文件加载或创建一个基本的元数据
            metadata = _repository.LoadMetadata(savePath);
            if (metadata == null)
            {
                metadata = new SolutionMetadata
                {
                    Id = CurrentSolution.Id,
                    Name = Path.GetFileNameWithoutExtension(savePath),
                    Description = "",
                    Version = CurrentSolution.Version,
                    FilePath = savePath,
                    DirectoryPath = Path.GetDirectoryName(savePath) ?? "",
                    CreatedTime = File.GetCreationTime(savePath),
                    ModifiedTime = DateTime.Now
                };
            }
        }
        else
        {
            // 更新现有元数据
            metadata.FilePath = savePath;
            metadata.DirectoryPath = Path.GetDirectoryName(savePath) ?? "";
            metadata.ModifiedTime = DateTime.Now;
        }

        // 更新统计数据
        metadata.UpdateStatistics(CurrentSolution);

        // 注册元数据
        _registry.Register(metadata);
        _cache.Set(metadata.Id, metadata);
        _settings.AddKnownSolution(metadata);

        MarkSettingsDirty();

        // 触发事件
        SolutionSaved?.Invoke(this, CurrentSolution);

        // ✅ 使用细粒度事件
        var updatedArgs = new SolutionMetadataEventArgs
        {
            Metadata = metadata,
            SolutionId = metadata.Id
        };
        SolutionUpdated?.Invoke(this, updatedArgs);

        _logger.Log(LogLevel.Success, $"保存解决方案成功: Id={CurrentSolution.Id} -> {savePath}", "SolutionManager");
        return true;
    }

    public SolutionMetadata? SaveAsSolution(string filePath, string? newName = null, string? newDescription = null)
    {
        if (string.IsNullOrEmpty(filePath))
        {
            _logger.Log(LogLevel.Warning, "另存为解决方案失败：未指定保存路径", "SolutionManager");
            return null;
        }

        if (CurrentSolution == null || string.IsNullOrEmpty(CurrentFilePath))
        {
            _logger.Log(LogLevel.Warning, "另存为解决方案失败：没有当前解决方案或文件路径", "SolutionManager");
            return null;
        }

        return CopyOrSaveAsSolution(
            CurrentFilePath,
            newName,
            newDescription,
            filePath,
            switchToNewSolution: true);
    }

    /// <summary>
    /// 关闭当前解决方案
    /// </summary>
    public void CloseSolution()
    {
        if (CurrentSolution != null && !string.IsNullOrEmpty(CurrentFilePath))
        {
            SaveSolution();
        }

        CurrentSolution = null;
        CurrentFilePath = null;

        // ✅ 清除当前解决方案ID（确保与内存状态同步）
        _settings.CurrentSolutionId = "";
        MarkSettingsDirty();

        SolutionClosed?.Invoke(this, EventArgs.Empty);

        // ✅ 触发当前解决方案变更事件（通知UI更新）
        var args = new SolutionMetadataEventArgs
        {
            Metadata = null,
            SolutionId = ""
        };
        CurrentSolutionChanged?.Invoke(this, args);

        _logger.Log(LogLevel.Info, "关闭解决方案", "SolutionManager");
    }

    /// <summary>
    /// 删除解决方案
    /// </summary>
    /// <param name="solutionId">解决方案ID</param>
    /// <param name="deleteFile">是否同时删除磁盘上的解决方案文件</param>
    /// <returns>是否成功。删文件失败且文件仍存在时返回 false。</returns>
    /// <remarks>
    /// 优化（2026-03-23）：
    /// 1. 区分当前解决方案和未打开解决方案
    /// 2. 如果是当前解决方案，先自动关闭再删除元数据
    /// 3. deleteFile 为 true 时，关闭后删除磁盘文件（删除失败且文件仍存在则中止，条目保留）
    /// 4. 删除后确保 CurrentSolutionId 被清除（如果是当前解决方案）
    /// 5. 提供清晰的日志记录，区分删除场景
    /// </remarks>
    public bool DeleteSolution(string solutionId, bool deleteFile = false)
    {
        if (string.IsNullOrEmpty(solutionId))
        {
            _logger.Log(LogLevel.Warning, "删除解决方案失败：解决方案ID为空", "SolutionManager");
            return false;
        }

        // 检查是否是当前打开的解决方案
        bool isCurrentSolution = CurrentSolution?.Id == solutionId;

        // 如果是当前解决方案，先关闭（释放文件句柄，避免后续删文件被占用）
        if (isCurrentSolution)
        {
            _logger.Log(LogLevel.Info, $"删除当前打开的解决方案: Id={solutionId}", "SolutionManager");
            CloseSolution();
        }
        else
        {
            _logger.Log(LogLevel.Info, $"删除未打开的解决方案: Id={solutionId}", "SolutionManager");
        }

        // 获取元数据
        var metadata = _registry.Get(solutionId);
        if (metadata == null)
        {
            _logger.Log(LogLevel.Warning, $"删除解决方案失败：元数据不存在: {solutionId}", "SolutionManager");
            return false;
        }

        // 按需删除磁盘文件
        if (deleteFile)
        {
            string filePath = metadata.FilePath;

            if (!string.IsNullOrEmpty(filePath) && _repository.Exists(filePath))
            {
                bool fileDeleted = _repository.Delete(filePath);
                if (!fileDeleted && _repository.Exists(filePath))
                {
                    // rule-015：删文件失败且文件仍存在，不静默继续，中止并保留条目
                    _logger.Log(LogLevel.Error, $"删除解决方案失败：无法删除源文件，已中止: {filePath}", "SolutionManager");
                    return false;
                }
            }

            _logger.Log(LogLevel.Success, $"删除解决方案及源文件: {filePath}", "SolutionManager");
        }
        else
        {
            _logger.Log(LogLevel.Info, $"仅删除元数据，保留解决方案文件: {metadata.FilePath}", "SolutionManager");
        }

        // 从注册表移除
        _registry.Unregister(solutionId);

        // 从缓存移除
        _cache.Invalidate(solutionId);

        // 从已知解决方案移除
        _settings.RemoveKnownSolution(solutionId);

        // ✅ 确保清除 CurrentSolutionId（如果删除的是当前解决方案）
        // CloseSolution() 已经处理了 CurrentSolutionId 的清除，但这里再次确保
        if (isCurrentSolution && _settings.CurrentSolutionId == solutionId)
        {
            _settings.CurrentSolutionId = "";
        }

        ForceSaveSettings();

        // 触发事件
        var removedArgs = new SolutionMetadataEventArgs
        {
            SolutionId = solutionId
        };
        SolutionRemoved?.Invoke(this, removedArgs);

        _logger.Log(LogLevel.Success, $"删除解决方案成功: {metadata.Name} (Id={solutionId})", "SolutionManager");
        return true;
    }

    /// <summary>
    /// 重命名解决方案文件
    /// </summary>
    /// <param name="solutionId">解决方案ID</param>
    /// <param name="newName">新名称</param>
    /// <returns>是否成功</returns>
    /// <remarks>
    /// 重命名流程（新架构）：
    /// 1. 验证参数有效性
    /// 2. 获取元数据（从注册表）
    /// 3. 构建新文件路径
    /// 4. 重命名物理文件（通过 Repository.Move）
    /// 5. 更新元数据（Name、FilePath）
    /// 6. 更新注册表和缓存
    /// 7. 更新 KnownSolutions 设置
    /// 8. 如果 Solution 已加载，更新其 FilePath（Solution 本身没有 Name 属性）
    ///
    /// 设计原则（rule-002）：
    /// - 方法使用 PascalCase，动词开头
    /// - 命名符合视觉软件行业标准
    ///
    /// 日志规范（rule-003）：
    /// - 使用 VisionLogger 记录日志
    /// - 使用适当的日志级别
    /// </remarks>
    public bool RenameSolutionFile(string solutionId, string newName)
    {
        // 1. 参数验证
        if (string.IsNullOrEmpty(solutionId))
        {
            _logger.Log(LogLevel.Warning, "重命名解决方案失败：解决方案ID为空", "SolutionManager");
            return false;
        }

        if (string.IsNullOrWhiteSpace(newName))
        {
            _logger.Log(LogLevel.Warning, "重命名解决方案失败：新名称为空", "SolutionManager");
            return false;
        }

        // 2. 获取元数据
        var metadata = _registry.Get(solutionId);
        if (metadata == null)
        {
            _logger.Log(LogLevel.Warning, $"重命名解决方案失败：元数据不存在: {solutionId}", "SolutionManager");
            return false;
        }

        if (string.IsNullOrEmpty(metadata.FilePath))
        {
            _logger.Log(LogLevel.Warning, $"重命名解决方案失败：文件路径为空: {metadata.Name}", "SolutionManager");
            return false;
        }

        // 3. 检查名称是否变化
        if (metadata.Name == newName)
        {
            _logger.Log(LogLevel.Info, $"解决方案名称未变化，跳过重命名: {metadata.Name}", "SolutionManager");
            return true;
        }

        try
        {
            var oldName = metadata.Name;
            var oldFilePath = metadata.FilePath;
            var directory = Path.GetDirectoryName(oldFilePath) ?? _solutionsDirectory;
            var newFilePath = Path.Combine(directory, $"{newName}.solution");

            _logger.Log(LogLevel.Info, $"开始重命名解决方案: {oldName} -> {newName}", "SolutionManager");
            _logger.Log(LogLevel.Info, $"文件路径: {oldFilePath} -> {newFilePath}", "SolutionManager");

            // 4. 检查目标文件是否已存在
            if (_repository.Exists(newFilePath))
            {
                _logger.Log(LogLevel.Warning, $"重命名解决方案失败：目标文件已存在: {newFilePath}", "SolutionManager");
                return false;
            }

            // 5. 重命名物理文件
            bool moveSuccess = _repository.Move(oldFilePath, newFilePath, overwrite: false);
            if (!moveSuccess)
            {
                _logger.Log(LogLevel.Error, $"重命名解决方案失败：文件移动失败: {oldFilePath} -> {newFilePath}", "SolutionManager");
                return false;
            }

            // 6. 更新元数据
            metadata.Name = newName;
            metadata.FilePath = newFilePath;
            metadata.DirectoryPath = directory;
            metadata.ModifiedTime = DateTime.Now;

            // 7. 更新注册表和缓存
            _registry.Register(metadata);
            _cache.Set(metadata.Id, metadata);

            // 8. 更新 KnownSolutions 设置
            _settings.AddKnownSolution(metadata);
            MarkSettingsDirty();

            // 9. 更新当前文件路径（如果是当前解决方案）
            if (CurrentSolution?.Id == solutionId)
            {
                CurrentFilePath = newFilePath;
                CurrentSolution.FilePath = newFilePath;
            }

            // 10. 触发事件
            var renamedArgs = new SolutionMetadataEventArgs
            {
                Metadata = metadata,
                SolutionId = metadata.Id,
                OldName = oldName,
                NewName = newName
            };
            SolutionRenamed?.Invoke(this, renamedArgs);

            _logger.Log(LogLevel.Success, $"重命名解决方案成功: {oldName} -> {newName}", "SolutionManager");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"重命名解决方案失败: {metadata.Name} -> {newName}, 错误: {ex.Message}", "SolutionManager", ex);
            return false;
        }
    }

    /// <summary>
    /// 复制解决方案（文件级操作 + JSON元数据更新）
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="newName">新名称</param>
    /// <param name="newDescription">新描述（可选）</param>
    /// <param name="targetDirectory">目标目录（默认与源文件相同）</param>
    /// <returns>复制后的元数据对象，失败返回 null</returns>
    /// <remarks>
    /// 优化说明（2026-03-24）：
    /// 1. 文件级操作：直接复制 .solution 文件
    /// 2. JSON元数据更新：修改文件中的 Solution.Id, Name, Description
    /// 3. 重新加载元数据：确保内存和文件内容一致
    /// 4. 遵循SolutionMetadata和Solution同步原则
    /// 5. 自动保存：如果源是当前解决方案，自动保存
    /// </remarks>
    public SolutionMetadata? CopySolution(string sourcePath, string newName, string? newDescription = null, string? targetDirectory = null)
    {
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(newName))
        {
            _logger.Log(LogLevel.Warning, "复制解决方案失败：源路径或新名称为空", "SolutionManager");
            return null;
        }

        if (!_repository.Exists(sourcePath))
        {
            _logger.Log(LogLevel.Warning, $"复制解决方案失败：源文件不存在: {sourcePath}", "SolutionManager");
            return null;
        }

        // 构建目标文件路径
        var targetDir = targetDirectory ?? Path.GetDirectoryName(sourcePath) ?? _solutionsDirectory;
        var targetFilePath = Path.Combine(targetDir, $"{newName}.solution");

        // ✅ 调用统一实现方法（switchToNewSolution: false）
        return CopyOrSaveAsSolution(
            sourcePath,
            newName,
            newDescription,
            targetFilePath,
            switchToNewSolution: false);
    }

    /// <summary>
    /// 统一的复制/另存为实现（消除代码重复）
    /// </summary>
    /// <param name="sourcePath">源文件路径</param>
    /// <param name="newName">新名称</param>
    /// <param name="newDescription">新描述（可选，如果为null则保持原描述）</param>
    /// <param name="targetFilePath">目标文件路径</param>
    /// <param name="switchToNewSolution">是否切换到新解决方案（另存为=true，复制=false）</param>
    /// <returns>新元数据对象，失败返回 null</returns>
    /// <remarks>
    /// 统一实现说明（2026-03-24）：
    /// 1. 文件级操作：直接复制 .solution 文件
    /// 2. JSON元数据更新：修改文件中的 Solution.Id, Name, Description
    /// 3. 自动保存：如果源是当前解决方案，自动保存
    /// 4. 重新加载元数据：确保内存和文件内容一致
    /// 5. 根据switchToNewSolution参数决定是否切换到新解决方案
    /// 6. 触发相应的事件（SolutionAdded 或 CurrentSolutionChanged）
    ///
    /// 行为差异：
    /// - SaveAsSolution (switchToNewSolution=true): 
    ///   - 自动保存当前解决方案
    ///   - 切换到新解决方案（CurrentSolutionId 更新）
    ///   - 触发 SolutionAdded 和 CurrentSolutionChanged 事件
    /// - CopySolution (switchToNewSolution=false):
    ///   - 如果源是当前解决方案，自动保存
    ///   - 不切换到新解决方案
    ///   - 仅触发 SolutionAdded 事件
    /// </remarks>
    private SolutionMetadata? CopyOrSaveAsSolution(
        string sourcePath,
        string newName,
        string? newDescription,
        string targetFilePath,
        bool switchToNewSolution)
    {
        if (string.IsNullOrEmpty(sourcePath) || string.IsNullOrEmpty(newName))
        {
            _logger.Log(LogLevel.Warning, "复制/另存为解决方案失败：源路径或新名称为空", "SolutionManager");
            return null;
        }

        if (!_repository.Exists(sourcePath))
        {
            _logger.Log(LogLevel.Warning, $"复制/另存为解决方案失败：源文件不存在: {sourcePath}", "SolutionManager");
            return null;
        }

        try
        {
            var targetDir = Path.GetDirectoryName(targetFilePath) ?? _solutionsDirectory;
            var newSolutionId = Guid.NewGuid().ToString();

            _logger.Log(LogLevel.Info, $"开始{(switchToNewSolution ? "另存为" : "复制")}解决方案: {sourcePath} -> {targetFilePath}", "SolutionManager");
            _logger.Log(LogLevel.Info, $"新解决方案ID: {newSolutionId}, 新名称: {newName}, 新描述: {newDescription ?? "(保持原描述)"}", "SolutionManager");

            // ==================== 步骤1：文件级操作 ====================
            // 根据是否当前解决方案选择不同策略
            bool isCurrentSolution = CurrentSolution != null && CurrentFilePath == sourcePath;
            bool success;

            if (isCurrentSolution)
            {
                // ✅ 优化：克隆并修改 CurrentSolution，一次性保存（避免两次写入）
                // 名称和描述由 SolutionMetadata 统一管理，不需要同步到 Solution

                // 克隆 CurrentSolution（使用序列化/反序列化）
                var json = JsonSerializer.Serialize(CurrentSolution, WorkflowSerializationOptions.Default);
                var newSolution = JsonSerializer.Deserialize<Solution>(json, WorkflowSerializationOptions.Default);
                if (newSolution == null)
                {
                    _logger.Log(LogLevel.Error, "另存为失败：克隆解决方案对象失败", "SolutionManager");
                    return null;
                }

                // 修改克隆对象的 ID 和路径
                newSolution.Id = newSolutionId;
                newSolution.FilePath = targetFilePath;
                newSolution.Version = "1.0";  // 新解决方案版本重置为 1.0

                // 一次性保存克隆对象
                success = _repository.Save(newSolution, targetFilePath);
                _logger.Log(LogLevel.Info, "另存为：克隆并保存当前解决方案到新文件（一次性完成）", "SolutionManager");
            }
            else
            {
                // 复制文件（适用于未打开的解决方案）
                success = _repository.Copy(sourcePath, targetFilePath, overwrite: true);
                _logger.Log(LogLevel.Info, "复制：从磁盘复制解决方案文件", "SolutionManager");
            }

            if (!success)
            {
                _logger.Log(LogLevel.Error, $"复制/另存为解决方案失败：文件操作失败", "SolutionManager");
                return null;
            }

            // ==================== 步骤2：更新JSON文件中的ID（仅适用于复制的文件） ====================
            // 对于当前解决方案，已经在保存前修改了ID，无需再更新
            // Name 和 Description 由 SolutionMetadata 统一管理
            if (!isCurrentSolution)
            {
                bool updateSuccess = UpdateSolutionIdInFile(targetFilePath, newSolutionId);

                if (!updateSuccess)
                {
                    _logger.Log(LogLevel.Error, $"复制/另存为解决方案失败：更新JSON文件ID失败", "SolutionManager");
                    // 删除已复制的文件
                    try { File.Delete(targetFilePath); } catch { }
                    return null;
                }
            }

            // ==================== 步骤3：重新加载元数据（确保一致性） ====================
            var metadata = _repository.LoadMetadata(targetFilePath);
            if (metadata == null)
            {
                _logger.Log(LogLevel.Error, $"复制/另存为解决方案失败：重新加载元数据失败", "SolutionManager");
                // 删除已复制的文件
                try { File.Delete(targetFilePath); } catch { }
                return null;
            }

            // ==================== 步骤4：注册元数据 ====================
            // 更新路径、名称、描述和时间戳
            metadata.FilePath = targetFilePath;
            metadata.DirectoryPath = targetDir;
            metadata.Name = newName;  // 设置新名称
            metadata.Description = newDescription ?? metadata.Description;  // 设置新描述（如果提供）
            metadata.CreatedTime = DateTime.Now;
            metadata.ModifiedTime = DateTime.Now;

            // 注册元数据
            _registry.Register(metadata);
            _cache.Set(metadata.Id, metadata);

            // 添加到已知解决方案
            _settings.AddKnownSolution(metadata);

            // ==================== 步骤5：根据switchToNewSolution参数决定行为 ====================
            if (switchToNewSolution)
            {
                // 另存为：切换到新解决方案
                _settings.CurrentSolutionId = metadata.Id;

                // 更新当前文件路径
                CurrentFilePath = targetFilePath;
                if (CurrentSolution != null)
                {
                    CurrentSolution.FilePath = targetFilePath;
                }

                _logger.Log(LogLevel.Info, $"另存为解决方案：切换到新解决方案", "SolutionManager");
            }

            SaveSettings();

            // ==================== 步骤6：触发事件 ====================
            var addedArgs = new SolutionMetadataEventArgs
            {
                Metadata = metadata,
                SolutionId = metadata.Id
            };
            _logger.Log(LogLevel.Info, $"[SolutionAdded #{_solutionAddedEventCount}] 准备触发 SolutionAdded 事件: {metadata.Name} (Id={metadata.Id}, FilePath={metadata.FilePath})", "SolutionManager");
            SolutionAdded?.Invoke(this, addedArgs);
            _logger.Log(LogLevel.Success, $"[SolutionAdded #{_solutionAddedEventCount}] SolutionAdded 事件已触发", "SolutionManager");
            _solutionAddedEventCount++;

            // 如果切换到新解决方案，触发 CurrentSolutionChanged 事件
            if (switchToNewSolution)
            {
                var currentSolutionArgs = new SolutionMetadataEventArgs
                {
                    Metadata = metadata,
                    SolutionId = metadata.Id
                };
                CurrentSolutionChanged?.Invoke(this, currentSolutionArgs);
            }

            _logger.Log(LogLevel.Success, $"{(switchToNewSolution ? "另存为" : "复制")}解决方案成功: Id={metadata.Id}, Name={metadata.Name}, Path={targetFilePath}", "SolutionManager");
            return metadata;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"复制/另存为解决方案失败: {sourcePath}, 错误: {ex.Message}", "SolutionManager", ex);
            return null;
        }
    }

    /// <summary>
    /// 更新解决方案文件中的 ID
    /// </summary>
    /// <param name="filePath">解决方案文件路径</param>
    /// <param name="newId">新的解决方案ID</param>
    /// <returns>是否成功</returns>
    /// <remarks>
    /// 架构改进（2026-03-25）：
    /// - Name 和 Description 由 SolutionMetadata 统一管理，不再存储在 Solution 文件中
    /// - 此方法仅更新 Solution.Id
    /// </remarks>
    private bool UpdateSolutionIdInFile(string filePath, string newId)
    {
        try
        {
            _logger.Log(LogLevel.Info, $"开始更新JSON文件ID: {filePath}", "SolutionManager");
            _logger.Log(LogLevel.Info, $"新ID: {newId}", "SolutionManager");

            // 读取JSON文件
            var jsonContent = File.ReadAllText(filePath);
            using var jsonDoc = JsonDocument.Parse(jsonContent);
            var root = jsonDoc.RootElement;

            // 构建新的JSON内容
            using var memoryStream = new MemoryStream();
            using var writer = new Utf8JsonWriter(memoryStream, new JsonWriterOptions { Indented = true });

            writer.WriteStartObject();

            bool idUpdated = false;

            // 遍历所有属性
            foreach (var property in root.EnumerateObject())
            {
                if (property.NameEquals("Id") && !idUpdated)
                {
                    writer.WriteString("Id", newId);
                    idUpdated = true;
                    _logger.Log(LogLevel.Info, $"✓ 更新 Solution.Id: {newId}", "SolutionManager");
                }
                // 移除 Name 和 Description 字段（由 SolutionMetadata 管理）
                else if (property.NameEquals("Name") || property.NameEquals("Description"))
                {
                    // 跳过这些字段，不再写入
                    _logger.Log(LogLevel.Info, $"✓ 跳过字段: {property.Name}（由 SolutionMetadata 管理）", "SolutionManager");
                }
                else
                {
                    // 其他属性保持不变
                    property.WriteTo(writer);
                }
            }

            // 如果文件中没有 Id 字段，添加它
            if (!idUpdated)
            {
                writer.WriteString("Id", newId);
                _logger.Log(LogLevel.Info, $"✓ 添加 Solution.Id: {newId}", "SolutionManager");
            }

            writer.WriteEndObject();
            writer.Flush();

            // 写回文件
            var newJsonContent = System.Text.Encoding.UTF8.GetString(memoryStream.ToArray());
            File.WriteAllText(filePath, newJsonContent);

            _logger.Log(LogLevel.Success, $"JSON文件ID更新成功", "SolutionManager");
            return true;
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Error, $"更新JSON文件ID失败: {filePath}, 错误: {ex.Message}", "SolutionManager", ex);
            return false;
        }
    }

    /// <summary>
    /// 更新元数据（不涉及 Solution 文件）
    /// </summary>
    /// <param name="metadata">更新后的元数据</param>
    /// <remarks>
    /// 用于编辑解决方案名称、描述等元数据，不需要加载完整 Solution 文件。
    ///
    /// 更新内容：
    /// - Name、Description、ModifiedTime 等
    /// - Registry、Cache、Settings 中的元数据
    ///
    /// 设计原则（rule-002）：
    /// - 方法使用 PascalCase，动词开头
    /// - 命名符合视觉软件行业标准
    /// </remarks>
    public void UpdateMetadata(SolutionMetadata metadata)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        // 更新修改时间
        metadata.UpdateModifiedTime();

        // 更新注册表
        _registry.Register(metadata);

        // 更新缓存
        _cache.Set(metadata.Id, metadata);

        // 更新 KnownSolutions
        _settings.AddKnownSolution(metadata);
        MarkSettingsDirty();

        // ✅ 使用细粒度事件
        var updatedArgs = new SolutionMetadataEventArgs
        {
            Metadata = metadata,
            SolutionId = metadata.Id
        };
        SolutionUpdated?.Invoke(this, updatedArgs);

        _logger.Log(LogLevel.Success, $"更新解决方案元数据: {metadata.Name} (Id={metadata.Id})", "SolutionManager");
    }

    /// <summary>
    /// 注册元数据
    /// </summary>
    /// <param name="metadata">元数据对象</param>
    public void RegisterMetadata(SolutionMetadata metadata)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        // 注册到注册表
        _registry.Register(metadata);

        // 加入缓存
        _cache.Set(metadata.Id, metadata);

        _logger.Log(LogLevel.Info, $"注册元数据: {metadata.Name} (Id={metadata.Id})", "SolutionManager");
    }

    /// <summary>
    /// 注册已知解决方案（持久化到Settings）
    /// </summary>
    /// <param name="metadata">元数据对象</param>
    /// <remarks>
    /// 用于OpenSolution等场景，需要将解决方案添加到已知列表并持久化。
    /// </remarks>
    public void RegisterKnownSolution(SolutionMetadata metadata)
    {
        if (metadata == null)
            throw new ArgumentNullException(nameof(metadata));

        // 注册到注册表
        _registry.Register(metadata);

        // 加入缓存
        _cache.Set(metadata.Id, metadata);

        // 添加到已知解决方案
        _settings.AddKnownSolution(metadata);
        MarkSettingsDirty();

        _logger.Log(LogLevel.Success, $"注册已知解决方案: {metadata.Name} (Id={metadata.Id})", "SolutionManager");
    }

    /// <summary>
    /// 获取元数据（优先从缓存）
    /// </summary>
    /// <param name="solutionId">解决方案ID</param>
    /// <returns>元数据对象，不存在返回 null</returns>
    public SolutionMetadata? GetMetadata(string solutionId)
    {
        if (string.IsNullOrEmpty(solutionId))
            return null;

        // 先从缓存获取
        var metadata = _cache.Get(solutionId);
        if (metadata != null)
            return metadata;

        // 从注册表获取
        metadata = _registry.Get(solutionId);
        if (metadata != null)
        {
            // 加入缓存
            _cache.Set(solutionId, metadata);
            return metadata;
        }

        return null;
    }

    /// <summary>
    /// 获取所有元数据
    /// </summary>
    /// <returns>元数据列表</returns>
    public List<SolutionMetadata> GetAllMetadata()
    {
        // ✅ 修复：确保返回的元数据包含正确的 IsDefault 标志
        UpdateMetadataIsDefaultFlags();
        return _registry.GetAll();
    }

    /// <summary>
    /// 设置默认解决方案
    /// </summary>
    /// <param name="solutionId">解决方案ID</param>
    public void SetDefaultSolution(string solutionId)
    {
        if (string.IsNullOrEmpty(solutionId))
        {
            _settings.DefaultSolutionId = "";
            UpdateMetadataIsDefaultFlags();
            ForceSaveSettings();
            _logger.Log(LogLevel.Info, "取消默认解决方案", "SolutionManager");
            return;
        }

        var metadata = GetMetadata(solutionId);
        if (metadata != null)
        {
            _settings.DefaultSolutionId = solutionId;
            UpdateMetadataIsDefaultFlags();
            ForceSaveSettings();
            _logger.Log(LogLevel.Success,
                $"设置默认解决方案: {metadata.Name}", "SolutionManager");
        }
        else
        {
            _logger.Log(LogLevel.Warning,
                $"设置默认解决方案失败：找不到解决方案（ID={solutionId}）", "SolutionManager");
        }
    }

    /// <summary>
    /// 获取默认解决方案元数据
    /// </summary>
    /// <returns>元数据对象，如果不存在则返回 null</returns>
    public SolutionMetadata? GetDefaultSolutionMetadata()
    {
        var defaultId = _settings.DefaultSolutionId;
        if (string.IsNullOrEmpty(defaultId))
            return null;

        return GetMetadata(defaultId);
    }

    /// <summary>
    /// 更新所有元数据的 IsDefault 标志
    /// </summary>
    /// <remarks>
    /// 修复说明（2026-03-23）：
    /// 修复前：调用 GetAll() 获取克隆对象并修改，导致修改无效
    /// 修复后：调用 UpdateAllIsDefaultFlags() 直接修改 Registry 内部原始对象
    /// </remarks>
    private void UpdateMetadataIsDefaultFlags()
    {
        var defaultId = _settings.DefaultSolutionId;

        // ✅ 修复：调用 Registry 的批量更新方法
        // 直接修改 Registry 内部字典中的原始对象
        // 不需要修改克隆对象（克隆对象是临时的）
        _registry.UpdateAllIsDefaultFlags(defaultId);
    }

    /// <summary>
    /// 刷新元数据（从KnownSolutions加载并验证文件存在性）
    /// </summary>
    /// <remarks>
    /// 改进（2026-03-17）：添加文件名变化检测
    /// - 自动检测用户直接在文件系统中修改的文件名
    /// - 通过ID匹配查找重命名的文件
    /// - 自动更新 KnownSolutions、Registry、Cache 中的路径
    /// - 记录日志并触发事件通知UI层
    /// </remarks>
    public void RefreshMetadata()
    {
        _logger.Log(LogLevel.Info, "开始刷新元数据：从KnownSolutions加载", "SolutionManager");

        // 从Settings加载已知解决方案
        var knownSolutions = _settings.GetKnownSolutions();
        var validMetadataList = new List<SolutionMetadata>();
        int renamedCount = 0;
        int missingCount = 0;

        foreach (var metadata in knownSolutions)
        {
            // ==================== 步骤1：检测文件名变化 ====================
            if (!string.IsNullOrEmpty(metadata.FilePath) && !_repository.Exists(metadata.FilePath))
            {
                // 文件不存在，尝试查找重命名的文件
                var newFilePath = TryFindRenamedFile(metadata);

                if (newFilePath != null)
                {
                    // ✅ 步骤2：更新元数据对象
                    var oldName = metadata.Name;
                    var oldFilePath = metadata.FilePath;

                    metadata.FilePath = newFilePath;
                    metadata.Name = Path.GetFileNameWithoutExtension(newFilePath);
                    metadata.DirectoryPath = Path.GetDirectoryName(newFilePath) ?? "";
                    metadata.ModifiedTime = DateTime.Now;

                    // ✅ 步骤3：更新 KnownSolutions（后面统一保存）
                    _settings.AddKnownSolution(metadata);

                    // ✅ 步骤4：更新当前解决方案（如果已加载）
                    if (CurrentSolution?.Id == metadata.Id)
                    {
                        CurrentSolution.FilePath = newFilePath;
                    }

                    // ✅ 步骤5：记录日志
                    _logger.Log(LogLevel.Warning,
                        $"检测到文件名变化: {oldName} → {metadata.Name} (Id={metadata.Id})",
                        "SolutionManager");
                    _logger.Log(LogLevel.Info,
                        $"文件路径: {oldFilePath} → {newFilePath}",
                        "SolutionManager");

                    renamedCount++;
                }
                else
                {
                    // 文件确实丢失，记录警告
                    _logger.Log(LogLevel.Warning,
                        $"解决方案文件丢失: {metadata.Name} (FilePath={metadata.FilePath}, Id={metadata.Id})",
                        "SolutionManager");
                    missingCount++;
                    continue; // 跳过此元数据
                }
            }

            // 添加到有效列表
            if (!string.IsNullOrEmpty(metadata.FilePath))
            {
                validMetadataList.Add(metadata);
            }
        }

        // ✅ 步骤6：注册元数据到 Registry
        int registeredCount = _registry.RegisterBatch(validMetadataList);

        // ✅ 步骤7：更新 IsDefault 标志（根据 DefaultSolutionId）
        UpdateMetadataIsDefaultFlags();

        // ✅ 步骤8：加入缓存
        foreach (var metadata in validMetadataList)
        {
            _cache.Set(metadata.Id, metadata);
        }

        // ✅ 步骤9：持久化更新 settings.json（如果有重命名）
        if (renamedCount > 0)
        {
            SaveSettings();
            _logger.Log(LogLevel.Success,
                $"已自动更新 {renamedCount} 个重命名的解决方案",
                "SolutionManager");
        }

        _logger.Log(LogLevel.Success,
            $"刷新元数据完成: 已知 {knownSolutions.Count} 个, 有效 {validMetadataList.Count} 个, 注册 {registeredCount} 个, 重命名 {renamedCount} 个, 丢失 {missingCount} 个",
            "SolutionManager");

        // ✅ 步骤10：触发事件通知 UI 层
        MetadataRefreshed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 尝试查找重命名的文件
    /// </summary>
    /// <param name="metadata">元数据对象</param>
    /// <returns>新文件路径，未找到返回 null</returns>
    /// <remarks>
    /// 查找策略：
    /// 1. 在同目录下查找所有 .solution 文件
    /// 2. 快速读取元数据（不加载完整 Solution）
    /// 3. 比较 ID 是否匹配
    /// 4. 找到匹配则返回新文件路径
    ///
    /// 性能优化：
    /// - 使用 LoadMetadata() 仅读取元数据，不加载完整对象
    /// - 文件数量较少时性能影响可忽略
    /// - 文件数量较多时（>100个）建议用户使用重命名功能
    /// </remarks>
    private string? TryFindRenamedFile(SolutionMetadata metadata)
    {
        if (string.IsNullOrEmpty(metadata.FilePath))
            return null;

        var directory = Path.GetDirectoryName(metadata.FilePath);
        if (string.IsNullOrEmpty(directory) || !Directory.Exists(directory))
            return null;

        try
        {
            // 在同目录下查找所有 .solution 文件
            var solutionFiles = Directory.GetFiles(directory, "*.solution", SearchOption.TopDirectoryOnly);

            _logger.Log(LogLevel.Info,
                $"在目录 {directory} 中查找 {solutionFiles.Length} 个 .solution 文件，匹配 ID={metadata.Id}",
                "SolutionManager");

            foreach (var filePath in solutionFiles)
            {
                // 快速读取元数据（不加载完整 Solution）
                var fileMetadata = _repository.LoadMetadata(filePath);

                if (fileMetadata?.Id == metadata.Id)
                {
                    // 找到了！ID 匹配
                    _logger.Log(LogLevel.Info,
                        $"找到匹配文件: {filePath} (ID={metadata.Id})",
                        "SolutionManager");
                    return filePath;
                }
            }

            _logger.Log(LogLevel.Info,
                $"未找到匹配文件: ID={metadata.Id}",
                "SolutionManager");
        }
        catch (Exception ex)
        {
            _logger.Log(LogLevel.Warning,
                $"查找重命名文件失败: {ex.Message}",
                "SolutionManager");
        }

        return null;
    }

    /// <summary>
    /// 扫描指定目录并注册元数据
    /// </summary>
    /// <param name="directoryPath">目录路径</param>
    public void ScanDirectory(string directoryPath)
    {
        _logger.Log(LogLevel.Info, $"开始扫描目录: {directoryPath}", "SolutionManager");

        // 扫描目录
        var metadataList = _repository.ScanDirectory(directoryPath);

        // 注册元数据
        int registeredCount = _registry.RegisterBatch(metadataList);

        // 加入缓存
        foreach (var metadata in metadataList)
        {
            _cache.Set(metadata.Id, metadata);
        }

        _logger.Log(LogLevel.Success, $"扫描目录完成: 扫描到 {metadataList.Count} 个解决方案, 注册了 {registeredCount} 个", "SolutionManager");

        // ✅ 触发事件
        MetadataRefreshed?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>
    /// 批量加载元数据
    /// </summary>
    /// <param name="filePaths">文件路径列表</param>
    /// <returns>元数据列表</returns>
    public List<SolutionMetadata> LoadMetadataBatch(System.Collections.Generic.IEnumerable<string> filePaths)
    {
        _logger.Log(LogLevel.Info, $"开始批量加载元数据: 数量={filePaths.Count()}", "SolutionManager");

        // ✅ 加载元数据（从文件名推断基本元数据）
        var metadataList = _repository.LoadMetadataBatch(filePaths);

        // ✅ 补充：从 KnownSolutions 获取详细元数据（Description 等）
        foreach (var metadata in metadataList)
        {
            var knownMetadata = _settings.GetKnownSolutions()
                .FirstOrDefault(m => m.Id == metadata.Id);

            if (knownMetadata != null)
            {
                // 使用 KnownSolutions 中的详细信息覆盖
                metadata.Name = knownMetadata.Name;  // 使用用户自定义的名称
                metadata.Description = knownMetadata.Description;
                metadata.CreatedTime = knownMetadata.CreatedTime;
                metadata.ModifiedTime = knownMetadata.ModifiedTime;
                metadata.LastAccessTime = knownMetadata.LastAccessTime;

                _logger.Log(LogLevel.Info,
                    $"从 KnownSolutions 补充元数据: {metadata.Name} (Id={metadata.Id})",
                    "SolutionManager");
            }
        }

        // 注册元数据
        int registeredCount = _registry.RegisterBatch(metadataList);

        // 加入缓存
        foreach (var metadata in metadataList)
        {
            _cache.Set(metadata.Id, metadata);
        }

        _logger.Log(LogLevel.Success, $"批量加载元数据完成: 加载 {metadataList.Count} 个, 注册 {registeredCount} 个", "SolutionManager");

        return metadataList;
    }

    /// <summary>
    /// 工作流管理
    /// </summary>
    public Workflow? AddWorkflow(string name, string description = "")
    {
        if (CurrentSolution == null)
        {
            _logger.Log(LogLevel.Warning, "添加工作流失败：没有当前解决方案", "SolutionManager");
            return null;
        }

        var workflow = CurrentSolution.AddWorkflow(name);
        WorkflowAdded?.Invoke(this, workflow);
        return workflow;
    }

    public bool RemoveWorkflow(string workflowId)
    {
        if (CurrentSolution == null)
        {
            _logger.Log(LogLevel.Warning, "移除工作流失败：没有当前解决方案", "SolutionManager");
            return false;
        }

        var workflow = CurrentSolution.GetWorkflow(workflowId);
        if (workflow == null)
        {
            _logger.Log(LogLevel.Warning, $"移除工作流失败：工作流不存在: {workflowId}", "SolutionManager");
            return false;
        }

        bool removed = CurrentSolution.RemoveWorkflow(workflowId);
        if (removed)
        {
            WorkflowRemoved?.Invoke(this, workflow);
        }
        return removed;
    }

    /// <summary>
    /// 全局变量管理
    /// </summary>
    public GlobalVariable? AddGlobalVariable(string name, object? value, string type = "String")
    {
        if (CurrentSolution == null)
        {
            _logger.Log(LogLevel.Warning, "添加全局变量失败：没有当前解决方案", "SolutionManager");
            return null;
        }

        var globalVariable = CurrentSolution.AddGlobalVariable(name, value, type);
        GlobalVariableAdded?.Invoke(this, globalVariable);
        return globalVariable;
    }

    public bool RemoveGlobalVariable(string name)
    {
        if (CurrentSolution == null)
        {
            _logger.Log(LogLevel.Warning, "移除全局变量失败：没有当前解决方案", "SolutionManager");
            return false;
        }

        return CurrentSolution.RemoveGlobalVariable(name);
    }

    /// <summary>
    /// 设备管理
    /// </summary>
    public Device? AddDevice(string name, DeviceType type)
    {
        if (CurrentSolution == null)
        {
            _logger.Log(LogLevel.Warning, "添加设备失败：没有当前解决方案", "SolutionManager");
            return null;
        }

        var device = CurrentSolution.AddDevice(name, type);
        DeviceAdded?.Invoke(this, device);
        return device;
    }

    public bool RemoveDevice(string deviceId)
    {
        if (CurrentSolution == null)
        {
            _logger.Log(LogLevel.Warning, "移除设备失败：没有当前解决方案", "SolutionManager");
            return false;
        }

        return CurrentSolution.RemoveDevice(deviceId);
    }

    /// <summary>
    /// 通讯配置管理
    /// </summary>
    public Communication? AddCommunication(string name, CommunicationType type)
    {
        if (CurrentSolution == null)
        {
            _logger.Log(LogLevel.Warning, "添加通讯配置失败：没有当前解决方案", "SolutionManager");
            return null;
        }

        var communication = CurrentSolution.AddCommunication(name, type);
        CommunicationAdded?.Invoke(this, communication);
        return communication;
    }

    public bool RemoveCommunication(string communicationId)
    {
        if (CurrentSolution == null)
        {
            _logger.Log(LogLevel.Warning, "移除通讯配置失败：没有当前解决方案", "SolutionManager");
            return false;
        }

        return CurrentSolution.RemoveCommunication(communicationId);
    }

    /// <summary>
    /// 设置当前解决方案
    /// </summary>
    public void SetCurrentSolution(Solution solution)
    {
        if (solution == null)
            throw new ArgumentNullException(nameof(solution));

        // 诊断日志：开始设置
        _logger.Log(LogLevel.Info, 
            $"[诊断] 开始设置当前解决方案: Solution.Id={solution.Id}", 
            "SolutionManager");

        // 直接设置，不重复加载（避免违背惰性加载）
        CurrentSolution = solution;
        CurrentFilePath = solution.FilePath;

        // 更新设置
        _settings.CurrentSolutionId = solution.Id;
        MarkSettingsDirty();

        // 触发事件（同时触发两个事件，与 OpenSolution 保持一致）
        SolutionOpened?.Invoke(this, CurrentSolution);

        // 诊断日志：查找元数据前的状态
        _logger.Log(LogLevel.Info, 
            $"[诊断] 缓存数量: {_cache.Count}, 注册表数量: {_registry.GetAll().Count}", 
            "SolutionManager");

        // 触发细粒度事件
        var metadata = _cache.Get(solution.Id) ?? _registry.Get(solution.Id);
        if (metadata == null)
        {
            // 诊断日志：列出所有已知的元数据
            var allMetadata = _registry.GetAll();
            _logger.Log(LogLevel.Warning, 
                $"[诊断] 未找到元数据，Solution.Id={solution.Id}", 
                "SolutionManager");
            
            foreach (var m in allMetadata)
            {
                _logger.Log(LogLevel.Info, 
                    $"[诊断] 已知元数据: Id={m.Id}, Name={m.Name}, FilePath={m.FilePath}", 
                    "SolutionManager");
            }

            // 如果元数据不存在，从文件路径创建临时元数据
            var filePath = solution.FilePath;
            metadata = !string.IsNullOrEmpty(filePath) 
                ? _repository.LoadMetadata(filePath) 
                : SolutionMetadata.Create(solution.Id);
            
            if (metadata == null)
            {
                metadata = new SolutionMetadata
                {
                    Id = solution.Id,
                    Name = !string.IsNullOrEmpty(filePath) ? Path.GetFileNameWithoutExtension(filePath) : solution.Id,
                    Description = "",
                    FilePath = filePath ?? ""
                };
            }
            
            // ✅ 关键修复：将临时元数据添加到缓存
            _cache.Set(solution.Id, metadata);
            
            _logger.Log(LogLevel.Warning, 
                $"创建临时元数据并缓存: Solution.Id={solution.Id}", 
                "SolutionManager");
        }
        else
        {
            _logger.Log(LogLevel.Info, 
                $"[诊断] 找到元数据: Id={metadata.Id}, Name={metadata.Name}, FilePath={metadata.FilePath}", 
                "SolutionManager");
        }

        var currentSolutionArgs = new SolutionMetadataEventArgs
        {
            Metadata = metadata,
            SolutionId = metadata.Id
        };
        CurrentSolutionChanged?.Invoke(this, currentSolutionArgs);

        _logger.Log(LogLevel.Info, 
            $"设置当前解决方案成功: Id={CurrentSolution.Id}", 
            "SolutionManager");
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        _logger.Log(LogLevel.Info, $"开始加载设置文件: {_configFilePath}", "SolutionManager");

        _settings.Load(_configFilePath);

        _logger.Log(LogLevel.Success, $"设置加载完成: {_settings.GetStatistics()}", "SolutionManager");
    }

    /// <summary>
    /// 标记设置为脏（需要保存）
    /// </summary>
    public void MarkSettingsDirty()
    {
        if (!_isSettingsDirty)
        {
            _logger.Log(LogLevel.Info, "标记设置为脏（需要保存）", "SolutionManager");
            _isSettingsDirty = true;
        }
    }

    /// <summary>
    /// 保存设置（带脏标记检查）
    /// </summary>
    private void SaveSettings()
    {
        // ✅ 日志监控：生成唯一调用ID
        long callId = System.Threading.Interlocked.Increment(ref _saveSettingsCallCount);

        if (!_isSettingsDirty)
        {
            _logger.Log(LogLevel.Info, $"[SaveSettings #{callId}] 设置未变更，跳过保存\n堆栈: {GetShortStackTrace(3)}", "SolutionManager");
            return;
        }

        _logger.Log(LogLevel.Info, $"[SaveSettings #{callId}] 开始保存设置文件: {_configFilePath}\n已知解决方案数量: {_settings.GetKnownSolutions().Count}\n堆栈: {GetShortStackTrace(3)}", "SolutionManager");
        _settings.Save(_configFilePath);
        _isSettingsDirty = false;
        _logger.Log(LogLevel.Success, $"[SaveSettings #{callId}] 设置保存完成", "SolutionManager");
    }

    /// <summary>
    /// 保存设置操作调用计数器（用于日志监控）
    /// </summary>
    private static long _saveSettingsCallCount = 0;

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
    /// 立即保存设置（强制保存，忽略脏标记）
    /// </summary>
    private void ForceSaveSettings()
    {
        _logger.Log(LogLevel.Info, $"强制保存设置文件: {_configFilePath}", "SolutionManager");
        _settings.Save(_configFilePath);
        _isSettingsDirty = false;
        _logger.Log(LogLevel.Success, "设置强制保存完成", "SolutionManager");
    }

    /// <summary>
    /// 保存用户设置（公开接口，用于窗口关闭时调用）
    /// </summary>
    public void SaveUserSettings()
    {
        if (_isSettingsDirty)
        {
            SaveSettings();
        }
        else
        {
            _logger.Log(LogLevel.Info, "设置未变更，无需保存", "SolutionManager");
        }
    }

    /// <summary>
    /// 获取统计信息
    /// </summary>
    /// <returns>统计信息字符串</returns>
    public string GetStatistics()
    {
        var currentSolutionInfo = CurrentSolution != null
            ? $"Id={CurrentSolution.Id}"
            : "无";

        return $"解决方案管理器统计:\n" +
               $"  - 注册表数量: {_registry.Count}\n" +
               $"  - 缓存数量: {_cache.Count}, 命中率: {_cache.HitRate:P2}\n" +
               $"  - 当前解决方案: {currentSolutionInfo}\n" +
               $"  - 设置统计: {_settings.GetStatistics()}";
    }

}
