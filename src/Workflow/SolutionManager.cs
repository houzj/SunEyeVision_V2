using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Core.Services.Serialization;
using SunEyeVision.Workflow.Persistence;
using System.Text.Json.Nodes;

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
    private string _solutionsDirectory;
    private string _configFilePath;
    private readonly ILogger _logger;

    private SolutionRegistry _registry;
    private SolutionCache _cache;
    private SolutionRepository _repository;
    private SolutionSettings _settings;

    private string _dataRoot;
    public SolutionSession? Session { get; private set; }
    private string? _saveError;
    public string? LastSaveWarning { get; private set; }
    public string DataRoot => _dataRoot;
    public string ConfigurationPath => _configFilePath;
    public string? LastError => _saveError ?? _repository.LastError;
    public bool HasUnsavedChanges => Session?.IsDirty == true;
    private static string Fingerprint(Solution solution) => SolutionSession.Serialize(solution);
    private void CaptureSavedState() => Session?.CaptureBaseline();

    private bool _isSettingsDirty = false;  // 脏标记：设置是否需要保存
    private long _solutionAddedEventCount = 0;  // SolutionAdded事件计数（用于日志监控）

    /// <summary>
    /// 当前解决方案
    /// </summary>
    public Solution? CurrentSolution
    {
        get => Session?.Document;
        private set => Session = value == null ? null : new SolutionSession(value);
    }

    /// <summary>
    /// 当前文件路径
    /// </summary>
    public string? CurrentFilePath
    {
        get => Session?.FilePath;
        private set { if (Session != null) Session.Document.FilePath = value; }
    }

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
    public event EventHandler<Solution>? SolutionSaving;

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
    public SolutionManager(ApplicationDataPaths paths) : this(paths.SolutionsDirectory, paths.ConfigFile, paths.RecoveryDirectory, paths.Root)
    {
        if (!File.Exists(paths.ConfigFile)) ForceSaveSettings();
        var marker = JsonNode.Parse(File.ReadAllText(paths.MarkerFile))!.AsObject();
        marker["configRequired"] = true;
        AtomicJsonFile.Write(paths.MarkerFile, marker.ToJsonString());
    }

    public SolutionManager(string solutionsDirectory, string? configurationPath = null, string? recoveryDirectory = null, string? dataRoot = null)
    {
        _solutionsDirectory = solutionsDirectory;
        _dataRoot = Path.GetFullPath(dataRoot ?? solutionsDirectory);
        _configFilePath = configurationPath ?? Path.Combine(_dataRoot, "Config", "user-settings.json");
        _logger = VisionLogger.Instance;

        // 初始化组件
        _registry = new SolutionRegistry();
        _cache = new SolutionCache(maxSize: 100, expirationTime: TimeSpan.FromMinutes(30));
        _repository = new SolutionRepository(recoveryDirectory ?? Path.Combine(_dataRoot, ".recovery"));
        _settings = new SolutionSettings();

        // ✅ 设置基准路径（用于相对路径转换）
        _settings.SetBasePath(_dataRoot);

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
        AtomicJsonFile.ValidateSolutionName(metadata.Name);
        var desiredPath = string.IsNullOrWhiteSpace(metadata.FilePath) ? Path.Combine(metadata.DirectoryPath, metadata.Name + ".solution") : metadata.FilePath;
        if (File.Exists(desiredPath)) throw new IOException("目标方案已存在，不会覆盖：" + desiredPath);

        // 创建新的 Solution 对象（纯数据模型，自动创建默认工作流）
        var solution = Solution.Create();
        solution.Version = metadata.Version;
        solution.Name = metadata.Name;
        solution.Description = metadata.Description;
        // Name 和 Description 由 SolutionMetadata 统一管理，不再同步到 Solution

        // 添加默认工作流
        var defaultWorkflow = solution.AddWorkflow("工作流1");

        // 构建文件路径
        var filePath = string.IsNullOrEmpty(metadata.FilePath)
            ? Path.Combine(metadata.DirectoryPath, $"{metadata.Name}.solution")
            : metadata.FilePath;

        solution.FilePath = filePath;

        // ✅ 文件级操作：保存到文件
        bool success = _repository.Save(solution, filePath, overwrite: false);
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
        try { ForceSaveSettings(); }
        catch
        {
            _registry.Unregister(newMetadata.Id); _cache.Invalidate(newMetadata.Id);
            _settings.RemoveKnownSolution(newMetadata.Id);
            // This file was created by this operation, never an existing user file.
            if (File.Exists(filePath) && _repository.LoadMetadata(filePath)?.Id == newMetadata.Id) File.Delete(filePath);
            throw;
        }

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

        // 加载解决方案
        var solution = _repository.Load(filePath);
        if (solution == null)
        {
            _logger.Log(LogLevel.Error, $"打开解决方案失败：加载失败: {filePath}", "SolutionManager");
            return null;
        }

        if (HasUnsavedChanges)
        {
            throw new InvalidOperationException("当前方案有未保存修改，请先保存或通过受保护的界面切换。");
        }
        SetCurrentSolution(solution);
        return solution;

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
        _saveError = null;
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.Log(LogLevel.Info,
            $"[LoadSolutionOnly] 开始加载解决方案: FilePath={filePath}, thread={Environment.CurrentManagedThreadId}", "SolutionManager");

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
            _logger.Log(LogLevel.Info,
                $"[LoadSolutionOnly] Repository.Load begin | elapsedMs={stopwatch.ElapsedMilliseconds}", "SolutionManager");
            var solution = _repository.Load(filePath);
            _logger.Log(LogLevel.Info,
                $"[LoadSolutionOnly] Repository.Load returned | null={solution is null}, elapsedMs={stopwatch.ElapsedMilliseconds}", "SolutionManager");

            if (solution == null)
            {
                _logger.Log(LogLevel.Error, $"[LoadSolutionOnly] 加载失败：Repository.Load 返回 null: {filePath}", "SolutionManager");
                return null;
            }

            _logger.Log(LogLevel.Success,
                $"[LoadSolutionOnly] 加载成功: Solution.Id={solution.Id}, Workflows={solution.Workflows.Count}, GlobalVariables={solution.GlobalVariables?.Count ?? 0}, Devices={solution.Devices?.Count ?? 0}, elapsedMs={stopwatch.ElapsedMilliseconds}",
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
            SolutionSaving?.Invoke(this, solution);
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
    public bool SaveSolution(string? filePath = null) => SaveCurrentDocument(filePath).FileWritten;

    public SolutionSaveResult SaveCurrentDocument(string? filePath = null, bool overwrite = true)
    {
        _saveError = null; LastSaveWarning = null;
        var session = Session;
        if (session == null) return SaveFailure("没有当前内存方案。");
        var savePath = filePath ?? session.FilePath;
        if (string.IsNullOrWhiteSpace(savePath)) return SaveFailure("尚未选择方案保存位置。");
        string written;
        SolutionSnapshot snapshot;
        try
        {
            savePath = Path.GetFullPath(savePath);
            SolutionSaving?.Invoke(this, session.Document);
            if (!ReferenceEquals(Session, session)) return SaveFailure("保存准备期间方案已切换，请重试。");
            snapshot = SolutionSnapshot.Prepare(session.Document, savePath);
            written = snapshot.Json;
            if (!_repository.WriteSnapshot(snapshot, overwrite).FileWritten)
                return SaveFailure(_repository.LastError ?? "方案文件写入失败。");
        }
        catch (Exception ex) { return SaveFailure(ex.Message); }
        if (session.IsUntitled && (string.IsNullOrWhiteSpace(session.Document.Name) || session.Document.Name == "未命名方案"))
            session.Document.Name = Path.GetFileNameWithoutExtension(savePath);
        session.CommitSave(savePath, written);
        var warnings = new List<string>();
        try
        {
            var document = session.Document;
            var metadata = new SolutionMetadata { Id = document.Id, Name = document.Name, Description = document.Description, Version = document.Version,
                FilePath = savePath, DirectoryPath = Path.GetDirectoryName(savePath) ?? "", CreatedTime = File.GetCreationTime(savePath), ModifiedTime = DateTime.Now };
            metadata.UpdateStatistics(document);
            _registry.Register(metadata); _cache.Set(metadata.Id, metadata); _settings.AddKnownSolution(metadata);
            _settings.CurrentSolutionId = document.Id; MarkSettingsDirty();
            try { SolutionUpdated?.Invoke(this, new SolutionMetadataEventArgs { Metadata = metadata, SolutionId = metadata.Id }); }
            catch (Exception ex) { warnings.Add("方案记录通知失败：" + ex.Message); }
        }
        catch (Exception ex) { warnings.Add("兼容目录更新失败：" + ex.Message); }
        try { SolutionSaved?.Invoke(this, session.Document); }
        catch (Exception ex) { warnings.Add("保存通知失败：" + ex.Message); }
        LastSaveWarning = warnings.Count == 0 ? null : string.Join("；", warnings);
        return new SolutionSaveResult(true, Warning: LastSaveWarning) { WrittenSnapshot = snapshot };
    }
    private SolutionSaveResult SaveFailure(string error) { _saveError = error; return new(false, error); }
    public Solution CreateUntitledSolution()
    {
        var previousRecentId = _settings.CurrentSolutionId;
        var document = Solution.Create(); document.Name = "未命名方案";
        document.Workflows.Add(new Workflow(Guid.NewGuid().ToString(), "工作流1"));
        try { SetCurrentSolution(document, registerInCatalog: false); CaptureSavedState(); }
        finally { _settings.CurrentSolutionId = previousRecentId; }
        return document;
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
    public void CloseSolution(bool saveChanges = true)
    {
        if (saveChanges && HasUnsavedChanges && (string.IsNullOrEmpty(CurrentFilePath) || !SaveSolution()))
            throw new IOException("保存失败，方案保持打开：" + LastError);
        CurrentSolution = null;
        CurrentFilePath = null;
        Session = null;
        _settings.CurrentSolutionId = "";
        MarkSettingsDirty();
        SolutionClosed?.Invoke(this, EventArgs.Empty);
        CurrentSolutionChanged?.Invoke(this, new SolutionMetadataEventArgs { SolutionId = "" });
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

        if (isCurrentSolution) CloseSolution(saveChanges: false);
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
        AtomicJsonFile.ValidateSolutionName(newName);
        var previous = GetMetadata(solutionId)?.Clone();
        if (previous == null || !File.Exists(previous.FilePath)) return false;
        if (previous.Name == newName) return true;
        var target = Path.Combine(Path.GetDirectoryName(previous.FilePath)!, newName + ".solution");
        if (File.Exists(target)) return false;
        var original = File.ReadAllText(previous.FilePath);
        var document = JsonNode.Parse(original)!.AsObject();
        document["Name"] = newName;
        var updated = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        var currentName = CurrentSolution?.Name;
        var moved = false;
        try
        {
            File.Move(previous.FilePath, target); moved = true;
            AtomicJsonFile.Write(target, updated);
            var metadata = previous.Clone();
            metadata.Name = newName; metadata.FilePath = target;
            metadata.DirectoryPath = Path.GetDirectoryName(target)!; metadata.ModifiedTime = DateTime.Now;
            var oldRecovery = _repository.GetRecoveryPath(previous.FilePath);
            if (File.Exists(oldRecovery)) AtomicJsonFile.Write(_repository.GetRecoveryPath(target), File.ReadAllText(oldRecovery));
            _registry.Register(metadata); _cache.Set(metadata.Id, metadata); _settings.AddKnownSolution(metadata);
            ForceSaveSettings();
            if (CurrentSolution?.Id == solutionId)
            { CurrentFilePath = target; CurrentSolution.FilePath = target; CurrentSolution.Name = newName; }
            SolutionRenamed?.Invoke(this, new SolutionMetadataEventArgs { Metadata = metadata, SolutionId = metadata.Id, OldName = previous.Name, NewName = newName });
            return true;
        }
        catch (Exception ex)
        {
            if (moved && File.Exists(target) && !File.Exists(previous.FilePath))
            { AtomicJsonFile.Write(target, original); File.Move(target, previous.FilePath); }
            _registry.Register(previous); _cache.Set(previous.Id, previous); _settings.AddKnownSolution(previous);
            if (CurrentSolution?.Id == solutionId)
            { CurrentFilePath = previous.FilePath; CurrentSolution.FilePath = previous.FilePath; CurrentSolution.Name = currentName ?? previous.Name; }
            _logger.Log(LogLevel.Error, "重命名未完成，原方案保留：" + ex.Message, "SolutionManager", ex);
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
    private SolutionMetadata? CopyOrSaveAsSolution(string sourcePath, string newName, string? newDescription,
        string targetFilePath, bool switchToNewSolution)
    {
        AtomicJsonFile.ValidateSolutionName(newName);
        if (AtomicJsonFile.SamePath(sourcePath, targetFilePath)) throw new IOException("副本或另存目标不能是源方案自身。");
        if (File.Exists(targetFilePath)) throw new IOException("目标方案已存在，不会覆盖：" + targetFilePath);
        var current = CurrentSolution != null && CurrentFilePath != null && AtomicJsonFile.SamePath(CurrentFilePath, sourcePath);
        var json = current ? Fingerprint(CurrentSolution!) : File.ReadAllText(sourcePath);
        json = SolutionResourceResolver.RebaseCopy(json, Path.GetDirectoryName(Path.GetFullPath(sourcePath))!, Path.GetDirectoryName(Path.GetFullPath(targetFilePath))!);
        var document = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidDataException("源方案格式错误。");
        var id = Guid.NewGuid().ToString();
        document["Id"] = id;
        document["Name"] = newName;
        document["Description"] = newDescription ?? (current ? CurrentSolution!.Description : document["Description"]?.GetValue<string>() ?? "");
        var updated = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        AtomicJsonFile.Write(targetFilePath, updated, overwrite: false);
        try
        {
            var metadata = _repository.LoadMetadata(targetFilePath) ?? throw new InvalidDataException("新方案元数据无法读取。");
            Solution? next = null;
            if (switchToNewSolution) next = _repository.Load(targetFilePath) ?? throw new InvalidDataException("新方案加载失败：" + LastError);
            RegisterKnownSolution(metadata);
            ForceSaveSettings();
            if (next != null) SetCurrentSolution(next);
            return metadata;
        }
        catch
        {
            // Roll back only the new file created by this operation, never a pre-existing target.
            if (File.Exists(targetFilePath) && File.ReadAllText(targetFilePath) == updated) File.Delete(targetFilePath);
            _registry.Unregister(id); _cache.Invalidate(id); _settings.RemoveKnownSolution(id);
            throw;
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
    public bool HasPreviousSave(string solutionId)
    {
        var metadata = GetMetadata(solutionId);
        return metadata != null && File.Exists(_repository.GetRecoveryPath(metadata.FilePath));
    }

    public Solution RestorePreviousSave(string solutionId)
    {
        var metadata = GetMetadata(solutionId) ?? throw new InvalidOperationException("方案记录不存在。");
        var recovery = _repository.GetRecoveryPath(metadata.FilePath);
        var restored = _repository.Load(recovery, Path.GetDirectoryName(Path.GetFullPath(metadata.FilePath))!)
            ?? throw new InvalidDataException("上一保存版本无法恢复：" + LastError);
        if (restored.Id != metadata.Id) throw new InvalidDataException("恢复文件身份不匹配，不会覆盖。");
        AtomicJsonFile.Write(metadata.FilePath, File.ReadAllText(recovery));
        restored.FilePath = metadata.FilePath;
        return restored;
    }

    public void UpdateMetadata(SolutionMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        AtomicJsonFile.ValidateSolutionName(metadata.Name);
        if (!File.Exists(metadata.FilePath)) throw new FileNotFoundException("方案文件失效，请先重新定位。", metadata.FilePath);
        var previous = GetMetadata(metadata.Id)?.Clone();
        var original = File.ReadAllText(metadata.FilePath);
        var document = JsonNode.Parse(original)!.AsObject();
        document["Name"] = metadata.Name; document["Description"] = metadata.Description;
        var updated = document.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
        AtomicJsonFile.Write(_repository.GetRecoveryPath(metadata.FilePath), original);
        AtomicJsonFile.Write(metadata.FilePath, updated);
        try
        {
            _registry.Register(metadata); _cache.Set(metadata.Id, metadata); _settings.AddKnownSolution(metadata);
            ForceSaveSettings();
        }
        catch
        {
            if (File.Exists(metadata.FilePath) && File.ReadAllText(metadata.FilePath) == updated) AtomicJsonFile.Write(metadata.FilePath, original);
            if (previous != null) { _registry.Register(previous); _cache.Set(previous.Id, previous); _settings.AddKnownSolution(previous); }
            throw;
        }
        if (CurrentSolution?.Id == metadata.Id) { CurrentSolution.Name = metadata.Name; CurrentSolution.Description = metadata.Description; }
        SolutionUpdated?.Invoke(this, new SolutionMetadataEventArgs { Metadata = metadata, SolutionId = metadata.Id });
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
        var sameFile = _registry.GetAll().FirstOrDefault(m => AtomicJsonFile.SamePath(m.FilePath, metadata.FilePath));
        if (sameFile != null && sameFile.Id != metadata.Id) throw new InvalidDataException("同一文件的方案身份发生变化，请移除旧记录后重新添加。");
        var sameId = _registry.Get(metadata.Id);
        if (sameId != null && !AtomicJsonFile.SamePath(sameId.FilePath, metadata.FilePath))
            throw new InvalidDataException("相同方案 ID 已存在于其他路径，请选择替换记录或创建独立副本。");
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

        // 触发 SolutionAdded 事件，让 UI 列表（卡片/最近打开/搜索过滤）即时刷新。
        // 与 CreateNewSolution/CopyOrSaveAsSolution 统一：注册新已知方案即通知 ViewModel。
        // ViewModel 端 OnSolutionAdded 已做 Any 去重，无需担心重复 Add。
        var addedArgs = new SolutionMetadataEventArgs
        {
            Metadata = metadata,
            SolutionId = metadata.Id
        };
        SolutionAdded?.Invoke(this, addedArgs);
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
    public void ReplaceKnownSolution(SolutionMetadata metadata)
    {
        if (CurrentSolution?.Id == metadata.Id) throw new InvalidOperationException("不能替换当前打开的方案，请先切换。");
        var previous = GetMetadata(metadata.Id)?.Clone();
        var previousDefault = _settings.DefaultSolutionId;
        _registry.Unregister(metadata.Id); _cache.Invalidate(metadata.Id); _settings.RemoveKnownSolution(metadata.Id);
        try { RegisterKnownSolution(metadata); _settings.DefaultSolutionId = previousDefault; ForceSaveSettings(); }
        catch
        {
            _registry.Unregister(metadata.Id); _cache.Invalidate(metadata.Id); _settings.RemoveKnownSolution(metadata.Id);
            if (previous != null) RegisterKnownSolution(previous);
            _settings.DefaultSolutionId = previousDefault;
            throw;
        }
    }

    public void ChangeDataLocation(ApplicationDataPaths paths)
    {
        if (CurrentSolution != null) throw new InvalidOperationException("请先关闭当前方案再迁移数据目录。");
        paths.ValidateAvailable();
        var settings = new SolutionSettings(); settings.SetBasePath(paths.Root);
        if (File.Exists(paths.ConfigFile) && !settings.Load(paths.ConfigFile)) throw new InvalidDataException("新配置无法加载。");
        _dataRoot = paths.Root; _solutionsDirectory = paths.SolutionsDirectory; _configFilePath = paths.ConfigFile;
        _settings = settings; _registry = new SolutionRegistry(); _cache = new SolutionCache();
        _repository = new SolutionRepository(paths.RecoveryDirectory); _isSettingsDirty = false;
        RefreshMetadata();
    }

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
        foreach (var metadata in _settings.GetKnownSolutions())
        {
            // Missing files remain actionable records; no whole-drive guesses or implicit relocation.
            if (File.Exists(metadata.FilePath))
            {
                var disk = _repository.LoadMetadata(metadata.FilePath);
                if (disk != null && disk.Id == metadata.Id)
                {
                    disk.LastAccessTime = metadata.LastAccessTime;
                    metadata.Name = disk.Name; metadata.Description = disk.Description;
                    metadata.ModifiedTime = disk.ModifiedTime; metadata.WorkflowCount = disk.WorkflowCount;
                }
            }
            _registry.Register(metadata); _cache.Set(metadata.Id, metadata);
        }
        UpdateMetadataIsDefaultFlags();
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
    public void SetCurrentSolution(Solution solution) => SetCurrentSolution(solution, registerInCatalog: true);

    public void SetCurrentSolution(Solution solution, bool registerInCatalog)
    {
        if (solution == null)
            throw new ArgumentNullException(nameof(solution));

        var setSolutionStopwatch = System.Diagnostics.Stopwatch.StartNew();
        _logger.Log(LogLevel.Info,
            $"[SolutionLifecycleTrace] SetCurrentSolution begin | id={solution.Id}, filePath={solution.FilePath}, thread={Environment.CurrentManagedThreadId}",
            "SolutionManager");

        // 诊断日志：开始设置
        _logger.Log(LogLevel.Info, 
            $"[诊断] 开始设置当前解决方案: Solution.Id={solution.Id}", 
            "SolutionManager");

        // 直接设置，不重复加载（避免违背惰性加载）
        var actualMetadata = registerInCatalog ? _repository.LoadMetadata(solution.FilePath ?? "") : new SolutionMetadata
        {
            Id = solution.Id, Name = solution.Name, Description = solution.Description, Version = solution.Version,
            FilePath = solution.FilePath ?? "", DirectoryPath = Path.GetDirectoryName(solution.FilePath ?? "") ?? "",
            LastAccessTime = DateTime.Now
        };
        if (registerInCatalog && actualMetadata != null) RegisterKnownSolution(actualMetadata);
        CurrentSolution = solution;
        CurrentFilePath = solution.FilePath;
        CaptureSavedState();

        var currentMetadata = registerInCatalog ? GetMetadata(solution.Id) : actualMetadata;
        if (registerInCatalog && currentMetadata != null) { currentMetadata.UpdateLastAccessTime(); _settings.AddKnownSolution(currentMetadata); }
        // 更新设置
        _settings.CurrentSolutionId = solution.Id;
        MarkSettingsDirty();

        // 触发事件（同时触发两个事件，与 OpenSolution 保持一致）
        _logger.Log(LogLevel.Info,
            $"[SolutionLifecycleTrace] SolutionOpened invoke begin | elapsedMs={setSolutionStopwatch.ElapsedMilliseconds}",
            "SolutionManager");
        SolutionOpened?.Invoke(this, CurrentSolution);
        _logger.Log(LogLevel.Info,
            $"[SolutionLifecycleTrace] SolutionOpened invoke returned | elapsedMs={setSolutionStopwatch.ElapsedMilliseconds}",
            "SolutionManager");

        // 诊断日志：查找元数据前的状态
        _logger.Log(LogLevel.Info, 
            $"[诊断] 缓存数量: {_cache.Count}, 注册表数量: {_registry.GetAll().Count}", 
            "SolutionManager");

        // 触发细粒度事件
        var metadata = registerInCatalog ? (_cache.Get(solution.Id) ?? _registry.Get(solution.Id)) : actualMetadata;
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
            if (registerInCatalog) _cache.Set(solution.Id, metadata);
            
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
        _logger.Log(LogLevel.Info,
            $"[SolutionLifecycleTrace] CurrentSolutionChanged invoke begin | metadataId={metadata.Id}, elapsedMs={setSolutionStopwatch.ElapsedMilliseconds}",
            "SolutionManager");
        CurrentSolutionChanged?.Invoke(this, currentSolutionArgs);
        _logger.Log(LogLevel.Info,
            $"[SolutionLifecycleTrace] CurrentSolutionChanged invoke returned | elapsedMs={setSolutionStopwatch.ElapsedMilliseconds}",
            "SolutionManager");

        _logger.Log(LogLevel.Info, 
            $"设置当前解决方案成功: Id={CurrentSolution.Id}, elapsedMs={setSolutionStopwatch.ElapsedMilliseconds}", 
            "SolutionManager");
    }

    /// <summary>
    /// 加载设置
    /// </summary>
    private void LoadSettings()
    {
        _logger.Log(LogLevel.Info, $"开始加载设置文件: {_configFilePath}", "SolutionManager");

        if (File.Exists(_configFilePath) && !_settings.Load(_configFilePath))
            throw new InvalidDataException("用户配置无法读取，不能使用空配置覆盖：" + _configFilePath);

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
        if (!_settings.Save(_configFilePath)) throw new IOException("用户配置保存失败：" + _configFilePath);
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
        _isSettingsDirty = true;
        if (!_settings.Save(_configFilePath)) throw new IOException("用户配置保存失败：" + _configFilePath);
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
