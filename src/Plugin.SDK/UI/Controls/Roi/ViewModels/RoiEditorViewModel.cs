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
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Logic;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.ViewModels
{
    /// <summary>
    /// 区域编辑器视图模型
    /// </summary>
    public class RoiEditorViewModel : ObservableObject, IDisposable
    {

        private readonly EditHistory _editHistory;
        private Roi? _selectedRoi;
        private RoiShape? _editingShape;
        private bool _isEditing;
        private bool _isDrawing;
        private RoiSourceMode _currentMode = RoiSourceMode.Drawing;
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
        /// 几何输入列表（元素为 <see cref="Roi"/>；注意：非像素区域概念）
        /// </summary>
        public ObservableCollection<Roi> Rois { get; private set; } = new();

        /// <summary>
        /// 选中的几何输入
        /// </summary>
        public Roi? SelectedRoi
        {
            get => _selectedRoi;
            set
            {
                if (SetProperty(ref _selectedRoi, value))
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
        public RoiShape? EditingShape
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
        /// <remarks>
        /// 本 setter 只切换 UI 模式，**不做数据副作用**（不改任何 Roi 绑定）。
        /// 「切到绘制 = 解除订阅回退本地」只在用户显式点「绘制」radio 时（IsDrawingMode setter）执行，
        /// 避免打开窗口/恢复模式等非用户路径误把订阅绑定清掉（区分「持久化丢失」与「切换清空」）。
        /// 模式切换本身虽不清数据，但订阅面板的 WholeRegionSetting/Parameters 是独立缓存，
        /// 可能因"SelectedRoi 未变"而残留旧回显（切绘制清空绑定后再切回订阅 → 看有绑、实际没绑），
        /// 故这里做**UI 回显同步**（把面板重新对准 SelectedRoi 的真实绑定状态），不改任何模型数据。
        /// </remarks>
        public RoiSourceMode CurrentMode
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
                    OnPropertyChanged(nameof(HasDrawingParametersVisible));
                    RefreshParameterPanelForSelectedRoi();
                }
            }
        }

        /// <summary>
        /// 把订阅参数面板回显同步到当前选中 Roi 的真实状态（模式切换后 UI 缓存可能滞后于模型）。
        /// SelectedRoi 可为 null：LoadFromRoi(null) 会清空面板缓存（整对象/几何参数回显），
        /// 确保"切到订阅但无选中项/绑定已被清空"时 UI 不残留旧绑定。
        /// </summary>
        private void RefreshParameterPanelForSelectedRoi()
        {
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RefreshPanel] CurrentMode={_currentMode} | SelectedRoi={SelectedRoi?.Name ?? "null"}",
                "RoiSubscribePanel");
            if (_parameterPanel == null) return;
            // 主模式切换：按 SelectedRoi.Source 同步 radio + 回灌视图缓存
            _parameterPanel.LoadFromRoi(SelectedRoi, syncSubMode: true);
        }

        /// <summary>
        /// 切回绘制模式：把集合中所有订阅/拼装 Roi 解除订阅，回退到本地本体几何。
        /// 本地几何优先复用已保留的绘制几何；若原本无本地几何（如由订阅直接建立的 Roi），
        /// 回退到当前形状的默认几何，保证切回绘制后有可见可编辑的本体。
        /// 仅由用户显式切换 radio 调用（见 <see cref="IsDrawingMode"/>）。
        /// </summary>
        private void RevertSubscribedRoisToLocal()
        {
            // ★ 判定依据 = 【是否存在订阅配置】（Binding/ParameterBindings 任一非空即视为需清空），
            // 不依赖激活 Source：订阅配置与激活来源可能错位（并存/历史数据），若按 Is* 过滤只清激活那侧，
            // 会造成"切绘制只清了一个子模式、另一个子模式订阅残留"。主模式"订阅→绘制"的语义是
            // 【清空该几何输入上的全部订阅配置】，故只要任一侧有配置都进入清理。
            var subscribed = Rois.Where(r => r.Binding != null || r.ParameterBindings is { Count: > 0 }).ToList();
            VisionLogger.Instance.Log(LogLevel.Info,
                $"[RevertSubscribed] 待清订阅/拼装 roi 数={subscribed.Count} | 全部={string.Join(",", subscribed.Select(x => x.Name))}",
                "RoiSubscribePanel");
            foreach (var roi in subscribed)
            {
                bool wasComposed = roi.IsComposed;
                // ★ 主模式"订阅→绘制"= 用户确认的"清空全部订阅配置"语义：清区域订阅与参数订阅配置，
                //   并回到本地绘制激活（Source=Drawing）。本地绘制几何保留（绘制底稿不丢）。
                roi.Binding = null;
                roi.ParameterBindings = null;
                roi.ComposeShapeType = ShapeType.None;
                roi.Source = RoiGeometrySource.Drawing;

                if (roi.LocalGeometry is not { Geometry: not null })
                {
                    roi.LocalGeometry = new RoiShape
                    {
                        Geometry = RoiShape.CreateDefaultGeometry(SelectedShapeType ?? ShapeType.Rectangle)
                    };
                }

                RoiChanged?.Invoke(this, new RoiChangedEventArgs(roi, RoiChangeType.Modified));
                StatusMessage = wasComposed
                    ? $"已切回绘制模式：清除参数拼装，回退本地几何 ({roi.Name})"
                    : $"已切回绘制模式：解除订阅，回退本地几何 ({roi.Name})";
            }
        }

        /// <summary>
        /// 是否为绘制模式。
        /// setter 仅由用户显式点击「绘制」radio 触发（RadioButton TwoWay 绑定）。
        /// 切到绘制 = 数据来源改为本地本体几何：解除集合内订阅/拼装 Roi 的订阅，
        /// 回退到保留的本地绘制几何（订阅配置随解除丢失，再切订阅需重新绑定——符合用户语义）。
        /// ★ 不在 CurrentMode setter 里做此副作用，防止打开窗口/恢复模式等非用户路径误清订阅。
        /// </summary>
        public bool IsDrawingMode
        {
            get => CurrentMode == RoiSourceMode.Drawing;
            set
            {
                if (value && CurrentMode != RoiSourceMode.Drawing)
                {
                    VisionLogger.Instance.Log(LogLevel.Info,
                        $"[ModeSwitch] 用户切到绘制 | Rois.Count={Rois.Count} | SelectedRoi={SelectedRoi?.Name ?? "null"}",
                        "RoiSubscribePanel");
                    RevertSubscribedRoisToLocal();
                    CurrentMode = RoiSourceMode.Drawing;
                }
            }
        }

        /// <summary>
        /// 当前工具模式描述（用于状态栏显示）
        /// </summary>
        public string CurrentToolMode
        {
            get
            {
                if (CurrentMode == RoiSourceMode.Subscribe)
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
                ShapeType.Annulus => "圆环",
                ShapeType.Arc => "弧形",
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
        /// 计算属性：直接引用选中区域的本地几何（RoiShape），避免创建不必要的副本
        /// </remarks>
        public RoiShape? Parameters => SelectedRoi?.LocalGeometry;

        /// <summary>
        /// 是否有选中区域的参数（用于控制绘制参数面板的可见性）
        /// </summary>
        public bool HasParametersVisible => Parameters != null;

        /// <summary>
        /// 绘制参数面板可见性：仅绘制模式且选中区域带本地几何时显示。
        /// 订阅模式下即使选中带本地几何的区域也不显示绘制参数（交互层语义归订阅面板）。
        /// </summary>
        public bool HasDrawingParametersVisible => IsDrawingMode && HasParametersVisible;

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
            get => CurrentMode == RoiSourceMode.Subscribe;
            set
            {
                if (value && CurrentMode != RoiSourceMode.Subscribe)
                    CurrentMode = RoiSourceMode.Subscribe;
            }
        }

        /// <summary>
        /// 是否显示图形类型选择器（仅绘制模式显示）
        /// </summary>
        public bool ShowShapeTypeSelector => IsDrawingMode;


        /// <summary>
        /// 选中区域的定义模式（整对象订阅或参数拼装均视为订阅模式）
        /// </summary>
        public RoiSourceMode SelectedRoiMode =>
            SelectedRoi is { } roi && (roi.IsSubscribed || roi.IsComposed)
                ? RoiSourceMode.Subscribe
                : RoiSourceMode.Drawing;

        /// <summary>
        /// 选中区域的形状类型
        /// </summary>
        public ShapeType? SelectedRoiShapeType =>
            SelectedRoi?.LocalGeometry?.ShapeType;

        /// <summary>
        /// 是否有选中项
        /// </summary>
        public bool HasSelection => SelectedRoi != null;

        /// <summary>
        /// 选中区域的订阅绑定路径（用于参数订阅模式展示；null=本地绘制）
        /// </summary>
        public string? SubscriptionBindingPath => SelectedRoi?.Binding?.ToString();

        /// <summary>
        /// 是否可编辑
        /// </summary>
        public bool CanEdit => SelectedRoi != null && SelectedRoi.IsEditable;

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

        public ICommand AddRoiCommand { get; }
        public ICommand RemoveRoiCommand { get; }
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
        /// 几何输入变更事件
        /// </summary>
        public event EventHandler<RoiChangedEventArgs>? RoiChanged;

        /// <summary>
        /// 几何输入选择变更事件
        /// </summary>
        public event EventHandler? SelectionChanged;

        public RoiEditorViewModel()
        {
            _dispatcher = Dispatcher.CurrentDispatcher;
            _editHistory = new EditHistory();

            AddRoiCommand = new RelayCommand<ShapeType>(AddRoi);
            RemoveRoiCommand = new RelayCommand(RemoveSelectedRoi, () => HasSelection);
            ClearAllCommand = new RelayCommand(ClearAllRois, () => Rois.Count > 0);
            ToggleInfoPanelCommand = new RelayCommand(() => IsInfoPanelVisible = !IsInfoPanelVisible);
            UndoCommand = new RelayCommand(Undo, () => CanUndo);
            RedoCommand = new RelayCommand(Redo, () => CanRedo);

            // 订阅编辑历史变更事件
            _editHistory.HistoryChanged += OnEditHistoryChanged;

            // ★ 立即创建订阅子模型并接线（此前依赖外部 Initialize→RoiEditorIntegration，实为死代码，
            //   导致订阅面板 DataContext=null、绑定变更不写回 Roi，UI 空/无法持久化）。
            EnsureSubscriptionInitialized();
        }

        /// <summary>
        /// 初始化数据源提供者（幂等；子模型创建与事件接线已提前到构造函数）。
        /// </summary>
        public void Initialize(DataSourceQueryService dataProvider)
        {
            _dataProvider = dataProvider;
            EnsureSubscriptionInitialized();
        }

        /// <summary>
        /// 幂等创建订阅面板/节点选择器子模型并接线事件。
        /// </summary>
        private void EnsureSubscriptionInitialized()
        {
            if (_parameterPanel != null) return;

            _parameterPanel = new ParameterPanelViewModel(_dataProvider);
            _nodeSelector = new NodeSelectorViewModel(_dataProvider);

            // 订阅事件（双子：整对象订阅 + 几何参数拼装 + 拼装形状切换）
            _nodeSelector.SelectionConfirmed += OnNodeSelectionConfirmed;
            _parameterPanel.BindingChanged += OnBindingChanged;
            _parameterPanel.ParameterSettingChanged += OnParameterSettingChanged;
            _parameterPanel.ShapeTypeChanged += OnShapeTypeChanged;

            OnPropertyChanged(nameof(ParameterPanel));
            OnPropertyChanged(nameof(NodeSelector));
        }

        /// <summary>
        /// 注入外部区域集合，与参数模型共享同一个 ObservableCollection 实例
        /// </summary>
        /// <remarks>
        /// 设计原则：单一数据源（Single Source of Truth）
        /// ViewModel 的 Rois 和参数模型的 Rois 指向同一个集合对象，
        /// 无需双向同步机制，从根本上消除数据不一致问题。
        /// </remarks>
        public void SetRoisSource(ObservableCollection<Roi> externalRois)
        {
            if (externalRois == null)
                throw new ArgumentNullException(nameof(externalRois));

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[SetRoisSource] 替换区域集合 | 外部集合数量={externalRois.Count} | 引用相同={Rois == externalRois}",
                "RegionEditor");

            Rois = externalRois;
            OnPropertyChanged(nameof(Rois));

            // 打开调试窗（SetRoisSource 注入集合）后恢复编辑模式：
            // 检测/屏蔽区各自独立——集合内存在订阅/拼装 ROI → 进订阅模式并选中首个订阅 ROI 回显绑定；
            // 全为本地绘制 → 保持绘制模式（无订阅可回显）。订阅子模式由 LoadFromRoi 按 Roi 三态推导。
            RestoreEditorModeFromRois();

            VisionLogger.Instance.Log(LogLevel.Success,
                $"[SetRoisSource] 区域集合已替换 | 区域数: {Rois.Count} | CurrentMode={_currentMode}",
                "RegionEditor");
        }

        /// <summary>
        /// 依据集合内订阅/拼装 ROI 恢复编辑器模式（打开调试窗时调用一次）。
        /// 存在启用订阅（整对象/拼装）ROI → 订阅模式并选中首个，触发订阅面板回显其绑定；
        /// 否则回落绘制模式。不改变手动切换语义（用户可随后自行切换 radio）。
        /// </summary>
        private void RestoreEditorModeFromRois()
        {
            if (Rois.Count == 0)
            {
                if (CurrentMode != RoiSourceMode.Drawing)
                    CurrentMode = RoiSourceMode.Drawing;
                return;
            }

            var subscribed = Rois.FirstOrDefault(r => r.IsEnabled && (r.IsSubscribed || r.IsComposed));
            if (subscribed != null)
            {
                if (CurrentMode != RoiSourceMode.Subscribe)
                    CurrentMode = RoiSourceMode.Subscribe;
                SelectedRoi = subscribed;
            }
            else if (CurrentMode != RoiSourceMode.Drawing)
            {
                CurrentMode = RoiSourceMode.Drawing;
            }
        }

        /// <summary>
        /// 节点选择确认事件处理（转给订阅面板产出 ParamSetting）。
        /// 双子路由：整对象模式 → ApplyWholeRegionSelection；几何参数模式 → ApplyBindingSelection（作用于选中参数行）。
        /// </summary>
        private void OnNodeSelectionConfirmed(object? sender, NodeOutputInfo? selectedNode)
        {
            if (selectedNode == null || _parameterPanel == null) return;

            if (_parameterPanel.SubscribeSubMode == RoiSubscribeSubMode.WholeRegion)
            {
                _parameterPanel.ApplyWholeRegionSelection(selectedNode);
            }
            else
            {
                _parameterPanel.ApplyBindingSelection(selectedNode);
            }
            StatusMessage = $"已绑定: {selectedNode.DisplayPath}";
        }

        /// <summary>
        /// 确保存在可写的数据源目标 Roi（统一"区域槽"兜底，A 方案核心）。
        /// 绘制/订阅/拼装/形状切换等所有"往当前区域写入数据源"的入口共用本方法：
        /// 优先 <see cref="ParameterPanelViewModel.TargetRoi"/>（订阅面板编辑目标），其次 SelectedRoi，
        /// 再其次集合内第一个启用 Roi；三者皆无（纯订阅、从未绘制）→ 自动创建空槽 Roi 作为载体加入集合并选中。
        /// </summary>
        /// <returns>可写目标 Roi；仍为 null 仅发生在空集合且无有效写入内容时。</returns>
        private Roi? EnsureTargetRoiSlot()
        {
            var target = _parameterPanel?.TargetRoi ?? SelectedRoi;
            if (target == null)
            {
                target = Rois.FirstOrDefault(r => r.IsEnabled);
                if (target != null) SelectedRoi = target;
            }

            if (target == null)
            {
                // ★ 纯订阅支持：集合为空（未先绘制本地区域）时自动创建空槽 Roi 作为载体，
                //   使"只订阅上游、不画区域"也能生效（检测区/屏蔽区一致）。槽由调用方随即填充 Binding/拼装集。
                target = new Roi
                {
                    Name = GetNextRoiName(),
                };
                Rois.Add(target);
                SelectedRoi = target;
                RoiChanged?.Invoke(this, new RoiChangedEventArgs(target, RoiChangeType.Added));
            }

            return target;
        }

        /// <summary>
        /// 区域订阅（整对象）变更事件处理（写入 Roi.Binding 契约路径；null=解除区域订阅）。
        /// 配置并存架构：写入 Binding 只更新【区域订阅配置】并把 <see cref="Roi.Source"/> 激活为
        /// <see cref="RoiGeometrySource.WholeRegionSub"/>，【不清空参数订阅配置】（ParameterBindings 保留，
        /// 切回参数订阅可恢复激活）。解除时若仍保留参数订阅配置则激活之，否则回退本地绘制。
        /// </summary>
        private void OnBindingChanged(object? sender, ParamSetting? binding)
        {
            // 解除订阅（binding==null）：只在有目标时回退本地，空集合不做任何事（不创建空槽）。
            var target = binding != null ? EnsureTargetRoiSlot() : (_parameterPanel?.TargetRoi ?? SelectedRoi);
            if (target == null) return;

            if (binding != null)
            {
                // 区域订阅 → 写 Binding 并激活 WholeRegionSub；
                // ★ 不清空 LocalGeometry：保留原绘制几何作为"绘制模式可还原的底稿"（切回绘制可见可编辑）；
                // ★ 不清空 ParameterBindings：参数订阅配置并存保留，切到参数订阅子模式时可恢复激活。
                target.Binding = binding;
                target.Source = RoiGeometrySource.WholeRegionSub;
                RoiChanged?.Invoke(this, new RoiChangedEventArgs(target, RoiChangeType.Modified));
                StatusMessage = $"已设置区域订阅源: {binding.SourceNodeId}.{binding.SourceProperty}";
                return;
            }

            // 解除区域订阅：若仍保留参数订阅配置则激活 GeometricSub，否则回退本地绘制
            target.Binding = null;
            if (target.ParameterBindings is { Count: > 0 })
            {
                target.Source = RoiGeometrySource.GeometricSub;
                RoiChanged?.Invoke(this, new RoiChangedEventArgs(target, RoiChangeType.Modified));
                StatusMessage = $"已解除区域订阅，切换到参数订阅 ({(target.ParameterBindings?.Count ?? 0)} 个字段保留)";
                return;
            }
            target.Source = RoiGeometrySource.Drawing;
            target.LocalGeometry ??= new RoiShape
            {
                Geometry = RoiShape.CreateDefaultGeometry(SelectedShapeType ?? ShapeType.Rectangle)
            };
            RoiChanged?.Invoke(this, new RoiChangedEventArgs(target, RoiChangeType.Modified));
        }

        /// <summary>
        /// 参数订阅（几何参数拼装）变更事件处理（写入 Roi.ParameterBindings[扁平参数名]；setting=null=移除该键）。
        /// 配置并存架构：写入某个字段只更新【参数订阅配置】并把 <see cref="Roi.Source"/> 激活为
        /// <see cref="RoiGeometrySource.GeometricSub"/>，【不清空区域订阅配置】（Binding 保留）。
        /// 移除键到空集时：若仍保留区域订阅配置则激活之，否则回退本地绘制。
        /// </summary>
        private void OnParameterSettingChanged(object? sender, ParameterSettingChangedEventArgs e)
        {
            if (e == null || string.IsNullOrEmpty(e.ParameterName)) return;

            // 移除参数键（setting=null）只在有目标时执行；空集合不创建空槽。
            var target = e.Setting != null ? EnsureTargetRoiSlot() : (_parameterPanel?.TargetRoi ?? SelectedRoi);
            if (target == null) return;

            if (e.Setting != null)
            {
                // 进入参数订阅；拼装形状 = 订阅面板当前形状（ComposeShapeType 持久化）
                // ★ 不清空 LocalGeometry / Binding：与区域订阅/绘制配置并存保留（按 Source 激活，切回可恢复）。
                var composeShape = _parameterPanel?.CurrentShapeType ?? ShapeType.Rectangle;
                target.ComposeShapeType = composeShape;
                target.ParameterBindings ??= new Dictionary<string, ParamSetting>();
                target.ParameterBindings[e.ParameterName] = e.Setting;
                target.Source = RoiGeometrySource.GeometricSub;
                RoiChanged?.Invoke(this, new RoiChangedEventArgs(target, RoiChangeType.Modified));
                StatusMessage = $"已设置参数 {e.ParameterName} 订阅源: {e.Setting.SourceNodeId}.{e.Setting.SourceProperty}";
                return;
            }

            // 移除该键
            target.ParameterBindings?.Remove(e.ParameterName);
            if (target.ParameterBindings is { Count: > 0 })
            {
                // 仍有字段：保持参数订阅激活
                RoiChanged?.Invoke(this, new RoiChangedEventArgs(target, RoiChangeType.Modified));
                return;
            }

            // 参数订阅配置已清空：仍保留区域订阅配置则激活之，否则回退本地绘制
            target.ParameterBindings = null;
            target.ComposeShapeType = ShapeType.None;
            if (target.Binding != null)
            {
                target.Source = RoiGeometrySource.WholeRegionSub;
                RoiChanged?.Invoke(this, new RoiChangedEventArgs(target, RoiChangeType.Modified));
                StatusMessage = $"已清空参数订阅，切换到区域订阅 (Binding 保留)";
                return;
            }
            target.Source = RoiGeometrySource.Drawing;
            target.LocalGeometry ??= new RoiShape
            {
                Geometry = RoiShape.CreateDefaultGeometry(SelectedShapeType ?? ShapeType.Rectangle)
            };
            RoiChanged?.Invoke(this, new RoiChangedEventArgs(target, RoiChangeType.Modified));
        }

        /// <summary>
        /// 拼装目标形状切换事件处理（同步 Roi.ComposeShapeType；整对象订阅模式忽略）
        /// </summary>
        private void OnShapeTypeChanged(object? sender, ShapeType shapeType)
        {
            var target = EnsureTargetRoiSlot();
            if (target == null) return;
            target.ComposeShapeType = shapeType;
        }

        /// <summary>
        /// 添加几何输入
        /// </summary>
        public void AddRoi(ShapeType shapeType)
        {
            var roi = new Roi
            {
                Name = GetNextRoiName(),
                LocalGeometry = new RoiShape
                {
                    Geometry = RoiShape.CreateDefaultGeometry(shapeType)
                }
            };

            // 记录编辑历史
            _editHistory.ExecuteAction(new CreateRoiAction(roi, this));

            Rois.Add(roi);
            SelectedRoi = roi;
            StatusMessage = $"已添加 {shapeType} 几何输入";

            RoiChanged?.Invoke(this, new RoiChangedEventArgs(roi, RoiChangeType.Added));
        }


        /// <summary>
        /// 获取下一个几何输入默认名，优先填充索引空洞
        /// </summary>
        /// <remarks>
        /// 从1开始查找第一个未被使用的索引，优先填充被删除输入留下的空洞。
        /// 例如：已有 几何输入_1、几何输入_3，则返回 几何输入_2
        /// </remarks>
        private string GetNextRoiName()
        {
            var usedIndices = new HashSet<int>();
            foreach (var roi in Rois)
            {
                if (roi.Name != null && roi.Name.StartsWith("几何输入_"))
                {
                    var indexPart = roi.Name.Substring(5);
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
            return $"几何输入_{nextIndex}";
        }

        /// <summary>
        /// 移除选中区域
        /// </summary>
        public void RemoveSelectedRoi()
        {
            if (SelectedRoi == null) return;

            var roi = SelectedRoi;

            // ★ 诊断日志：删除操作开始
            var beforeRoiNames = string.Join(", ", Rois.Select(x => $"{x.Name}(Id={x.Id:B})"));
            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[RemoveSelectedRoi] 删除开始 | 目标={roi.Name}(Id={roi.Id:B}) | Rois.Count={Rois.Count} | Rois=[{beforeRoiNames}] | SelectedRoi={SelectedRoi.Name}",
                "RoiEditorViewModel");

            // 记录编辑历史
            _editHistory.ExecuteAction(new DeleteRoiAction(roi, this));

            Rois.Remove(roi);
            SelectedRoi = Rois.FirstOrDefault();
            StatusMessage = $"已移除几何输入 {roi.Name}";

            // ★ 诊断日志：删除操作完成
            var afterRoiNames = string.Join(", ", Rois.Select(x => $"{x.Name}(Id={x.Id:B})"));
            VisionLogger.Instance.Log(LogLevel.Warning,
                $"[RemoveSelectedRoi] 删除完成 | Rois.Count={Rois.Count} | Rois=[{afterRoiNames}] | SelectedRoi={SelectedRoi?.Name ?? "null"}",
                "RoiEditorViewModel");

            RoiChanged?.Invoke(this, new RoiChangedEventArgs(roi, RoiChangeType.Removed));
        }

        /// <summary>
        /// 清除所有区域
        /// </summary>
        public void ClearAllRois()
        {
            if (Rois.Count == 0) return;

            var count = Rois.Count;
            var deletedRois = Rois.ToList();

            // 记录编辑历史
            _editHistory.ExecuteAction(new ClearAllRoisAction(deletedRois, this));

            Rois.Clear();
            SelectedRoi = null;
            StatusMessage = $"已清除 {count} 个区域";

            RoiChanged?.Invoke(this, new RoiChangedEventArgs(null, RoiChangeType.Cleared));
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
        /// 内部添加几何输入（用于撤销/重做）
        /// </summary>
        internal void AddRoiInternal(Roi roi)
        {
            Rois.Add(roi);
            RoiChanged?.Invoke(this, new RoiChangedEventArgs(roi, RoiChangeType.Added));
        }

        /// <summary>
        /// 内部移除几何输入（用于撤销/重做）
        /// </summary>
        internal void RemoveRoiInternal(Roi roi)
        {
            Rois.Remove(roi);
            if (SelectedRoi == roi)
                SelectedRoi = null;
            RoiChanged?.Invoke(this, new RoiChangedEventArgs(roi, RoiChangeType.Removed));
        }

        /// <summary>
        /// 内部设置几何输入列表（用于撤销/重做）
        /// </summary>
        internal void SetRoisInternal(IEnumerable<Roi> rois)
        {
            Rois.Clear();
            foreach (var roi in rois)
            {
                Rois.Add(roi);
            }
            SelectedRoi = null;
            RoiChanged?.Invoke(this, new RoiChangedEventArgs(null, RoiChangeType.Cleared));
        }

        /// <summary>
        /// 更新形状参数
        /// </summary>
        /// <remarks>
        /// Parameters 现在是计算属性，直接引用 SelectedRoi.LocalGeometry，
        /// 不需要手动创建和同步对象。
        /// </remarks>
        private void UpdateParameters()
        {
            OnPropertyChanged(nameof(Parameters));
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
            if (SelectedRoi?.LocalGeometry is RoiShape shapeDef)
            {
                EditingShape = shapeDef;
                SelectedShapeType = shapeDef.ShapeType;
            }
            else
            {
                EditingShape = null;
                SelectedShapeType = null;
            }

            // 更新参数面板（覆盖本地/订阅双态）：选中变化 → 按 SelectedRoi.Source 同步 radio + 回灌
            if (_parameterPanel != null && SelectedRoi != null)
            {
                _parameterPanel.LoadFromRoi(SelectedRoi, syncSubMode: true);
            }

            // 通知计算属性变更
            OnPropertyChanged(nameof(HasSelection));
            OnPropertyChanged(nameof(CanEdit));
            OnPropertyChanged(nameof(SelectedRoiMode));
            OnPropertyChanged(nameof(SelectedRoiShapeType));
            OnPropertyChanged(nameof(IsSubscribeMode));
            OnPropertyChanged(nameof(SubscriptionBindingPath));
            OnPropertyChanged(nameof(HasParametersVisible));
            OnPropertyChanged(nameof(HasDrawingParametersVisible));
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

        public void Dispose()
        {
            // 清理资源
            _editHistory.HistoryChanged -= OnEditHistoryChanged;

            if (_parameterPanel != null)
            {
                _parameterPanel.BindingChanged -= OnBindingChanged;
                _parameterPanel.ParameterSettingChanged -= OnParameterSettingChanged;
                _parameterPanel.ShapeTypeChanged -= OnShapeTypeChanged;
            }

            if (_nodeSelector != null)
            {
                _nodeSelector.SelectionConfirmed -= OnNodeSelectionConfirmed;
                _nodeSelector.Dispose();
            }
        }
    }

    /// <summary>
    /// 几何输入变更事件参数
    /// </summary>
    public class RoiChangedEventArgs : EventArgs
    {
        public Roi? Roi { get; }
        public RoiChangeType ChangeType { get; }

        public RoiChangedEventArgs(Roi? roi, RoiChangeType changeType)
        {
            Roi = roi;
            ChangeType = changeType;
        }
    }

    /// <summary>
    /// 几何输入变更类型
    /// </summary>
    public enum RoiChangeType
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
