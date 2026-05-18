using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.ViewModels;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Views
{
    public partial class RegionEditorControl : UserControl, INotifyPropertyChanged
    {
        private RegionEditorViewModel _viewModel;

        private readonly string _controlInstanceId = Guid.NewGuid().ToString("N").Substring(0, 8);

        /// <summary>编辑器类型标签，从 DefaultRegionType 派生，用于日志区分来源</summary>
        private string EditorTag => DefaultRegionType == RegionType.Mask ? "屏蔽区域" : "检测区域";

        /// <summary>编辑器实例 ID（Manager 日志引用）</summary>
        internal string InstanceId => _controlInstanceId;

        private HalconImageControl? _halconImageControl;

        // 区域名称索引管理（空洞池策略）
        private int _nextRegionIndex = 1;
        private readonly SortedSet<int> _availableIndices = new();

        public RegionEditorViewModel ViewModel => _viewModel;

        public event Action? RenderRequested;
        public event Action? ToolActivated;

        #region 依赖属性

        public static readonly DependencyProperty RegionsProperty =
            DependencyProperty.Register(nameof(Regions), typeof(IEnumerable<RegionData>), typeof(RegionEditorControl),
                new PropertyMetadata(null, OnRegionsChanged));

        public IEnumerable<RegionData>? Regions
        {
            get => (IEnumerable<RegionData>?)GetValue(RegionsProperty);
            set => SetValue(RegionsProperty, value);
        }

        private static void OnRegionsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RegionEditorControl control)
                control.RenderRequested?.Invoke();
        }

        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(nameof(AvailableDataSources),
                typeof(ObservableCollection<AvailableDataSource>),
                typeof(RegionEditorControl),
                new PropertyMetadata(null));

        public ObservableCollection<AvailableDataSource>? AvailableDataSources
        {
            get => (ObservableCollection<AvailableDataSource>?)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        public static readonly DependencyProperty DefaultRegionTypeProperty =
            DependencyProperty.Register(nameof(DefaultRegionType),
                typeof(RegionType),
                typeof(RegionEditorControl),
                new PropertyMetadata(RegionType.Inspection));

        public RegionType DefaultRegionType
        {
            get => (RegionType)GetValue(DefaultRegionTypeProperty);
            set => SetValue(DefaultRegionTypeProperty, value);
        }

        #region 区域标题属性

        public static readonly DependencyProperty HeaderTextProperty =
            DependencyProperty.Register(nameof(HeaderText), typeof(string), typeof(RegionEditorControl),
                new PropertyMetadata(null, OnHeaderTextChanged));

        public string? HeaderText
        {
            get => (string?)GetValue(HeaderTextProperty);
            set => SetValue(HeaderTextProperty, value);
        }

        public static readonly DependencyProperty HeaderFontWeightProperty =
            DependencyProperty.Register(nameof(HeaderFontWeight), typeof(FontWeight), typeof(RegionEditorControl),
                new PropertyMetadata(FontWeights.Bold));

        public FontWeight HeaderFontWeight
        {
            get => (FontWeight)GetValue(HeaderFontWeightProperty);
            set => SetValue(HeaderFontWeightProperty, value);
        }

        public static readonly DependencyProperty HeaderForegroundProperty =
            DependencyProperty.Register(nameof(HeaderForeground), typeof(Brush), typeof(RegionEditorControl),
                new PropertyMetadata(new SolidColorBrush(Color.FromRgb(0x44, 0x88, 0xFF))));

        public Brush HeaderForeground
        {
            get => (Brush)GetValue(HeaderForegroundProperty);
            set => SetValue(HeaderForegroundProperty, value);
        }

        private static void OnHeaderTextChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is RegionEditorControl ctrl && ctrl.HeaderTextBlock != null)
            {
                ctrl.HeaderTextBlock.Visibility = string.IsNullOrEmpty(ctrl.HeaderText)
                    ? Visibility.Collapsed : Visibility.Visible;
            }
        }

        #endregion

        #endregion

        #region 事件

        public event EventHandler<RegionData>? RegionDataChanged;

        #endregion

        #region 构造函数

        public RegionEditorControl()
        {
            if (DesignerProperties.GetIsInDesignMode(this))
                return;

            EnsureViewModel();
            InitializeComponent();

            ShapeSelector.ShapeTypeChanged += OnShapeTypeSelected;

            Focusable = true;
            FocusVisualStyle = null;

            Loaded += RegionEditorControl_Loaded;
            Unloaded += RegionEditorControl_Unloaded;

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[RegionEditorControl] 控件初始化完成 | InstanceId={_controlInstanceId}",
                "RegionEditorControl");
        }

        #endregion

        #region 初始化

        private void RegionEditorControl_Loaded(object sender, RoutedEventArgs e)
        {
            Keyboard.Focus(this);
        }

        private void RegionEditorControl_Unloaded(object sender, RoutedEventArgs e)
        {
        }

        private void EnsureViewModel()
        {
            if (_viewModel == null)
            {
                _viewModel = new RegionEditorViewModel();
                DataContext = _viewModel;
                _viewModel.PropertyChanged += OnViewModelPropertyChanged;
                _viewModel.RegionChanged += OnRegionChanged;
            }
        }

        #endregion

        #region 绑定 HalconImageControl

        public void SetHalconImageControl(HalconImageControl? imageControl)
        {
            // 取消旧控件的订阅
            if (_halconImageControl != null)
            {
                _halconImageControl.PreviewKeyDown -= OnImagePreviewKeyDown;
            }

            _halconImageControl = imageControl;

            // 订阅新控件
            if (_halconImageControl != null)
            {
                _halconImageControl.PreviewKeyDown += OnImagePreviewKeyDown;
            }
        }

        #endregion

        #region 区域数据管理

        public void SetRegionsSource(ObservableCollection<RegionData> externalRegions)
        {
            if (externalRegions == null)
                throw new ArgumentNullException(nameof(externalRegions));

            _viewModel.SetRegionsSource(externalRegions);
            InitializeRegionIndexFromExisting();
            ApplyAllRegionsToDrawingObjects();
            RenderRequested?.Invoke();
        }

        public IEnumerable<RegionData> GetRegions()
        {
            return _viewModel.Regions;
        }

        public RegionData? GetSelectedRegion()
        {
            return _selectedRegion;
        }

        public void SetSelectedRegion(RegionData? region)
        {
            _selectedRegion = region;
        }

        #endregion

        #region 形状选择（委托给 ShapeSelectorControl）

        private void OnShapeTypeSelected(object? sender, ShapeType shapeType)
        {
            VisionLogger.Instance.Log(LogLevel.Success,
                $"══════ 全链监控 ══════ {EditorTag}#{_controlInstanceId} 形状选择 → 绘制启动 | ShapeType={shapeType}",
                "RegionEditorControl");

            _viewModel.IsDrawing = true;
            _viewModel.SelectedShapeType = shapeType;
            StartDrawingSession(shapeType);
            ToolActivated?.Invoke();
        }

        #endregion

        #region 空闲模式

        /// <summary>进入空闲模式</summary>
        public void EnterIdleMode()
        {
            _viewModel.SelectedRegion = null;
            ShapeSelector.DeselectAll();
            _viewModel.IsDrawing = false;
        }

        #endregion

        #region 区域名称索引管理

        private void InitializeRegionIndexFromExisting()
        {
            _nextRegionIndex = 1;
            _availableIndices.Clear();

            foreach (var region in _viewModel.Regions)
            {
                if (region.Name != null && region.Name.Length > 1)
                {
                    var suffix = region.Name.Substring(1);
                    if (int.TryParse(suffix, out int index))
                    {
                        _availableIndices.Add(index);
                    }
                }
            }
        }

        #endregion

        #region ViewModel 事件

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(RegionEditorViewModel.IsDrawing))
            {
                if (!_viewModel.IsDrawing)
                {
                    CancelInteractiveDrawing();
                }
            }
        }

        private void OnRegionChanged(object? sender, EventArgs e)
        {
            RegionDataChanged?.Invoke(this, _selectedRegion!);
        }

        #endregion

        #region 公共方法

        public void DeselectAll()
        {
            _selectedRegion = null;
            RenderRequested?.Invoke();
        }

        public bool IsAnyInteractionInProgress => false;

        public void HandleDelete()
        {
            RenderRequested?.Invoke();
        }

        public void HandleEscape()
        {
            _viewModel.IsDrawing = false;
            CancelInteractiveDrawing();
            DeselectAll();
        }

        #endregion

        #region INotifyPropertyChanged

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged(string propertyName)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        #endregion

        #region 旧接口保留（空实现，保持编译兼容）

        private RegionData? _selectedRegion;

        #endregion

        #region ========== HDrawingObject 交互式绘制 ==========

        /// <summary>启动绘制会话</summary>
        public void StartDrawingSession(ShapeType shapeType)
        {
            if (_halconImageControl == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"[RegionEditorControl] StartDrawingSession 跳过 → HalconImageControl 为空",
                    "RegionEditorControl");
                return;
            }

            _halconImageControl.BeginInteractiveDraw(shapeType);
        }

        /// <summary>结束绘制会话并创建区域</summary>
        public void EndDrawingSession()
        {
            if (_halconImageControl == null) return;

            if (!_halconImageControl.EndInteractiveDraw(out var shape) || shape == null)
            {
                CancelInteractiveDrawing();
                return;
            }

            var shapeType = shape.ShapeType;
            var regionName = _viewModel.RequestNextRegionName();
            var region = RegionData.CreateDrawingRegion(regionName, shapeType);

            if (region.Parameters is ShapeParameters target)
            {
                target.CenterX = shape.CenterX;
                target.CenterY = shape.CenterY;
                target.Width = shape.Width;
                target.Height = shape.Height;
                target.Radius = shape.Radius;
                target.StartX = shape.StartX;
                target.StartY = shape.StartY;
                target.EndX = shape.EndX;
                target.EndY = shape.EndY;

                if (shapeType == ShapeType.Polygon)
                {
                    target.Points.Clear();
                    foreach (var pt in shape.Points)
                        target.Points.Add(new Point2D(pt.X, pt.Y));
                }

                if (DefaultRegionType == RegionType.Mask)
                {
                    region.RegionType = RegionType.Mask;
                }
            }

            _viewModel.Regions.Add(region);
            _viewModel.SelectedRegion = region;
            _viewModel.StatusMessage = $"已添加 {shapeType} 区域";

            // 渲染为 HDrawingObject
            _halconImageControl.RenderRegionDrawingObject(region.Id, shape);

            _viewModel.FireRegionChanged(region, RegionChangeType.Added);

            ShapeSelector.DeselectAll();
            _viewModel.IsDrawing = false;

            VisionLogger.Instance.Log(LogLevel.Success,
                $"══════ 全链监控 ══════ {EditorTag}#{_controlInstanceId} 区域已创建 | Name={regionName} | Type={shapeType}",
                "RegionEditorControl");
        }

        /// <summary>取消交互式绘制</summary>
        private void CancelInteractiveDrawing()
        {
            _halconImageControl?.CancelInteractiveDraw();
        }

        #endregion

        #region ========== HDrawingObject 渲染管理 ==========

        /// <summary>将所有区域渲染为 HDrawingObject（初始化或重置）</summary>
        public void ApplyAllRegionsToDrawingObjects()
        {
            if (_halconImageControl == null) return;

            _halconImageControl.ClearAllRegionDrawingObjects();

            foreach (var region in _viewModel.Regions)
            {
                if (region.Parameters is ShapeParameters shape)
                {
                    _halconImageControl.RenderRegionDrawingObject(region.Id, shape);
                }
            }
        }

        /// <summary>移除单个区域的 HDrawingObject</summary>
        public void DetachRegionDrawingObject(RegionData region)
        {
            _halconImageControl?.RemoveRegionDrawingObject(region.Id);
        }

        #endregion

        #region ========== Delete 键删除选中区域 ==========

        private void OnImagePreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Delete || _viewModel.SelectedRegion == null)
                return;

            DeleteSelectedRegion();
            e.Handled = true;
        }

        private void DeleteSelectedRegion()
        {
            var region = _viewModel.SelectedRegion;
            if (region == null) return;

            var regionName = region.Name;

            DetachRegionDrawingObject(region);
            _viewModel.RemoveSelectedRegion();

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[RegionEditorControl] Delete: 区域已删除 | Name={regionName}",
                "RegionEditorControl");

            RenderRequested?.Invoke();
        }

        #endregion
    }
}
