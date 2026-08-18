using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Shell.Controls.Panels;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Workflow;

using SunEyeVision.UI.Features.WorkflowUI;

namespace SunEyeVision.UI.Shared
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
                    NotifyConfigChanged();
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
                    NotifyConfigChanged();
                }
            }
        }

        /// <summary>
        /// 是否启用自动切换
        /// </summary>
        public bool AutoSwitchEnabled
        {
            get => _autoSwitchEnabled;
            set
            {
                if (SetProperty(ref _autoSwitchEnabled, value))
                {
                    NotifyConfigChanged();
                }
            }
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
        /// 配置同步回调：每次状态变更（加/删/清/切图、模式切换）时把最新配置写回父节点。
        /// 内存实时同步：父节点据此更新 ImageSourceConfig（Solution 共享对象），保存时自动带出，
        /// 避免"保存入口各自转换"导致的漂移（与全局变量/参数实时同步模式对齐）。
        /// </summary>
        public Action<ImageSourceConfig>? ConfigChanged { get; set; }

        /// <summary>
        /// 触发配置同步：把当前状态转成可序列化配置并通知父节点。
        /// </summary>
        private void NotifyConfigChanged()
        {
            ConfigChanged?.Invoke(ToConfig());
        }

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
            NotifyConfigChanged();
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
            NotifyConfigChanged();
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
            NotifyConfigChanged();
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
            NotifyConfigChanged();
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

        /// <summary>
        /// 转为可序列化配置（保存方案时调用）
        /// </summary>
        /// <remarks>
        /// 只导出路径与状态，不导出图片；对齐参考项目 VisionMaster 只存路径的持久化模式。
        /// </remarks>
        public ImageSourceConfig ToConfig()
        {
            return new ImageSourceConfig
            {
                NodeId = NodeId,
                FilePaths = GetFilePaths().ToList(),
                CurrentIndex = CurrentIndex,
                AutoSwitchEnabled = AutoSwitchEnabled,
                RunMode = (int)RunMode,
                ForRunPaths = _images.Where(img => img.IsForRun).Select(img => img.FilePath).ToList()
            };
        }

        /// <summary>
        /// 从配置恢复输入源（打开方案时调用）
        /// </summary>
        /// <remarks>
        /// 依据 FilePaths 重建 ImageInfo（保留勾选路径），并恢复当前索引/运行模式/自动切换状态。
        /// </remarks>
        public void LoadFromConfig(ImageSourceConfig config)
        {
            if (config == null) throw new ArgumentNullException(nameof(config));

            var infos = config.FilePaths
                .Where(p => !string.IsNullOrEmpty(p))
                .Select(p => new ImageInfo
                {
                    FilePath = p,
                    Name = System.IO.Path.GetFileName(p),
                    IsForRun = config.ForRunPaths?.Contains(p) == true
                })
                .ToList();

            _images.Clear();
            if (infos.Count > 0)
            {
                _images.AddRange(infos);
            }

            CurrentIndex = config.CurrentIndex >= 0 && config.CurrentIndex < _images.Count
                ? config.CurrentIndex
                : -1;
            AutoSwitchEnabled = config.AutoSwitchEnabled;
            RunMode = config.RunMode == 1 ? ImageRunMode.运行选择 : ImageRunMode.运行全部;

            // 缩略图由虚拟化容器 Loaded 事件按需解码（OnItemLoaded），恢复路径无需全量触发，
            // 避免海量图片同时排队解码导致首屏/远端延迟。

            OnPropertyChanged(nameof(Count));
            OnPropertyChanged(nameof(HasImages));
            OnPropertyChanged(nameof(GetSelectedImages));
        }
    }
}
