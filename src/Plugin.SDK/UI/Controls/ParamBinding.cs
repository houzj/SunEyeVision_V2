using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls
{
    /// <summary>
    /// 参数绑定控件 - 支持树形结构的数据源选择
    /// </summary>
    /// <remarks>
    /// V2.0 重构要点：
    /// 1. 继承 ParamBindingBase，专注于参数绑定功能
    /// 2. 支持树形结构显示数据源
    /// 3. 支持按数据类型过滤
    /// 4. 独立的绑定选择功能
    /// </remarks>
    public class ParamBinding : ParamBindingBase
    {
        #region 依赖属性

        public static readonly DependencyProperty ParameterNameProperty =
            DependencyProperty.Register(
                nameof(ParameterName),
                typeof(string),
                typeof(ParamBinding),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// 所属节点ID（用于消费者绑定记录）
        /// </summary>
        public static readonly DependencyProperty NodeIdProperty =
            DependencyProperty.Register(
                nameof(NodeId),
                typeof(string),
                typeof(ParamBinding),
                new PropertyMetadata(string.Empty));

        /// <summary>
        /// 所属节点名称（用于消费者绑定记录）
        /// </summary>
        public static readonly DependencyProperty NodeNameProperty =
            DependencyProperty.Register(
                nameof(NodeName),
                typeof(string),
                typeof(ParamBinding),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty IsBoundProperty =
            DependencyProperty.Register(
                nameof(IsBound),
                typeof(bool),
                typeof(ParamBinding),
                new FrameworkPropertyMetadata(false));

        public static readonly DependencyProperty BindingDisplayTextProperty =
            DependencyProperty.Register(
                nameof(BindingDisplayText),
                typeof(string),
                typeof(ParamBinding),
                new PropertyMetadata(string.Empty));

        public static readonly DependencyProperty BindingSourceProperty =
            DependencyProperty.Register(
                nameof(BindingSource),
                typeof(string),
                typeof(ParamBinding),
                new FrameworkPropertyMetadata(string.Empty,
                    FrameworkPropertyMetadataOptions.BindsTwoWayByDefault,
                    OnBindingSourceChanged));

        public static readonly DependencyProperty AvailableDataSourcesProperty =
            DependencyProperty.Register(
                nameof(AvailableDataSources),
                typeof(System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>),
                typeof(ParamBinding),
                new PropertyMetadata(null, OnAvailableDataSourcesChanged));

        public static readonly DependencyProperty TreeNodesProperty =
            DependencyProperty.Register(
                nameof(TreeNodes),
                typeof(System.Collections.ObjectModel.ObservableCollection<TreeNodeData>),
                typeof(ParamBinding),
                new PropertyMetadata(null));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(ParamBinding),
                new PropertyMetadata(new CornerRadius(4)));

        /// <summary>
        /// 编辑同步开关（契约 C2）：true 时用户编辑显示文本框的内容全文回写 BindingSource。
        /// 默认 false（保持现状，仅索引同步），零影响其他工具。条件分支等需要"所见即所存"的控件显式开启。
        /// </summary>
        public static readonly DependencyProperty SyncEditingToSourceProperty =
            DependencyProperty.Register(
                nameof(SyncEditingToSource),
                typeof(bool),
                typeof(ParamBinding),
                new PropertyMetadata(false));

        /// <summary>
        /// 形状筛选（可选）：目标为 ROI 容器（词目未限定形状）时，把绑定候选树收敛为**单一形状叶**。
        /// </summary>
        /// <remarks>
        /// 呈现诉求与值语义分离（方案 §11 决策 15）：本属性只影响"候选树形态"，**不影响绑定值类型** ——
        /// 区域源的 <c>[Type=X]</c> 过滤结果恒为 <c>RoiShape[]</c>，故 <c>DataType</c> 保持 RoiShape[]。
        /// <c>null</c> = 不筛选（全部形状叶）；区域订阅面板用它实现"图形类型必选"。
        /// </remarks>
        public static readonly DependencyProperty ShapeTypeFilterProperty =
            DependencyProperty.Register(
                nameof(ShapeTypeFilter),
                typeof(ShapeType?),
                typeof(ParamBinding),
                new PropertyMetadata(null, OnShapeTypeFilterChanged));

        #endregion

        #region 属性封装

        /// <summary>
        /// 参数名称
        /// </summary>
        public string ParameterName
        {
            get => (string)GetValue(ParameterNameProperty);
            set => SetValue(ParameterNameProperty, value);
        }

        /// <summary>
        /// 所属节点ID（用于消费者绑定记录）
        /// </summary>
        public string NodeId
        {
            get => (string)GetValue(NodeIdProperty);
            set => SetValue(NodeIdProperty, value);
        }

        /// <summary>
        /// 所属节点名称（用于消费者绑定记录）
        /// </summary>
        public string NodeName
        {
            get => (string)GetValue(NodeNameProperty);
            set => SetValue(NodeNameProperty, value);
        }

        /// <summary>
        /// 是否已绑定
        /// </summary>
        public bool IsBound
        {
            get => (bool)GetValue(IsBoundProperty);
            set => SetValue(IsBoundProperty, value);
        }

        /// <summary>
        /// 绑定显示文本（中文，用于界面显示）
        /// </summary>
        public string BindingDisplayText
        {
            get => (string)GetValue(BindingDisplayTextProperty);
            set => SetValue(BindingDisplayTextProperty, value);
        }

        /// <summary>
        /// 绑定源路径（格式：节点ID.属性名）
        /// </summary>
        public string BindingSource
        {
            get => (string)GetValue(BindingSourceProperty);
            set => SetValue(BindingSourceProperty, value);
        }

        /// <summary>
        /// 可用数据源列表
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>? AvailableDataSources
        {
            get => (System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>?)GetValue(AvailableDataSourcesProperty);
            set => SetValue(AvailableDataSourcesProperty, value);
        }

        /// <summary>
        /// 树形结构节点
        /// </summary>
        public System.Collections.ObjectModel.ObservableCollection<TreeNodeData>? TreeNodes
        {
            get => (System.Collections.ObjectModel.ObservableCollection<TreeNodeData>?)GetValue(TreeNodesProperty);
            set => SetValue(TreeNodesProperty, value);
        }

        /// <summary>
        /// 圆角半径
        /// </summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
        }

        /// <summary>
        /// 编辑同步开关（契约 C2）：true 时用户编辑显示文本框的内容全文回写 BindingSource。
        /// </summary>
        public bool SyncEditingToSource
        {
            get => (bool)GetValue(SyncEditingToSourceProperty);
            set => SetValue(SyncEditingToSourceProperty, value);
        }

        /// <summary>
        /// 形状筛选（可选）：ROI 目标下把候选收敛为单一形状叶；<c>null</c> = 不筛选。详见依赖属性注释。
        /// </summary>
        public ShapeType? ShapeTypeFilter
        {
            get => (ShapeType?)GetValue(ShapeTypeFilterProperty);
            set => SetValue(ShapeTypeFilterProperty, value);
        }

        #endregion

        #region 数据源反查（P0.5-F0）

        /// <summary>
        /// 按当前绑定路径反查**选中的数据源**（沿候选树匹配 <c>GetBindingPath()</c>）。
        /// </summary>
        /// <returns>命中的数据源；树未就绪、路径为空、或为手写非候选路径时返回 <c>null</c>。</returns>
        /// <remarks>
        /// ★ 用途（方案 P0.5-F0）：写入持久化时**从模型取展示名**（<c>SourceNodeName</c> /
        /// <c>EffectiveLeafDisplayName</c>），取代"从 <c>BindingDisplayText</c> 反解" ——
        /// 显示文本是命名规则的**投影**，反解等于把显示名当数据存盘（改一次命名规则老工程就读错）。
        /// </remarks>
        public AvailableDataSource? ResolveSelectedDataSource()
        {
            var path = BindingSource;
            if (string.IsNullOrEmpty(path) || TreeNodes == null)
                return null;

            foreach (var node in TreeNodes)
            {
                var hit = FindNodeByBindingPath(node, path);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        private static AvailableDataSource? FindNodeByBindingPath(TreeNodeData node, string path)
        {
            var ds = node.DataSource;
            if (ds != null && string.Equals(ds.GetBindingPath(), path, StringComparison.Ordinal))
                return ds;

            if (node.Children == null)
                return null;

            foreach (var child in node.Children)
            {
                var hit = FindNodeByBindingPath(child, path);
                if (hit != null)
                    return hit;
            }

            return null;
        }

        #endregion

        #region 事件

        public static readonly RoutedEvent BindingStateChangedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(BindingStateChanged),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(ParamBinding));

        public static readonly RoutedEvent BindingSourceSelectedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(BindingSourceSelected),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(ParamBinding));

        /// <summary>
        /// 绑定状态变更事件
        /// </summary>
        public event RoutedEventHandler BindingStateChanged
        {
            add => AddHandler(BindingStateChangedEvent, value);
            remove => RemoveHandler(BindingStateChangedEvent, value);
        }

        /// <summary>
        /// 绑定源选择事件
        /// </summary>
        public event RoutedEventHandler BindingSourceSelected
        {
            add => AddHandler(BindingSourceSelectedEvent, value);
            remove => RemoveHandler(BindingSourceSelectedEvent, value);
        }

        /// <summary>
        /// 全局变量选择请求事件 — 点击"全局变量"动作节点时触发
        /// </summary>
        /// <remarks>
        /// 宿主代码处理此事件，打开全局变量选择对话框，选择完成后调用
        /// <see cref="CompleteGlobalVariableBinding"/> 设置绑定结果。
        /// </remarks>
        public static readonly RoutedEvent GlobalVariableSelectionRequestedEvent =
            EventManager.RegisterRoutedEvent(
                nameof(GlobalVariableSelectionRequested),
                RoutingStrategy.Bubble,
                typeof(RoutedEventHandler),
                typeof(ParamBinding));

        /// <summary>
        /// 全局变量选择请求事件
        /// </summary>
        public event RoutedEventHandler GlobalVariableSelectionRequested
        {
            add => AddHandler(GlobalVariableSelectionRequestedEvent, value);
            remove => RemoveHandler(GlobalVariableSelectionRequestedEvent, value);
        }

        #endregion

        #region 控件引用

        private Button? _bindingButton;
        private Popup? _bindingPopup;
        private TreeView? _bindingTreeView;
        private TextBox? _bindingDisplay;

        // 私有字段：保存当前数据源集合的引用
        private System.Collections.ObjectModel.ObservableCollection<AvailableDataSource>? _currentDataSources;

        /// <summary>
        /// 数据源集合变更后的重建请求已排队（Dispatcher 防抖合并标志）
        /// </summary>
        private bool _rebuildPending;

        /// <summary>
        /// 程序回填显示文本抑制标志：true 时 OnBindingDisplayTextChanged 直接返回，不触发编辑回写。
        /// 用于区分"程序赋值显示名"（绑定选择/解绑/全局变量/数据源刷新/BindingSource 回调）与"用户编辑"，
        /// 防止程序回填误覆盖 BindingSource（A2 治本）。
        /// </summary>
        private bool _isProgrammaticDisplayUpdate;

        #endregion

        #region 构造函数

        static ParamBinding()
        {
            DefaultStyleKeyProperty.OverrideMetadata(
                typeof(ParamBinding),
                new FrameworkPropertyMetadata(typeof(ParamBinding)));
        }

        public ParamBinding()
        {
            TreeNodes = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>();
        }

        #endregion

        #region 回调方法

        private static void OnAvailableDataSourcesChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ParamBinding control)
                return;

            // ★ 根治 A：宿主（ToolDebugControlBase）填充数据源用的是**同一集合实例**的 Clear/Add，
            //   DP 回调因此只在"实例被替换"时触发一次；若那一刻集合尚空，会建成空树且**永不重建**
            //   → 绑定显示名反查永远失败（表现为"关闭重开调试窗口，绑定显示成节点 GUID"）。
            //   订阅集合内容变更，保证填充完成后重建树并重译显示名。
            if (control._currentDataSources != null)
            {
                control._currentDataSources.CollectionChanged -= control.OnDataSourcesCollectionChanged;
            }

            if (e.NewValue is System.Collections.ObjectModel.ObservableCollection<AvailableDataSource> newDataSources)
            {
                control._currentDataSources = newDataSources;
                newDataSources.CollectionChanged += control.OnDataSourcesCollectionChanged;
                control.RebuildTreeNodes();
            }
            else
            {
                control._currentDataSources = null;
                control.TreeNodes = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>();
            }
        }

        /// <summary>
        /// 数据源集合内容变化（Clear + 逐项 Add 填充）→ 防抖重建树并重译显示名。
        /// 用 Dispatcher 合并连续变更，避免逐项触发多次重建。
        /// </summary>
        private void OnDataSourcesCollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            if (_rebuildPending)
                return;

            _rebuildPending = true;
            Dispatcher.BeginInvoke(new Action(() =>
            {
                _rebuildPending = false;
                RebuildTreeNodes();
            }), System.Windows.Threading.DispatcherPriority.Background);
        }

        /// <summary>
        /// BindingSource 变更回调：
        /// 1) 契约 C2 单轨模式（SyncEditingToSource=true）：用绑定路径反查中文显示名回填显示位。
        /// 2) 默认模式：程序回显 BindingSource（如重开调试窗 LoadFromRoi 恢复订阅）时，若尚未有显示名，
        ///    同样反查翻译回填——修复"订阅面板区域源关闭重开显示空白、运行仍按绑定解析"的显示缺失。
        /// 程序回填显示名时抑制编辑回写，避免误覆盖绑定源（A2/A3 治本）。
        /// </summary>
        private static void OnBindingSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ParamBinding control)
                return;

            var bindingPath = e.NewValue as string;
            if (control.SyncEditingToSource)
            {
                control._isProgrammaticDisplayUpdate = true;
                try
                {
                    control.BindingDisplayText = string.IsNullOrEmpty(bindingPath)
                        ? string.Empty
                        : control.GenerateBindingDisplayText(bindingPath);
                }
                finally
                {
                    control._isProgrammaticDisplayUpdate = false;
                }
                return;
            }

            // 绑定源被程序清空（如模式切换解除订阅 / 清除区域源）：必须同步清空显示文本，
            // 否则控件残留旧显示名——造成"UI 看着仍绑定、运行时绑定已失效"的假象。
            if (string.IsNullOrEmpty(bindingPath))
            {
                control._isProgrammaticDisplayUpdate = true;
                try
                {
                    control.BindingDisplayText = string.Empty;
                }
                finally
                {
                    control._isProgrammaticDisplayUpdate = false;
                }
                return;
            }

            // 默认模式：仅当还没有显示名时补翻译（程序回显场景），已有显示名（用户刚选择生成/容器已赋值）不覆盖。
            control.BackfillDisplayTextFromSource();
        }

        /// <summary>
        /// 程序回显补翻译：BindingSource 非空且当前无显示名（或显示名仍是原文占位=上次树未就绪翻译失败）时，
        /// 反查数据源树生成中文显示名回填。用抑制标志包住，避免触发编辑回写误覆盖 BindingSource。
        /// 树尚未就绪时翻译会回退原文占位，树重建后（OnAvailableDataSourcesChanged → RebuildTreeNodes）会再次调用本方法。
        /// </summary>
        private void BackfillDisplayTextFromSource()
        {
            var bindingPath = BindingSource;
            if (string.IsNullOrEmpty(bindingPath))
                return;

            // BindingDisplayText == BindingSource 表示上次翻译失败留下的原文占位（树未就绪），需要重试。
            if (!string.IsNullOrEmpty(BindingDisplayText) && BindingDisplayText != bindingPath)
                return;

            _isProgrammaticDisplayUpdate = true;
            try
            {
                BindingDisplayText = GenerateBindingDisplayText(bindingPath);
            }
            finally
            {
                _isProgrammaticDisplayUpdate = false;
            }
        }

        /// <summary>
        /// DataType 变更时重新构建树形结构
        /// </summary>
        protected override void OnDataTypeChangedCore(DependencyPropertyChangedEventArgs e)
        {
            RebuildTreeNodes();
        }

        /// <summary>
        /// 形状筛选变更 → 重建候选树（区域订阅面板切换"图形类型"时收敛候选）。
        /// </summary>
        private static void OnShapeTypeFilterChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ParamBinding control)
            {
                control.RebuildTreeNodes();
            }
        }

        #endregion

        #region 树形结构构建

        /// <summary>
        /// 重新构建树形结构
        /// </summary>
        private void RebuildTreeNodes()
        {
            var nodes = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>();

            if (_currentDataSources != null)
            {
                var dataSourceList = _currentDataSources.ToList();

                // 使用基类的静态方法进行类型过滤
                var filteredDataSources = ParamBindingBase.FilterDataSourcesByType(dataSourceList, DataType);

                // 使用基类的静态方法构建树形结构（传 DataType：RoiShape[] 子树按目标词目定型
                // —— 数值目标出类型容器+字段叶，ROI/几何目标出类型集合叶；「整体」已取消，见方案 §11）
                // 形状筛选（区域订阅）走独立入参，不影响 DataType 的值语义（决策 15）
                var treeNodes = ParamBindingBase.BuildTreeStructure(filteredDataSources, DataType, ShapeTypeFilter);
                foreach (var node in treeNodes)
                {
                    nodes.Add(node);
                }
            }

            // 最前面插入"无"选项（用于解绑）
            nodes.Insert(0, new TreeNodeData
            {
                Text = "无",
                IsSelectable = true,
                DataSource = null,
            });

            // 始终在末尾追加"全局变量"动作节点（点击打开选择对话框，保留搜索/分组/表格等便捷能力）
            nodes.Add(new TreeNodeData
            {
                Text = "全局变量",
                IsSelectable = true,
                IsActionNode = true,
                DataSource = null,
            });

            TreeNodes = nodes;

            // 数据源树就绪/重建后，重试翻译回填绑定显示名：
            // 解决程序回显早于数据源注入时首次翻译失败（显示名停留原文占位）的问题。
            BackfillDisplayTextFromSource();
        }

        #endregion

        #region 模板应用

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _bindingButton = GetTemplateChild("PART_BindingButton") as Button;
            _bindingPopup = GetTemplateChild("PART_BindingPopup") as Popup;
            _bindingTreeView = GetTemplateChild("PART_BindingTreeView") as TreeView;
            _bindingDisplay = GetTemplateChild("PART_BindingDisplay") as TextBox;

            // 绑定事件
            if (_bindingButton != null)
            {
                _bindingButton.Click += OnBindingButtonClick;
            }

            if (_bindingTreeView != null)
            {
                _bindingTreeView.SelectedItemChanged += OnBindingTreeViewSelectedItemChanged;
                _bindingTreeView.PreviewMouseLeftButtonDown += OnBindingTreeViewPreviewMouseLeftButtonDown;
            }

            // 监听显示文本变更（用户编辑数组索引）
            if (_bindingDisplay != null)
            {
                _bindingDisplay.TextChanged += OnBindingDisplayTextChanged;
            }

            // 初始化树形结构
            if (AvailableDataSources != null && AvailableDataSources.Count > 0)
            {
                RebuildTreeNodes();
            }
        }

        #endregion

        #region 事件处理

        private void OnBindingButtonClick(object sender, RoutedEventArgs e)
        {
            // 显示绑定选择器 Popup
            if (_bindingPopup != null)
            {
                _bindingPopup.IsOpen = true;
            }

            RaiseEvent(new RoutedEventArgs(BindingStateChangedEvent));
        }

        /// <summary>
        /// TreeView 鼠标左键按下事件（用于处理重复点击已选中节点的情况）
        /// </summary>
        private void OnBindingTreeViewPreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            var originalSource = e.OriginalSource as DependencyObject;
            var treeViewItem = FindParent<TreeViewItem>(originalSource);

            if (treeViewItem != null && treeViewItem.DataContext is TreeNodeData selectedNode)
            {
                // 动作节点：触发全局变量选择
                if (selectedNode.IsActionNode)
                {
                    e.Handled = true;
                    RaiseEvent(new RoutedEventArgs(GlobalVariableSelectionRequestedEvent));
                    return;
                }

                // 容器节点（可展开的类型分组/数组容器）：单击整行 = 展开/收起导航，绝不绑定。
                // 唯一例外：无子叶的"无"节点（解绑）与可绑叶子在后续分支处理。
                if (IsNavigationContainer(selectedNode))
                {
                    selectedNode.IsExpanded = !selectedNode.IsExpanded;
                    e.Handled = true;
                    return;
                }

                // "无"节点：解绑
                if (selectedNode.IsSelectable && selectedNode.DataSource == null)
                {
                    e.Handled = true;
                    BindingSource = string.Empty;
                    _isProgrammaticDisplayUpdate = true;
                    try
                    {
                        BindingDisplayText = string.Empty;
                    }
                    finally
                    {
                        _isProgrammaticDisplayUpdate = false;
                    }
                    IsBound = false;

                    if (_bindingPopup != null)
                    {
                        _bindingPopup.IsOpen = false;
                    }

                    RaiseEvent(new RoutedEventArgs(BindingSourceSelectedEvent));
                    RaiseEvent(new RoutedEventArgs(BindingStateChangedEvent));
                    return;
                }

                if (selectedNode.IsSelectable && selectedNode.DataSource != null)
                {
                    ApplyDataSourceBinding(selectedNode);

                    // 关闭 Popup
                    if (_bindingPopup != null)
                    {
                        _bindingPopup.IsOpen = false;
                    }

                    RaiseEvent(new RoutedEventArgs(BindingStateChangedEvent));

                    // 标记事件已处理，防止触发 SelectedItemChanged
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 判定节点是否为"导航容器"（只展开、不可绑定）。
        /// 规则：绑定终点必为叶子——凡含可绑子叶（IsSelectable）的节点一律视为容器；
        /// 或自身不可选（IsSelectable=false）但挂有 children 的数组/类型分组节点。
        /// 防御意义：即便树构建误标，含可绑子叶的节点也绝不会被当作绑定终点（防"展开节点误绑"）。
        /// </summary>
        private static bool IsNavigationContainer(TreeNodeData node)
        {
            if (node.Children is { Count: > 0 } && node.Children.Any(c => c.IsSelectable))
                return true;
            return !node.IsSelectable && node.Children is { Count: > 0 };
        }

        /// <summary>
        /// 查找指定类型的父元素
        /// </summary>
        private static T? FindParent<T>(DependencyObject? child) where T : DependencyObject
        {
            if (child == null)
                return null;

            var parentObject = VisualTreeHelper.GetParent(child);

            if (parentObject == null)
                return null;

            if (parentObject is T parent)
                return parent;

            return FindParent<T>(parentObject);
        }

        private void OnBindingTreeViewSelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            if (e.NewValue is TreeNodeData selectedNode)
            {
                // 动作节点：触发全局变量选择
                if (selectedNode.IsActionNode)
                {
                    e.Handled = true;
                    RaiseEvent(new RoutedEventArgs(GlobalVariableSelectionRequestedEvent));
                    return;
                }

                // 容器节点：仅展开/收起，绝不绑定（与 PreviewMouseLeftButtonDown 同规则）
                if (IsNavigationContainer(selectedNode))
                {
                    selectedNode.IsExpanded = !selectedNode.IsExpanded;
                    return;
                }

                // "无"节点：解绑
                if (selectedNode.IsSelectable && selectedNode.DataSource == null)
                {
                    e.Handled = true;
                    BindingSource = string.Empty;
                    _isProgrammaticDisplayUpdate = true;
                    try
                    {
                        BindingDisplayText = string.Empty;
                    }
                    finally
                    {
                        _isProgrammaticDisplayUpdate = false;
                    }
                    IsBound = false;

                    if (_bindingPopup != null)
                    {
                        _bindingPopup.IsOpen = false;
                    }

                    RaiseEvent(new RoutedEventArgs(BindingSourceSelectedEvent));
                    RaiseEvent(new RoutedEventArgs(BindingStateChangedEvent));
                    return;
                }

                if (selectedNode.IsSelectable && selectedNode.DataSource != null)
                {
                    ApplyDataSourceBinding(selectedNode);

                    // 关闭 Popup
                    if (_bindingPopup != null)
                    {
                        _bindingPopup.IsOpen = false;
                    }

                    RaiseEvent(new RoutedEventArgs(BindingStateChangedEvent));
                }
            }
        }

        /// <summary>
        /// 应用数据源绑定：设置绑定源路径与显示文本，触发绑定事件。
        /// </summary>
        private void ApplyDataSourceBinding(TreeNodeData selectedNode)
        {
            var ds = selectedNode.DataSource!;
            BindingSource = BuildScalarSafeBindingPath(ds);

            var rootNode = GetRootNode(selectedNode);
            var rootName = rootNode?.Text ?? "";
            var leafName = GetDisplayLeafName(selectedNode);

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[ParamBinding] 选中节点: {leafName}, 父节点: {selectedNode.Parent?.Text ?? "null"}, 根节点: {rootName}",
                "ParamBinding");

            // 程序回填显示名：包抑制标志，避免触发编辑回写误覆盖 BindingSource（A2 治本）。
            // 单轨模式下 OnBindingSourceChanged 已回填显示文本，此处再赋值保持一致（双轨工具仍靠这里）。
            _isProgrammaticDisplayUpdate = true;
            try
            {
                BindingDisplayText = $"{rootName}.{leafName}";
            }
            finally
            {
                _isProgrammaticDisplayUpdate = false;
            }

            RaiseEvent(new RoutedEventArgs(BindingSourceSelectedEvent));
            IsBound = true;
        }

        /// <summary>
        /// 标量参数选中"数组单值输出"时生成无索引绑定路径。
        /// </summary>
        /// <remarks>
        /// 数据源对外元数据统一提升为数组（单值 = 长度 1 数组，对齐 Halcon tuple 语义），
        /// 标量参数（DataType 非数组）若绑定默认的 "[]" 路径会保留整个数组，导致执行层类型不匹配。
        /// 改为无索引路径，让 OutputVariableResolver 自动解包长度 1 数组为单值（决策3 存量兼容）。
        /// RoI 带 BindingSuffix（[Type=Circle]/[i]）的合成节点不受影响，仍走 GetBindingPath。
        ///
        /// ★ 契约：本方法是 BindingSource 的**唯一**路径生成入口。
        ///   写入（ApplyDataSourceBinding）与反查（FindNodeRecursive）必须共用它；
        ///   一旦"写读规则分叉"，绑定显示名反查就会失败（曾表现为重开窗口显示节点 GUID）。
        /// </remarks>
        private string BuildScalarSafeBindingPath(AvailableDataSource ds)
        {
            if (DataType != null && !DataType.IsArray
                && ds.PropertyType?.IsArray == true
                && ds.BindingSuffix == null)
            {
                return $"{ds.SourceNodeId}.{ds.PropertyName}";
            }
            return ds.GetBindingPath();
        }

        /// <summary>
        /// 获取节点的根节点（向上遍历 Parent 引用）
        /// </summary>
        /// <param name="node">目标节点</param>
        /// <returns>根节点</returns>
        private static TreeNodeData? GetRootNode(TreeNodeData node)
        {
            var current = node;
            while (current.Parent != null)
            {
                current = current.Parent;
            }
            return current;
        }

        /// <summary>
        /// 根据绑定源路径查找对应的 TreeNodeData
        /// </summary>
        /// <param name="bindingPath">绑定源路径（格式：节点ID.属性名，属性名可含 [N] 索引）</param>
        /// <returns>对应的 TreeNodeData，未找到则返回 null</returns>
        private TreeNodeData? FindNodeByBindingPath(string bindingPath)
        {
            if (TreeNodes == null || string.IsNullOrEmpty(bindingPath))
                return null;

            // 用与写入侧相同的路径规则（BuildScalarSafeBindingPath）在树中精确匹配
            foreach (var rootNode in TreeNodes)
            {
                var node = FindNodeRecursive(rootNode, bindingPath);
                if (node != null)
                    return node;
            }

            return null;
        }

        /// <summary>
        /// 递归查找节点（按与写入侧**完全相同**的路径规则匹配）。
        /// ★ 根治 B：必须调用 BuildScalarSafeBindingPath —— 它是 BindingSource 的唯一路径生成入口
        ///   （写入侧 ApplyDataSourceBinding 同用）。历史上此处误用 DataSource.GetBindingPath()：
        ///   对"标量参数绑定数组属性"（DataType 非数组 + 属性是数组）会多出 "[]" 后缀，
        ///   与写入值不相等 → 反查失败 → 显示名回退成节点 GUID。
        /// </summary>
        private TreeNodeData? FindNodeRecursive(TreeNodeData node, string fullBindingPath)
        {
            if (node.DataSource != null && BuildScalarSafeBindingPath(node.DataSource) == fullBindingPath)
                return node;

            foreach (var child in node.Children)
            {
                var found = FindNodeRecursive(child, fullBindingPath);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// 由宿主（订阅面板等）按模型当前状态显式同步"绑定源 + 显示文本"。
        /// 当绑定源经 OneWay 绑定/局部值覆盖等路径不可靠时，宿主调用本方法强制控件与模型一致：
        /// bindingPath 为空 → 源与显示文本都清空（对应"解除绑定/模式切换清空"）；
        /// 非空 → 写源并翻译显示名。始终用抑制标志包裹，避免触发编辑回写。
        /// </summary>
        public void SyncDisplay(string? bindingPath)
        {
            BindingSource = bindingPath ?? string.Empty;
            _isProgrammaticDisplayUpdate = true;
            try
            {
                BindingDisplayText = string.IsNullOrEmpty(bindingPath)
                    ? string.Empty
                    : GenerateBindingDisplayText(bindingPath);
            }
            finally
            {
                _isProgrammaticDisplayUpdate = false;
            }
            IsBound = !string.IsNullOrEmpty(bindingPath);
        }

        /// <summary>
        /// 根据绑定源路径生成绑定显示文本
        /// </summary>
        /// <param name="bindingPath">绑定源路径（格式：节点ID.属性名，属性名可含 [N] 索引）</param>
        /// <returns>绑定显示文本（格式：根节点.叶子节点），未找到则返回绑定路径</returns>
        public string GenerateBindingDisplayText(string bindingPath)
        {
            var node = FindNodeByBindingPath(bindingPath);
            if (node == null)
            {
                // 分级可观测（禁止静默降级）：
                // - 树里还没有任何真实数据源 = 注入尚未到达（可重试）→ Debug 降噪；
                // - 树已就绪却找不到 = 真实路径错配（写读规则分叉 / 源不在候选）→ Warning，
                //   避免再次出现"显示成节点 GUID 却无人察觉"的静默降级。
                bool treeReady = TreeNodes != null && TreeNodes.Any(n => n.DataSource != null);
                if (treeReady)
                {
                    VisionLogger.Instance.Log(LogLevel.Warning,
                        $"[ParamBinding] 绑定路径在数据源树中不存在（疑似路径错配或源不在候选）: {bindingPath}",
                        "ParamBinding");
                }
                else
                {
                    VisionLogger.Instance.Log(LogLevel.Debug,
                        $"[ParamBinding] 未找到绑定路径对应的节点（等待树重建重试）: {bindingPath}",
                        "ParamBinding");
                }
                return bindingPath;
            }

            // 生成显示文本：根节点.叶子节点
            var rootNode = GetRootNode(node);
            var rootName = rootNode?.Text ?? "";
            var leafName = GetDisplayLeafName(node);

            // 从 bindingPath 中提取索引部分，替换默认的 []
            // P6：用 IndexOf('.') 只切第一个点，属性路径含点（[0].Geometry.X）时不被截断。
            var dotIndex = bindingPath.IndexOf('.');
            var propertyFullName = dotIndex >= 0 ? bindingPath[(dotIndex + 1)..] : bindingPath;
            var (_, arrayIndex, _) = PropertyPathParser.ParsePropertyPart(propertyFullName);

            // 仅有用户手写的具体索引（>=0）才替换默认的 []；-1 表示显式 "[]"/无索引 = 数组整体，保持 "名[]" 不自动带数字
            if (arrayIndex.HasValue && arrayIndex.Value >= 0 && leafName.EndsWith("[]"))
            {
                leafName = leafName.Substring(0, leafName.Length - 2) + $"[{arrayIndex.Value}]";
            }

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[ParamBinding] 生成显示文本: {rootName}.{leafName}, 绑定路径: {bindingPath}",
                "ParamBinding");

            return $"{rootName}.{leafName}";
        }

        #endregion

        #region 辅助方法

        /// <summary>
        /// 获取叶子节点的显示名称 —— **显示名 SSOT 的消费端**。
        /// </summary>
        /// <param name="node">叶子节点</param>
        /// <returns>由数据源模型声明的叶子显示名（含数组语义 <c>[]</c>）</returns>
        /// <remarks>
        /// ★ 历史缺陷：本方法曾按 <c>BindingSuffix</c> 的字符串形态三分支**推断**显示名，
        ///   导致几何字段叶显示英文 CLR 路径（<c>Center.X</c>）且丢失 <c>[]</c>，
        ///   与 ROI 字段叶（<c>中心X[]</c>）不一致。
        ///   现改为**只读投影**：叶子名与数组语义均由 <see cref="AvailableDataSource"/>
        ///   在构造时**声明**（<c>LeafDisplayName</c> / <c>IsArraySemantics</c>），本层不再推断。
        ///   <c>BindingSuffix</c> 只服务运行期路径解析，不参与显示。
        /// </remarks>
        private static string GetDisplayLeafName(TreeNodeData node)
        {
            var ds = node.DataSource;
            if (ds == null)
                return node.Text;
            return ds.GetBindingDisplayName(null);
        }

        /// <summary>
        /// 显示文本变更事件处理 — 同步数组索引到 BindingSource（契约 C2 编辑同步）。
        /// </summary>
        /// <remarks>
        /// 默认（SyncEditingToSource=false，现状）：仅当编辑含 [N]/[] 索引时同步到 BindingSource，
        /// 其余情况不更新（保持历史行为，零影响其他工具）。
        /// SyncEditingToSource=true（契约 C2，条件分支等"所见即所存"控件）：无绑定输常量、或翻译
        /// 失败（改成了常量文本如 "123"/"OK" 等无法翻译为绑定路径）→ 原文回写 BindingSource，
        /// 杜绝"先绑定后改常量仍判绑定"的两轨错位（所见 = 所存 = 所解析）。
        /// 程序回填显示名时（_isProgrammaticDisplayUpdate=true）直接返回，绝不触发编辑回写。
        /// </remarks>
        private void OnBindingDisplayTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_bindingDisplay == null)
                return;

            // 程序回填显示名 → 不触发编辑回写（A2 治本，防程序赋值误覆盖 BindingSource）
            if (_isProgrammaticDisplayUpdate)
                return;

            var displayText = _bindingDisplay.Text;
            var hasBinding = !string.IsNullOrEmpty(BindingSource);

            // 从显示文本提取索引
            var bracketStart = displayText.LastIndexOf('[');
            var bracketEnd = displayText.LastIndexOf(']');
            var hasBracket = bracketStart >= 0 && bracketEnd > bracketStart;

            // 契约 C2：SyncEditingToSource=true → 无绑定输常量（A1 治本，去 hasBinding 守卫）、
            // 或有绑定但翻译失败（无有效索引）时原文回写，让用户手输的常量文本直接成为 BindingSource。
            if (SyncEditingToSource)
            {
                if (!hasBracket || !TrySyncIndexToSource(displayText, bracketStart, bracketEnd))
                {
                    BindingSource = displayText;
                }
                return;
            }

            // 现状：仅同步含有效索引的编辑
            if (!hasBinding || !hasBracket)
                return;

            TrySyncIndexToSource(displayText, bracketStart, bracketEnd);
        }

        /// <summary>
        /// 尝试把显示文本中的索引同步到 BindingSource。
        /// 成功返回 true；无有效索引内容返回 false（用于契约 C2 原文回写判断）。
        /// </summary>
        private bool TrySyncIndexToSource(string displayText, int bracketStart, int bracketEnd)
        {
            if (string.IsNullOrEmpty(BindingSource))
                return false;

            var indexStr = displayText.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

            // 从当前 BindingSource 提取节点ID和属性名。
            // P6：用 IndexOf('.') 只切第一个点，属性路径含点（[0].Geometry.X）时不被截断。
            var dotIndex = BindingSource.IndexOf('.');
            if (dotIndex < 0)
                return false;
            var nodeId = BindingSource[..dotIndex];
            var propertyFullName = BindingSource[(dotIndex + 1)..];
            // P4：提取类型过滤 token 并保留。RoI 分组绑定 "Node1.检测区域[Type=Circle]"，
            // 用户改显示名 []→[n] 时若丢掉 [Type=Circle]，会静默取错元素（违反 rule-015）。
            var (propertyName, _, typeFilter) = PropertyPathParser.ParsePropertyPart(propertyFullName);

            // 构建新的 BindingSource（保留类型过滤 + 字段子路径，仅替换索引位）
            string indexPart;
            if (string.IsNullOrEmpty(indexStr))
                indexPart = "[]";  // [] 空括号 → 整个数组（不指定索引）
            else if (int.TryParse(indexStr, out _))
                indexPart = $"[{indexStr}]";  // [0] [1] 等有效索引
            else
                return false;  // 无效内容 → 同步失败

            // 类型过滤 token（可能不存在，如普通数组绑定）
            var typePart = typeFilter != null ? $"[Type={typeFilter}]" : "";

            // 重建属性部分：属性名 + [Type=X] + 新索引。字段下钻子路径（.Geometry.Center.X 等）
            // 位于属性括号之后，需原样保留，否则手动改索引会把字段叶路径退化为数组整体（rule-015 报错优先）。
            var fieldSubPath = ExtractFieldSubPath(propertyFullName);
            BindingSource = $"{nodeId}.{propertyName}{typePart}{indexPart}{fieldSubPath}";
            return true;
        }

        /// <summary>
        /// 提取属性路径中"数组括号之后"的字段下钻子路径（如 [Type=X][].Geometry.Center.X → ".Geometry.Center.X"）。
        /// 若绑定的是普通数组/类型集合（无字段子路径）返回空串。
        /// </summary>
        private static string ExtractFieldSubPath(string propertyFullName)
        {
            var lastBracket = propertyFullName.LastIndexOf(']');
            if (lastBracket < 0 || lastBracket >= propertyFullName.Length - 1)
                return string.Empty;
            var sub = propertyFullName[(lastBracket + 1)..];
            return sub.StartsWith(".", StringComparison.Ordinal) ? sub : string.Empty;
        }

        #endregion

        /// <summary>
        /// 完成全局变量绑定（由宿主代码在处理 <see cref="GlobalVariableSelectionRequestedEvent"/> 后调用）
        /// </summary>
        /// <param name="variableName">选中变量的名称</param>
        public void CompleteGlobalVariableBinding(string variableName)
        {
            if (string.IsNullOrEmpty(variableName))
                return;

            // 绑定源格式：__GlobalVars__.变量名
            BindingSource = $"__GlobalVars__.{variableName}";
            // 程序回填显示名：包抑制标志，避免触发编辑回写误覆盖 BindingSource（A2 治本）
            _isProgrammaticDisplayUpdate = true;
            try
            {
                BindingDisplayText = $"全局变量.{variableName}";
            }
            finally
            {
                _isProgrammaticDisplayUpdate = false;
            }
            IsBound = true;

            // 触发事件
            RaiseEvent(new RoutedEventArgs(BindingSourceSelectedEvent));

            // 关闭 Popup
            if (_bindingPopup != null)
            {
                _bindingPopup.IsOpen = false;
            }

            RaiseEvent(new RoutedEventArgs(BindingStateChangedEvent));

            VisionLogger.Instance.Log(LogLevel.Info,
                $"[ParamBinding] 全局变量绑定: {variableName}, BindingSource={BindingSource}",
                "ParamBinding");
        }

        #region 基类方法实现

        /// <summary>
        /// 获取可用的数据源（从 AvailableDataSources 属性）
        /// </summary>
        /// <returns>数据源集合</returns>
        public override List<AvailableDataSource> GetAvailableDataSources()
        {
            return AvailableDataSources?.ToList() ?? new List<AvailableDataSource>();
        }

        #endregion
    }
}
