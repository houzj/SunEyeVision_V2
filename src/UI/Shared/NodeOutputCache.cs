using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Imaging;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Views.Controls.Panels;

namespace SunEyeVision.UI.Models
{
    /// <summary>
    /// 节点输出缓存 - 存储执行结果图像和数据
    /// </summary>
    /// <remarks>
    /// 设计原则：
    /// 1. 输出缓存是执行结果的容器，可被执行流程更新
    /// 2. 与输入源分离，确保用户输入不被执行结果覆盖
    /// 3. 支持历史记录和当前结果的切换
    /// 
    /// 与 ImageInputSource 的区别：
    /// - ImageInputSource: 输入数据，用户选择，只读
    /// - NodeOutputCache: 输出数据，执行结果，可变
    /// </remarks>
    public class NodeOutputCache : INotifyPropertyChanged, INotifyCollectionChanged
    {
        private readonly BatchObservableCollection<OutputImageInfo> _outputImages;
        private int _currentIndex = -1;
        private ToolResults? _lastResult;
        private DateTime? _lastExecutionTime;
        private bool _hasOutput;
        private string? _executionError;

        /// <summary>
        /// 关联的节点ID
        /// </summary>
        public string NodeId { get; }

        /// <summary>
        /// 输出图像集合
        /// </summary>
        public BatchObservableCollection<OutputImageInfo> OutputImages => _outputImages;

        /// <summary>
        /// 当前显示的输出图像索引
        /// </summary>
        public int CurrentIndex
        {
            get => _currentIndex;
            set
            {
                if (_currentIndex != value)
                {
                    _currentIndex = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(CurrentOutputImage));
                    CurrentImageChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// 当前显示的输出图像
        /// </summary>
        public OutputImageInfo? CurrentOutputImage =>
            _currentIndex >= 0 && _currentIndex < _outputImages.Count
                ? _outputImages[_currentIndex]
                : null;

        /// <summary>
        /// 输出图像总数
        /// </summary>
        public int Count => _outputImages.Count;

        /// <summary>
        /// 是否有输出
        /// </summary>
        public bool HasOutput
        {
            get => _hasOutput;
            private set => SetProperty(ref _hasOutput, value);
        }

        /// <summary>
        /// 最近一次执行结果
        /// </summary>
        public ToolResults? LastResult
        {
            get => _lastResult;
            private set
            {
                if (_lastResult != value)
                {
                    _lastResult = value;
                    OnPropertyChanged();
                    LastResultChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// 最近执行时间
        /// </summary>
        public DateTime? LastExecutionTime
        {
            get => _lastExecutionTime;
            private set => SetProperty(ref _lastExecutionTime, value);
        }

        /// <summary>
        /// 执行错误信息（如果有）
        /// </summary>
        public string? ExecutionError
        {
            get => _executionError;
            private set => SetProperty(ref _executionError, value);
        }

        /// <summary>
        /// 当前图像变化事件
        /// </summary>
        public event EventHandler<int>? CurrentImageChanged;

        /// <summary>
        /// 结果变化事件
        /// </summary>
        public event EventHandler<ToolResults?>? LastResultChanged;

        /// <summary>
        /// 集合变化事件
        /// </summary>
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => _outputImages.CollectionChanged += value;
            remove => _outputImages.CollectionChanged -= value;
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        /// <summary>
        /// 创建节点输出缓存
        /// </summary>
        /// <param name="nodeId">关联的节点ID</param>
        public NodeOutputCache(string nodeId)
        {
            NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            _outputImages = new BatchObservableCollection<OutputImageInfo>();
        }

        /// <summary>
        /// 更新执行结果（执行流程调用）
        /// </summary>
        /// <param name="result">执行结果</param>
        /// <param name="outputImage">输出图像</param>
        /// <param name="sourceFilePath">源文件路径（可选）</param>
        public void Update(ToolResults result, BitmapSource? outputImage, string? sourceFilePath = null)
        {
            if (result == null) return;

            // 缓存结果
            LastResult = result;
            LastExecutionTime = DateTime.Now;
            ExecutionError = null;
            HasOutput = true;

            // 添加输出图像（如果有）
            if (outputImage != null)
            {
                var imageInfo = new OutputImageInfo
                {
                    Id = Guid.NewGuid().ToString(),
                    Name = !string.IsNullOrEmpty(sourceFilePath)
                        ? System.IO.Path.GetFileName(sourceFilePath)
                        : $"Output_{DateTime.Now:HHmmss}",
                    SourceFilePath = sourceFilePath ?? string.Empty,
                    Image = outputImage,
                    Timestamp = DateTime.Now
                };

                AddOutputImage(imageInfo);
            }
        }

        /// <summary>
        /// 设置执行错误
        /// </summary>
        public void SetError(string error)
        {
            ExecutionError = error;
            LastExecutionTime = DateTime.Now;
        }

        /// <summary>
        /// 添加输出图像
        /// </summary>
        public void AddOutputImage(OutputImageInfo image)
        {
            _outputImages.Add(image);
            CurrentIndex = _outputImages.Count - 1;
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(HasOutput));
        }

        /// <summary>
        /// 批量添加输出图像
        /// </summary>
        public void AddOutputImages(IEnumerable<OutputImageInfo> images)
        {
            _outputImages.AddRange(images);
            CurrentIndex = _outputImages.Count - 1;
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(HasOutput));
        }

        /// <summary>
        /// 清空输出（用于重新执行或重置）
        /// </summary>
        public void Clear()
        {
            _outputImages.Clear();
            CurrentIndex = -1;
            LastResult = null;
            LastExecutionTime = null;
            ExecutionError = null;
            HasOutput = false;
            OnPropertyChanged(nameof(Count));
        }

        /// <summary>
        /// 重置状态
        /// </summary>
        public void Reset()
        {
            Clear();
        }

        /// <summary>
        /// 获取所有输出图像的名称列表
        /// </summary>
        public IEnumerable<string> GetImageNames()
        {
            return _outputImages.Select(img => img.Name);
        }

        protected bool SetProperty<T>(ref T field, T value, [CallerMemberName] string? name = null)
        {
            if (EqualityComparer<T>.Default.Equals(field, value)) return false;
            field = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(name));
            return true;
        }

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    /// <summary>
    /// 输出图像信息
    /// </summary>
    public class OutputImageInfo : INotifyPropertyChanged
    {
        private BitmapSource? _image;
        private string? _imageSourceId;

        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = string.Empty;
        public string SourceFilePath { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.Now;

        /// <summary>
        /// 关联的图像源ID（用于从ImageSourceManager获取图像）
        /// </summary>
        public string? ImageSourceId
        {
            get => _imageSourceId;
            set
            {
                if (_imageSourceId != value)
                {
                    _imageSourceId = value;
                    OnPropertyChanged();
                }
            }
        }

        /// <summary>
        /// 输出图像（向后兼容，推荐使用ImageSourceId）
        /// </summary>
        public BitmapSource? Image
        {
            get => _image;
            set
            {
                if (_image != value)
                {
                    _image = value;
                    OnPropertyChanged();
                }
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
