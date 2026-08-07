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
                new FrameworkPropertyMetadata(false, OnIsBoundChanged));

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
                new FrameworkPropertyMetadata(string.Empty, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

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

        public static readonly DependencyProperty IconSymbolProperty =
            DependencyProperty.Register(
                nameof(IconSymbol),
                typeof(string),
                typeof(ParamBinding),
                new PropertyMetadata("🔗"));

        public static readonly DependencyProperty CornerRadiusProperty =
            DependencyProperty.Register(
                nameof(CornerRadius),
                typeof(CornerRadius),
                typeof(ParamBinding),
                new PropertyMetadata(new CornerRadius(4)));

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
        /// 图标符号
        /// </summary>
        public string IconSymbol
        {
            get => (string)GetValue(IconSymbolProperty);
            set => SetValue(IconSymbolProperty, value);
        }

        /// <summary>
        /// 圆角半径
        /// </summary>
        public CornerRadius CornerRadius
        {
            get => (CornerRadius)GetValue(CornerRadiusProperty);
            set => SetValue(CornerRadiusProperty, value);
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
            IconSymbol = "🔗"; // 固定图标，不随绑定状态切换
        }

        #endregion

        #region 回调方法

        private static void OnIsBoundChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            // 图标固定为 🔗，不随绑定状态切换
        }

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
                    BindingDisplayText = string.Empty;
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
                    BindingDisplayText = string.Empty;
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

            BindingDisplayText = $"{rootName}.{leafName}";

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
        /// 显示文本变更事件处理 — 同步数组索引到 BindingSource
        /// </summary>
        /// <remarks>
        /// 用户可能将 "直方图数据[]" 编辑为 "直方图数据[0]"
        /// 需要同步索引到 BindingSource 中的属性路径
        /// </remarks>
        private void OnBindingDisplayTextChanged(object sender, TextChangedEventArgs e)
        {
            if (_bindingDisplay == null || string.IsNullOrEmpty(BindingSource))
                return;

            var displayText = _bindingDisplay.Text;

            // 从显示文本提取索引
            var bracketStart = displayText.LastIndexOf('[');
            var bracketEnd = displayText.LastIndexOf(']');

            if (bracketStart < 0 || bracketEnd <= bracketStart)
                return;  // 无括号，不需要同步

            var indexStr = displayText.Substring(bracketStart + 1, bracketEnd - bracketStart - 1);

            // 从当前 BindingSource 提取节点ID和属性名。
            // P6：用 IndexOf('.') 只切第一个点，属性路径含点（[0].Geometry.X）时不被截断。
            var dotIndex = BindingSource.IndexOf('.');
            if (dotIndex < 0)
                return;
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
                return;  // 无效内容 → 不更新

            var typePart = typeFilter != null ? $"[Type={typeFilter}]" : "";
            BindingSource = $"{nodeId}.{propertyName}{typePart}{indexPart}";
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
            BindingDisplayText = $"全局变量.{variableName}";
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
