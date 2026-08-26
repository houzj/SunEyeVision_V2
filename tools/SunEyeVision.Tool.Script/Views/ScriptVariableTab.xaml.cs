using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Tool.Script.Models;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.VisionAdapters;

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
        /// 监视树的根节点集合（SSOT）。
        /// 外部可通过此属性直接增删改监视节点，UI 自动刷新（ItemsSource 绑定到本集合）。
        /// </summary>
        public ObservableCollection<MonitorNode> MonitorNodes { get; } = new();

        /// <summary>
        /// 类型下拉选项，统一收口到 SDK 层的 SdkTypeVocabulary（与全局变量管理器、子程序参数对话框一致）。
        public List<TypeOption> AvailableTypes { get; } = SdkTypeVocabulary.GetAllTypeOptions();

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

        /// <summary>
        /// 变量监视值的显示颜色（主题感知）：有值且暗色主题 → 浅灰（黑字在暗底不可见），
        /// 有值且浅色主题 → 黑；无值 → 灰（两主题对比度均可）。
        /// 避免固定 Brushes.Black 在暗色主题下值不可见。
        /// </summary>
        /// <param name="hasValue">该节点是否有实际值</param>
        /// <returns>适配当前主题的显示刷</returns>
        private static Brush GetValueBrush(bool hasValue)
        {
            if (!hasValue)
                return Brushes.Gray;
            return IsDarkTheme() ? Brushes.LightGray : Brushes.Black;
        }

        /// <summary>
        /// 判断当前是否暗色主题：扫描应用 MergedDictionaries 是否含 DarkTheme 字典（按 URI 识别）。
        /// 与 CommonControl.ThemeResource.IsDarkTheme 同逻辑（该 internal 类工具层不可访问），
        /// 本地实现避免跨程序集引用。纯资源扫描，不依赖具体程序集。
        /// </summary>
        /// <returns>true = 当前暗色主题</returns>
        private static bool IsDarkTheme()
        {
            var res = System.Windows.Application.Current?.Resources;
            if (res == null)
                return false;
            foreach (var dict in res.MergedDictionaries)
            {
                if (dict.Source != null &&
                    dict.Source.OriginalString.Contains(
                        "Themes/Dark/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        /// <summary>
        /// 声明式数据源依赖属性。外部设置此属性后，内部自动同步所有监视节点的值。
        /// 数据驱动替代之前的过程式 SetMonitorValue 逐项调用。
        /// </summary>
        public static readonly DependencyProperty ValueSourceProperty =
            DependencyProperty.Register(nameof(ValueSource), typeof(IDictionary<string, object?>),
                typeof(ScriptVariableTab), new PropertyMetadata(null, OnValueSourceChanged));

        /// <summary>
        /// 数据源字典（变量名 → 值）。设置后自动触发 UI 同步。
        /// </summary>
        public IDictionary<string, object?>? ValueSource
        {
            get => (IDictionary<string, object?>?)GetValue(ValueSourceProperty);
            set => SetValue(ValueSourceProperty, value);
        }

        private static void OnValueSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var tab = (ScriptVariableTab)d;
            tab.SyncValuesFromSource();
        }

        /// <summary>
        /// 强制从 ValueSource 重新同步（即使 ValueSource 引用未变）。
        /// 用于工作流执行完成后，字典内容原地更新但引用相同的场景。
        /// </summary>
        public void RefreshFromSource()
        {
            SunEyeVision.Plugin.SDK.Logging.PluginLogger.Info($"[ScriptVariableTab] RefreshFromSource 被调用, _isInput={_isInput}", nameof(ScriptVariableTab));
            SyncValuesFromSource();
        }

        /// <summary>
        /// 从 ValueSource 数据源同步到 MonitorNodes（保留根节点结构，只更新子节点树）。
        /// 数据驱动核心：外部只需设置 ValueSource，DP 变更时自动触发此方法。
        /// </summary>
        private void SyncValuesFromSource()
        {
            var source = ValueSource;
            SunEyeVision.Plugin.SDK.Logging.PluginLogger.Info($"[ScriptVariableTab] SyncValuesFromSource: _isInput={_isInput}, source={(source == null ? "null" : $"Count={source.Count}")}, MonitorNodes.Count={MonitorNodes.Count}", nameof(ScriptVariableTab));

            if (source == null)
            {
                // 数据源清空 → 重置所有节点为"无结果"
                foreach (var node in MonitorNodes)
                    node.ClearValuesRecursive();
                return;
            }

            foreach (var node in MonitorNodes)
            {
                SunEyeVision.Plugin.SDK.Logging.PluginLogger.Info($"[ScriptVariableTab]   node='{node.Name}', source.TryGetValue={source.TryGetValue(node.Name, out _)}", nameof(ScriptVariableTab));

                if (!source.TryGetValue(node.Name, out var value))
                {
                    // 没有匹配 → 重置为"无结果"
                    node.ClearValuesRecursive();
                    continue;
                }

                node.ValueBrush = GetValueBrush(value != null);
                node.ClearChildren();

                if (value != null)
                {
                    BuildChildren(value, node);
                }
                else
                {
                    // value=null：创建"无结果"占位子节点（保持树形结构一致）
                    node.AddChild(new MonitorNode
                    {
                        Name = "",
                        DisplayValue = "无结果",
                        ValueBrush = Brushes.Gray,
                    });
                }

                if (!node.HasChildren)
                {
                    node.AddChild(new MonitorNode
                    {
                        Name = "",
                        DisplayValue = value?.ToString() ?? "(null)",
                        ValueBrush = node.ValueBrush
                    });
                }
                node.DisplayValue = "";
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
            foreach (var node in MonitorNodes)
            {
                if (node.Name == name)
                {
                    node.ValueBrush = GetValueBrush(hasResult);
                    node.ClearChildren();

                    if (rawValue != null)
                    {
                        BuildChildren(rawValue, node);
                    }
                    else
                    {
                        // rawValue=null：创建"无结果"占位子节点
                        node.AddChild(new MonitorNode
                        {
                            Name = "",
                            DisplayValue = "无结果",
                            ValueBrush = Brushes.Gray,
                        });
                    }

                    // 标量（无子节点）→ 加一个值子节点，使根节点总是可折叠
                    if (!node.HasChildren)
                    {
                        var vc = new MonitorNode
                        {
                            Name = "",                                              // 子节点不显示 Name（值就是值，没有"名字"）
                            DisplayValue = rawValue?.ToString() ?? "(null)",         // 值显示在 DisplayValue（XAML 模板的叶子节点位置）
                            ValueBrush = GetValueBrush(hasResult)
                        };
                        node.AddChild(vc);
                    }
                    node.DisplayValue = "";
                    break;
                }
            }
        }

        /// <summary>
        /// 清除所有节点（含子节点）的监视值，恢复为初始状态
        /// </summary>
        public void ClearAllValues()
        {
            foreach (var node in MonitorNodes)
                node.ClearValuesRecursive();
        }

        /// <summary>
        /// 将对象值拆解为子节点树
        /// 优先级：IMonitorable → WPF 内置结构(Point/Size/Rect/Vector) → 数组/集合 → 标量 ToString
        /// </summary>
        private static void BuildChildren(object? value, MonitorNode parent)
        {
            // 1) IMonitorable 协议：业务对象结构化展开
            if (value is IMonitorable monitorable)
            {
                var fields = monitorable.GetMonitorFields();
                foreach (var (fieldName, fieldValue) in fields)
                {
                    var child = new MonitorNode
                    {
                        Name = fieldName,
                        ValueBrush = GetValueBrush(fieldValue != null),
                    };
                    SetChildValue(child, fieldValue);
                    parent.AddChild(child);
                }
                return;
            }

            // 2) WPF 内置几何结构：点/尺寸/矩形/向量显式展开（无 IMonitorable）
            switch (value)
            {
                case System.Windows.Point p:
                    AddSimpleChild(parent, "X", p.X);
                    AddSimpleChild(parent, "Y", p.Y);
                    return;
                case System.Windows.Size sz:
                    AddSimpleChild(parent, "宽", sz.Width);
                    AddSimpleChild(parent, "高", sz.Height);
                    return;
                case System.Windows.Rect r:
                    AddSimpleChild(parent, "X", r.X);
                    AddSimpleChild(parent, "Y", r.Y);
                    AddSimpleChild(parent, "宽", r.Width);
                    AddSimpleChild(parent, "高", r.Height);
                    return;
                case System.Windows.Vector v:
                    AddSimpleChild(parent, "X", v.X);
                    AddSimpleChild(parent, "Y", v.Y);
                    return;
            }

            // 3) OpenCvSharp 内置几何结构
            switch (value)
            {
                case OpenCvSharp.Point op:
                    AddSimpleChild(parent, "X", op.X);
                    AddSimpleChild(parent, "Y", op.Y);
                    return;
                case OpenCvSharp.Size os:
                    AddSimpleChild(parent, "宽", os.Width);
                    AddSimpleChild(parent, "高", os.Height);
                    return;
                case OpenCvSharp.Rect orc:
                    AddSimpleChild(parent, "X", orc.X);
                    AddSimpleChild(parent, "Y", orc.Y);
                    AddSimpleChild(parent, "宽", orc.Width);
                    AddSimpleChild(parent, "高", orc.Height);
                    return;
            }

            // 4) 数组：按 [0]/[1]/... 拆解
            if (value is Array arr)
            {
                for (int i = 0; i < arr.Length; i++)
                {
                    var child = new MonitorNode { Name = $"[{i}]" };
                    SetChildValue(child, arr.GetValue(i));
                    parent.AddChild(child);
                }
                return;
            }

            // 5) 集合（非 string）
            if (value is System.Collections.IEnumerable enumerable)
            {
                int i = 0;
                foreach (var item in enumerable)
                {
                    var child = new MonitorNode { Name = $"[{i}]" };
                    SetChildValue(child, item);
                    parent.AddChild(child);
                    i++;
                }
                return;
            }
        }

        /// <summary>
        /// 添加单值子节点（叶节点）
        /// </summary>
        private static void AddSimpleChild(MonitorNode parent, string name, object value)
        {
            var child = new MonitorNode
            {
                Name = name,
                DisplayValue = FormatScalar(value),
                ValueBrush = GetValueBrush(true),
            };
            parent.AddChild(child);
        }

        /// <summary>
        /// 标量值格式化：double/float 用 F3，其他类型用 ToString
        /// </summary>
        private static string FormatScalar(object value)
        {
            return value switch
            {
                double d => d.ToString("F3", CultureInfo.InvariantCulture),
                float f => f.ToString("F3", CultureInfo.InvariantCulture),
                _ => value.ToString() ?? "(null)"
            };
        }

        /// <summary>
        /// 设置子节点的值（递归展开：容器拆分子节点，叶子直接显示字符串）
        /// </summary>
        private static void SetChildValue(MonitorNode node, object? value)
        {
            if (value == null)
            {
                node.DisplayValue = "无结果";
                node.ValueBrush = Brushes.Gray;
                return;
            }

            // 1) IMonitorable 协议
            if (value is IMonitorable monitorable)
            {
                var fields = monitorable.GetMonitorFields();
                if (fields.Count == 0)
                {
                    node.DisplayValue = value.ToString() ?? "(null)";
                    return;
                }
                node.DisplayValue = "";
                foreach (var (fieldName, fieldValue) in fields)
                {
                    var child = new MonitorNode
                    {
                        Name = fieldName,
                        ValueBrush = GetValueBrush(fieldValue != null),
                    };
                    SetChildValue(child, fieldValue);
                    node.AddChild(child);
                }
                return;
            }

            // 2) WPF / OpenCvSharp 内置结构 → 直接展开为子节点（与 BuildChildren 复用）
            if (value is System.Windows.Point or System.Windows.Size or System.Windows.Rect or System.Windows.Vector
                || value is OpenCvSharp.Point or OpenCvSharp.Size or OpenCvSharp.Rect)
            {
                node.DisplayValue = "";
                BuildChildren(value, node);
                return;
            }

            // 3) 数组 / 集合：递归展开
            if (value is Array arr)
            {
                if (arr.Length == 0)
                {
                    node.DisplayValue = "(空数组)";
                    return;
                }
                node.DisplayValue = "";
                BuildChildren(value, node);
                return;
            }

            if (value is System.Collections.IEnumerable enumerable)
            {
                bool hasItems = false;
                foreach (var _ in enumerable) { hasItems = true; break; }
                if (!hasItems)
                {
                    node.DisplayValue = "(空集合)";
                    return;
                }
                node.DisplayValue = "";
                BuildChildren(value, node);
                return;
            }

            // 4) 标量：直接显示
            node.DisplayValue = FormatScalar(value);
        }

        /// <summary>
        /// 重建监视树 — 填充 MonitorNodes（SSOT），UI 自动刷新。
        /// 重建后自动从 ValueSource 同步值（数据驱动）。
        /// </summary>
        private void RebuildMonitor()
        {
            MonitorNodes.Clear();
            if (_sourceItems != null)
            {
                foreach (var item in _sourceItems)
                {
                    var name = GetPropertyValue<string>(item, "Name") ?? "?";
                    var typeName = GetPropertyValue<string>(item, "TypeName") ?? "?";
                    // 提取绑定源信息（仅输入变量有），如果没有则留空
                    var bindingDisplayText = GetPropertyValue<string>(item, "BindingDisplayText") ?? string.Empty;
                    MonitorNodes.Add(new MonitorNode
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
            MonitorItems.ItemsSource = MonitorNodes;

            // 数据驱动：如果已有 ValueSource，自动同步值
            SyncValuesFromSource();
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

            var selectedType = TypeCombo.SelectedValue as string ?? "Integer";
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
