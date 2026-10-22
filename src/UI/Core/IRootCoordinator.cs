using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;

using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Core
{
    /// <summary>
    /// 根协调器 - 供各域子 ViewModel 访问跨域协调能力的窄接口。
    /// 各域子 VM 通过该接口触发工作流运行、更新状态文字、记录日志、更新模块结果树等，
    /// 而不直接持有 MainWindowViewModel 类型引用，实现"子 VM 独立、跨域能力解耦"。
    /// </summary>
    public interface IRootCoordinator
    {
        /// <summary>
        /// 触发当前选中工作流运行（顶栏/图像区"运行"入口共用）
        /// </summary>
        ICommand RunWorkflowCommand { get; }

        /// <summary>
        /// 状态栏/顶栏共享的状态文字
        /// </summary>
        string StatusText { get; set; }

        /// <summary>
        /// 是否正在运行（跨域共享运行状态）
        /// </summary>
        bool IsRunning { get; set; }

        /// <summary>
        /// 相机状态文字（跨域共享）
        /// </summary>
        string CameraStatus { get; }

        /// <summary>
        /// 工作流数量（跨域共享）
        /// </summary>
        int WorkflowCount { get; }

        /// <summary>
        /// 根 VM 属性变更事件（供子 VM 订阅跨域状态变化）
        /// </summary>
        event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 记录日志（智能解析 Emoji 前缀）
        /// </summary>
        void AddLog(string message);

        /// <summary>
        /// 记录带级别的日志
        /// </summary>
        void AddLog(LogLevel level, string message, string? source = null);

        /// <summary>
        /// 更新模块结果树（分组可折叠显示）
        /// </summary>
        void UpdateModuleResults(ToolResults? result, string? nodeName, string? toolId = null, string? nodeId = null, ToolParameters? parameters = null);

        /// <summary>
        /// 图像显示源索引变化回调（由图像结果子 VM 触发，根 VM 据此刷新显示图像）
        /// </summary>
        void OnSelectedImageSourceIndexChanged(int index);

        /// <summary>
        /// 按当前选中的图像源刷新显示（可用性回退 + 下拉跟随）
        /// </summary>
        void RefreshDisplayImageFromCurrentSource();

        /// <summary>
        /// 保存当前解决方案命令
        /// </summary>
        ICommand SaveCurrentSolutionCommand { get; }
        ICommand NewSolutionCommand { get; }
        ICommand OpenSolutionCommand { get; }
        ICommand ShowStartPageCommand { get; }

        /// <summary>
        /// 停止工作流命令
        /// </summary>
        ICommand StopWorkflowCommand { get; }

        /// <summary>
        /// 显示路径拐点命令
        /// </summary>
        ICommand TogglePathPointsCommand { get; }

        /// <summary>
        /// 显示解决方案配置命令
        /// </summary>
        ICommand ShowSolutionConfigurationCommand { get; }

        /// <summary>
        /// 显示全局变量管理命令
        /// </summary>
        ICommand ShowGlobalVariableManagerCommand { get; }

        /// <summary>
        /// 显示相机管理命令
        /// </summary>
        ICommand ShowCameraManagerCommand { get; }

        /// <summary>
        /// 显示通讯管理命令
        /// </summary>
        ICommand ShowCommunicationManagerCommand { get; }

        /// <summary>
        /// 显示帮助命令
        /// </summary>
        ICommand ShowHelpCommand { get; }

        /// <summary>
        /// 显示快捷键命令
        /// </summary>
        ICommand ShowShortcutsCommand { get; }

        /// <summary>
        /// 显示关于命令
        /// </summary>
        ICommand ShowAboutCommand { get; }

        /// <summary>
        /// 运行全部工作流命令
        /// </summary>
        ICommand RunAllWorkflowsCommand { get; }

        /// <summary>
        /// 切换全部连续运行命令
        /// </summary>
        ICommand ToggleContinuousAllCommand { get; }

        /// <summary>
        /// 是否全部工作流正在运行（连续运行按钮状态）
        /// </summary>
        bool IsAllWorkflowsRunning { get; }

        /// <summary>
        /// 是否已有当前解决方案
        /// </summary>
        bool HasCurrentSolution { get; }

        /// <summary>
        /// 当前解决方案名称
        /// </summary>
        string CurrentSolutionName { get; }

        /// <summary>
        /// 当前选中工作流标签页（只读，供 View 单向绑定）
        /// 变更一律通过 <see cref="SetSelectedTab"/> 命令入口，避免双向绑定回写造成重入。
        /// </summary>
        object? SelectedTab { get; }

        /// <summary>
        /// 设置当前选中工作流标签页（唯一写入口）。
        /// 用户操作（顶栏下拉框/TabControl 切换）与代码路径均通过此方法变更，保证单向数据流。
        /// </summary>
        /// <param name="tab">目标标签页</param>
        void SetSelectedTab(object? tab);

        /// <summary>
        /// 当前选中工作流标签页变化事件（代码路径切换 Tab 时通知子 VM 刷新绑定）
        /// </summary>
        event EventHandler? SelectedTabChanged;

        /// <summary>
        /// 工作流标签页集合（顶栏下拉框）
        /// </summary>
        System.Collections.IList Tabs { get; }
    }
}
