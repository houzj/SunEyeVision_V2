using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.Dialogs;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.GlobalVariableManager
{
    /// <summary>
    /// 全局变量管理器对话框
    /// </summary>
    public partial class GlobalVariableManagerDialog : ThemeDialogWindow
    {
        private readonly System.Collections.Generic.HashSet<string> _valueEdits = new(StringComparer.Ordinal);
        private readonly System.Collections.Generic.Dictionary<string, string> _pendingNames = new(StringComparer.Ordinal);
        private readonly System.Collections.Generic.Dictionary<string, string> _pendingValues = new(StringComparer.Ordinal);
        private bool _refreshing;
        public GlobalVariableManagerDialog(GlobalVariableManagerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));

            // Tab 新增时自动滚动到最右端
            viewModel.Groups.CollectionChanged += OnGroupsCollectionChanged;

            // 监听 ScrollViewer 状态变化，动态切换两个 "+" 按钮
            GroupTabScrollViewer.Loaded += (s, e) => UpdateAddButtonPosition();
            GroupTabScrollViewer.SizeChanged += (s, e) => UpdateAddButtonPosition();
        }

        /// <summary>
        /// 分组集合变化时自动滚动 ScrollViewer 到最右端
        /// </summary>
        private void OnGroupsCollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
        {
            if (e.Action == NotifyCollectionChangedAction.Add && GroupTabScrollViewer != null)
            {
                GroupTabScrollViewer.Dispatcher.BeginInvoke(new Action(() =>
                {
                    GroupTabScrollViewer.ScrollToRightEnd();
                    UpdateAddButtonPosition();
                }));
            }
        }

        /// <summary>
        /// 根据 Tab 是否溢出，切换两个 "+" 按钮的可见性
        /// 未溢出 → 显示 ScrollViewer 内部的按钮（紧跟在 Tab 右侧）
        /// 已溢出 → 显示列1 的固定按钮（始终可见）
        /// </summary>
        private void UpdateAddButtonPosition()
        {
            bool isOverflow = GroupTabScrollViewer.ScrollableWidth > 0;

            if (isOverflow)
            {
                ScrollableAddButton.Visibility = Visibility.Collapsed;
                FixedAddButton.Visibility = Visibility.Visible;
            }
            else
            {
                ScrollableAddButton.Visibility = Visibility.Visible;
                FixedAddButton.Visibility = Visibility.Collapsed;
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        /// <summary>
        /// 分组标签选中事件
        /// </summary>
        private void GroupRadioButton_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton radioButton && radioButton.DataContext is VariableGroupViewModel groupVM)
            {
                var viewModel = DataContext as GlobalVariableManagerViewModel;
                if (viewModel != null && viewModel.SelectedGroup != groupVM)
                {
                    viewModel.SelectedGroup = groupVM;
                }

                // 将选中的 Tab 滚动到可视区域（特别用于通过下拉 Popup 切换的场景）
                var container = GroupItemsControl?.ItemContainerGenerator.ContainerFromItem(groupVM);
                if (container is FrameworkElement element)
                {
                    element.BringIntoView();
                }
            }
        }

        /// <summary>
        /// 重命名分组菜单点击 → 改为触发内联编辑（不再弹窗）
        /// </summary>
        private void RenameGroupMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.CommandParameter is VariableGroupViewModel groupVM)
            {
                groupVM.BeginEdit();
            }
        }

        /// <summary>
        /// 删除分组菜单点击
        /// </summary>
        private void DeleteGroupMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuItem && menuItem.CommandParameter is VariableGroupViewModel groupVM)
            {
                var viewModel = DataContext as GlobalVariableManagerViewModel;
                if (viewModel == null) return;

                // 先选中要删除的分组
                viewModel.SelectedGroup = groupVM;

                // 级联删除确认（允许删除非空分组）
                if (groupVM.Variables.Count > 0)
                {
                    var result = ThemeMessageDialog.Show(
                        $"分组 '{groupVM.Name}' 中有 {groupVM.Variables.Count} 个变量，\n删除分组将同时删除这些变量。\n\n是否继续？",
                        "确认删除",
                        MessageBoxButton.YesNo,
                        ThemeMessageDialog.MessageIcon.Warning);

                    if (result != MessageBoxResult.Yes)
                        return;
                }

                viewModel.DeleteGroupWithVariables();
            }
        }

        /// <summary>
        /// 编辑 TextBox 可见性变化时自动聚焦并全选，同时评估溢出状态
        /// </summary>
        private void EditTextBox_IsVisibleChanged(object sender, DependencyPropertyChangedEventArgs e)
        {
            if (sender is TextBox textBox)
            {
                if (textBox.IsVisible)
                {
                    textBox.Focus();
                    textBox.SelectAll();
                    // 编辑开始：TextBox 可能比原 RadioButton 更宽，需重新评估溢出并滚动到最右
                    textBox.Dispatcher.BeginInvoke(new Action(() =>
                    {
                        GroupTabScrollViewer.ScrollToRightEnd();
                        UpdateAddButtonPosition();
                    }));
                }
                else
                {
                    // 编辑结束：重新评估溢出状态
                    UpdateAddButtonPosition();
                }
            }
        }

        /// <summary>
        /// 编辑中文字变化时重新评估溢出状态（编辑框宽度变化可能导致溢出）
        /// </summary>
        private void EditTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.IsVisible)
            {
                textBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    UpdateAddButtonPosition();
                }));
            }
        }

        /// <summary>
        /// 编辑 TextBox 键盘事件：Enter=提交，Escape=取消
        /// </summary>
        private void EditTextBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is VariableGroupViewModel groupVM)
            {
                var viewModel = DataContext as GlobalVariableManagerViewModel;
                if (viewModel == null) return;

                if (e.Key == Key.Enter)
                {
                    viewModel.ConfirmGroupRename(groupVM);
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    groupVM.CancelEdit();
                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// 编辑 TextBox 失焦时自动提交
        /// </summary>
        private void EditTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox textBox && textBox.DataContext is VariableGroupViewModel groupVM)
            {
                var viewModel = DataContext as GlobalVariableManagerViewModel;
                viewModel?.ConfirmGroupRename(groupVM);
            }
        }

        /// <summary>
        /// 鼠标点击 ComboBox 时延迟展开下拉。
        /// 不阻止事件传播，DataGrid 自然处理行选中和单元格激活。
        /// 延迟展开确保 DataGrid 编辑模式建立后 SelectedItem 绑定已连接到正确行。
        /// 键盘 Tab 导航不会触发此方法，所以不会误展开。
        /// </summary>
        private void TypeComboBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ComboBox comboBox && !comboBox.IsDropDownOpen)
            {
                comboBox.Dispatcher.BeginInvoke(new Action(() =>
                {
                    comboBox.IsDropDownOpen = true;
                }), System.Windows.Threading.DispatcherPriority.Input);
            }
        }

        private bool TryValidateNameEditor(TextBox box, out string error)
        {
            error = "";
            if (box.DataContext is not GlobalVariable variable || DataContext is not GlobalVariableManagerViewModel vm) return true;
            var name = box.Text.Trim();
            var variables = vm.DraftDocument.GlobalVariables;
            if (name.Length == 0) error = "变量名称不能为空，请修改当前输入后再保存。";
            else if (variables != null && GlobalVariable.IsNameDuplicate(variables, name, variable.Id)) error = $"变量名称“{name}”已存在。";
            var binding = box.GetBindingExpression(TextBox.TextProperty);
            if (binding != null)
            {
                if (error.Length > 0) System.Windows.Controls.Validation.MarkInvalid(binding, new ValidationError(new ExceptionValidationRule(), binding, error, null));
                else System.Windows.Controls.Validation.ClearInvalid(binding);
            }
            return error.Length == 0;
        }
        private void VariableNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            if (sender is TextBox box && box.DataContext is GlobalVariable variable &&
                DataContext is GlobalVariableManagerViewModel vm && TryValidateNameEditor(box, out _) && box.Text.Trim() != variable.Name)
                vm.ConfirmVariableRename(variable, box.Text.Trim(), variable.Name);
        }

        /// <summary>
        /// 当前值 TextBox 失焦时按变量类型解析文本并写回
        /// 仅文本类（整型/双精度/字符串/布尔）可编辑，图像/图形/矩阵等只读占位不解析。
        /// </summary>
        private void VariableValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (_refreshing) return;
            if (sender is not TextBox textBox || textBox.DataContext is not GlobalVariable variable)
                return;
            if (textBox.IsReadOnly)
                return;

            if (!TryParseValueEditor(textBox, out var value, out _, _valueEdits.Contains(variable.Id))) return;
            if (value != null && DataContext is GlobalVariableManagerViewModel vm) vm.SetDraftValue(variable, value);

        }
        private void VariableValueTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (_refreshing || sender is not TextBox { IsLoaded: true, IsReadOnly: false } box ||
                box.DataContext is not GlobalVariable variable) return;
            var current = new GlobalVariableValueTextConverter().Convert(variable.Value!, typeof(string), null!, CultureInfo.InvariantCulture)?.ToString() ?? "";
            if (box.Text != current || _valueEdits.Contains(variable.Id))
            {
                _valueEdits.Add(variable.Id);
                _pendingValues[variable.Id] = box.Text;
            }
        }
        private void VariableNameTextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_refreshing && sender is TextBox { IsLoaded: true } box && box.DataContext is GlobalVariable variable && box.Text != variable.Name)
                _pendingNames[variable.Id] = box.Text;
        }
        private void VariableEditor_Loaded(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox box || box.DataContext is not GlobalVariable variable) return;
            var pending = box.Name == "VariableNameEditor" ? _pendingNames : _pendingValues;
            if (!pending.TryGetValue(variable.Id, out var raw)) return;
            _refreshing = true;
            try { box.SetCurrentValue(TextBox.TextProperty, raw); }
            finally { _refreshing = false; }
        }
        internal bool HasRetainedPendingEdits => _pendingNames.Count != 0 || _pendingValues.Count != 0 ||
            SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.HasPendingEdits(this);
        internal string PendingEditStamp => System.Text.Json.JsonSerializer.Serialize(new { Names = _pendingNames, Values = _pendingValues });
        internal void AcknowledgeRetainedEdits(string stamp)
        {
            using var saved = System.Text.Json.JsonDocument.Parse(stamp);
            foreach (var item in saved.RootElement.GetProperty("Names").EnumerateObject())
                if (_pendingNames.TryGetValue(item.Name, out var current) && current == item.Value.GetString()) _pendingNames.Remove(item.Name);
            foreach (var item in saved.RootElement.GetProperty("Values").EnumerateObject())
                if (_pendingValues.TryGetValue(item.Name, out var current) && current == item.Value.GetString())
                { _pendingValues.Remove(item.Name); _valueEdits.Remove(item.Name); }
        }
        internal void AcceptPublication()
        {
            _refreshing = true;
            try
            {
                _valueEdits.Clear();
                _pendingNames.Clear();
                _pendingValues.Clear();
                if (DataContext is GlobalVariableManagerViewModel vm) vm.AcceptPublication();
            }
            finally { _refreshing = false; }
        }
        private static System.Collections.Generic.IEnumerable<DependencyObject> Visuals(DependencyObject root)
        {
            yield return root;
            for (var i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Visuals(System.Windows.Media.VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        private static bool TryParseValueEditor(TextBox box, out object? parsed, out string error, bool explicitEdit = false)
        {
            parsed = null; error = "";
            if (box.IsReadOnly || box.DataContext is not GlobalVariable variable) return true;
            var binding = box.GetBindingExpression(TextBox.TextProperty);
            var currentText = new GlobalVariableValueTextConverter().Convert(variable.Value!, typeof(string), null!, CultureInfo.InvariantCulture)?.ToString() ?? "";
            if (!explicitEdit && box.Text == currentText)
            { if (binding != null) System.Windows.Controls.Validation.ClearInvalid(binding); return true; }
            parsed = GlobalVariableValueTextConverter.ParseText(box.Text, variable.Type);
            if (parsed == null)
            {
                error = $"变量“{variable.Name}”的{variable.Type}值无效，请修改当前输入后再保存。";
                if (binding != null) System.Windows.Controls.Validation.MarkInvalid(binding,
                    new ValidationError(new ExceptionValidationRule(), binding, error, null));
                return false; // Keep the typed text and existing model value; never UpdateTarget here.
            }
            if (binding != null) System.Windows.Controls.Validation.ClearInvalid(binding);
            return true;
        }
        public bool TryValidatePendingEdits(out string error)
        {
            if (DataContext is GlobalVariableManagerViewModel vm && !vm.TryPrepare(out error)) return false;
            if (!TryValidateRetainedEdits(out error)) return false;
            foreach (var box in Visuals(this).OfType<TextBox>().Where(b => b.Name == "VariableNameEditor"))
                if (!TryValidateNameEditor(box, out error)) return false;
            foreach (var box in Visuals(this).OfType<TextBox>().Where(b => b.Name == "VariableValueEditor"))
                if (!TryParseValueEditor(box, out _, out error,
                    box.DataContext is GlobalVariable v && _valueEdits.Contains(v.Id))) return false;
            return SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.TryValidate(this, out error);
        }
        private bool TryValidateRetainedEdits(out string error)
        {
            error = "";
            if (DataContext is not GlobalVariableManagerViewModel vm) return true;
            var variables = vm.DraftDocument.GlobalVariables;
            foreach (var variable in variables)
            {
                var name = _pendingNames.TryGetValue(variable.Id, out var rawName) ? rawName.Trim() : variable.Name;
                if (string.IsNullOrWhiteSpace(name)) { error = "变量名称不能为空，当前输入已保留。"; return false; }
                if (variables.Any(other => other.Id != variable.Id && string.Equals(
                    _pendingNames.TryGetValue(other.Id, out var otherName) ? otherName.Trim() : other.Name,
                    name, StringComparison.OrdinalIgnoreCase)))
                { error = $"变量名称“{name}”已存在，当前输入已保留。"; return false; }
                if (_pendingValues.TryGetValue(variable.Id, out var rawValue) &&
                    GlobalVariableValueTextConverter.ParseText(rawValue, variable.Type) is null)
                { error = $"变量“{name}”的{variable.Type}值无效，当前输入已保留。"; return false; }
            }
            return true;
        }
        public bool TryCommitPendingEdits(out string error)
        {
            if (!TryValidatePendingEdits(out error)) return false;
            var nameEditors = Visuals(this).OfType<TextBox>().Where(b => b.Name == "VariableNameEditor").Select(b => (Box: b, Text: b.Text)).ToArray();
            var valueEditors = Visuals(this).OfType<TextBox>()
                .Where(b => b.Name == "VariableValueEditor" && !b.IsReadOnly)
                .Select(b => (Box: b, Text: b.Text)).ToArray();
            // Commit normal bindings and DataGrid rows first; invalid edits are not cancelled.
            if (!SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.TryPrepare(this, out error)) return false;
            foreach (var entry in nameEditors)
            {
                entry.Box.SetCurrentValue(TextBox.TextProperty, entry.Text);
                if (!TryValidateNameEditor(entry.Box, out error)) return false;
                if (entry.Box.DataContext is GlobalVariable variable && DataContext is GlobalVariableManagerViewModel vm &&
                    entry.Text.Trim() != variable.Name && !vm.ConfirmVariableRename(variable, entry.Text.Trim(), variable.Name))
                { error = "变量重命名未完成，当前输入保留。"; return false; }
            }
            var parsed = new System.Collections.Generic.List<(GlobalVariable Variable, object Value)>();
            foreach (var entry in valueEditors)
            {
                // A DataGrid commit can refresh OneWay displays. Restore the pending text before parsing.
                entry.Box.SetCurrentValue(TextBox.TextProperty, entry.Text);
                if (!TryParseValueEditor(entry.Box, out var value, out error,
                    entry.Box.DataContext is GlobalVariable v && _valueEdits.Contains(v.Id))) return false;
                if (value != null && entry.Box.DataContext is GlobalVariable variable) parsed.Add((variable, value));
            }
            if (DataContext is GlobalVariableManagerViewModel model)
            {
                foreach (var item in parsed) model.SetDraftValue(item.Variable, item.Value);
                foreach (var variable in model.DraftDocument.GlobalVariables)
                {
                    if (_pendingNames.TryGetValue(variable.Id, out var rawName)) variable.Name = rawName.Trim();
                    if (_pendingValues.TryGetValue(variable.Id, out var rawValue))
                        model.SetDraftValue(variable, GlobalVariableValueTextConverter.ParseText(rawValue, variable.Type));
                }
            }
            error = ""; return true;
        }
    }

    /// <summary>
    /// 全局变量当前值显示转换器：把 object 值（基础类型数组/图像/图形/矩阵）格式化为可读文本。
    /// 只负责显示（Convert），回写由 VariableValueTextBox_LostFocus 按 Type 解析。
    /// </summary>
    public class GlobalVariableValueTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            if (value == null) return string.Empty;

            return value switch
            {
                long[] arr   => string.Join(", ", arr),
                double[] arr => string.Join(", ", arr.Select(v => v.ToString(CultureInfo.InvariantCulture))),
                string[] arr => string.Join(", ", arr),
                bool[] arr   => string.Join(", ", arr),
                long l       => l.ToString(CultureInfo.InvariantCulture),
                double d     => d.ToString(CultureInfo.InvariantCulture),
                string s     => s,
                bool b       => b.ToString(),
                _ => FormatComplex(value)
            };
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;

        private static string FormatComplex(object value) => value switch
        {
            SunEyeVision.Plugin.SDK.Models.Communication.BinaryPayload payload => payload.ToString(),
            SunEyeVision.Plugin.SDK.Models.Communication.BinaryPayload[] payloads =>
                string.Join(" | ", payloads.Select(p => p.ToString())),
            Mat => "[图像]",
            IVisionImage => "[图像]",
            AffineMatrix => "[变换矩阵]",
            RoiShape[] => "[图形定义]",
            Circle2d[] => "[圆]",
            RotatedRect2d[] => "[旋转矩形]",
            SunEyeVision.Plugin.SDK.Models.Geometry.Point2d[] => "[点]",
            Polygon2d[] => "[多边形]",
            Line2d[] => "[直线]",
            Annulus2d[] => "[圆环]",
            _ => value.ToString() ?? string.Empty
        };

        /// <summary>
        /// 按变量类型把文本解析回值（数组形态，对齐反序列化契约）。解析失败返回 null。
        /// </summary>
        public static object? ParseText(string? text, string typeName)
        {
            switch (typeName)
            {
                case "Integer":
                    if (long.TryParse(text, NumberStyles.Integer, CultureInfo.InvariantCulture, out var l))
                        return new[] { l };
                    break;
                case "Double":
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && double.IsFinite(d))
                        return new[] { d };
                    break;
                case "Boolean":
                    if (bool.TryParse(text, out var b))
                        return new[] { b };
                    break;
                case "String":
                    return new[] { text ?? string.Empty };
            }
            return null;
        }
    }

    /// <summary>全局变量类型词汇本地化转换器。</summary>
    public sealed class GlobalVariableTypeTextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
            => value is string type && SunEyeVision.Plugin.SDK.Models.SdkTypeVocabulary.ChineseNames.TryGetValue(type, out var name)
                ? name
                : value?.ToString() ?? string.Empty;

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => Binding.DoNothing;
    }
}
