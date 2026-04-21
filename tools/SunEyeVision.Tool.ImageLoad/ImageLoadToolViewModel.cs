using System;
using System.IO;
using System.Windows.Input;
using Microsoft.Win32;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.ViewModels;
using OpenCvSharp;
using OpenCvSharp.WpfExtensions;

namespace SunEyeVision.Tool.ImageLoad
{
    /// <summary>
    /// 图像载入工具ViewModel - 简化版
    /// 只负责图像载入，不进行图像处理
    /// </summary>
    public class ImageLoadToolViewModel : ToolViewModelBase
    {
        private readonly ImageLoadTool _tool;
        private ImageLoadParameters _parameters;
        private ImageLoadResults? _lastResult;
        private System.Windows.Media.Imaging.BitmapSource? _outputImage;
        private Mat? _outputMat;
        private bool _isExecuting;

        #region 属性

        /// <summary>
        /// 文件路径
        /// </summary>
        public string FilePath
        {
            get => _parameters.FilePath;
            set
            {
                if (_parameters.FilePath != value)
                {
                    _parameters.FilePath = value;
                    if (SetProperty(ref _filePathCache, value, "文件路径"))
                    {
                        OnPropertyChanged(nameof(FileName));
                        OnPropertyChanged(nameof(HasValidFile));
                        SetParamValue("FilePath", value);
                    }
                }
            }
        }

        // 用于属性变化通知的缓存字段
        private string _filePathCache = string.Empty;

        /// <summary>
        /// 文件名（不含路径）
        /// </summary>
        public string FileName => string.IsNullOrEmpty(FilePath) 
            ? "未选择文件" 
            : Path.GetFileName(FilePath);

        /// <summary>
        /// 是否有有效文件
        /// </summary>
        public bool HasValidFile => !string.IsNullOrEmpty(FilePath) && File.Exists(FilePath);

        /// <summary>
        /// 最后一次执行结果
        /// </summary>
        public ImageLoadResults? LastResult
        {
            get => _lastResult;
            private set
            {
                _lastResult = value;
                OnPropertyChanged(nameof(LastResult));
                OnPropertyChanged(nameof(HasResult));
                OnPropertyChanged(nameof(ResultSummary));
            }
        }

        /// <summary>
        /// 是否有结果
        /// </summary>
        public bool HasResult => LastResult != null && LastResult.IsSuccess;

        /// <summary>
        /// 结果摘要
        /// </summary>
        public string ResultSummary
        {
            get
            {
                if (LastResult == null) return "未执行";
                if (!LastResult.IsSuccess) return $"失败: {LastResult.ErrorMessage}";
                return $"{LastResult.Width}x{LastResult.Height}, {LastResult.Channels}通道, {LastResult.ExecutionTimeMs}ms";
            }
        }

        /// <summary>
        /// 输出图像（用于显示）
        /// </summary>
        public System.Windows.Media.Imaging.BitmapSource? OutputImage
        {
            get => _outputImage;
            private set
            {
                _outputImage = value;
                OnPropertyChanged(nameof(OutputImage));
            }
        }

        /// <summary>
        /// 输出Mat（用于传递给下游工具）
        /// </summary>
        public Mat? OutputMat
        {
            get => _outputMat;
            private set
            {
                _outputMat = value;
                OnPropertyChanged(nameof(OutputMat));
            }
        }

        /// <summary>
        /// 是否正在执行
        /// </summary>
        public new bool IsExecuting
        {
            get => _isExecuting;
            private set
            {
                if (SetProperty(ref _isExecuting, value))
                {
                    OnPropertyChanged(nameof(CanRun));
                }
            }
        }

        /// <summary>
        /// 是否可以运行
        /// </summary>
        public bool CanRun => HasValidFile && !IsExecuting;

        #endregion

        #region 命令

        /// <summary>
        /// 浏览文件命令
        /// </summary>
        public ICommand BrowseFileCommand { get; }

        /// <summary>
        /// 执行命令
        /// </summary>
        public ICommand ExecuteCommand { get; }

        #endregion

        /// <summary>
        /// 构造函数
        /// </summary>
        public ImageLoadToolViewModel()
        {
            _tool = new ImageLoadTool();
            _parameters = new ImageLoadParameters();

            BrowseFileCommand = new RelayCommand(BrowseFile);
            ExecuteCommand = new RelayCommand(Execute, () => CanRun);
        }

        /// <summary>
        /// 初始化
        /// </summary>
        public override void Initialize(string toolId, IToolPlugin? toolPlugin, ToolMetadata? toolMetadata)
        {
            base.Initialize(toolId, toolPlugin, toolMetadata);
            _parameters = new ImageLoadParameters();
        }

