using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using OpenCvSharp;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models;
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

        /// <summary>
        /// 名称 TextBox 获得焦点时记录编辑前名称（Tag），用于失焦校验失败时还原
        /// </summary>
        private void VariableNameTextBox_GotKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is TextBox textBox)
                textBox.Tag = textBox.Text;
        }

        /// <summary>
        /// 名称 TextBox 失焦时提交重命名校验（空名/重名 → 弹窗并还原）
        /// 名称列改为内嵌可编辑 TextBox（单击即编辑），校验从 DataGrid CellEditEnding 迁移到此。
        /// </summary>
        private void VariableNameTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not GlobalVariable variable)
                return;
            if (DataContext is not GlobalVariableManagerViewModel viewModel)
                return;

            var newName = textBox.Text.Trim();
            var oldName = textBox.Tag as string ?? variable.Name;
            if (string.Equals(oldName, newName, StringComparison.Ordinal))
                return;

            if (!viewModel.ConfirmVariableRename(variable, newName, oldName))
            {
                // 校验失败：还原名称（绑定实时写回，需手动恢复）
                variable.Name = oldName;
                textBox.Text = oldName;
            }
            textBox.Tag = null;
        }

        /// <summary>
        /// 当前值 TextBox 失焦时按变量类型解析文本并写回
        /// 仅文本类（整型/双精度/字符串/布尔）可编辑，图像/图形/矩阵等只读占位不解析。
        /// </summary>
        private void VariableValueTextBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is not TextBox textBox || textBox.DataContext is not GlobalVariable variable)
                return;
            if (textBox.IsReadOnly)
                return;

            var newValue = GlobalVariableValueTextConverter.ParseText(textBox.Text, variable.Type);
            if (newValue == null)
            {
                ThemeMessageDialog.Show($"请输入有效的{variable.Type}值（如 128 / 3.14 / True）",
                    "提示", MessageBoxButton.OK, ThemeMessageDialog.MessageIcon.Warning);
                // 不写回：刷新绑定显示旧值
                textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateTarget();
                return;
            }
            variable.Value = newValue;
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
            Mat => "[图像]",
            IVisionImage => "[图像]",
            AffineMatrix => "[变换矩阵]",
            ShapeParameters[] => "[图形定义]",
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
                    if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
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
}
