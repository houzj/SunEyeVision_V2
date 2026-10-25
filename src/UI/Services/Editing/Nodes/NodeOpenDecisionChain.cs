using System;
using SunEyeVision.Workflow;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;

namespace SunEyeVision.UI.Services.Editing
{
    /// <summary>
    /// 节点打开决策链 - 集中处理节点双击/打开的决策流程
    /// </summary>
    /// <remarks>
    /// 决策链步骤：
    /// 1. 判断是否为控制流节点（Subroutine 仍然通过 SubroutineId 区分，由 MainWindowViewModel 处理导航）
    /// 2. 算法节点流程：获取元数据 → 决定窗口类型 → 创建调试控件 → 创建窗口
    /// </remarks>
    public class NodeOpenDecisionChain
    {
        /// <summary>
        /// 控制流节点打开事件 - 由 MainWindowViewModel 订阅处理子程序导航等
        /// </summary>
        public static event Action<WorkflowNodeBase>? OnControlFlowNodeOpen;

        /// <summary>
        /// 执行决策链
        /// </summary>
        public bool Execute(WorkflowNodeBase node, NodeOpenContext context, Func<System.Windows.FrameworkElement?>? createEditor = null)
        {
            try
            {
                // Step 1: 设置上下文
                context.Node = node;

                // Step 2: 判断是否为子程序节点（通过 SubroutineId 区分）
                if (!string.IsNullOrEmpty(node.SubroutineId))
                {
                    OnControlFlowNodeOpen?.Invoke(node);
                    return true;
                }

                // Step 3: 算法节点 — 执行 ToolRegistry 流程
                FetchMetadata(context);
                DecideWindowStyle(context);

                if (context.WindowStyle == DebugWindowStyle.None)
                    return true;

                if (createEditor is null) CreateDebugControl(context);
                else context.DebugControl = createEditor();

                // Step 4: 创建并显示调试窗口
                DefaultOpenWindow(context);
                return true;
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"节点打开失败: {ex.Message}",
                    "NodeOpenDecisionChain", ex);
                return false;
            }
        }

        /// <summary>
        /// 获取工具元数据
        /// </summary>
        private void FetchMetadata(NodeOpenContext context)
        {
            var node = context.Node;
            var metadata = ToolRegistry.GetToolMetadata(node.ToolType);
            context.Metadata = metadata;

            if (metadata == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"未找到工具元数据: {node.ToolType}",
                    "NodeOpenDecisionChain");
            }
        }

        /// <summary>
        /// 决定窗口类型（优先级：节点级 → 工具级 → 全局默认）
        /// </summary>
        private void DecideWindowStyle(NodeOpenContext context)
        {
            var node = context.Node;
            var metadata = context.Metadata;

            if (node.DebugWindowStyle != DebugWindowStyle.Default)
            {
                context.WindowStyle = node.DebugWindowStyle;
                return;
            }

            if (metadata is not null && metadata.DebugWindowStyle != DebugWindowStyle.Default)
            {
                context.WindowStyle = metadata.DebugWindowStyle;
                return;
            }

            context.WindowStyle = DebugWindowStyle.Default;
        }

        /// <summary>
        /// 创建调试控件
        /// </summary>
        private void CreateDebugControl(NodeOpenContext context)
        {
            var node = context.Node;

            try
            {
                var toolInstance = node.CreateInstance();
                context.ToolInstance = toolInstance;

                if (toolInstance == null)
                {
                    VisionLogger.Instance.Log(LogLevel.Warning,
                        $"无法创建工具实例: {node.ToolType}",
                        "NodeOpenDecisionChain");
                    return;
                }

                var debugControl = ToolConfigurationEditors.CreateControl(toolInstance);
                context.DebugControl = debugControl;

                if (debugControl == null)
                {
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"工具未提供调试控件: {node.ToolType}",
                        "NodeOpenDecisionChain");
                }
            }
            catch (Exception ex)
            {
                VisionLogger.Instance.Log(LogLevel.Error,
                    $"创建调试控件失败: {ex.Message}",
                    "NodeOpenDecisionChain", ex);
            }
        }

        /// <summary>
        /// 默认打开窗口逻辑 - 使用 DefaultDebugWindow 包装调试控件
        /// </summary>
        private void DefaultOpenWindow(NodeOpenContext context)
        {
            if (context.DebugControl == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    "调试控件为空，无法创建窗口",
                    "NodeOpenDecisionChain");
                return;
            }

            // 控件已经是 Window 类型，直接使用
            if (context.DebugControl is System.Windows.Window existingWindow)
            {
                if (context.MainWindow != null && existingWindow.Owner == null)
                    existingWindow.Owner = context.MainWindow;
                context.CreatedWindow = existingWindow;
                return;
            }

            if (context.DebugControl is System.Windows.Controls.UserControl control)
            {
                // A declaration editor supplies configuration content, not a complete custom
                // window layout/footer or a result event relay. It always needs the complete host shell.
                System.Windows.Window window = context.WindowStyle == DebugWindowStyle.Custom&&control is not DeclaredToolConfigurationControl
                    ? new SunEyeVision.Plugin.SDK.UI.Windows.CustomDebugWindow(control)
                    : new SunEyeVision.Plugin.SDK.UI.Windows.DefaultDebugWindow(control);
                if (context.MainWindow is not null) window.Owner = context.MainWindow;
                context.CreatedWindow = window;
                return;
            }
            context.CreatedWindow = null;        }
    }
}
