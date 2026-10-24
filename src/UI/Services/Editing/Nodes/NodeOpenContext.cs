using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Editing
{
    /// <summary>
    /// 节点打开上下文 - 封装节点打开过程中的所有数据
    /// </summary>
    /// <remarks>
    /// 上下文贯穿整个决策链，包含：
    /// 1. 输入数据：节点、工具实例、元数据
    /// 2. 决策结果：窗口类型、调试控件
    /// 3. 运行时信息：主窗口引用、ImageControl
    /// 
    /// 决策链各步骤通过读取和修改上下文来协同工作。
    /// </remarks>
    public class NodeOpenContext
    {
        #region 输入数据

        /// <summary>
        /// 要打开的节点
        /// </summary>
        public WorkflowNodeBase Node { get; set; } = null!;

        /// <summary>
        /// 工具实例（从节点创建）
        /// </summary>
        public IToolPlugin? ToolInstance { get; set; }

        /// <summary>
        /// 工具元数据（从 ToolRegistry 获取）
        /// </summary>
        public ToolMetadata? Metadata { get; set; }

        #endregion

        #region 决策结果

        /// <summary>
        /// 窗口类型（从决策链得出）
        /// </summary>
        /// <remarks>
        /// 决策优先级：节点级配置 → 工具级配置 → 全局默认值
        /// </remarks>
        public DebugWindowStyle WindowStyle { get; set; } = DebugWindowStyle.Default;

        /// <summary>
        /// 调试控件（从工具创建）
        /// </summary>
        /// <remarks>
        /// 由 IToolEditorFactory.CreateDebugControl() 创建。
        /// 如果为 null，表示该工具无调试界面。
        /// </remarks>
        public FrameworkElement? DebugControl { get; set; }

        /// <summary>
        /// 创建的窗口实例（由策略创建）
        /// </summary>
        /// <remarks>
        /// 用于返回给调用者进行全局单例管理。
        /// </remarks>
        public Window? CreatedWindow { get; set; }

        #endregion

        #region 运行时信息

        /// <summary>
        /// 主窗口引用（用于设置窗口所有者）
        /// </summary>
        public Window? MainWindow { get; set; }

        /// <summary>
        /// 主窗口的 ImageControl（用于区域编辑器绑定）
        /// </summary>
        public FrameworkElement? MainImageControl { get; set; }

        #endregion

        /// <summary>
        /// 创建节点打开上下文
        /// </summary>
        public NodeOpenContext()
        {
        }

        /// <summary>
        /// 创建节点打开上下文（带主窗口引用）
        /// </summary>
        public NodeOpenContext(Window? mainWindow, FrameworkElement? mainImageControl)
        {
            MainWindow = mainWindow;
            MainImageControl = mainImageControl;
        }
    }
}
