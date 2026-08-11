using System.Windows.Controls;

namespace SunEyeVision.UI.Views.Controls.Shell
{
    /// <summary>
    /// 底部状态栏：真实状态、相机状态、运行状态、流程计数。
    /// 从 MainWindow 拆分而来，移除假 CPU/FPS 硬编码（rule-008）。
    /// </summary>
    public partial class StatusBarControl : UserControl
    {
        public StatusBarControl()
        {
            InitializeComponent();
        }
    }
}
