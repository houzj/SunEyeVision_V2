using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Views.Controls.Panels;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.UI.Models
{
    /// <summary>
    /// 图像输入源 - 存储用户选择的图像文件（只读语义）
    /// </summary>
    /// <remarks>
    /// 设计原则：
    /// 1. 输入源是用户选择的原始数据，执行流程不应修改
    /// 2. 提供只读语义，保护用户输入不被意外修改
    /// 3. 支持运行模式选择（运行全部/运行选择）
    ///
    /// 与 NodeOutputCache 的区别：
    /// - ImageInputSource: 输入数据，用户选择，只读
    /// - NodeOutputCache: 输出数据，执行结果，可变
    /// </remarks>
    public class ImageInputSource : ObservableObject, INotifyCollectionChanged
    {
        private readonly BatchObservableCollection<ImageInfo> _images;
        private int _currentIndex = -1;
        private ImageRunMode _runMode = ImageRunMode.运行全部;
        private bool _autoSwitchEnabled = true;
        private bool _isReadOnly = false;

        /// <summary>
        /// 关联的节点ID
        /// </summary>
        public string NodeId { get; }

        /// <summary>
        /// 图像集合（只读访问）
        /// </summary>
        public BatchObservableCollection<ImageInfo> Images => _images;

        /// <summary>
        /// 当前选中图像索引
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
                    OnPropertyChanged(nameof(CurrentImage));
                    CurrentImageChanged?.Invoke(this, value);
                }
            }
        }

        /// <summary>
        /// 当前选中的图像
        /// </summary>
        public ImageInfo? CurrentImage => 
            _currentIndex >= 0 && _currentIndex < _images.Count 
                ? _images[_currentIndex] 
                : null;

        /// <summary>
        /// 图像总数
        /// </summary>
        public int Count => _images.Count;

        /// <summary>
        /// 是否有图像
        /// </summary>
        public bool HasImages => _images.Count > 0;

        /// <summary>
        /// 图像运行模式
        /// </summary>
        public ImageRunMode RunMode
        {
            get => _runMode;
            set
            {
                if (_runMode != value)
                {
                    _runMode = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(GetSelectedImages));
                }
            }
        }

        /// <summary>
        /// 是否启用自动切换
        /// </summary>
        public bool AutoSwitchEnabled
        {
            get => _autoSwitchEnabled;
            set => SetProperty(ref _autoSwitchEnabled, value);
        }

        /// <summary>
        /// 是否设置为只读模式（防止执行流程修改）
        /// </summary>
        public bool IsReadOnly
        {
            get => _isReadOnly;
            set => SetProperty(ref _isReadOnly, value);
        }

        /// <summary>
        /// 当前图像变化事件
        /// </summary>
        public event EventHandler<int>? CurrentImageChanged;

        /// <summary>
        /// 图像集合变化事件
        /// </summary>
        public event NotifyCollectionChangedEventHandler? CollectionChanged
        {
            add => _images.CollectionChanged += value;
            remove => _images.CollectionChanged -= value;
        }

        /// <summary>
        /// 创建图像输入源
        /// </summary>
        /// <param name="nodeId">关联的节点ID</param>
        public ImageInputSource(string nodeId)
        {
            NodeId = nodeId ?? throw new ArgumentNullException(nameof(nodeId));
            _images = new BatchObservableCollection<ImageInfo>();
        }

        /// <summary>
        /// 添加图像（用户操作）
        /// </summary>
        public void AddImage(ImageInfo image)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("输入源已锁定为只读，无法添加图像");

            _images.Add(image);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(HasImages));
            OnPropertyChanged(nameof(GetSelectedImages));
        }

        /// <summary>
        /// 批量添加图像（用户操作）
        /// </summary>
        public void AddImages(IEnumerable<ImageInfo> images)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("输入源已锁定为只读，无法添加图像");

            _images.AddRange(images);
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(HasImages));
            OnPropertyChanged(nameof(GetSelectedImages));
        }

        /// <summary>
        /// 移除图像（用户操作）
        /// </summary>
        public void RemoveImage(ImageInfo image)
        {
            if (_isReadOnly)
                throw new InvalidOperationException("输入源已锁定为只读，无法移除图像");

            _images.Remove(image);
            if (CurrentIndex >= _images.Count)
            {
                CurrentIndex = _images.Count - 1;
            }
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(HasImages));
            OnPropertyChanged(nameof(GetSelectedImages));
        }

        /// <summary>
        /// 清空所有图像（仅用于用户手动清空，非执行流程）
        /// </summary>
        public void ClearImages()
        {
            if (_isReadOnly)
                throw new InvalidOperationException("输入源已锁定为只读，无法清空图像");

            _images.Clear();
            CurrentIndex = -1;
            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(HasImages));
            OnPropertyChanged(nameof(GetSelectedImages));
        }

        /// <summary>
        /// 获取选中用于运行的图像
        /// </summary>
        public IEnumerable<ImageInfo> GetSelectedImages()
        {
            if (RunMode == ImageRunMode.运行全部)
            {
                return _images;
            }
            return _images.Where(img => img.IsForRun);
        }

        /// <summary>
        /// 获取所有图像的文件路径
        /// </summary>
        public string[] GetFilePaths()
        {
            return _images.Select(img => img.FilePath).ToArray();
        }

        /// <summary>
        /// 重置状态
        /// </summary>
        public void Reset()
        {
            ClearImages();
            RunMode = ImageRunMode.运行全部;
            AutoSwitchEnabled = true;
            IsReadOnly = false;
        }

        /// <summary>
        /// 准备显示（延迟渲染优化）
        /// </summary>
        public int PrepareForDisplay()
        {
            int count = _images.Count;
            foreach (var image in _images)
            {
                image.Thumbnail = null;
            }
            return count;
        }
    }
}
