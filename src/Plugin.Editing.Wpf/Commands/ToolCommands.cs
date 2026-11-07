using System.Windows.Input;

namespace SunEyeVision.Plugin.Editing.Wpf.Commands
{
    /// <summary>
    /// 工具调试窗口标准路由命令
    /// </summary>
    /// <remarks>
    /// 使用说明：
    /// - 所有命令均为全局路由命令，可在任意位置使用
    /// - 支持 XAML 声明式绑定
    /// - 支持快捷键绑定
    /// 
    /// XAML 使用示例：
    /// <![CDATA[
    /// <Button Content="执行"
    ///         Command="commands:ToolCommands.Execute"
    ///         CommandTarget="{Binding ElementName=ContentHost}"/>
    /// ]]>
    /// 
    /// 快捷键定义：
    /// - Execute: F5
    /// - Confirm: Ctrl+Enter
    /// - ContinuousExecute: F6
    /// - ResetParameters: Ctrl+R
    /// </remarks>
    public static class ToolCommands
    {
        #region 标准命令

        /// <summary>
        /// 执行命令 - 触发工具执行
        /// </summary>
        /// <remarks>
        /// 快捷键: F5
        /// 用途: 执行一次工具处理逻辑
        /// </remarks>
        public static readonly RoutedUICommand Execute = new(
            "执行",
            "Execute",
            typeof(ToolCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F5)
            });

        /// <summary>
        /// 确认命令 - 确认参数并关闭窗口
        /// </summary>
        /// <remarks>
        /// 快捷键: Ctrl+Enter
        /// 用途: 保存参数并关闭调试窗口
        /// </remarks>
        public static readonly RoutedUICommand Confirm = new(
            "确认",
            "Confirm",
            typeof(ToolCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.Enter, ModifierKeys.Control)
            });

        /// <summary>
        /// 连续执行命令 - 启动/停止连续执行模式
        /// </summary>
        /// <remarks>
        /// 快捷键: F6
        /// 用途: 循环执行工具处理逻辑（适用于实时预览场景）
        /// </remarks>
        public static readonly RoutedUICommand ContinuousExecute = new(
            "连续执行",
            "ContinuousExecute",
            typeof(ToolCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.F6)
            });

        /// <summary>
        /// 重置参数命令 - 恢复参数到默认值
        /// </summary>
        /// <remarks>
        /// 快捷键: Ctrl+R
        /// 用途: 重置所有参数到初始状态
        /// </remarks>
        public static readonly RoutedUICommand ResetParameters = new(
            "重置参数",
            "ResetParameters",
            typeof(ToolCommands),
            new InputGestureCollection
            {
                new KeyGesture(Key.R, ModifierKeys.Control)
            });

        #endregion
    }
}
