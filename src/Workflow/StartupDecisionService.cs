using System;
using System.IO;

namespace SunEyeVision.Workflow;

/// <summary>
/// 启动决策结果
/// </summary>
public enum StartupDecision
{
    /// <summary>
    /// 显示配置界面
    /// </summary>
    ShowConfiguration,

    /// <summary>
    /// 跳过配置界面，直接进入主界面
    /// </summary>
    SkipConfiguration
}

/// <summary>
/// 启动决策服务
/// </summary>
/// <remarks>
/// 职责：
/// 1. 检查 SolutionSettings
/// 2. 决定启动时是否显示配置界面
/// 3. 返回决策结果
///
/// 使用场景：
/// 1. 应用启动时调用
/// 2. 根据用户配置决定启动流程
///
/// 重构说明（2026-03-16）：
/// - 已移除对 RuntimeConfig 的依赖
/// - 直接使用 SolutionSettings
/// - 代码更简洁，逻辑更清晰
/// </remarks>
public class StartupDecisionService
{
    private readonly SolutionManager _solutionManager;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="solutionManager">解决方案管理器</param>
    public StartupDecisionService(SolutionManager solutionManager)
    {
        _solutionManager = solutionManager ?? throw new ArgumentNullException(nameof(solutionManager));
    }

    /// <summary>
    /// 获取启动决策
    /// </summary>
    /// <returns>启动决策结果</returns>
    public StartupDecision GetStartupDecision()
    {
        // 检查是否跳过配置
        if (_solutionManager.Settings.StartupMode != "Choose")
        {
            return StartupDecision.SkipConfiguration;
        }

        // 显示配置界面
        return StartupDecision.ShowConfiguration;
    }
}
