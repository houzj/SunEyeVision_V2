using System.Windows;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shell.Windows;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Features.Camera
{
    /// <summary>
    /// 相机管理器对话框
    /// </summary>
    public partial class CameraManagerDialog : ThemeDialogWindow
    {
        public CameraManagerDialog(CameraManagerViewModel viewModel)
        {
            InitializeComponent();
            
            // 设置 ViewModel
            DataContext = viewModel;
        }
    }
}
