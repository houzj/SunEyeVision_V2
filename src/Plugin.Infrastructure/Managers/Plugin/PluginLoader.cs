using System;
using System.IO;
using System.Linq;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

namespace SunEyeVision.Plugin.Infrastructure.Managers.Plugin;

/// <summary>
/// 全局插件加载状态管理器
/// </summary>
/// <remarks>
/// 职责：
/// - 提供全局的插件加载状态检查
/// - 确保插件只加载一次（单例模式）
/// - 为 SolutionRepository 等组件提供统一的插件状态查询
///
/// 设计原则（rule-004）：
/// - 统一插件加载入口，避免多处加载
/// - 使用双重检查锁定模式保证线程安全
///
/// 使用场景：
/// - App.OnStartup() 加载插件后调用 MarkAsLoaded()
/// - SolutionRepository.EnsurePluginsLoaded() 使用 IsLoaded/EnsureLoaded()
/// </remarks>
public static class PluginLoader
{
    /// <summary>
    /// 插件加载状态（volatile 保证多线程可见性）
    /// </summary>
    private static volatile bool _isLoaded = false;

    /// <summary>
    /// 插件加载锁（双重检查锁定模式）
    /// </summary>
    private static readonly object _lock = new();

    /// <summary>
    /// 默认插件目录名
    /// </summary>
    private const string DefaultPluginDirectoryName = "plugins";

    /// <summary>
    /// 插件是否已加载
    /// </summary>
    public static bool IsLoaded => _isLoaded;

    /// <summary>
    /// 标记插件已加载（由 App.OnStartup 调用）
    /// </summary>
    /// <remarks>
    /// 调用时机：在 App.OnStartup() 中完成插件加载后立即调用
    /// </remarks>
    public static void MarkAsLoaded()
    {
        lock (_lock)
        {
            _isLoaded = true;
            VisionLogger.Instance.Log(LogLevel.Info, "插件加载状态已标记为完成", "PluginLoader");
        }
    }

    /// <summary>
    /// 确保插件已加载（如果未加载则自动加载）
    /// </summary>
    /// <remarks>
    /// 使用双重检查锁定模式保证线程安全
    /// 如果 App.OnStartup() 尚未完成插件加载，会自动触发加载
    /// </remarks>
    /// <returns>是否成功加载</returns>
    public static bool EnsureLoaded()
    {
        // 第一次检查（无锁）
        if (_isLoaded) return true;

        lock (_lock)
        {
            // 第二次检查（有锁）
            if (_isLoaded) return true;

            return LoadPluginsInternal();
        }
    }

    /// <summary>
    /// 使用默认路径加载插件
    /// </summary>
    /// <returns>是否成功加载</returns>
    public static bool LoadPlugins()
    {
        return LoadPlugins(GetDefaultPluginPath());
    }

    /// <summary>
    /// 从指定路径加载插件
    /// </summary>
    /// <param name="pluginDirectory">插件目录路径</param>
    /// <returns>是否成功加载</returns>
    public static bool LoadPlugins(string pluginDirectory)
    {
        // 第一次检查（无锁）
        if (_isLoaded)
        {
            VisionLogger.Instance.Log(LogLevel.Info, "插件已加载，跳过重复加载", "PluginLoader");
            return true;
        }

        lock (_lock)
        {
            // 第二次检查（有锁）
            if (_isLoaded) return true;

            return LoadPluginsInternal(pluginDirectory);
        }
    }

    /// <summary>
    /// 内部加载实现（调用前必须已获取锁）
    /// </summary>
    private static bool LoadPluginsInternal(string? pluginDirectory = null)
    {
        var logger = VisionLogger.Instance;
        pluginDirectory ??= GetDefaultPluginPath();

        try
        {
            logger.Log(LogLevel.Info, $"开始加载插件: {pluginDirectory}", "PluginLoader");

            if (!Directory.Exists(pluginDirectory))
            {
                logger.Log(LogLevel.Warning, $"插件目录不存在: {pluginDirectory}", "PluginLoader");
                return false;
            }

            // 使用 PluginManager 加载插件（传入 VisionLogger 以便输出日志）
            var pluginManager = new PluginManager(logger);
            pluginManager.LoadPlugins(pluginDirectory);
            pluginManager.AssertCompleteReleaseToolSet(ToolRegistry.DirectoryCatalog?.ToolIds ?? Array.Empty<string>());

            // 验证加载结果
            var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
            var toolAssemblies = loadedAssemblies
                .Where(a => a.GetName().Name?.StartsWith("SunEyeVision.Tool.") == true)
                .ToList();

            logger.Log(LogLevel.Success, $"插件加载完成，共注册 {ToolRegistry.DirectoryCatalog!.ToolIds.Count} 个工具", "PluginLoader");

            foreach (var assembly in toolAssemblies)
            {
                logger.Log(LogLevel.Info, $"  - {assembly.GetName().Name} (Version={assembly.GetName().Version})", "PluginLoader");
            }

            _isLoaded = true;
            return true;
        }
        catch (Exception ex)
        {
            logger.Log(LogLevel.Error, $"插件加载失败: {ex.Message}", "PluginLoader", ex);
            return false;
        }
    }

    /// <summary>
    /// 获取默认插件路径
    /// </summary>
    private static string GetDefaultPluginPath()
    {
        return Path.Combine(AppDomain.CurrentDomain.BaseDirectory, DefaultPluginDirectoryName);
    }

    /// <summary>
    /// 重置加载状态（仅用于单元测试）
    /// </summary>
    internal static void ResetForTesting()
    {
        lock (_lock)
        {
            _isLoaded = false;
        }
    }
}
