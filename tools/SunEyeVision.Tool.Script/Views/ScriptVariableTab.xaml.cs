using System;
using System.Collections;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Tool.Script.Views
{
    /// <summary>
    /// ScriptVariableTab — 输入/输出变量配置的监视/编辑双模式控件。
    /// 3 行布局：标题栏（含✏编辑） / DataGrid（自适应高度） / 底部添加栏。
    /// 输入设置：变量名 | 类型 | 订阅关系(ParamBinding) | 操作(↑↓✕)
    /// 输出设置：变量名 | 类型 | 订阅关系 | 操作(↑↓)
    /// 输出设置无 ✕ 删除按钮，无订阅关系列。
    /// </summary>
    public partial class ScriptVariableTab : UserControl
    {
        private IList? _sourceItems;
        private bool _isInput;
        private bool _isEditMode;
        private ObservableCollection<AvailableDataSource>? _availableSources;

        /// <summary>
        /// 供 ComboBox 显示的字符串列表
        public string[] AvailableTypes { get; } =
        [
            "Image", "Integer", "Integer[]", "Double", "Double[]", "Float", "Float[]", "String", "String[]", "Boolean", "Byte",
            "Point", "Rectangle", "Size",
            "Region", "Circle", "Line", "Annulus", "RotatedRect", "Polygon"
        ];

        /// <summary>
        /// 可供 ParamBinding 选择的数据源
        /// </summary>
        public ObservableCollection<AvailableDataSource>? AvailableSources
        {
            get => _availableSources;
            set
            {
                _availableSources = value;
                // 刷新 ParamBinding 列
                if (_isInput && EditGrid?.Columns.Count > 2)
                    EditGrid.Columns[2].Visibility = value != null ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        public ScriptVariableTab()
        {
            InitializeComponent();
            PopulateTypeCombo();
        }

        private void PopulateTypeCombo()
        {
            TypeCombo.ItemsSource = AvailableTypes;
            TypeCombo.SelectedIndex = 0;
        }

        /// <summary>
        /// 设置数据源（简化版，保留默认类型列表）
        /// </summary>
        public void SetAvailableSources(ObservableCollection<AvailableDataSource>? sources)
        {
            AvailableSources = sources;
        }

        /// <summary>
        /// 初始化绑定到数据源
        /// </summary>
        public void Initialize(IList sourceItems, bool isInput)
        {
            _sourceItems = sourceItems;
            _isInput = isInput;

            // 设置标题
            TitleText.Text = isInput ? "输入变量" : "输出变量";

            // 订阅关系列仅输入可见
            BindingColumn.Visibility = isInput ? Visibility.Visible : Visibility.Collapsed;

            // 输出 Tab 隐藏删除按钮（通过 UpdateDeleteButtonVisibility 管理）


            if (_sourceItems is INotifyCollectionChanged incc)
                incc.CollectionChanged += OnSourceCollectionChanged;

            RebuildMonitor();
            // 默认监视模式
            _isEditMode = false;
            UpdateModeVisibility();
        }

        /// <summary>
        /// 切换到编辑模式
        /// </summary>
        public void ShowEditMode()
        {
            _isEditMode = true;
            UpdateModeVisibility();
            EditGrid.ItemsSource = _sourceItems;
            Dispatcher.BeginInvoke(new Action(UpdateDeleteButtonVisibility));
        }

        /// <summary>
        /// 强制提交 DataGrid 中任何挂起的编辑事务（Cell → Row 两级提交）。
        /// 不清除选中项，不改变当前编辑状态，仅确保 EditItem 事务结束。
        /// 避免在切换模式/重新绑定时触发 "DeferRefresh is not allowed during AddNew or EditItem transaction" 异常。
        /// </summary>
        private void CommitDataGridEdit()
        {
            if (!_isEditMode) return;

            // 先提交 Cell 级别编辑（当前激活的单元格）
            EditGrid.CommitEdit(DataGridEditingUnit.Cell, true);
            // 再提交 Row 级别编辑（确保整行的 EditItem 事务结束）
            EditGrid.CommitEdit(DataGridEditingUnit.Row, true);
            // 强制取消任何残留的编辑事务（安全调用，无活跃编辑时无副作用）
            EditGrid.CancelEdit();
        }

        /// <summary>
        /// 切换到监视模式
        /// </summary>
        public void ShowMonitorMode()
        {
            CommitDataGridEdit();  // ★ 先提交 DataGrid 挂起编辑，再切换模式
            _isEditMode = false;
            UpdateModeVisibility();
            RebuildMonitor();
        }

        /// <summary>
        /// 更新监视/编辑可见性
        /// </summary>
        private void UpdateModeVisibility()
        {
            MonitorItems.Visibility = _isEditMode ? Visibility.Collapsed : Visibility.Visible;
            EditView.Visibility = _isEditMode ? Visibility.Visible : Visibility.Collapsed;
            AddBar.Visibility = _isEditMode ? Visibility.Visible : Visibility.Collapsed;

            if (_isEditMode)
            {
                ToggleEditButton.Content = "完成";
                ToggleEditButton.Background = new SolidColorBrush(Color.FromRgb(0x10, 0x7C, 0x10));
            }
            else
            {
                ToggleEditButton.Content = "✏ 编辑";
                ToggleEditButton.Background = new SolidColorBrush(Color.FromRgb(0x00, 0x7A, 0xCC));
            }
        }

        /// <summary>
        /// 更新指定变量的监视值。
        /// 统一树形结构：标量也包装成单子节点（让根节点可折叠），值在子节点上显示。
        /// </summary>
        public void SetMonitorValue(string name, object? rawValue, bool hasResult)
        {
            foreach (var item in MonitorItems.Items)
            {
                if (item is MonitorNode node && node.Name == name)
                {
                    node.ValueBrush = hasResult ? Brushes.Black : Brushes.Gray;
                    node.ClearChildren();

                    if (rawValue != null)
                        BuildChildren(rawValue, node);

                    // 标量（无子节点）→ 加一个值子节点，使根节点总是可折叠
                    if (!node.HasChildren)
                    {
                        var vc = new MonitorNode
                        {
                            Name = "",                                              // 子节点不显示 Name（值就是值，没有"名字"）
                            DisplayValue = rawValue?.ToString() ?? "(null)",         // 值显示在 DisplayValue（XAML 模板的叶子节点位置）
                            ValueBrush = hasResult ? Brushes.Black : Brushes.Gray
                        };
                        node.AddChild(vc);
                    }
                    node.DisplayValue = "";
                    node.IsExpanded = true;
                    break;
                }
            }
        }

        /// <summary>
        /// 清除所有节点（含子节点）的监视值，恢复为初始状态
        /// </summary>
        public void ClearAllValues()
        {
            foreach (var item in MonitorItems.Items)
            {
                if (item is MonitorNode node)
                    node.ClearValuesRecursive();
            }
        }

        /// <summary>
        /// 将数组/集合值拆解为子节点树
        /// </summary>
        private static void BuildChildren(object? value, MonitorNode parent)
        {
            if (value is Array arr)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    var child = new MonitorNode { Name = $"[{i}]" };
                    SetChildValue(child, arr.GetValue(i));
                    parent.AddChild(child);
                }
            }
            else if (value is System.Collections.IEnumerable enumerable and not string)
            {
                int i = 0;
                foreach (var item in enumerable)
                {
                    var child = new MonitorNode { Name = $"[{i}]" };
                    SetChildValue(child, item);
                    parent.AddChild(child);
                    i++;
                }
            }
        }

        /// <summary>
        /// 设置子节点的值（叶子节点直接赋值，容器节点递归展开）
        /// </summary>
        private static void SetChildValue(MonitorNode node, object? value)
        {
            if (value is Array arr && arr.Length > 0)
            {
                node.DisplayValue = "";
                BuildChildren(value, node);
            }
            else if (value is System.Collections.IEnumerable enumerable and not string)
            {
                bool hasItems = false;
                foreach (var _ in enumerable) { hasItems = true; break; }
                if (hasItems)
                {
                    node.DisplayValue = "";
                    BuildChildren(value, node);
                }
                else
                {
                    node.DisplayValue = value?.ToString() ?? "(null)";
                }
            }
            else
            {
                node.DisplayValue = value?.ToString() ?? "(null)";
            }
        }

        /// <summary>
        /// 重建监视树
        /// </summary>
        private void RebuildMonitor()
        {
            var nodes = new ObservableCollection<MonitorNode>();
            if (_sourceItems != null)
            {
                foreach (var item in _sourceItems)
                {
                    var name = GetPropertyValue<string>(item, "Name") ?? "?";
                    var typeName = GetPropertyValue<string>(item, "TypeName") ?? "?";
                    // 提取绑定源信息（仅输入变量有），如果没有则留空
                    var bindingDisplayText = GetPropertyValue<string>(item, "BindingDisplayText") ?? string.Empty;
                    nodes.Add(new MonitorNode
                    {
                        Name = name,
                        TypeDisplay = $"({typeName})",
                        DisplayValue = "无结果",
                        ValueBrush = Brushes.Gray,
                        BindingDisplayText = bindingDisplayText,
                        HasBinding = !string.IsNullOrEmpty(bindingDisplayText)
                    });
                }
            }
            MonitorItems.ItemsSource = nodes;
        }

        /// <summary>
        /// 源集合变化时重建监视树
        /// </summary>
        private void OnSourceCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (!_isEditMode)
                RebuildMonitor();
        }

        // ===== 模式切换 =====

        /// <summary>
        /// ✏编辑/完成 按钮点击
        /// </summary>
        private void ToggleEditMode_Click(object sender, RoutedEventArgs e)
        {
            if (_isEditMode)
            {
                ShowMonitorMode();
                // 通知父控件退出编辑模式（重建 workspace）
                var parent = FindParent<ScriptDebugControl>(this);
                parent?.ExitEditMode();
            }
            else
            {
                ShowEditMode();
            }
        }

        // ===== 编辑模式操作 =====

        /// <summary>
        /// 新增变量（使用 TypeCombo 当前选中的类型）
        /// </summary>
        private void AddVariable_Click(object sender, RoutedEventArgs e)
        {
            CommitDataGridEdit();  // ★ 先提交 DataGrid 挂起编辑，再操作集合
            var list = _sourceItems;
            if (list == null) return;

            var selectedType = TypeCombo.SelectedItem as string ?? "Integer";
            int index = list.Count + 1;

            if (_isInput)
            {
                var newItem = new ScriptInputConfig
                {
                    Name = $"input{index}",
                    TypeName = selectedType
                };
                list.Add(newItem);
                RebuildMonitor();
            }
            else
            {
                var newItem = new ScriptOutputConfig
                {
                    Name = $"output{index}",
                    TypeName = selectedType
                };
                list.Add(newItem);
                RebuildMonitor();
            }

            // 刷新 DataGrid 数据源
            EditGrid.ItemsSource = null;
            EditGrid.ItemsSource = _sourceItems;
            Dispatcher.BeginInvoke(new Action(UpdateDeleteButtonVisibility));
        }

        /// <summary>
        /// 删除变量（至少保留 1 个）
        /// </summary>
        private void DeleteItem_Click(object sender, RoutedEventArgs e)
        {
            CommitDataGridEdit();  // ★ 先提交 DataGrid 挂起编辑，再操作集合
            if (_sourceItems == null || _sourceItems.Count <= 1) return;

            var item = (sender as FrameworkElement)?.DataContext;
            if (item == null) return;

            _sourceItems.Remove(item);
            EditGrid.ItemsSource = null;
            EditGrid.ItemsSource = _sourceItems;
            Dispatcher.BeginInvoke(new Action(UpdateDeleteButtonVisibility));
            RebuildMonitor();
        }

        /// <summary>
        /// 上移一项
        /// </summary>
        private void MoveUp_Click(object sender, RoutedEventArgs e)
        {
            CommitDataGridEdit();  // ★ 先提交 DataGrid 挂起编辑，再操作集合
            var item = (sender as FrameworkElement)?.DataContext;
            if (item == null || _sourceItems == null) return;

            int index = _sourceItems.IndexOf(item);
            if (index <= 0) return;

            if (_sourceItems is IList list && !list.IsFixedSize)
            {
                list.RemoveAt(index);
                list.Insert(index - 1, item);
                EditGrid.ItemsSource = null;
                EditGrid.ItemsSource = _sourceItems;
                EditGrid.SelectedItem = item;
                RebuildMonitor();
            }
        }

        /// <summary>
        /// 下移一项
        /// </summary>
        private void MoveDown_Click(object sender, RoutedEventArgs e)
        {
            CommitDataGridEdit();  // ★ 先提交 DataGrid 挂起编辑，再操作集合
            var item = (sender as FrameworkElement)?.DataContext;
            if (item == null || _sourceItems == null) return;

            int index = _sourceItems.IndexOf(item);
            if (index < 0 || index >= _sourceItems.Count - 1) return;

            if (_sourceItems is IList list && !list.IsFixedSize)
            {
                list.RemoveAt(index);
                list.Insert(index + 1, item);
                EditGrid.ItemsSource = null;
                EditGrid.ItemsSource = _sourceItems;
                EditGrid.SelectedItem = item;
                RebuildMonitor();
            }
        }

        /// <summary>
        /// 更新删除按钮可见性（≥2 项显示，1 项隐藏）
        /// </summary>
        private void UpdateDeleteButtonVisibility()
        {
            int count = _sourceItems?.Count ?? 0;
            bool showDelete = count > 1;

            foreach (var item in EditGrid.Items)
            {
                var row = EditGrid.ItemContainerGenerator.ContainerFromItem(item) as DataGridRow;
                if (row == null) continue;

                var deleteBtn = FindChild<Button>(row, "DeleteButton");
                if (deleteBtn != null)
                    deleteBtn.Visibility = showDelete ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        // ===== Helper Methods =====

        private static T? GetPropertyValue<T>(object obj, string propertyName)
        {
            var prop = obj.GetType().GetProperty(propertyName);
            return prop != null ? (T?)prop.GetValue(obj) : default;
        }

        private static T? FindChild<T>(DependencyObject parent, string childName)
            where T : DependencyObject
        {
            if (parent == null) return null;

            int count = VisualTreeHelper.GetChildrenCount(parent);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);
                if (child is T typedChild)
                {
                    if (string.IsNullOrEmpty(childName) ||
                        (child is FrameworkElement fe && fe.Name == childName))
                        return typedChild;
                }

                var result = FindChild<T>(child, childName);
                if (result != null) return result;
            }
            return null;
        }

        /// <summary>
        /// 在可视化树中查找指定类型的父元素
        /// </summary>
        private static T? FindParent<T>(DependencyObject child) where T : DependencyObject
        {
            DependencyObject parent = VisualTreeHelper.GetParent(child);
            while (parent != null)
            {
                if (parent is T typedParent)
                    return typedParent;
                parent = VisualTreeHelper.GetParent(parent);
            }
            return null;
        }

        /// <summary>
        /// 清理资源
        /// </summary>
        public void Cleanup()
        {
            if (_sourceItems is INotifyCollectionChanged incc)
                incc.CollectionChanged -= OnSourceCollectionChanged;
        }
    }
}
