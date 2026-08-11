using System;
using System.Windows;
using SunEyeVision.Core.Services.CameraDiscovery;
using SunEyeVision.UI.ViewModels;

namespace SunEyeVision.UI.Views.Windows
{
    /// <summary>
    /// 添加相机引导界面
    /// </summary>
    public partial class AddCameraDialog : Window
    {
        private readonly AddCameraViewModel _viewModel;
        
        public AddCameraDialog(CameraDiscoveryAggregator discoveryAggregator, CameraManagerViewModel cameraManager)
        {
            InitializeComponent();
            
            // 创建 ViewModel
            _viewModel = new AddCameraViewModel(discoveryAggregator, cameraManager);
            DataContext = _viewModel;
            
            // 订阅关闭事件
            _viewModel.RequestClose += ViewModel_RequestClose;
            
            Loaded += AddCameraDialog_Loaded;
        }
        
        private void AddCameraDialog_Loaded(object sender, RoutedEventArgs e)
        {
            // 窗口加载完成后的初始化逻辑
        }
        
        private void ViewModel_RequestClose(object? sender, EventArgs e)
        {
            DialogResult = true;
            Close();
        }
    }
}