        /// <summary>
        /// 获取当前运行参数
        /// </summary>
        protected override ToolParameters GetRunParameters()
        {
            return new ImageLoadParameters
            {
                FilePath = this.FilePath
            };
        }

        /// <summary>
        /// 浏览文件
        /// </summary>
        private void BrowseFile()
        {

            var dialog = new OpenFileDialog
            {
                Title = "选择图像文件",
                Filter = "图像文件|*.bmp;*.jpg;*.jpeg;*.png;*.tiff;*.tif;*.gif;*.webp|所有文件|*.*",
                FilterIndex = 1,
                RestoreDirectory = true,
                CheckFileExists = true
            };

            if (dialog.ShowDialog() == true)
            {
                FilePath = dialog.FileName;
            }
            else
            {
            }
        }

        /// <summary>
        /// 执行工具
        /// </summary>
        private void Execute()
        {
            if (!CanRun) return;

            try
            {
                IsExecuting = true;
                ToolStatus = "运行中";
                StatusMessage = "正在载入图像...";

                var result = _tool.Run(_parameters);
                LastResult = result;

                if (result.IsSuccess && result.OutputImage != null)
                {
                    // 更新输出图像
                    OutputImage = result.OutputImage.ToBitmapSource();
                    OutputMat = result.OutputImage.Clone();
                    StatusMessage = $"载入成功: {result.Width}x{result.Height}";
                    ToolStatus = "就绪";
                }
                else
                {
                    StatusMessage = $"载入失败: {result.ErrorMessage}";
                    ToolStatus = "错误";
                }

                ExecutionTime = $"{result.ExecutionTimeMs} ms";
            }
            catch (Exception ex)
            {
                StatusMessage = $"执行错误: {ex.Message}";
                ToolStatus = "错误";
                LastResult = new ImageLoadResults();
                LastResult.SetError(ex.Message, ex);
            }
            finally
            {
                IsExecuting = false;
            }
        }

        /// <summary>
        /// 设置文件路径（从外部调用，如 ImagePreviewControl）
        /// </summary>
        public void SetFilePath(string filePath)
        {
            if (File.Exists(filePath))
            {
                FilePath = filePath;
            }
            else
            {
            }
        }

        #region 图像预览器配合接口

        /// <summary>
        /// 从图像预览器接收图像路径
        /// </summary>
        /// <param name="filePath">图像文件路径</param>
        /// <param name="previewImage">预览图像（可选，如果预览器已加载则直接使用）</param>
        public void SetImageFromPreview(string filePath, Mat? previewImage = null)
        {
            if (!File.Exists(filePath))
            {
                StatusMessage = $"文件不存在: {filePath}";
                ToolStatus = "错误";
                return;
            }

            FilePath = filePath;
            StatusMessage = $"已接收图像: {FileName}";
            ToolStatus = "就绪";

            // 如果预览器已经加载了图像，可以直接使用
            if (previewImage != null)
            {
                try
                {
                    OutputMat = previewImage.Clone();
                    OutputImage = previewImage.ToBitmapSource();
                    StatusMessage = $"已加载预览图像: {OutputMat.Width}x{OutputMat.Height}";
                }
                catch (Exception ex)
                {
                    StatusMessage = $"预览图像转换失败: {ex.Message}";
                    ToolStatus = "警告";
                }
            }
        }

        /// <summary>
        /// 获取输出图像供工作流使用
        /// </summary>
        /// <returns>输出图像的副本</returns>
        public Mat? GetOutputForWorkflow()
        {
            return OutputMat?.Clone();
        }

        /// <summary>
        /// 尝试自动加载图像（如果已设置文件路径但未加载）
        /// </summary>
        /// <returns>是否成功加载</returns>
        public bool TryAutoLoad()
        {
            if (!HasValidFile)
            {
                return false;
            }

            if (OutputMat != null)
            {
                return true; // 已加载
            }
            Execute();
            return HasResult;
        }

        #endregion

        /// <summary>
        /// 获取当前参数
        /// </summary>
        public ImageLoadParameters GetTypedParameters()
        {
            return (ImageLoadParameters)_parameters.Clone();
        }

        /// <summary>
        /// 设置参数
        /// </summary>
        public void SetTypedParameters(ImageLoadParameters parameters)
        {
            _parameters = (ImageLoadParameters)parameters.Clone();
            OnPropertyChanged(nameof(FilePath));
            OnPropertyChanged(nameof(FileName));
        }
    }

    /// <summary>
    /// 简单的中继命令实现
    /// </summary>
    public class RelayCommand : ICommand
    {
        private readonly Action _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add { CommandManager.RequerySuggested += value; }
            remove { CommandManager.RequerySuggested -= value; }
        }

        public bool CanExecute(object? parameter)
        {
            return _canExecute == null || _canExecute();
        }

        public void Execute(object? parameter)
        {
            _execute();
        }
    }
}
