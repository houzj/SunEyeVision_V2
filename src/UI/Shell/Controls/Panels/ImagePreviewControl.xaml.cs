using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Services.Thumbnail;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Shell.Controls.Panels
{
    /// <summary>
    /// 图像预览控件
    /// </summary>
    /// <remarks>
    /// 缩略图架构（2026-08-18 治本）：
    /// - ImageInfo.EnsureThumbnail：后台线程 WIC 解码（DecodePixelWidth=100 + Freeze）+ 静态 LRU 缓存，
    ///   回 UI 线程绑定 Thumbnail；滚动时缓存命中秒出，未命中后台解码不阻塞 UI。
    /// - 已清理自研加载/缓存/调度死代码（PriorityThumbnailLoader/SmartThumbnailLoader/WicGpuDecoder/
    ///   ImageSharpDecoder/ThumbnailCacheManager/MemoryPressureMonitor/FilePathToImageSourceConverter）。
    /// - 保留对外契约：4 个依赖属性、WorkflowExecutionRequested 事件、4 个命令、ImageCountDisplay。
    /// - 图像条目归Shared，解码/缓存归Services.Thumbnail；本控件仅消费，不拥有业务资产。
    /// </remarks>
    public partial class ImagePreviewControl : System.Windows.Controls.UserControl, INotifyPropertyChanged
    {
        // ListBox 控件引用（用于同步选中项）
        private ListBox? _thumbnailListBox;
        private ImageInfo? _highlightedImage;
        private readonly HashSet<ImageInfo> _realizedThumbnails = new();
        private const int MaximumRealizedThumbnails = 128;

        // 防止 CurrentImageIndex DP ↔ ListBox.SelectedItem 循环回调
        private bool _isSyncingListBox;

        // 主动驱动刷新：滚动停止后 Debounce 触发（对齐 ImageGlass RefreshRealizedThumbnails）
        private readonly System.Windows.Threading.DispatcherTimer _refreshDebounceTimer;

        public static readonly DependencyProperty AutoSwitchEnabledProperty =
            DependencyProperty.Register("AutoSwitchEnabled", typeof(bool), typeof(ImagePreviewControl),
                new PropertyMetadata(false));

        public static readonly DependencyProperty CurrentImageIndexProperty =
            DependencyProperty.Register("CurrentImageIndex", typeof(int), typeof(ImagePreviewControl),
                new PropertyMetadata(-1, OnCurrentImageIndexChanged));

        public static readonly DependencyProperty ImageCollectionProperty =
            DependencyProperty.Register("ImageCollection", typeof(ReadOnlyObservableCollection<ImageInfo>), typeof(ImagePreviewControl),
                new PropertyMetadata(null, OnImageCollectionChanged));

        /// <summary>
        /// 绑定的活动输入源（真相源）。AppendFiles 直接写 ImageInputSource._images，
        /// 不依赖 ImageCollection 绑定回写（Images 是只读属性，TwoWay 绑定无法建立）。
        /// </summary>
        public static readonly DependencyProperty ActiveInputSourceProperty =
            DependencyProperty.Register(nameof(ActiveInputSource), typeof(ImageInputSource), typeof(ImagePreviewControl),
                new PropertyMetadata(null));

        public static readonly DependencyProperty ImageRunModeProperty =
            DependencyProperty.Register("ImageRunMode", typeof(ImageRunMode), typeof(ImagePreviewControl),
                new PropertyMetadata(ImageRunMode.运行全部, OnImageRunModeChanged));

        /// <summary>
        /// 是否启用自动切换
        /// </summary>
        public bool AutoSwitchEnabled
        {
            get => (bool)GetValue(AutoSwitchEnabledProperty);
            set => SetCurrentValue(AutoSwitchEnabledProperty, value);
        }

        /// <summary>
        /// 当前显示的图像索引
        /// </summary>
        public int CurrentImageIndex
        {
            get => (int)GetValue(CurrentImageIndexProperty);
            set => SetCurrentValue(CurrentImageIndexProperty, value);
        }

        /// <summary>
        /// 图像集合（使用批量操作优化集合）
        /// </summary>
        public ReadOnlyObservableCollection<ImageInfo> ImageCollection
        {
            get => (ReadOnlyObservableCollection<ImageInfo>)GetValue(ImageCollectionProperty);
            set => SetValue(ImageCollectionProperty, value);
        }

        /// <summary>
        /// 绑定的活动输入源（真相源，添加图像时直接写回 ImageInputSource._images）
        /// </summary>
        public ImageInputSource? ActiveInputSource
        {
            get => (ImageInputSource?)GetValue(ActiveInputSourceProperty);
            set => SetValue(ActiveInputSourceProperty, value);
        }

        /// <summary>
        /// 图像运行模式
        /// </summary>
        public ImageRunMode ImageRunMode
        {
            get => (ImageRunMode)GetValue(ImageRunModeProperty);
            set => SetCurrentValue(ImageRunModeProperty, value);
        }

        /// <summary>
        /// 工作流执行请求事件 - 当用户点击图片请求执行工作流时触发
        /// </summary>
        public event EventHandler<WorkflowExecutionRequestEventArgs>? WorkflowExecutionRequested;

        /// <summary>
        /// 图像计数显示文本
        /// </summary>
        public string ImageCountDisplay
        {
            get
            {
                int count = ImageCollection?.Count ?? 0;
                int current = CurrentImageIndex >= 0 && CurrentImageIndex < count ? CurrentImageIndex + 1 : 0;
                return count > 0 ? $"图像源 ({current}/{count})" : "图像源";
            }
        }

        public ICommand AddImageCommand { get; }
        public ICommand AddFolderCommand { get; }
        public ICommand DeleteSingleImageCommand { get; }
        public ICommand ClearAllCommand { get; }

        public ImagePreviewControl()
        {
            InitializeComponent();
            ImageCollection = new ReadOnlyObservableCollection<ImageInfo>(new ObservableCollection<ImageInfo>());

            AddImageCommand = new RelayCommand(ExecuteAddImage);
            AddFolderCommand = new RelayCommand(ExecuteAddFolder);
            DeleteSingleImageCommand = new RelayCommand<ImageInfo>(ExecuteDeleteSingleImage);
            ClearAllCommand = new RelayCommand(ExecuteClearAll, CanExecuteClearAll);
            CancelImportCommand = new RelayCommand(CancelFileImport, () => IsImporting);


            // 滚动停止后 100ms 触发主动刷新（Debounce，避免拖动中高频遍历）
            _refreshDebounceTimer = new System.Windows.Threading.DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(100)
            };
            _refreshDebounceTimer.Tick += (s, e) =>
            {
                _refreshDebounceTimer.Stop();
                RefreshVisibleThumbnails();
            };

            Loaded += OnControlLoaded;
            Unloaded += OnUnloaded;
            SizeChanged += (s, e) => ScheduleRefreshVisibleThumbnails();
        }

        private void OnControlLoaded(object sender, RoutedEventArgs e)
        {
            // 延迟初始化，确保 ListBox 已完全加载
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _thumbnailListBox = FindName("ThumbnailListBox") as ListBox;
                if (_thumbnailListBox != null)
                {
                    // 运行选择模式下方向键只在已勾选（应运行）图之间导航
                    _thumbnailListBox.PreviewKeyDown -= OnThumbnailPreviewKeyDown;
                    _thumbnailListBox.PreviewKeyDown += OnThumbnailPreviewKeyDown;
                }
                // 控件加载后立即触发一次主动刷新，保证初始可见项都被解码（主动驱动兜底 Loaded 事件）
                RefreshVisibleThumbnails();
            }), DispatcherPriority.Loaded);
        }

        private void OnUnloaded(object sender, RoutedEventArgs e)
        {
            CancelFileImport();
            _refreshDebounceTimer.Stop();
            if (_thumbnailListBox != null) _thumbnailListBox.PreviewKeyDown -= OnThumbnailPreviewKeyDown;
            _thumbnailListBox = null;
            // Unloading a view must not clear the persisted input source.
            if (ImageCollection != null) foreach (var image in ImageCollection) image.CancelThumbnail();
            _realizedThumbnails.Clear();
        }

        #region 命令实现

        private async void ExecuteAddImage()
        {
            try
            {
                var openFileDialog = new Microsoft.Win32.OpenFileDialog
                {
                    Filter = "图像文件|*.jpg;*.jpeg;*.png;*.bmp;*.tiff|所有文件|*.*",
                    Title = "选择图像文件",
                    Multiselect = true
                };

                if (openFileDialog.ShowDialog() == true)
                {
                    await ImportFilePathsAsync(openFileDialog.FileNames);
                }
            }
            catch (Exception ex)
            {
                ThemeMessageDialog.Show($"添加图像失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            }
        }

        private async void ExecuteAddFolder()
        {
            try
            {
                var folderDialog = new Microsoft.Win32.OpenFolderDialog
                {
                    Title = "选择图像文件夹"
                };

                if (folderDialog.ShowDialog() == true)
                {
                    await ImportDirectoryAsync(folderDialog.FolderName);
                }
            }
            catch (Exception ex)
            {
                ThemeMessageDialog.Show($"添加文件夹失败: {ex.Message}", "错误",
                    MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Error);
            }
        }

        /// <summary>
        /// 虚拟化容器被物化（进入可视区域）时触发缩略图解码。
        /// 业界成熟做法：由虚拟化系统决定"哪些项可见"，容器 Loaded 即代表即将显示，
        /// 无需手算视口/索引，对项宽、DPI、滚动位置变化完全免疫。
        /// </summary>
        private void OnItemLoaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageInfo info)
            {
                if (ImageCollection?.Contains(info) != true) return;
                if (_realizedThumbnails.Contains(info) || _realizedThumbnails.Count < MaximumRealizedThumbnails)
                {
                    _realizedThumbnails.Add(info);
                    info.EnsureThumbnail();
                }
            }
        }

        /// <summary>
        /// 虚拟化容器滚出可视区域（被回收）时取消进行中的缩略图解码。
        /// 对齐 ImageGlass GalleryControl：不可见即取消，省 CPU 给当前视口。
        /// 滚动回来时 OnItemLoaded 会用新 token 重新触发解码，旧任务的结果被丢弃。
        /// </summary>
        private void OnItemUnloaded(object sender, RoutedEventArgs e)
        {
            if (sender is FrameworkElement element && element.DataContext is ImageInfo info)
            {
                _realizedThumbnails.Remove(info);
                info.CancelThumbnail();
            }
        }

        private void ExecuteDeleteSingleImage(ImageInfo? imageInfo)
        {
            if (imageInfo == null) return;
            CancelFileImport();

            // The source requests formal acceptance before changing its read-only display collection.
            var source = ActiveInputSource;
            if (source == null) return;

            int index = source.Images.IndexOf(imageInfo);
            if (index < 0) return;

            source.RemoveImage(imageInfo);

            // The truth source already adjusted the index; do not decrement twice.
            CurrentImageIndex = source.CurrentIndex;

            RefreshImageDisplayState();
        }

        private void ExecuteClearAll()
        {
            CancelFileImport();
            if (!CanExecuteClearAll()) return;
            if (ThemeMessageDialog.Show("清空所有图像引用？不会删除磁盘文件。", "清空图像",
                MessageBoxButton.YesNo, ThemeMessageDialog.MessageIcon.Question) == MessageBoxResult.Yes)
                ClearInputReferences();
        }
        public void ClearInputReferences()
        {
            if (ActiveInputSource is null) return;
            ActiveInputSource.ClearImages();
            SetCurrentValue(CurrentImageIndexProperty, -1);
            RefreshImageDisplayState();
            CommandManager.InvalidateRequerySuggested();
        }
        private bool CanExecuteClearAll() => (ActiveInputSource?.Count ?? ImageCollection?.Count ?? 0) > 0 || CurrentImageIndex >= 0;

        #endregion

        #region 辅助方法

        private static void OnCurrentImageIndexChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
        {
            var control = (ImagePreviewControl)d;
            control.UpdateImageSelection();
            control.OnPropertyChanged(nameof(ImageCountDisplay));

            if (control._thumbnailListBox != null && control.ImageCollection != null &&
                control.CurrentImageIndex >= 0 && control.CurrentImageIndex < control.ImageCollection.Count)
            {
                control._isSyncingListBox = true;
                control._thumbnailListBox.SelectedItem = control.ImageCollection[control.CurrentImageIndex];
                control._isSyncingListBox = false;
            }
        }

        private static void OnImageCollectionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var control = (ImagePreviewControl)d;
            control.CancelFileImport();
            if (control._highlightedImage != null) control._highlightedImage.IsSelected = false;
            control._highlightedImage = null;
            if (e.OldValue is ReadOnlyObservableCollection<ImageInfo> oldCollection)
            {
                ((System.Collections.Specialized.INotifyCollectionChanged)oldCollection).CollectionChanged -= control.OnBoundCollectionChanged;
                foreach (var image in oldCollection) image.CancelThumbnail();
                control._realizedThumbnails.Clear();
            }
            if (e.NewValue is ReadOnlyObservableCollection<ImageInfo> newCollection)
            {
                ((System.Collections.Specialized.INotifyCollectionChanged)newCollection).CollectionChanged += control.OnBoundCollectionChanged;
                foreach (var image in newCollection) image.IsSelected = false;
            }
            control.UpdateImageSelection();
            control.OnPropertyChanged(nameof(ImageCountDisplay));
            CommandManager.InvalidateRequerySuggested();
        }
        private void OnBoundCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            OnPropertyChanged(nameof(ImageCountDisplay));
            CommandManager.InvalidateRequerySuggested();
            RefreshImageDisplayState();
        }

        private static void OnImageRunModeChanged(DependencyObject d, DependencyPropertyChangedEventArgs _)
        {
            var control = (ImagePreviewControl)d;
            control.OnPropertyChanged(nameof(ImageRunMode));
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// 刷新图像显示状态（删除图片后同步选中项与计数）
        /// </summary>
        private void RefreshImageDisplayState()
        {
            OnPropertyChanged(nameof(ImageCountDisplay));
            UpdateImageSelection();

            if (_thumbnailListBox != null)
            {
                if (CurrentImageIndex >= 0 && ImageCollection != null && CurrentImageIndex < ImageCollection.Count)
                {
                    _isSyncingListBox = true;
                    _thumbnailListBox.SelectedItem = ImageCollection[CurrentImageIndex];
                    _isSyncingListBox = false;
                }
                else
                {
                    _thumbnailListBox.SelectedItem = null;
                }
            }
        }

        /// <summary>
        /// 更新图像选中状态（增量更新，仅变更新旧两项）
        /// </summary>
        private void UpdateImageSelection()
        {
            var next = ImageCollection != null && CurrentImageIndex >= 0 && CurrentImageIndex < ImageCollection.Count
                ? ImageCollection[CurrentImageIndex] : null;
            if (ReferenceEquals(next, _highlightedImage)) return;
            if (_highlightedImage != null) _highlightedImage.IsSelected = false;
            _highlightedImage = next;
            if (next != null) next.IsSelected = true;
        }

        #region 自动切换（流程驱动：宿主在工作流执行完成后调用推进，非固定间隔）

        /// <summary>计算"应运行"图片子集：运行全部=所有图；运行选择=仅勾选 IsForRun</summary>
        private List<ImageInfo> GetRunnableImages()
        {
            if (ImageCollection == null || ImageCollection.Count == 0)
                return new List<ImageInfo>();
            if (ImageRunMode == ImageRunMode.运行选择)
                return ImageCollection.Where(i => i.IsForRun && File.Exists(i.FilePath)).ToList();
            return ImageCollection.Where(i => File.Exists(i.FilePath)).ToList();
        }

        /// <summary>
        /// 推进到"当前图之后"的第一张应运行图并同步索引（不触发执行），**末尾循环回第一张**。
        /// 由宿主在每轮工作流执行开始前调用（单次执行点执行 / 连续执行每轮），
        /// 输出下一张应运行图供宿主注入运行时参数。仅当应运行图不足 2 张时返回 false（无实质切换）。
        /// </summary>
        public bool TryStepNextImageForAuto(out ImageInfo? nextImage)
        {
            nextImage = null;
            if (!AutoSwitchEnabled) return false;

            var runnables = GetRunnableImages();
            if (runnables.Count == 0) return false;

            int curIndex = CurrentImageIndex;
            int pos = -1;
            for (int i = 0; i < runnables.Count; i++)
            {
                if (ImageCollection.IndexOf(runnables[i]) == curIndex)
                {
                    pos = i;
                    break;
                }
            }

            // 循环推进：当前之后一张，到末尾则回绕到第一张（所有图片执行完从头再开始）
            int next = pos < 0 ? 0 : (pos + 1) % runnables.Count;
            if (next == pos)
                return false;   // 只有一张应运行图：无实质切换，沿用当前

            var target = runnables[next];
            int targetIndex = ImageCollection.IndexOf(target);
            if (targetIndex < 0) return false;

            CurrentImageIndex = targetIndex;
            nextImage = target;
            return true;
        }

        #endregion

        #endregion

        #region 交互事件

        /// <summary>
        /// 运行选择模式：方向键只在"勾选（IsForRun）应运行图"之间导航，跳过未勾选图。
        /// 所有模式均从明确的键盘入口请求执行，选中变化本身不触发执行。
        /// </summary>
        private void OnThumbnailPreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (_thumbnailListBox == null) return;
            for (var source = e.OriginalSource as DependencyObject; source != null;)
            {
                if (source is System.Windows.Controls.Primitives.ButtonBase) return;
                source = source is Visual ? VisualTreeHelper.GetParent(source) : LogicalTreeHelper.GetParent(source);
            }

            bool goNext = e.Key == Key.Right || e.Key == Key.Down;
            bool goPrev = e.Key == Key.Left || e.Key == Key.Up;
            if (e.Key == Key.Enter || e.Key == Key.Space)
            {
                if (_thumbnailListBox.SelectedItem is ImageInfo selected &&
                    (ImageRunMode != ImageRunMode.运行选择 || selected.IsForRun))
                    RequestWorkflowExecution(selected, ImageCollection.IndexOf(selected));
                e.Handled = true; return;
            }
            if (!goNext && !goPrev && e.Key != Key.Home && e.Key != Key.End) return;

            var runnables = GetRunnableImages();
            if (runnables.Count == 0) return;

            int curIndex = CurrentImageIndex;
            int pos = -1;
            for (int i = 0; i < runnables.Count; i++)
            {
                if (ImageCollection.IndexOf(runnables[i]) == curIndex)
                {
                    pos = i;
                    break;
                }
            }

            int target;
            if (e.Key == Key.Home) target = 0;
            else if (e.Key == Key.End) target = runnables.Count - 1;
            else if (pos < 0) target = goNext ? 0 : -1;
            else target = goNext ? pos + 1 : pos - 1;

            if (target < 0 || target >= runnables.Count)
            {
                // 已在边界：吃掉按键，避免高亮/选中移到未勾选图
                e.Handled = true;
                return;
            }

            var targetImage = runnables[target];
            e.Handled = true;
            _thumbnailListBox.SelectedItem = targetImage;
            _thumbnailListBox.ScrollIntoView(targetImage);
            RequestWorkflowExecution(targetImage, ImageCollection.IndexOf(targetImage));
        }

        private void OnThumbnailRemoveReference(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem { DataContext: ImageInfo image }) ExecuteDeleteSingleImage(image);
        }

        private void OnThumbnailClick(object sender, MouseButtonEventArgs e)
        {
            if (e.ChangedButton != MouseButton.Left) return;
            // 如果点击的是删除按钮或 CheckBox，跳过处理
            DependencyObject? current = e.OriginalSource as DependencyObject;
            while (current != null)
            {
                if (current is Button || current is CheckBox)
                    return;
                current = VisualTreeHelper.GetParent(current);
            }

            if (sender is Border border && border.Tag is ImageInfo imageInfo)
            {
                int index = ImageCollection.IndexOf(imageInfo);
                if (index >= 0)
                {
                    // 运行选择模式：只有已勾选的图片才执行工作流；运行全部模式：点击任意图片执行
                    if (ImageRunMode == ImageRunMode.运行选择 && !imageInfo.IsForRun)
                        return;

                    RequestWorkflowExecution(imageInfo, index);
                }
            }
        }

        private void RequestWorkflowExecution(ImageInfo imageInfo, int index)
        {
            var source=ActiveInputSource;
            if(source is null||!ReferenceEquals(ImageCollection,source.Images)||index<0||index>=source.Count||
                !ReferenceEquals(source.Images[index],imageInfo)||!File.Exists(imageInfo.FilePath))return;
            var version=source.EditVersion;
            CurrentImageIndex = index;
            if(!ReferenceEquals(ActiveInputSource,source)||source.EditVersion!=version||index>=source.Count||
                !ReferenceEquals(source.Images[index],imageInfo))return;
            WorkflowExecutionRequested?.Invoke(this,new WorkflowExecutionRequestEventArgs(source,version,imageInfo,index));
        }

        private void OnThumbnailListBoxSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_isSyncingListBox) return;

            if (e.AddedItems.Count == 0 || e.AddedItems[0] is not ImageInfo selectedImage)
                return;

            int index = ImageCollection?.IndexOf(selectedImage) ?? -1;
            if (index < 0) return;

            // Selection may come from right-click, binding, restore or keyboard.
            // Only explicit left-click/key handlers may request execution.
            CurrentImageIndex = index;
        }

        private void OnRunModeButtonClick(object sender, RoutedEventArgs e)
        {
            if (RunModePopup != null)
            {
                RunModePopup.IsOpen = !RunModePopup.IsOpen;
            }
        }

        private void OnRunModeListBoxPreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (RunModeListBox == null || RunModePopup == null) return;

            var originalSource = e.OriginalSource as DependencyObject;
            if (originalSource == null) return;

            var listBoxItem = FindParent<ListBoxItem>(originalSource);
            if (listBoxItem == null) return;

            ImageRunMode? selectedMode = null;
            if (listBoxItem.Content is TextBlock textBlock && textBlock.DataContext is ImageRunMode mode1)
            {
                selectedMode = mode1;
            }
            else if (listBoxItem.DataContext is ImageRunMode mode2)
            {
                selectedMode = mode2;
            }

            if (selectedMode.HasValue)
            {
                ImageRunMode = selectedMode.Value;
            }

            RunModePopup.IsOpen = false;
            e.Handled = true;
        }

        /// <summary>
        /// 滚动变化：Debounce 后主动刷新可见缩略图。
        /// 依赖 Loaded 事件不可靠（虚拟化复用容器时不重新触发），滚动停止后主动遍历物化项保证可见项都被触发。
        /// 对齐 ImageGlass GalleryControl.RefreshRealizedThumbnails 的主动驱动模型。
        /// </summary>
        private void OnScrollChanged(object sender, ScrollChangedEventArgs e)
        {
            ScheduleRefreshVisibleThumbnails();
        }

        /// <summary>
        /// Debounce 调度主动刷新：拖动中高频滚动只重置计时器，滚动停止 100ms 后才真正刷新一次。
        /// </summary>
        private void ScheduleRefreshVisibleThumbnails()
        {
            _refreshDebounceTimer.Stop();
            _refreshDebounceTimer.Start();
        }

        /// <summary>
        /// 主动遍历虚拟化系统当前物化的 ListBoxItem，对可见项幂等触发缩略图解码。
        /// 物化 = 虚拟化系统判定"即将显示"（含可视区 + 缓冲），故无需手算视口像素。
        /// EnsureThumbnail 幂等：已加载 / Loading 中跳过，Pending / Failed 触发。
        /// 对齐 ImageGlass GalleryControl.axaml.cs:246-252 RefreshRealizedThumbnails。
        /// </summary>
        private void RefreshVisibleThumbnails()
        {
            if (_thumbnailListBox == null || ImageCollection == null) return;
            var visible = FindVisualChildren<ListBoxItem>(_thumbnailListBox)
                .Select(item => item.DataContext).OfType<ImageInfo>()
                .Where(info => ImageCollection.Contains(info)).Take(MaximumRealizedThumbnails).ToHashSet();
            foreach (var old in _realizedThumbnails.Where(info => !visible.Contains(info)).ToArray()) old.CancelThumbnail();
            _realizedThumbnails.Clear();
            foreach (var info in visible)
            {
                _realizedThumbnails.Add(info);
                info.EnsureThumbnail();
            }
        }

        /// <summary>
        /// 深度优先遍历可视化树，收集所有 T 类型的子节点。
        /// </summary>
        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject parent) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typed) yield return typed;
                foreach (var descendant in FindVisualChildren<T>(child))
                {
                    yield return descendant;
                }
            }
        }

        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            var parentObject = VisualTreeHelper.GetParent(child);
            if (parentObject == null) return null;
            return parentObject is T parent ? parent : FindParent<T>(parentObject);
        }

        #endregion

        /// <summary>
        /// 属性更改通知
        /// </summary>
        private void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
    }

    /// <summary>
    /// 工作流执行请求事件参数
    /// </summary>
    public sealed class WorkflowExecutionRequestEventArgs(ImageInputSource source,long editVersion,ImageInfo imageInfo,int index) : EventArgs
    {
        public ImageInputSource Source {get;}=source;
        public long EditVersion {get;}=editVersion;
        public ImageInfo ImageInfo {get;}=imageInfo;
        public int Index {get;}=index;
    }

    /// <summary>
    /// 图像运行模式显示转换器
    /// </summary>
    public class ImageRunModeDisplayConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ImageRunMode runMode)
            {
                return runMode switch
                {
                    ImageRunMode.运行全部 => "运行全部",
                    ImageRunMode.运行选择 => "运行选中",
                    _ => "运行全部"
                };
            }
            return "运行全部";
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is string str)
            {
                return str switch
                {
                    "运行全部" => ImageRunMode.运行全部,
                    "运行选中" => ImageRunMode.运行选择,
                    _ => ImageRunMode.运行全部
                };
            }
            return ImageRunMode.运行全部;
        }
    }

    /// <summary>
    /// 运行模式到可见性转换器（运行选择模式时显示）
    /// </summary>
    public class RunModeToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value is ImageRunMode mode && mode == ImageRunMode.运行选择)
            {
                return Visibility.Visible;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
