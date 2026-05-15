using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
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
        private ShapeType _currentTool = ShapeType.Rectangle;
        private Button? _activeShapeButton;

        private readonly string _controlInstanceId = Guid.NewGuid().ToString("N").Substring(0, 8);

        private HalconImageControl? _halconImageControl;

        // 区域名称索引管理（空洞池策略）
        private int _nextRegionIndex = 1;
        private readonly SortedSet<int> _availableIndices = new();

        #region 双向同步防护

        // 防止 DrawingObject 拖拽 → ShapeParameters → PropertyChanged → DrawingObject 循环递归
        private bool _isSyncingToShape;
        private bool _isSyncingToDraw;

        // 已订阅 PropertyChanged 的 ShapeParameters 区域 ID
        private readonly HashSet<Guid> _subscribedShapeIds = new();

        // 已创建 DrawingObject 的区域 ID
        private readonly Dictionary<Guid, ShapeType> _regionDrawTypes = new();

        // 去抖定时器：ShapeParameters PropertyChanged → 延迟更新 DrawingObject
        private const double DEBOUNCE_INTERVAL_MS = 50;
        private readonly Dictionary<Guid, DispatcherTimer> _debounceTimers = new();

        #endregion

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

        public ShapeType CurrentShapeTool => _currentTool;

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
            // 清理所有去抖定时器
            foreach (var timer in _debounceTimers.Values)
            {
                timer.Stop();
            }
            _debounceTimers.Clear();
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
            _halconImageControl = imageControl;
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

        #region 形状选择

        private void OnSelectButtonClick(object sender, RoutedEventArgs e)
        {
            SetActiveTool(ShapeType.Point, sender as Button);
            _viewModel.IsDrawing = false;
            EndDrawingSession();
            ToolActivated?.Invoke();
        }

        private void OnCircleButtonClick(object sender, RoutedEventArgs e)
        {
            SetActiveTool(ShapeType.Circle, sender as Button);
            _viewModel.IsDrawing = true;
            StartDrawingSession(ShapeType.Circle);
            ToolActivated?.Invoke();
        }

        private void OnRotatedRectangleButtonClick(object sender, RoutedEventArgs e)
        {
            SetActiveTool(ShapeType.RotatedRectangle, sender as Button);
            _viewModel.IsDrawing = true;
            StartDrawingSession(ShapeType.RotatedRectangle);
            ToolActivated?.Invoke();
        }

        private void OnLineButtonClick(object sender, RoutedEventArgs e)
        {
            SetActiveTool(ShapeType.Line, sender as Button);
            _viewModel.IsDrawing = true;
            ToolActivated?.Invoke();
        }

        private void OnPolygonButtonClick(object sender, RoutedEventArgs e)
        {
            SetActiveTool(ShapeType.Polygon, sender as Button);
            _viewModel.IsDrawing = true;
            ToolActivated?.Invoke();
        }

        /// <summary>
        /// 矩形按钮点击
        /// </summary>
        private void OnRectangleButtonClick(object sender, RoutedEventArgs e)
        {
            SetActiveTool(ShapeType.Rectangle, sender as Button);
            _viewModel.IsDrawing = true;
            StartDrawingSession(ShapeType.Rectangle);
            ToolActivated?.Invoke();
        }

        private void SetActiveTool(ShapeType tool, Button? button)
        {
            _currentTool = tool;

            ResetAllShapeButtons(FindShapeContainer());

            if (button != null)
            {
                SetShapeButtonActive(button);
                _activeShapeButton = button;
            }

            OnPropertyChanged(nameof(CurrentShapeTool));
        }

        #endregion

        #region 形状按钮样式

        private void SetShapeButtonActive(Button button)
        {
            button.Background = new SolidColorBrush(Color.FromRgb(230, 242, 255));
            button.BorderBrush = new SolidColorBrush(Color.FromRgb(0, 120, 215));
            button.BorderThickness = new Thickness(2);

            UpdateIconColor(button, Color.FromRgb(0, 120, 215));
        }

        private void ResetShapeButtonStyle(Button? button)
        {
            if (button == null) return;
            button.Background = Brushes.Transparent;
            button.BorderBrush = new SolidColorBrush(Color.FromRgb(224, 224, 224));
            button.BorderThickness = new Thickness(1);
            UpdateIconColor(button, Color.FromRgb(85, 85, 85));
        }

        private static void UpdateIconColor(Button button, Color color)
        {
            var brush = new SolidColorBrush(color);

            if (button.Content is Shape shape)
            {
                shape.Stroke = brush;
                if (shape.Fill != Brushes.Transparent)
                    shape.Fill = brush;
            }
            else if (button.Content is Grid grid)
            {
                foreach (var child in grid.Children)
                {
                    if (child is Shape s)
                    {
                        s.Stroke = brush;
                        if (s.Fill != Brushes.Transparent)
                            s.Fill = brush;
                    }
                    else if (child is Path p)
                    {
                        p.Stroke = brush;
                        if (p.Fill != Brushes.Transparent)
                            p.Fill = brush;
                    }
                }
            }
        }

        private void ResetAllShapeButtons(Panel container)
        {
            foreach (var child in container.Children)
            {
                if (child is Button button)
                    ResetShapeButtonStyle(button);
                else if (child is Panel childPanel)
                    ResetAllShapeButtons(childPanel);
            }
        }

        private Panel? FindShapeContainer()
        {
            return ShapeButtonPanel;
        }

        #endregion

        #region 空闲模式

        public void EnterIdleMode()
        {
            SetActiveTool(ShapeType.Point, SelectButton);
            _viewModel.IsDrawing = false;
            EndDrawingSession();
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
            // 当 IsDrawing 改变时管理绘制会话
            if (e.PropertyName == nameof(RegionEditorViewModel.IsDrawing))
            {
                if (!_viewModel.IsDrawing)
                {
                    EndDrawingSession();
                }
            }
        }

        private void OnRegionChanged(object? sender, EventArgs e)
        {
            RegionDataChanged?.Invoke(this, _selectedRegion!);
        }

        #endregion

        #region 公共方法（简化版）

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
            DeselectAll();
            EndDrawingSession();
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

        #region ========== 预览绘制 → DrawingObject 交互（双向同步） ==========

        /// <summary>启动绘制会话：注册 HalconImageControl 预览回调</summary>
        public void StartDrawingSession(ShapeType shapeType)
        {
            if (_halconImageControl == null) return;

            // 形状类型必须是可交互的
            if (shapeType != ShapeType.Rectangle &&
                shapeType != ShapeType.Circle &&
                shapeType != ShapeType.RotatedRectangle)
                return;

            // 注册预览绘制完成回调
            _halconImageControl.OnPreviewDrawComplete = OnPreviewDrawFinished;

            // 启动预览模式
            _halconImageControl.BeginPreviewDraw();

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RegionEditorControl] 绘制会话启动 | ShapeType={shapeType}",
                "RegionEditorControl");
        }

        /// <summary>结束绘制会话：清除预览回调</summary>
        public void EndDrawingSession()
        {
            if (_halconImageControl == null) return;

            _halconImageControl.OnPreviewDrawComplete = null;
            _halconImageControl.EndPreviewDraw();

            VisionLogger.Instance.Log(LogLevel.Info,
                "[RegionEditorControl] 绘制会话结束",
                "RegionEditorControl");
        }

        /// <summary>预览绘制完成回调：创建区域 + DrawingObject + 注册双向同步</summary>
        private bool OnPreviewDrawFinished(double r1, double c1, double r2, double c2)
        {
            if (_halconImageControl == null) return false;

            var shapeType = _currentTool;

            // 计算形状参数
            var centerX = (c1 + c2) / 2;
            var centerY = (r1 + r2) / 2;
            var width = Math.Abs(c2 - c1);
            var height = Math.Abs(r2 - r1);

            // 创建区域数据
            var regionName = _viewModel.RequestNextRegionName();
            var region = RegionData.CreateDrawingRegion(regionName, shapeType);

            if (region.Parameters is ShapeParameters shape)
            {
                shape.CenterX = centerX;
                shape.CenterY = centerY;
                shape.Width = width;
                shape.Height = height;

                if (shapeType == ShapeType.Circle)
                {
                    shape.Radius = Math.Max(width, height) / 2;
                }

                // 应用区域类型颜色
                if (DefaultRegionType == RegionType.Mask)
                {
                    region.RegionType = RegionType.Mask;
                }
            }

            // 添加区域到集合
            _viewModel.Regions.Add(region);
            _viewModel.SelectedRegion = region;
            _viewModel.StatusMessage = $"已添加 {shapeType} 区域";

            // 为当前区域创建 DrawingObject 并注册回调
            ApplyRegionDrawingObject(region);

            // 触发变更事件
            _viewModel.FireRegionChanged(region, RegionChangeType.Added);

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[OnPreviewDrawFinished] 区域已创建 | Name={regionName} | Type={shapeType} | Center=({centerX:F1}, {centerY:F1}) | Size={width:F1}x{height:F1}",
                "RegionEditorControl");

            return true; // 已处理，HalconImageControl 不再创建默认 DrawingObject
        }

        #endregion

        #region ========== DrawingObject 渲染 ==========

        /// <summary>将所有现有区域渲染为 DrawingObject（用于初始化或重置）</summary>
        public void ApplyAllRegionsToDrawingObjects()
        {
            if (_halconImageControl == null) return;

            // 先清理所有已关联的 DrawingObject
            foreach (var regionId in _regionDrawTypes.Keys.ToList())
            {
                UnsubscribeShapeParams(regionId);
                _halconImageControl.DetachRegionDrawingObject(regionId);
            }
            _regionDrawTypes.Clear();

            // 逐个创建
            foreach (var region in _viewModel.Regions)
            {
                if (region.Parameters is ShapeParameters shape)
                {
                    ApplyRegionDrawingObject(region);
                }
            }
        }

        /// <summary>为单个区域创建 DrawingObject 并注册回调</summary>
        private void ApplyRegionDrawingObject(RegionData region)
        {
            if (_halconImageControl == null) return;
            if (region.Parameters is not ShapeParameters shape) return;

            var regionId = region.Id;

            // 创建 DrawingObject
            var drawObj = _halconImageControl.CreateRegionDrawingObject(regionId, shape);
            if (drawObj == null) return;

            // 记录形状类型
            _regionDrawTypes[regionId] = shape.ShapeType;

            // 注册 HDrawingObject 拖拽/缩放回调 → ShapeParameters 同步
            RegisterDrawObjectCallbacks(drawObj, regionId);

            // 订阅 ShapeParameters.PropertyChanged → DrawingObject 同步
            SubscribeShapeParams(regionId, shape);

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[ApplyRegionDrawingObject] DrawingObject 已创建 | RegionId={regionId:B} | Type={shape.ShapeType}",
                "RegionEditorControl");
        }

        /// <summary>移除单个区域的 DrawingObject 并取消回调订阅</summary>
        public void DetachRegionDrawingObject(RegionData region)
        {
            if (_halconImageControl == null) return;

            UnsubscribeShapeParams(region.Id);
            _regionDrawTypes.Remove(region.Id);
            StopDebounceTimer(region.Id);
            _halconImageControl.DetachRegionDrawingObject(region.Id);
        }

        #endregion

        #region ========== DrawingObject 回调注册（DrawingObject → ShapeParameters） ==========

        /// <summary>
        /// 注册 DrawingObject 回调（适配 HALCON HDrawingObjectCallback 三参数签名）
        /// </summary>
        private void RegisterDrawObjectCallbacks(HDrawingObject drawObj, Guid regionId)
        {
            var drawId = (IntPtr)drawObj.GetDrawingObjectParams("id").L; // 获取 IntPtr 句柄
            _callbackMap[drawId] = regionId;

            // OnDrag — 拖拽过程中实时同步
            drawObj.OnDrag(new HDrawingObject.HDrawingObjectCallback(OnDrawObjDrag));

            // OnResize — 缩放过程中实时同步
            drawObj.OnResize(new HDrawingObject.HDrawingObjectCallback(OnDrawObjResize));

            // OnSelect — 选中时通知 ViewModel
            drawObj.OnSelect(new HDrawingObject.HDrawingObjectCallback(OnDrawObjSelect));
        }

        /// <summary>IntPtr 句柄 → RegionId 映射字典</summary>
        private readonly Dictionary<IntPtr, Guid> _callbackMap = new();

        private void OnDrawObjDrag(IntPtr drawId, IntPtr windowHandle, string type)
        {
            if (_isSyncingToDraw) return;
            if (!_callbackMap.TryGetValue(drawId, out var regionId)) return;
            SyncDrawToShape(drawId, regionId);
        }

        private void OnDrawObjResize(IntPtr drawId, IntPtr windowHandle, string type)
        {
            if (_isSyncingToDraw) return;
            if (!_callbackMap.TryGetValue(drawId, out var regionId)) return;
            SyncDrawToShape(drawId, regionId);
        }

        private void OnDrawObjSelect(IntPtr drawId, IntPtr windowHandle, string type)
        {
            if (!_callbackMap.TryGetValue(drawId, out var regionId)) return;

            Application.Current.Dispatcher.BeginInvoke(new Action(() =>
            {
                if (_viewModel.Regions.FirstOrDefault(r => r.Id == regionId) is RegionData selectedRegion)
                {
                    _viewModel.SelectedRegion = selectedRegion;
                }
            }), DispatcherPriority.Background);
        }

        /// <summary>从 HDrawingObject 读取几何参数，同步到 ShapeParameters</summary>
        private void SyncDrawToShape(IntPtr drawId, Guid regionId)
        {
            if (_isSyncingToDraw) return;

            var region = _viewModel.Regions.FirstOrDefault(r => r.Id == regionId);
            if (region?.Parameters is not ShapeParameters shape) return;

            _isSyncingToShape = true;

            try
            {
                _halconImageControl?.ReadDrawingObjectParams(regionId, shape);
                region.MarkModified();
            }
            finally
            {
                _isSyncingToShape = false;
            }
        }

        #endregion

        #region ========== ShapeParameters PropertyChanged 订阅（ShapeParameters → DrawingObject） ==========

        private void SubscribeShapeParams(Guid regionId, ShapeParameters shape)
        {
            if (!_subscribedShapeIds.Contains(regionId))
            {
                shape.PropertyChanged += OnShapeParamChanged;
                _subscribedShapeIds.Add(regionId);
            }
        }

        private void UnsubscribeShapeParams(Guid regionId)
        {
            if (!_subscribedShapeIds.Contains(regionId)) return;

            var region = _viewModel.Regions.FirstOrDefault(r => r.Id == regionId);
            if (region?.Parameters is ShapeParameters shape)
            {
                shape.PropertyChanged -= OnShapeParamChanged;
            }
            _subscribedShapeIds.Remove(regionId);
        }

        /// <summary>ShapeParameters 属性变更 → 去抖后更新 DrawingObject</summary>
        private void OnShapeParamChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (_isSyncingToShape) return; // 来自 DrawingObject 拖拽同步，跳过反向更新
            if (sender is not ShapeParameters shape) return;

            // 查找对应区域 ID
            var region = _viewModel.Regions.FirstOrDefault(r => r.Parameters == shape);
            if (region == null) return;

            // 启动/重启去抖定时器
            StartOrResetDebounce(region.Id, shape);
        }

        /// <summary>去抖后执行 ShapeParameters → DrawingObject 更新</summary>
        private void FlushShapeSync(Guid regionId, ShapeParameters shape)
        {
            if (_isSyncingToShape) return;

            _isSyncingToDraw = true;

            try
            {
                _halconImageControl?.UpdateRegionDrawingObject(regionId, shape);
            }
            finally
            {
                _isSyncingToDraw = false;
            }
        }

        #endregion

        #region ========== 去抖定时器管理 ==========

        private void StartOrResetDebounce(Guid regionId, ShapeParameters shape)
        {
            if (_debounceTimers.TryGetValue(regionId, out var existingTimer))
            {
                existingTimer.Stop();
            }
            else
            {
                var timer = new DispatcherTimer(
                    TimeSpan.FromMilliseconds(DEBOUNCE_INTERVAL_MS),
                    DispatcherPriority.Background,
                    (s, e) => OnDebounceTick(regionId),
                    Dispatcher);
                timer.IsEnabled = false;
                _debounceTimers[regionId] = timer;
            }

            _debounceTimers[regionId].Start();
        }

        private void OnDebounceTick(Guid regionId)
        {
            if (_debounceTimers.TryGetValue(regionId, out var timer))
            {
                timer.Stop();
            }

            var region = _viewModel.Regions.FirstOrDefault(r => r.Id == regionId);
            if (region?.Parameters is ShapeParameters shape)
            {
                FlushShapeSync(regionId, shape);
            }
        }

        private void StopDebounceTimer(Guid regionId)
        {
            if (_debounceTimers.TryGetValue(regionId, out var timer))
            {
                timer.Stop();
                _debounceTimers.Remove(regionId);
            }
        }

        #endregion
    }
}
