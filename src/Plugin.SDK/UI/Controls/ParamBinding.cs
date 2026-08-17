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
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;

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
            if (d is ParamBinding control)
            {
                if (e.NewValue is System.Collections.ObjectModel.ObservableCollection<AvailableDataSource> newDataSources)
                {
                    control._currentDataSources = newDataSources;
                    control.RebuildTreeNodes();
                }
                else
                {
                    control._currentDataSources = null;
                    control.TreeNodes = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>();
                }
            }
        }

        /// <summary>
        /// BindingSource 变更回调（契约 C2，单轨模式）：用绑定路径反查中文显示名回填显示位。
        /// 程序回填显示名时抑制编辑回写，避免误覆盖绑定源（A2/A3 治本）。
        /// </summary>
        private static void OnBindingSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is not ParamBinding control || !control.SyncEditingToSource)
                return;

            var bindingPath = e.NewValue as string;
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
        }

        /// <summary>
        /// DataType 变更时重新构建树形结构
        /// </summary>
        protected override void OnDataTypeChangedCore(DependencyPropertyChangedEventArgs e)
        {
            RebuildTreeNodes();
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

                // 使用基类的静态方法构建树形结构
                var treeNodes = ParamBindingBase.BuildTreeStructure(filteredDataSources);
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

            // 用完整绑定路径（含 [Type=]/[i] 后缀）在树中精确匹配合成 DataSource
            foreach (var rootNode in TreeNodes)
            {
                var node = FindNodeRecursive(rootNode, bindingPath);
                if (node != null)
                    return node;
            }

            return null;
        }

        /// <summary>
        /// 递归查找节点（按 DataSource 完整 GetBindingPath 匹配，含 [Type=]/[i] 后缀）
        /// </summary>
        private TreeNodeData? FindNodeRecursive(TreeNodeData node, string fullBindingPath)
        {
            if (node.DataSource != null && node.DataSource.GetBindingPath() == fullBindingPath)
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
        /// 根据绑定源路径生成绑定显示文本
        /// </summary>
        /// <param name="bindingPath">绑定源路径（格式：节点ID.属性名，属性名可含 [N] 索引）</param>
        /// <returns>绑定显示文本（格式：根节点.叶子节点），未找到则返回绑定路径</returns>
        public string GenerateBindingDisplayText(string bindingPath)
        {
            var node = FindNodeByBindingPath(bindingPath);
            if (node == null)
            {
                VisionLogger.Instance.Log(LogLevel.Warning,
                    $"[ParamBinding] 未找到绑定路径对应的节点: {bindingPath}",
                    "ParamBinding");
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

            // 如果有具体索引（>=0），替换掉默认的 []；-1 表示显式 "[]" 保留整个数组
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
        /// 获取叶子节点的显示名称（区分数组类型）
        /// </summary>
        /// <param name="node">叶子节点</param>
        /// <returns>显示名称，数组类型追加 [] 后缀</returns>
        private static string GetDisplayLeafName(TreeNodeData node)
        {
            var ds = node.DataSource;
            if (ds == null)
                return node.Text;
            if (ds.BindingSuffix != null)
            {
                // P2：RoI 类型分组（[Type=Circle]）显示名统一为"短名[]"（圆[]），与普通数组"宽度[]"同风格。
                // 单元素（[i]）保持 "属性名[i]"（检测区域[0]），整体（BindingSuffix=null）走下方数组分支。
                if (ds.BindingSuffix.StartsWith("[Type=", StringComparison.OrdinalIgnoreCase))
                    return $"{node.Text}[]";
                return $"{ds.PropertyName}{TranslateBindingSuffix(ds.BindingSuffix)}";
            }
            if (ds.PropertyType?.IsArray == true)
                return $"{ds.DisplayName}[]";
            return node.Text;
        }

        /// <summary>
        /// 把绑定后缀翻译为中文短名：<c>[Type=Circle]</c> → <c>[圆]</c>，<c>[0]</c> → <c>[0]</c>。
        /// </summary>
        private static string TranslateBindingSuffix(string suffix)
        {
            const string marker = "[Type=";
            if (suffix.StartsWith(marker, StringComparison.OrdinalIgnoreCase) && suffix.EndsWith("]"))
            {
                var enumName = suffix.Substring(marker.Length, suffix.Length - marker.Length - 1);
                if (Enum.TryParse<ShapeType>(enumName, out var st))
                    return $"[{ArrayTreeFormatter.GetShapeTypeShortName(st)}]";
                return suffix;
            }
            return suffix;
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

            // 构建新的 BindingSource（保留类型过滤，仅替换索引位）
            string indexPart;
            if (string.IsNullOrEmpty(indexStr))
                indexPart = "[]";  // [] 空括号 → 整个数组
            else if (int.TryParse(indexStr, out _))
                indexPart = $"[{indexStr}]";  // [0] [1] 等有效索引
            else
                return false;  // 无效内容 → 同步失败

            var typePart = typeFilter != null ? $"[Type={typeFilter}]" : "";
            BindingSource = $"{nodeId}.{propertyName}{typePart}{indexPart}";
            return true;
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
