using System.Windows.Controls;

namespace SunEyeVision.UI.Features.WorkflowUI.Diagnostics
{
    /// <summary>
    /// 共享调试控件
    /// 提供通用的调试控制功能。
    /// </summary>
    public partial class SharedDebugControl : UserControl
    {
        public SharedDebugControl()
        {
            InitializeComponent();
        }

        private void OnStartDebug(object sender, System.Windows.RoutedEventArgs e)
        {
            // 触发开始调试事件。
        }

        private void OnStopDebug(object sender, System.Windows.RoutedEventArgs e)
        {
            // 触发停止调试事件
        }

        private void OnStep(object sender, System.Windows.RoutedEventArgs e)
        {
            // 触发单步执行事件
        }

        private void OnReset(object sender, System.Windows.RoutedEventArgs e)
        {
            // 触发重置事件
        }
    }
}
