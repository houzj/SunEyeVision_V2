using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.UI.ViewModels;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Views.Windows
{
    /// <summary>
    /// 全局变量管理器对话框
    /// </summary>
    public partial class GlobalVariableManagerDialog : Window
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
                    var result = MessageBox.Show(
                        $"分组 '{groupVM.Name}' 中有 {groupVM.Variables.Count} 个变量，\n删除分组将同时删除这些变量。\n\n是否继续？",
                        "确认删除",
                        MessageBoxButton.YesNo,
                        MessageBoxImage.Warning);

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
        /// DataGrid 单元格编辑结束 — 拦截变量重命名重名
        /// </summary>
        private void DataGrid_CellEditEnding(object sender, DataGridCellEditEndingEventArgs e)
        {
            if (e.Column.Header is string header && header == "名称")
            {
                var editingElement = e.EditingElement as TextBox;
                if (editingElement == null) return;

                var variable = e.Row.Item as GlobalVariable;
                var viewModel = DataContext as GlobalVariableManagerViewModel;
                if (variable == null || viewModel == null) return;

                var newName = editingElement.Text.Trim();

                // 名称未变 → 直接通过
                if (newName == variable.Name)
                    return;

                if (!viewModel.ConfirmVariableRename(variable, newName))
                {
                    e.Cancel = true;
                }
            }
        }
    }

    /// <summary>
    /// 布尔值反向转换器：true→Collapsed, false→Visible
    /// </summary>
    public class InverseBoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            bool boolValue = value is bool b && b;
            return boolValue ? Visibility.Collapsed : Visibility.Visible;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        {
            return value is Visibility v && v == Visibility.Collapsed;
        }
    }
}
