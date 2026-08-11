using System;
using System.IO;
using OpenCvSharp;
using SunEyeVision.UI.Shell.Controls.Panels;

namespace SunEyeVision.UI.Services
{
    /// <summary>
    /// 图像预览集成服务 - 协调ImagePreviewControl和ViewModel之间的交互
    /// </summary>
    /// <remarks>
    /// 此服务提供ImagePreviewControl与外部ViewModel之间的松耦合集成。
    /// 使用接口IImageLoadViewModelHandler来避免对具体ViewModel类型的依赖。
    /// </remarks>
    public class ImagePreviewIntegrationService : IDisposable
    {
        #region 字段

        private readonly ImagePreviewControl _previewControl;
        private readonly IImageLoadViewModelHandler? _viewModelHandler;
        private bool _disposed = false;

        #endregion

        #region 构造函数

        /// <summary>
        /// 创建图像预览集成服务
        /// </summary>
        public ImagePreviewIntegrationService(
            ImagePreviewControl previewControl,
            IImageLoadViewModelHandler? viewModelHandler = null)
        {
            _previewControl = previewControl ?? throw new ArgumentNullException(nameof(previewControl));
            _viewModelHandler = viewModelHandler;

            // 订阅工作流执行请求事件
            _previewControl.WorkflowExecutionRequested += OnWorkflowExecutionRequested;
        }

        #endregion

        #region 事件

        /// <summary>
        /// 工作流执行请求事件
        /// </summary>
        public event EventHandler<WorkflowExecutionRequestEventArgs>? WorkflowExecutionRequested;

        #endregion

        #region 公共方法

        /// <summary>
        /// 更新预览器显示的图像
        /// </summary>
        public void UpdatePreview(Mat? image)
        {
            if (image == null || image.Empty())
            {
                return;
            }

            // 这里可以添加将Mat转换为BitmapSource并更新预览器的逻辑
            // 目前ImagePreviewControl主要处理文件路径加载，暂不实现
        }

        /// <summary>
        /// 设置文件路径并加载图像
        /// </summary>
        public async void SetFilePathAndLoad(string filePath)
        {
            if (!File.Exists(filePath))
            {
                return;
            }

            // 如果有ViewModel处理器，使用它来设置参数和加载
            if (_viewModelHandler != null)
            {
                _viewModelHandler.SetFilePath(filePath);
                await _viewModelHandler.LoadImageAsync(filePath);
            }
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
            {
                return;
            }

            // 取消订阅事件
            _previewControl.WorkflowExecutionRequested -= OnWorkflowExecutionRequested;

            // 清理ViewModel
            _viewModelHandler?.Cleanup();

            _disposed = true;
        }

        #endregion

        #region 私有方法

        private void OnWorkflowExecutionRequested(object? sender, WorkflowExecutionRequestEventArgs e)
        {
            // 如果有ViewModel处理器，更新当前索引
            if (_viewModelHandler != null)
            {
                _viewModelHandler.SetCurrentImageIndex(e.Index);
            }

            // 转发事件
            WorkflowExecutionRequested?.Invoke(this, e);
        }

        #endregion
    }

    /// <summary>
    /// 图像载入ViewModel处理器接口 - 用于解耦UI服务和具体ViewModel
    /// </summary>
    public interface IImageLoadViewModelHandler
    {
        /// <summary>
        /// 设置文件路径
        /// </summary>
        void SetFilePath(string filePath);

        /// <summary>
        /// 异步加载图像
        /// </summary>
        System.Threading.Tasks.Task LoadImageAsync(string filePath);

        /// <summary>
        /// 设置当前图像索引
        /// </summary>
        void SetCurrentImageIndex(int index);

        /// <summary>
        /// 清理资源
        /// </summary>
        void Cleanup();
    }
}
