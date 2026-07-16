using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Logic;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.ViewModels
{
    /// <summary>
    /// 区域编辑器视图模型
    /// </summary>
    public class RegionEditorViewModel : ObservableObject, IDisposable
    {

        private readonly Logic.RegionResolver _resolver;
        private readonly EditHistory _editHistory;
        private RegionData? _selectedRegion;
        private ShapeParameters? _editingShape;
        private bool _isEditing;
        private bool _isDrawing;
        private RegionSourceMode _currentMode = RegionSourceMode.Drawing;
        private ShapeType? _selectedShapeType = null;  // 默认为null（无形状选择）
        private string _statusMessage = "就绪";
        private bool _isInfoPanelVisible = true;

        // 新增：子ViewModel
        private ParameterPanelViewModel? _parameterPanel;
        private NodeSelectorViewModel? _nodeSelector;
        private DataSourceQueryService? _dataProvider;

        // WPF Dispatcher（用于延迟UI更新）
        private readonly Dispatcher _dispatcher;

        /// <summary>
        /// 区域列表
        /// </summary>
        public ObservableCollection<RegionData> Regions { get; private set; } = new();

        /// <summary>
        /// 选中的区域
        /// </summary>
        public RegionData? SelectedRegion
        {
            get => _selectedRegion;
            set
            {
                if (SetProperty(ref _selectedRegion, value))
                {
                    // 使用 Dispatcher.BeginInvoke 异步更新，避免阻塞属性设置
                    _dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateEditingShape();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
                else if (value != null)
                {
                    // 即使选中同一个区域，也强制更新参数面板
                    // 解决绘制后首次选中时，因属性相同导致面板不更新的问题
                    _dispatcher.BeginInvoke(new Action(() =>
                    {
                        UpdateEditingShape();
                    }), System.Windows.Threading.DispatcherPriority.Background);
                }
            }
        }

        /// <summary>
        /// 当前编辑的形状定义
        /// </summary>
        public ShapeParameters? EditingShape
        {
            get => _editingShape;
            private set => SetProperty(ref _editingShape, value);
        }

        /// <summary>
        /// 是否正在编辑
        /// </summary>
        public bool IsEditing
        {
            get => _isEditing;
            set => SetProperty(ref _isEditing, value);
        }

        /// <summary>
        /// 是否正在绘制
        /// </summary>
        public bool IsDrawing
        {
            get => _isDrawing;
            set
            {
                if (SetProperty(ref _isDrawing, value))
                {
                    OnPropertyChanged(nameof(IsSelectMode));
                    OnPropertyChanged(nameof(CurrentToolMode));
                }
            }
        }

        /// <summary>
        /// 是否在选择模式
        /// </summary>
        public bool IsSelectMode => !IsDrawing;

        /// <summary>
        /// 当前编辑模式（绘制/订阅）
        /// </summary>
        public RegionSourceMode CurrentMode
        {
            get => _currentMode;
            set
            {
                if (SetProperty(ref _currentMode, value))
                {
                    OnPropertyChanged(nameof(IsDrawingMode));
                    OnPropertyChanged(nameof(IsSubscribeMode));
                    OnPropertyChanged(nameof(ShowShapeTypeSelector));
                    OnPropertyChanged(nameof(CurrentToolMode));
                }
            }
        }

        /// <summary>
        /// 是否为绘制模式
        /// </summary>
        public bool IsDrawingMode
        {
            get => CurrentMode == RegionSourceMode.Drawing;
            set
            {
                if (value && CurrentMode != RegionSourceMode.Drawing)
                    CurrentMode = RegionSourceMode.Drawing;
            }
        }

        /// <summary>
        /// 当前工具模式描述（用于状态栏显示）
        /// </summary>
        public string CurrentToolMode
        {
            get
            {
                if (CurrentMode == RegionSourceMode.Subscribe)
                    return "订阅模式";
                return IsDrawing && SelectedShapeType.HasValue
                    ? $"绘制{GetShapeTypeName(SelectedShapeType.Value)}"
                    : "选择/编辑模式";
            }
        }

        /// <summary>
        /// 获取形状类型的中文名称
        /// </summary>
        private static string GetShapeTypeName(ShapeType shapeType)
        {
            return shapeType switch
            {
                ShapeType.Rectangle => "矩形",
                ShapeType.Circle => "圆形",
                ShapeType.RotatedRectangle => "旋转矩形",
                ShapeType.Line => "直线",
                ShapeType.Point => "点",
                ShapeType.Polygon => "多边形",
                _ => shapeType.ToString()
            };
        }

        /// <summary>
        /// 选中的形状类型
        /// </summary>
        public ShapeType? SelectedShapeType
        {
            get => _selectedShapeType;
            set
            {
                if (SetProperty(ref _selectedShapeType, value))
                {
                    OnPropertyChanged(nameof(CurrentToolMode));
                    OnPropertyChanged(nameof(IsAnyShapeSelected));
                    UpdateParameters();
                }
            }
        }

        /// <summary>
        /// 形状参数（用于绘制参数面板）
        /// </summary>
        /// <remarks>
        /// 计算属性：直接引用选中区域的参数，避免创建不必要的副本
        /// </remarks>
        public ShapeParameters? Parameters => SelectedRegion?.Parameters as ShapeParameters;

        /// <summary>
        /// 是否有选中区域的参数（用于控制绘制参数面板的可见性）
        /// </summary>
        public bool HasParametersVisible => Parameters != null;

        /// <summary>
        /// 是否选择了任何形状
        /// </summary>
        public bool IsAnyShapeSelected => _selectedShapeType.HasValue;

        /// <summary>
        /// 预览直线长度
        /// </summary>
        public double PreviewLength
        {
            get
            {
                if (Parameters?.Geometry is Line2d l)
                {
                    var endPt = l.GetPointAt(100);
                    var dx = endPt.X - l.Point.X;
                    var dy = endPt.Y - l.Point.Y;
                    return Math.Sqrt(dx * dx + dy * dy);
                }
                return 0;
            }
        }

        /// <summary>
        /// 是否为订阅模式
        /// </summary>
        public bool IsSubscribeMode
        {
            get => CurrentMode == RegionSourceMode.Subscribe;
            set
            {
                if (value && CurrentMode != RegionSourceMode.Subscribe)
                    CurrentMode = RegionSourceMode.Subscribe;
            }
        }

        /// <summary>
        /// 是否显示图形类型选择器（仅绘制模式显示）
        /// </summary>
        public bool ShowShapeTypeSelector => IsDrawingMode;


        /// <summary>
        /// 选中区域的定义模式
        /// </summary>
        public RegionSourceMode SelectedRegionMode => SelectedRegion?.GetMode() ?? RegionSourceMode.Drawing;

        /// <summary>
        /// 选中区域的形状类型
        /// </summary>
        public ShapeType? SelectedRegionShapeType => SelectedRegion?.GetShapeType();

        /// <summary>
        /// 是否有选中项
        /// </summary>
        public bool HasSelection => SelectedRegion != null;

        /// <summary>
        /// 选中区域的参数绑定（用于参数订阅模式）
        /// </summary>
        public Dictionary<string, ParameterSource>? ParamSettings =>
            (SelectedRegion?.Parameters as ComputedRegion)?.ParameterBindings;

        /// <summary>
        /// 是否可编辑
        /// </summary>
        public bool CanEdit => SelectedRegion != null && SelectedRegion.IsEditable;

        /// <summary>
        /// 状态消息
        /// </summary>
        public string StatusMessage
        {
            get => _statusMessage;
            set => SetProperty(ref _statusMessage, value);
        }

        /// <summary>
        /// 信息面板是否可见
        /// </summary>
        public bool IsInfoPanelVisible
        {
            get => _isInfoPanelVisible;
            set => SetProperty(ref _isInfoPanelVisible, value);
        }

        /// <summary>
        /// 编辑历史管理器
        /// </summary>
        public EditHistory EditHistory => _editHistory;

        /// <summary>
        /// 是否可撤销
        /// </summary>
        public bool CanUndo => _editHistory.CanUndo;

        /// <summary>
        /// 是否可重做
        /// </summary>
        public bool CanRedo => _editHistory.CanRedo;

        /// <summary>
        /// 可用的形状类型列表
        /// </summary>
        public IReadOnlyList<ShapeType> AvailableShapeTypes { get; } = new[]
        {
            ShapeType.Point,
            ShapeType.Line,
            ShapeType.Circle,
            ShapeType.Rectangle,
            ShapeType.RotatedRectangle,
            ShapeType.Polygon
        };

        /// <summary>
        /// 可用的区域类型列表（检测/屏蔽）
        /// </summary>
        public IReadOnlyList<RegionType> RegionTypeList { get; } = new[]
        {
            RegionType.Inspection,
            RegionType.Mask
        };

        /// <summary>
        /// 参数面板视图模型
        /// </summary>
        public ParameterPanelViewModel? ParameterPanel
        {
            get => _parameterPanel;
            private set => SetProperty(ref _parameterPanel, value);
        }

        /// <summary>
        /// 节点选择器视图模型
        /// </summary>
        public NodeSelectorViewModel? NodeSelector
        {
            get => _nodeSelector;
            private set => SetProperty(ref _nodeSelector, value);
        }

        #region 命令

        public ICommand AddRegionCommand { get; }
        public ICommand RemoveRegionCommand { get; }
        public ICommand ClearAllCommand { get; }
        public ICommand ToggleInfoPanelCommand { get; }
        public ICommand UndoCommand { get; }
        public ICommand RedoCommand { get; }

        #endregion

        /// <summary>
        /// 重写 GetLogSource，启用ObservableObject的详细性能日志
        /// </summary>
        protected override string? GetLogSource() => "RegionEditor";

        /// <summary>
        /// 区域变更事件
        /// </summary>
        public event EventHandler<RegionChangedEventArgs>? RegionChanged;

        /// <summary>
        /// 区域选择变更事件
        /// </summary>
        public event EventHandler? SelectionChanged;

        public RegionEditorViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _resolver = new Logic.RegionResolver();
            _editHistory = new EditHistory();

            AddRegionCommand = new RelayCommand<ShapeType>(AddRegion);
            RemoveRegionCommand = new RelayCommand(RemoveSelectedRegion, () => HasSelection);
            ClearAllCommand = new RelayCommand(ClearAllRegions, () => Regions.Count > 0);
            ToggleInfoPanelCommand = new RelayCommand(() => IsInfoPanelVisible = !IsInfoPanelVisible);
            UndoCommand = new RelayCommand(Undo, () => CanUndo);
            RedoCommand = new RelayCommand(Redo, () => CanRedo);

            // 订阅编辑历史变更事件
            _editHistory.HistoryChanged += OnEditHistoryChanged;
        }

        /// <summary>
        /// 初始化数据源提供者
        /// </summary>
        public void Initialize(DataSourceQueryService dataProvider)
        {
            _dataProvider = dataProvider;
            _parameterPanel = new ParameterPanelViewModel(dataProvider);
            _nodeSelector = new NodeSelectorViewModel(dataProvider);

            // 订阅事件
            _nodeSelector.SelectionConfirmed += OnNodeSelectionConfirmed;
            _parameterPanel.ParamSettingChanged += OnParamSettingChanged;
            _parameterPanel.WholeRegionChanged += OnWholeRegionChanged;

            OnPropertyChanged(nameof(ParameterPanel));
            OnPropertyChanged(nameof(NodeSelector));
        }

        /// <summary>
        /// 注入外部区域集合，与参数模型共享同一个 ObservableCollection 实例
        /// </summary>
        /// <remarks>
        /// 设计原则：单一数据源（Single Source of Truth）
        /// ViewModel 的 Regions 和参数模型的 Regions 指向同一个集合对象，
        /// 无需双向同步机制，从根本上消除数据不一致问题。
        /// </remarks>
        public void SetRegionsSource(ObservableCollection<RegionData> externalRegions)
        {
            if (externalRegions == null)
                throw new ArgumentNullException(nameof(externalRegions));

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[SetRegionsSource] 替换区域集合 | 外部集合数量={externalRegions.Count} | 引用相同={Regions == externalRegions}",
                "RegionEditor");

            Regions = externalRegions;
            OnPropertyChanged(nameof(Regions));

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[SetRegionsSource] 区域集合已替换 | 区域数: {Regions.Count}",
                "RegionEditor");
        }

        /// <summary>
        /// 节点选择确认事件处理
        /// </summary>
        private void OnNodeSelectionConfirmed(object? sender, NodeOutputInfo? selectedNode)
        {
            if (selectedNode != null && _parameterPanel != null)
            {
                _parameterPanel.ApplyBindingSelection(selectedNode);
                StatusMessage = $"已绑定: {selectedNode.DisplayPath}";
            }
        }

        /// <summary>
        /// 参数绑定变更事件处理
        /// </summary>
        private void OnParamSettingChanged(object? sender, ParamSettingChangedEventArgs e)
        {
            if (SelectedRegion?.Parameters is ComputedRegion computed)
            {
                if (e.NewSource != null)
                {
                    computed.SetParameterBinding(e.ParameterName, e.NewSource);
                }
                else
                {
                    computed.RemoveParameterBinding(e.ParameterName);
                }

                SelectedRegion.MarkModified();
                RegionChanged?.Invoke(this, new RegionChangedEventArgs(SelectedRegion, RegionChangeType.Modified));
            }
        }

        /// <summary>
        /// 整区域选择变更事件处理（写 FixedRegion）
        /// </summary>
        private void OnWholeRegionChanged(object? sender, WholeRegionSelectedEventArgs e)
        {
            if (SelectedRegion == null) return;

            if (e.Source is NodeOutputSource nos)
            {
                SelectedRegion.Parameters = new FixedRegion(nos.NodeId, nos.OutputName, nos.Index);
                SelectedRegion.MarkModified();
                RegionChanged?.Invoke(this, new RegionChangedEventArgs(SelectedRegion, RegionChangeType.Modified));
                StatusMessage = $"已设置订阅源: {nos.OutputName}";
                return;
            }

            // 解绑整区域：由固定区域回退为默认几何区域
            if (SelectedRegion.Parameters is FixedRegion)
            {
                SelectedRegion.Parameters = new ComputedRegion
                {
                    TargetShapeType = SelectedShapeType ?? ShapeType.Rectangle
                };
                SelectedRegion.MarkModified();
                RegionChanged?.Invoke(this, new RegionChangedEventArgs(SelectedRegion, RegionChangeType.Modified));
            }
        }

        /// <summary>
        /// 添加区域
        /// </summary>
        public void AddRegion(ShapeType shapeType)
        {
            var region = RegionData.CreateDrawingRegion(GetNextRegionName(), shapeType);

            // 设置默认参数（由 CreateDrawingRegion 的 CreateDefaultGeometry 负责）

            // 记录编辑历史
            _editHistory.ExecuteAction(new CreateRegionAction(region, this));

            Regions.Add(region);
            SelectedRegion = region;
            StatusMessage = $"已添加 {shapeType} 区域";

            RegionChanged?.Invoke(this, new RegionChangedEventArgs(region, RegionChangeType.Added));
        }


        /// <summary>
        /// 获取下一个区域名称，优先填充索引空洞
        /// </summary>
        /// <remarks>
        /// 从1开始查找第一个未被使用的索引，优先填充被删除区域留下的空洞。
        /// 例如：已有 区域_1、区域_3，则返回 区域_2
        /// </remarks>
        private string GetNextRegionName()
        {
            var usedIndices = new HashSet<int>();
            foreach (var region in Regions)
            {
                if (region.Name != null && region.Name.StartsWith("区域_"))
                {
                    var indexPart = region.Name.Substring(3);
                    if (int.TryParse(indexPart, out var index) && index > 0)
                    {
                        usedIndices.Add(index);
                    }
                }
            }

            var nextIndex = 1;
            while (usedIndices.Contains(nextIndex))
            {
                nextIndex++;
            }
            return $"区域_{nextIndex}";
        }

        /// <summary>
        /// 移除选中区域
        /// </summary>
        public void RemoveSelectedRegion()
        {
            if (SelectedRegion == null) return;

            var region = SelectedRegion;

            // ★ 诊断日志：删除操作开始
            var beforeRegionNames = string.Join(", ", Regions.Select(r => $"{r.Name}(Id={r.Id:B})"));
            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[RemoveSelectedRegion] 删除开始 | 目标={region.Name}(Id={region.Id:B}) | Regions.Count={Regions.Count} | Regions=[{beforeRegionNames}] | SelectedRegion={SelectedRegion.Name}",
                "RegionEditorViewModel");

            // 记录编辑历史
            _editHistory.ExecuteAction(new DeleteRegionAction(region, this));

            Regions.Remove(region);
            SelectedRegion = Regions.FirstOrDefault();
            StatusMessage = $"已移除区域 {region.Name}";

            // ★ 诊断日志：删除操作完成
            var afterRegionNames = string.Join(", ", Regions.Select(r => $"{r.Name}(Id={r.Id:B})"));
            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[RemoveSelectedRegion] 删除完成 | Regions.Count={Regions.Count} | Regions=[{afterRegionNames}] | SelectedRegion={SelectedRegion?.Name ?? "null"}",
                "RegionEditorViewModel");

            RegionChanged?.Invoke(this, new RegionChangedEventArgs(region, RegionChangeType.Removed));
        }

        /// <summary>
        /// 清除所有区域
        /// </summary>
        public void ClearAllRegions()
        {
            if (Regions.Count == 0) return;

            var count = Regions.Count;
            var deletedRegions = Regions.ToList();

            // 记录编辑历史
            _editHistory.ExecuteAction(new ClearAllAction(deletedRegions, this));

            Regions.Clear();
            SelectedRegion = null;
            StatusMessage = $"已清除 {count} 个区域";

            RegionChanged?.Invoke(this, new RegionChangedEventArgs(null, RegionChangeType.Cleared));
        }

        /// <summary>
        /// 撤销操作
        /// </summary>
        public void Undo()
        {
            if (_editHistory.CanUndo)
            {
                _editHistory.Undo();
                StatusMessage = "已撤销";
            }
        }

        /// <summary>
        /// 重做操作
        /// </summary>
        public void Redo()
        {
            if (_editHistory.CanRedo)
            {
                _editHistory.Redo();
                StatusMessage = "已重做";
            }
        }

        /// <summary>
        /// 编辑历史变更处理
        /// </summary>
        private void OnEditHistoryChanged(object? sender, EventArgs e)
        {
            OnPropertyChanged(nameof(CanUndo));
            OnPropertyChanged(nameof(CanRedo));
            CommandManager.InvalidateRequerySuggested();
        }

        /// <summary>
        /// 内部添加区域（用于撤销/重做）
        /// </summary>
        internal void AddRegionInternal(RegionData region)
        {
            Regions.Add(region);
            RegionChanged?.Invoke(this, new RegionChangedEventArgs(region, RegionChangeType.Added));
        }

        /// <summary>
        /// 内部移除区域（用于撤销/重做）
        /// </summary>
        internal void RemoveRegionInternal(RegionData region)
        {
            Regions.Remove(region);
            if (SelectedRegion == region)
                SelectedRegion = null;
            RegionChanged?.Invoke(this, new RegionChangedEventArgs(region, RegionChangeType.Removed));
        }

        /// <summary>
        /// 内部设置区域列表（用于撤销/重做）
        /// </summary>
        internal void SetRegionsInternal(IEnumerable<RegionData> regions)
        {
            Regions.Clear();
            foreach (var region in regions)
            {
                Regions.Add(region);
            }
            SelectedRegion = null;
            RegionChanged?.Invoke(this, new RegionChangedEventArgs(null, RegionChangeType.Cleared));
        }

        /// <summary>
        /// 更新形状参数
        /// </summary>
        /// <remarks>
        /// 已废弃：Parameters 改为计算属性后，不再需要手动更新
        /// 保留此方法仅为兼容性，可以移除
        /// </remarks>
        private void UpdateParameters()
        {
            // Parameters 现在是计算属性，直接引用 SelectedRegion.Parameters
            // 不需要手动创建和同步对象
            OnPropertyChanged(nameof(Parameters));
        }

        /// <summary>
        /// 根据形状类型创建参数对象
        /// </summary>
        /// <remarks>
        /// 已废弃：不再需要此方法，直接修改 SelectedRegion.Parameters 即可
        /// 保留此方法仅为兼容性，可以移除
        /// </remarks>
        private ShapeParameters CreateParametersByType(ShapeType shapeType, double centerX, double centerY, double width, double height, double angle)
        {
            var parameters = new ShapeParameters();
            parameters.Geometry = shapeType switch
            {
                ShapeType.Circle => new Circle2d(centerX, centerY, Math.Max(width, height) / 2),
                ShapeType.Line => Line2d.FromTwoPoints(
                    new System.Windows.Point(centerX - width / 2, centerY),
                    new System.Windows.Point(centerX + width / 2, centerY)),
                ShapeType.RotatedRectangle => new RotatedRect2d(centerX, centerY,
                    width > 0 ? width : 100, height > 0 ? height : 100, angle),
                _ => new RotatedRect2d(centerX, centerY,
                    width > 0 ? width : 100, height > 0 ? height : 100, 0),
            };
            return parameters;
        }

        /// <summary>
        /// 公共方法：强制更新形状参数
        /// </summary>
        public void CallUpdateParameters() => UpdateParameters();

        /// <summary>
        /// 公共方法：强制触发 SelectedShapeType 的 PropertyChanged 事件
        /// </summary>
        public void NotifySelectedShapeTypeChanged() => OnPropertyChanged(nameof(SelectedShapeType));

        /// <summary>
        /// 公共方法：强制触发 IsSubscribeMode 的 PropertyChanged 事件
        /// </summary>
        public void NotifyIsSubscribeModeChanged() => OnPropertyChanged(nameof(IsSubscribeMode));

        /// <summary>
        /// 更新编辑形状
        /// </summary>
        private void UpdateEditingShape()
        {
            if (SelectedRegion?.Parameters is ShapeParameters shapeDef)
            {
                EditingShape = shapeDef;
                SelectedShapeType = shapeDef.ShapeType;
            }
            else
            {
                EditingShape = null;
                SelectedShapeType = null;
            }

            // 更新参数面板（LoadFromRegionDefinition 覆盖 Fixed/Computed/Shape 三种）
            if (_parameterPanel != null && SelectedRegion?.Parameters != null)
            {
                _parameterPanel.LoadFromRegionDefinition(SelectedRegion.Parameters);
            }

            // 通知计算属性变更
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(SelectedRegionMode));
            OnPropertyChanged(nameof(SelectedRegionShapeType));
            OnPropertyChanged(nameof(IsSubscribeMode));
            OnPropertyChanged(nameof(ParamSettings));
            OnPropertyChanged(nameof(HasParametersVisible));
            OnPropertyChanged(nameof(Parameters));
            OnPropertyChanged(nameof(PreviewLength));
        }

        /// <summary>
        /// 开始绘制
        /// </summary>
        public void StartDrawing(ShapeType shapeType)
        {
            IsDrawing = true;
            SelectedShapeType = shapeType;
            StatusMessage = $"正在绘制 {shapeType}";
        }

        /// <summary>
        /// 结束绘制
        /// </summary>
        public void EndDrawing()
        {
            IsDrawing = false;
            StatusMessage = "绘制完成";
        }

        /// <summary>
        /// 更新形状参数
        /// </summary>
        public void UpdateShapeParameter(string parameterName, object value)
        {
            if (EditingShape == null) return;

            var prop = typeof(ShapeParameters).GetProperty(parameterName);
            if (prop != null && prop.CanWrite)
            {
                prop.SetValue(EditingShape, Convert.ChangeType(value, prop.PropertyType));
                SelectedRegion?.MarkModified();
                RegionChanged?.Invoke(this, new RegionChangedEventArgs(SelectedRegion, RegionChangeType.Modified));
            }
        }

        /// <summary>
        /// 设置区域订阅源
        /// </summary>
        public void SetRegionSubscription(string nodeId, string outputName, int? index = null)
        {
            if (SelectedRegion == null) return;

            SelectedRegion.Parameters = new FixedRegion(nodeId, outputName, index);
            SelectedRegion.MarkModified();

            RegionChanged?.Invoke(this, new RegionChangedEventArgs(SelectedRegion, RegionChangeType.Modified));
            StatusMessage = $"已设置订阅源: {outputName}";
        }

        /// <summary>
        /// 设置参数绑定
        /// </summary>
        public void SetParamSetting(string parameterName, ParameterSource source)
        {
            if (SelectedRegion?.Parameters is ComputedRegion computedDef)
            {
                computedDef.SetParameterBinding(parameterName, source);
                SelectedRegion.MarkModified();

                RegionChanged?.Invoke(this, new RegionChangedEventArgs(SelectedRegion, RegionChangeType.Modified));
                StatusMessage = $"已设置参数绑定: {parameterName}";
            }
        }

        /// <summary>
        /// 解析选中区域
        /// </summary>
        public Logic.ResolvedRegion? ResolveSelectedRegion()
        {
            return _resolver.Resolve(SelectedRegion);
        }

        /// <summary>
        /// 解析所有区域
        /// </summary>
        public List<Logic.ResolvedRegion> ResolveAllRegions()
        {
            return _resolver.ResolveAll(Regions);
        }

        public void Dispose()
        {
            // 清理资源
            _editHistory.HistoryChanged -= OnEditHistoryChanged;

            if (_parameterPanel != null)
            {
                _parameterPanel.ParamSettingChanged -= OnParamSettingChanged;
                _parameterPanel.WholeRegionChanged -= OnWholeRegionChanged;
            }

            if (_nodeSelector != null)
            {
                _nodeSelector.SelectionConfirmed -= OnNodeSelectionConfirmed;
                _nodeSelector.Dispose();
            }
        }
    }

    /// <summary>
    /// 区域变更事件参数
    /// </summary>
    public class RegionChangedEventArgs : EventArgs
    {
        public RegionData? Region { get; }
        public RegionChangeType ChangeType { get; }

        public RegionChangedEventArgs(RegionData? region, RegionChangeType changeType)
        {
            Region = region;
            ChangeType = changeType;
        }
    }

    /// <summary>
    /// 区域变更类型
    /// </summary>
    public enum RegionChangeType
    {
        Added,
        Removed,
        Modified,
        Cleared
    }

    /// <summary>
    /// 泛型命令实现
    /// </summary>
    public class RelayCommand<T> : ICommand
    {
        private readonly Action<T?> _execute;
        private readonly Func<bool>? _canExecute;

        public RelayCommand(Action<T?> execute, Func<bool>? canExecute = null)
        {
            _execute = execute ?? throw new ArgumentNullException(nameof(execute));
            _canExecute = canExecute;
        }

        public event EventHandler? CanExecuteChanged
        {
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter)
        {
            if (parameter == null)
                _execute(default);
            else if (parameter is T t)
                _execute(t);
            else
                _execute((T)Convert.ChangeType(parameter, typeof(T)));
        }
    }

    /// <summary>
    /// 非泛型命令实现
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
            add => CommandManager.RequerySuggested += value;
            remove => CommandManager.RequerySuggested -= value;
        }

        public bool CanExecute(object? parameter) => _canExecute?.Invoke() ?? true;

        public void Execute(object? parameter) => _execute();

        public void RaiseCanExecuteChanged()
        {
            CommandManager.InvalidateRequerySuggested();
        }
    }
}
