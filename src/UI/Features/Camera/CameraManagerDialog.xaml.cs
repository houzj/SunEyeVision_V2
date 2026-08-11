using System.Windows;
using SunEyeVision.UI.ViewModels;

namespace SunEyeVision.UI.Views.Windows
{
    /// <summary>
    /// 相机管理器对话框
    /// </summary>
    public partial class CameraManagerDialog : Window
    {
        public CameraManagerDialog(CameraManagerViewModel viewModel)
        {
            InitializeComponent();
            
            // 设置 ViewModel
            DataContext = viewModel;
        }
    }
}
