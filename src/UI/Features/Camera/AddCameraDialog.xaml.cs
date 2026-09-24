using System;
using System.Windows;
using SunEyeVision.DeviceDriver.Cameras;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Shell.Windows;

using SunEyeVision.UI.Core;

namespace SunEyeVision.UI.Features.Camera
{
    /// <summary>
    /// 添加相机引导界面
    /// </summary>
    public partial class AddCameraDialog : ThemeDialogWindow
    {
        private readonly AddCameraViewModel _viewModel;
        
        public AddCameraDialog(CameraRuntimeComposition runtime, CameraManagerViewModel cameraManager)
        {
            InitializeComponent();
            
            // 创建 ViewModel
            _viewModel = new AddCameraViewModel(runtime, cameraManager);
            DataContext = _viewModel;
            
            // 订阅关闭事件
            _viewModel.RequestClose += ViewModel_RequestClose;
            
            Loaded += AddCameraDialog_Loaded;
        }
        
        private void AddCameraDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // 打开窗口即开始发现设备；接口选择仍可由用户调整后再次搜索。
            if (_viewModel.SearchCommand.CanExecute(null))
                _viewModel.SearchCommand.Execute(null);
        }
        
        private void ViewModel_RequestClose(object? sender, EventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
