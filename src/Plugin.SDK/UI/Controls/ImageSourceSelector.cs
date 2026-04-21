using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 图像源选择器 - 显示所有父节点的图像输出供选择（核心控件，不包含标签和外框）
    /// </summary>
    /// <remarks>
    /// 用于工作流中选择输入图像来源。显示当前节点所有上游节点的图像输出端口。
    /// 与 BindableParameter 保持一致的设计模式：
    /// - 使用 AvailableDataSource 作为统一数据模型
    /// - SelectedDataSource 类型为 AvailableDataSource?，与参数属性类型一致
    /// - 唯一区别是下拉样式：图像源用 ListBox，数值源用 TreeView
    /// 
    /// 设计理念：
    /// - 统一绑定到 AvailableDataSources（包含所有类型的数据源）
    /// - 控件内部根据参数类型自动过滤
    /// - 简化 XAML 绑定：统一使用 AvailableDataSources
    /// 
    /// 使用示例：
    /// <code>
    /// &lt;controls:ImageSourceSelector
    ///     AvailableDataSources="{Binding AvailableDataSources, RelativeSource={RelativeSource FindAncestor, AncestorType={x:Type controls:ToolDebugControlBase}}}"
    ///     SelectedDataSource="{Binding Parameters.ImageSource, Mode=TwoWay}" /&gt;
    /// </code>
    /// </remarks>
    public class ImageSourceSelector : Control
    {
        static ImageSourceSelector()
        {
            DefaultStyleKeyProperty.OverrideMetadata(typeof(ImageSourceSelector),
                new FrameworkPropertyMetadata(typeof(ImageSourceSelector)));
        }

        #region 依赖属性

        public static readonly DependencyProperty SelectedDataSourceProperty =
            DependencyProperty.Register(nameof(SelectedDataSource), typeof(AvailableDataSource), typeof(ImageSourceSelector),
                new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, OnSelectedDataSourceChanged));

        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(nameof(AvailableDataSources), typeof(ObservableCollection<AvailableDataSource>), typeof(ImageSourceSelector),
                new PropertyMetadata(null, OnAvailableDataSourcesChanged));

        public static readonly DependencyProperty PlaceholderTextProperty =
            DependencyProperty.Register(nameof(PlaceholderText), typeof(string), typeof(ImageSourceSelector),
                new PropertyMetadata("选择图像源..."));

        public static readonly DependencyProperty SelectorWidthProperty =
            DependencyProperty.Register(nameof(SelectorWidth), typeof(double), typeof(ImageSourceSelector),
                new PropertyMetadata(200.0));

        public static readonly DependencyProperty FilteredImageSourcesProperty =
            DependencyProperty.Register(nameof(FilteredImageSources), typeof(ObservableCollection<AvailableDataSource>), typeof(ImageSourceSelector),
                new PropertyMetadata(null));

        #endregion

        #region 属性

        /// <summary>
        /// 当前选中的图像源（与 BindableParameter 绑定模式一致）
        /// </summary>
        public AvailableDataSource SelectedDataSource
        {
            get => (AvailableDataSource)GetValue(SelectedDataSourceProperty);
            set => SetValue(SelectedDataSourceProperty, value);
        }

        /// <summary>
        /// 所有类型的数据源集合（统一绑定源，控件内部过滤图像类型）
        /// </summary>
        public ObservableCollection<AvailableDataSource> AvailableDataSources
        {
            get => (ObservableCollection<AvailableDataSource>)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        /// <summary>
        /// 占位符文本
        /// </summary>
        public string PlaceholderText
        {
            get => (string)GetValue(PlaceholderTextProperty);
            set => SetValue(PlaceholderTextProperty, value);
        }

        /// <summary>
        /// 选择器宽度（默认200px）
        /// </summary>
        public double SelectorWidth
        {
            get => (double)GetValue(SelectorWidthProperty);
            set => SetValue(SelectorWidthProperty, value);
        }

        /// <summary>
        /// 过滤后的图像源集合（仅包含 Mat 类型的数据源，供 ListBox 绑定）
        /// </summary>
        public ObservableCollection<AvailableDataSource> FilteredImageSources
        {
            get => (ObservableCollection<AvailableDataSource>)GetValue(FilteredImageSourcesProperty);
            set => SetValue(FilteredImageSourcesProperty, value);
        }

        #endregion

        #region 事件

        /// <summary>
        /// 图像源选择变更事件
        /// </summary>
        public static readonly RoutedEvent ImageSourceChangedEvent =
            EventManager.RegisterRoutedEvent(nameof(ImageSourceChanged), RoutingStrategy.Bubble,
                typeof(RoutedEventHandler), typeof(ImageSourceSelector));

        public event RoutedEventHandler ImageSourceChanged
        {
            add => AddHandler(ImageSourceChangedEvent, value);
            remove => RemoveHandler(ImageSourceChangedEvent, value);
        }

        #endregion

        #region 控件引用

        private Button _selectorButton = null!;
        private Popup _popup = null!;
        private ListBox _imageSourceList = null!;
        private ObservableCollection<AvailableDataSource>? _currentDataSources;

        #endregion

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _selectorButton = GetTemplateChild("PART_SelectorButton") as Button 
                ?? throw new InvalidOperationException("PART_SelectorButton not found");
            _popup = GetTemplateChild("PART_Popup") as Popup 
                ?? throw new InvalidOperationException("PART_Popup not found");
            _imageSourceList = GetTemplateChild("PART_ImageSourceList") as ListBox 
                ?? throw new InvalidOperationException("PART_ImageSourceList not found");

            _selectorButton.Click += OnSelectorButtonClick;
            _imageSourceList.SelectionChanged += OnImageSourceListSelectionChanged;

            // 初始化过滤集合
            FilteredImageSources = new ObservableCollection<AvailableDataSource>();

            // 监听 AvailableDataSources 集合变化
            AttachCollectionListener();

            // 立即重建过滤列表（处理 AvailableDataSources 在模板应用前已设置的情况）
            RebuildFilteredList();
        }

        private void OnSelectorButtonClick(object sender, RoutedEventArgs e)
        {
            _popup.IsOpen = !_popup.IsOpen;
        }

        private void OnImageSourceListSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_imageSourceList.SelectedItem is AvailableDataSource selectedSource)
            {
                SelectedDataSource = selectedSource;
                _popup.IsOpen = false;
                RaiseEvent(new RoutedEventArgs(ImageSourceChangedEvent));
            }
        }

        private static void OnSelectedDataSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageSourceSelector selector)
            {
                selector.SyncSelectionWithFilteredList();
            }
        }

        private static void OnAvailableDataSourcesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ImageSourceSelector selector)
            {
                // 取消旧集合的监听
                if (e.OldValue is ObservableCollection<AvailableDataSource> oldCollection)
                {
                    oldCollection.CollectionChanged -= selector.OnAvailableDataSourcesCollectionChanged;
                    selector._currentDataSources = null;
                }

                // 保存新集合引用并监听
                selector.AttachCollectionListener();

                // 重建过滤列表
                selector.RebuildFilteredList();
            }
        }

        /// <summary>
        /// 监听 AvailableDataSources 集合的 CollectionChanged 事件
        /// </summary>
        private void AttachCollectionListener()
        {
            // 先取消旧监听
            if (_currentDataSources != null)
            {
                _currentDataSources.CollectionChanged -= OnAvailableDataSourcesCollectionChanged;
            }

            _currentDataSources = AvailableDataSources;

            if (_currentDataSources != null)
            {
                _currentDataSources.CollectionChanged += OnAvailableDataSourcesCollectionChanged;
            }
        }

        private void OnAvailableDataSourcesCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            RebuildFilteredList();
        }

        /// <summary>
        /// 从 AvailableDataSources 中筛选 Mat 类型的数据源，重建 FilteredImageSources
        /// </summary>
        private void RebuildFilteredList()
        {
            if (FilteredImageSources == null)
                return;

            // 使用名称匹配过滤 Mat 类型的数据源
            // 与之前 XAML DataTrigger (PropertyType.Name=="Mat") 行为一致，比精确类型匹配更健壮
            var filtered = AvailableDataSources != null
                ? AvailableDataSources.Where(ds => ds.PropertyType?.Name == "Mat").ToList()
                : new List<AvailableDataSource>();

            FilteredImageSources.Clear();
            foreach (var source in filtered)
            {
                FilteredImageSources.Add(source);
            }

            // 同步选中项引用
            SyncSelectionWithFilteredList();
        }

        /// <summary>
        /// 同步选中项引用：确保 SelectedDataSource 指向 FilteredImageSources 中的实例
        /// </summary>
        /// <remarks>
        /// ViewModel 绑定可能推来一个旧引用（与 FilteredImageSources 中的实例身份相同但引用不同），
        /// ListBox 无法匹配旧引用，导致选中项为空。此方法将旧引用替换为 FilteredImageSources 中的匹配实例。
        /// 
        /// 依赖 AvailableDataSource 实现了基于身份的值相等（IEquatable），
        /// FilteredImageSources.Contains() 能正确匹配。
        /// </remarks>
        private void SyncSelectionWithFilteredList()
        {
            if (FilteredImageSources == null || FilteredImageSources.Count == 0)
                return;

            // 没有选中项，自动选中第一个
            if (SelectedDataSource == null)
            {
                SetCurrentValue(SelectedDataSourceProperty, FilteredImageSources[0]);
                PluginLogger.Info(
                    $"图像源自动选中: {FilteredImageSources[0].DropdownDisplayText}",
                    "ImageSourceSelector");
                return;
            }

            // 如果已在列表中（引用相同或值相等），确保引用指向列表中的实例
            if (FilteredImageSources.Contains(SelectedDataSource))
            {
                var match = FilteredImageSources.First(ds => ds.Equals(SelectedDataSource));
                if (!ReferenceEquals(match, SelectedDataSource))
                {
                    SetCurrentValue(SelectedDataSourceProperty, match);
                }
                return;
            }

            // 不在列表中（数据源已移除），回退到第一个或清空
            SetCurrentValue(SelectedDataSourceProperty,
                FilteredImageSources.Count > 0 ? FilteredImageSources[0] : null);
        }
    }
}
