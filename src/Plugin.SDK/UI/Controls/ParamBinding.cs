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

        #endregion

        #region 控件引用

        private Button? _bindingButton;
        private Popup? _bindingPopup;
        private TreeView? _bindingTreeView;

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

        private static void OnDataTypeChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
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
            if (_currentDataSources != null)
            {
                var dataSourceList = _currentDataSources.ToList();
                
                // 使用基类的静态方法进行类型过滤
                var filteredDataSources = ParamBindingBase.FilterDataSourcesByType(dataSourceList, DataType);
                
                // 使用基类的静态方法构建树形结构
                var treeNodes = ParamBindingBase.BuildTreeStructure(filteredDataSources);
                TreeNodes = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>(treeNodes);
            }
            else
            {
                TreeNodes = new System.Collections.ObjectModel.ObservableCollection<TreeNodeData>();
            }
        }

        #endregion

        #region 模板应用

        public override void OnApplyTemplate()
        {
            base.OnApplyTemplate();

            _bindingButton = GetTemplateChild("PART_BindingButton") as Button;
            _bindingPopup = GetTemplateChild("PART_BindingPopup") as Popup;
            _bindingTreeView = GetTemplateChild("PART_BindingTreeView") as TreeView;

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

            if (treeViewItem != null && treeViewItem.DataContext is TreeNodeData selectedNode &&
                selectedNode.IsSelectable && selectedNode.DataSource != null)
            {
                // 设置绑定源路径（格式：节点ID.属性名）
                BindingSource = selectedNode.DataSource.GetBindingPath();

                // 设置友好显示名称：根据树形结构动态生成
                var rootNode = GetRootNode(selectedNode);
                var rootName = rootNode?.Text ?? "";
                var leafName = GetDisplayLeafName(selectedNode);

                BindingDisplayText = $"{rootName}.{leafName}";

                // 触发事件
                RaiseEvent(new RoutedEventArgs(BindingSourceSelectedEvent));
                IsBound = true;

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
            if (e.NewValue is TreeNodeData selectedNode && selectedNode.IsSelectable && selectedNode.DataSource != null)
            {
                // 设置绑定源路径（格式：节点ID.属性名）
                BindingSource = selectedNode.DataSource.GetBindingPath();

                // 设置绑定显示文本：根据树形结构动态生成
                // 格式：根节点名称.叶子节点名称
                // 例如：图像阈值化4.实际使用的阈值

                var rootNode = GetRootNode(selectedNode);
                var rootName = rootNode?.Text ?? "";
                var leafName = GetDisplayLeafName(selectedNode);

                VisionLogger.Instance.Log(LogLevel.Info,
                    $"[ParamBinding] 选中节点: {leafName}, 父节点: {selectedNode.Parent?.Text ?? "null"}, 根节点: {rootName}",
                    "ParamBinding");

                BindingDisplayText = $"{rootName}.{leafName}";

                // 触发事件
                RaiseEvent(new RoutedEventArgs(BindingSourceSelectedEvent));
                IsBound = true;

                // 关闭 Popup
                if (_bindingPopup != null)
                {
                    _bindingPopup.IsOpen = false;
                }

                RaiseEvent(new RoutedEventArgs(BindingStateChangedEvent));
            }
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
        /// <param name="bindingPath">绑定源路径（格式：节点ID.属性名）</param>
        /// <returns>对应的 TreeNodeData，未找到则返回 null</returns>
        private TreeNodeData? FindNodeByBindingPath(string bindingPath)
        {
            if (TreeNodes == null || string.IsNullOrEmpty(bindingPath))
                return null;

            var parts = bindingPath.Split('.');
            var sourceNodeId = parts[0];
            var propertyName = parts.Length > 1 ? parts[1] : string.Empty;

            // 在树中查找对应的节点
            foreach (var rootNode in TreeNodes)
            {
                var node = FindNodeRecursive(rootNode, sourceNodeId, propertyName);
                if (node != null)
                    return node;
            }

            return null;
        }

        /// <summary>
        /// 递归查找节点
        /// </summary>
        private TreeNodeData? FindNodeRecursive(TreeNodeData node, string sourceNodeId, string propertyName)
        {
            // 检查当前节点是否匹配
            if (node.DataSource != null &&
                node.DataSource.SourceNodeId == sourceNodeId &&
                node.DataSource.PropertyName == propertyName)
            {
                return node;
            }

            // 递归检查子节点
            foreach (var child in node.Children)
            {
                var found = FindNodeRecursive(child, sourceNodeId, propertyName);
                if (found != null)
                    return found;
            }

            return null;
        }

        /// <summary>
        /// 根据绑定源路径生成绑定显示文本
        /// </summary>
        /// <param name="bindingPath">绑定源路径（格式：节点ID.属性名）</param>
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
            var name = node.Text;
            if (node.DataSource?.PropertyType?.IsArray == true)
                name += "[]";
            return name;
        }

        #endregion

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
