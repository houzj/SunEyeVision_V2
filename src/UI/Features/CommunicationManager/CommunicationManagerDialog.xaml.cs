using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager
{
    /// <summary>
    /// 通讯管理器对话框
    /// </summary>
    public partial class CommunicationManagerDialog : ThemeDialogWindow
    {
        private bool _allowClosing;
        public CommunicationManagerDialog(CommunicationManagerViewModel viewModel)
        {
            InitializeComponent();
            DataContext = viewModel ?? throw new System.ArgumentNullException(nameof(viewModel));
            viewModel.RequestAddDevice += OnRequestAddDevice;
        }

        private void OnRequestAddDevice(object? sender, System.EventArgs e)
        {
            if (DataContext is not CommunicationManagerViewModel manager)
                return;

            var editor = new CommunicationDeviceEditorViewModel();
            var dialog = new CommunicationDeviceDialog(editor)
            {
                Owner = this
            };

            if (dialog.ShowDialog() == true && editor.Result != null)
                manager.AddCommunication(editor.Result);
        }

        private void DeviceItem_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (sender is ListBoxItem item)
            {
                item.IsSelected = true;
                item.Focus();
            }
        }

        private void RenameDevice_Click(object sender, RoutedEventArgs e)
        {
            var communication = GetContextMenuCommunication(sender);
            if (communication == null || DataContext is not CommunicationManagerViewModel manager)
                return;

            manager.SelectedCommunication = communication;
            var dialog = new CommunicationRenameDialog(communication.Name) { Owner = this };
            if (dialog.ShowDialog() == true)
                manager.RenameCommunication(communication, dialog.DeviceName);
        }

        private void DeleteDevice_Click(object sender, RoutedEventArgs e)
        {
            var communication = GetContextMenuCommunication(sender);
            if (communication == null || DataContext is not CommunicationManagerViewModel manager)
                return;

            manager.SelectedCommunication = communication;
            if (manager.DeleteCommunicationCommand.CanExecute(null))
                manager.DeleteCommunicationCommand.Execute(null);
        }

        private static Communication? GetContextMenuCommunication(object sender)
        {
            if (sender is not MenuItem menuItem ||
                ItemsControl.ItemsControlFromItemContainer(menuItem) is not ContextMenu contextMenu ||
                contextMenu.PlacementTarget is not FrameworkElement target)
                return null;

            return target.DataContext as Communication;
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void DevicePage_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CommunicationManagerViewModel manager) manager.ActivePage = "Devices";
        }

        private void ReceiveEventsPage_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CommunicationManagerViewModel manager) manager.ActivePage = "ReceiveEvents";
        }

        private void AddReceiveEvent_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CommunicationManagerViewModel manager) return;
            var dialog = new CommunicationEventDialog { Owner = this };
            if (dialog.ShowDialog() == true && dialog.Result != null)
                manager.AddReceiveEvent(dialog.Result);
        }

        private void AddModbusAddressPoint_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CommunicationManagerViewModel manager && manager.SelectedWorkspace != null)
                manager.SelectedWorkspace.AddModbusAddressPoint();
        }

        private async void AddOpcUaPoint_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CommunicationManagerViewModel manager || manager.SelectedWorkspace == null) return;
            var dialog = new OpcUaDataPointDialog { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Result == null) return;
            var error = await manager.AddOpcUaPointAsync(dialog.Result);
            if (error != null) MessageBox.Show(this, error, "添加数据点", MessageBoxButton.OK, MessageBoxImage.Warning);
        }

        private async void OpcUaDataGrid_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (e.OriginalSource is TextBox or Button || DataContext is not CommunicationManagerViewModel manager) return;

            var current = e.OriginalSource as DependencyObject;
            while (current != null && current is not DataGridRow)
                current = VisualTreeHelper.GetParent(current);
            if (current is not DataGridRow { DataContext: OpcUaPointRowViewModel row } || manager.SelectedWorkspace == null) return;

            manager.SelectedWorkspace.SelectedOpcUaPoint = row;
            var dialog = new OpcUaDataPointDialog(row.Point) { Owner = this };
            if (dialog.ShowDialog() != true || dialog.Result == null) return;
            var error = await manager.EditOpcUaPointAsync(row, dialog.Result);
            if (error != null) MessageBox.Show(this, error, "编辑数据点", MessageBoxButton.OK, MessageBoxImage.Warning);
            e.Handled = true;
        }

        private async void RefreshOpcUaPoints_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CommunicationManagerViewModel manager)
                await manager.RefreshOpcUaPointsAsync();
        }

        private async void RemoveOpcUaPoint_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CommunicationManagerViewModel manager || sender is not FrameworkElement { DataContext: OpcUaPointRowViewModel row }) return;
            if (row.IsWriting) return;
            if (manager.SelectedWorkspace != null) manager.SelectedWorkspace.SelectedOpcUaPoint = row;
            await manager.RemoveOpcUaPointAsync(row);
        }

        private async void OpcUaValueEditor_LostKeyboardFocus(object sender, KeyboardFocusChangedEventArgs e)
        {
            if (sender is not TextBox editor || editor.DataContext is not OpcUaPointRowViewModel row ||
                !row.CanEdit || DataContext is not CommunicationManagerViewModel manager ||
                string.Equals(editor.Text, row.ValueText, System.StringComparison.Ordinal)) return;
            row.DraftValue = editor.Text;
            row.IsEditing = true;
            await manager.WriteOpcUaPointAsync(row);
        }

        private async void OpcUaValueEditor_KeyDown(object sender, KeyEventArgs e)
        {
            if (sender is not FrameworkElement { DataContext: OpcUaPointRowViewModel row }) return;
            if (e.Key == Key.Escape) { row.CancelEdit(); e.Handled = true; }
            else if (e.Key == Key.Enter && DataContext is CommunicationManagerViewModel manager)
            {
                e.Handled = true;
                if (sender is TextBox editor)
                {
                    row.DraftValue = editor.Text;
                    row.IsEditing = row.DraftValue != row.ValueText;
                }
                await manager.WriteOpcUaPointAsync(row);
            }
        }

        private void RemoveModbusAddressPoint_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CommunicationManagerViewModel manager &&
                manager.SelectedWorkspace != null &&
                sender is FrameworkElement { Tag: ModbusAddressPoint point })
            {
                manager.SelectedWorkspace.RemoveModbusAddressPoint(point);
            }
        }

        private void ClearModbusAddresses_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CommunicationManagerViewModel manager ||
                manager.SelectedWorkspace is not { } workspace || workspace.ModbusAddressPoints.Count == 0)
                return;
            if (MessageBox.Show(this,
                    $"确定删除当前设备的全部 {workspace.ModbusAddressPoints.Count} 个地址块吗？此操作会清除当前值和接收记录，保存方案后无法撤销。",
                    "清空地址空间", MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes)
                return;
            workspace.ClearAllModbusAddressPoints();
        }

        private void ModbusAddressCell_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not CommunicationManagerViewModel manager ||
                manager.SelectedWorkspace == null ||
                sender is not FrameworkElement cell ||
                cell.Tag is not ModbusAddressSpaceCell { IsInRange: true } addressCell)
                return;

            if (addressCell.Point == null)
            {
                var editor = new ModbusAddressPointEditorViewModel(addressCell.Address);
                var dialog = new ModbusAddressPointDialog(editor) { Owner = this };
                if (dialog.ShowDialog() != true || editor.Result == null)
                    return;

                SaveModbusAddressPoint(manager, editor.Result);
                return;
            }

            var recordsDialog = new ModbusReceiveRecordsDialog(addressCell.Point) { Owner = this };
            recordsDialog.ShowDialog();
        }

        private void ModbusAddressBar_Click(object sender, MouseButtonEventArgs e)
        {
            if (DataContext is not CommunicationManagerViewModel manager ||
                manager.SelectedWorkspace == null ||
                sender is not FrameworkElement { Tag: ModbusAddressBarSegment { Point: { } point } })
                return;

            var editor = new ModbusAddressPointEditorViewModel(point.StartAddress, point);
            var dialog = new ModbusAddressPointDialog(editor) { Owner = this };
            if (dialog.ShowDialog() == true && editor.Result != null)
                SaveModbusAddressPoint(manager, editor.Result);
        }

        private void SaveModbusAddressPoint(CommunicationManagerViewModel manager, ModbusAddressPoint point)
        {
            if (manager.SelectedWorkspace == null)
                return;

            if (!manager.SelectedWorkspace.TrySaveModbusAddressPoint(point, out var error))
            {
                MessageBox.Show(this, error, "地址配置冲突", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            manager.SelectedWorkspace.ModbusAddress = point.StartAddress;
            manager.SelectedWorkspace.ModbusCount = point.Count;
            manager.SelectedWorkspace.ModbusResultText = $"已配置地址 {point.Name}（{point.RangeText}）。";
        }

        private void RemoveModbusAddressBar_Click(object sender, MouseButtonEventArgs e)
        {
            if (sender is FrameworkElement { Tag: ModbusAddressBarSegment { Point: { } point } } &&
                DataContext is CommunicationManagerViewModel manager &&
                manager.SelectedWorkspace != null)
            {
                manager.SelectedWorkspace.RemoveModbusAddressPoint(point);
                e.Handled = true;
            }
        }

        private async void SyncModbusData_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CommunicationManagerViewModel manager)
                return;
            try
            {
                await manager.SyncModbusDataAsync();
            }
            catch (System.Exception ex)
            {
                MessageBox.Show(this, ex.Message, "数据同步失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void SendModbusTest_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CommunicationManagerViewModel manager ||
                manager.SelectedWorkspace is not { } workspace)
                return;
            if (workspace.ModbusAddressPoints.Count == 0)
            {
                MessageBox.Show(this, "请先分配至少一个地址块。", "发送测试", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            new ModbusSendTestDialog(manager, workspace) { Owner = this }.ShowDialog();
        }

        private void TrafficLog_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox textBox)
                textBox.ScrollToEnd();
        }

        private void SendTextBox_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key != Key.Enter || Keyboard.Modifiers.HasFlag(ModifierKeys.Shift))
                return;

            e.Handled = true;
            if (DataContext is CommunicationManagerViewModel manager &&
                manager.SendDataCommand.CanExecute(null))
            {
                manager.SendDataCommand.Execute(null);
            }
        }

        private void ReceiveTerminatorShortcut_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CommunicationManagerViewModel manager &&
                manager.SelectedCommunication != null &&
                sender is Button { Tag: string value })
            {
                manager.SelectedCommunication.ReceiveTerminator = value;
            }
        }

        private void DeviceToggle_CheckedChanged(object sender, RoutedPropertyChangedEventArgs<bool> e)
        {
            if (sender is not ToggleSwitch toggle || toggle.DataContext is not Communication communication)
                return;
            if (communication.IsRuntimeActive == e.NewValue)
                return;

            if (DataContext is CommunicationManagerViewModel manager)
            {
                manager.SelectedCommunication = communication;
                if (manager.ToggleDeviceCommand.CanExecute(communication))
                    manager.ToggleDeviceCommand.Execute(communication);
            }

            // SDK ToggleSwitch handles pointer input internally; reassert the one-way runtime-state binding
            // after the user gesture so the control's temporary visual state cannot become authoritative.
            BindingOperations.SetBinding(toggle, ToggleSwitch.IsCheckedProperty, new Binding(nameof(Communication.IsRuntimeActive))
            {
                Source = communication,
                Mode = BindingMode.OneWay
            });
        }

        protected override void OnClosed(System.EventArgs e)
        {
            if (DataContext is CommunicationManagerViewModel viewModel)
            {
                viewModel.RequestAddDevice -= OnRequestAddDevice;
                _ = viewModel.DisposeAsync();
            }

            base.OnClosed(e);
        }

        protected override async void OnClosing(CancelEventArgs e)
        {
            if (_allowClosing || DataContext is not CommunicationManagerViewModel { HasUnsavedChanges: true } manager)
            {
                base.OnClosing(e);
                return;
            }
            e.Cancel = true;
            var choice = MessageBox.Show(this, "OPC UA 配置有未保存更改。是否保存后关闭？\n选择“否”将撤销本次未保存的 OPC UA 配置。",
                "未保存的通讯配置", MessageBoxButton.YesNoCancel, MessageBoxImage.Question);
            if (choice == MessageBoxResult.Cancel) return;
            if (choice == MessageBoxResult.Yes)
            {
                if (!await manager.SaveChangesAsync()) return;
            }
            else
            {
                try { await manager.DiscardOpcUaChangesAsync(); }
                catch (System.Exception ex)
                {
                    MessageBox.Show(this, ex.Message, "撤销通讯配置失败", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }
            _allowClosing = true;
            // OnClosing is still executing here; defer the final Close until this
            // Closing event has returned to avoid WPF's VerifyNotClosing re-entry.
            Dispatcher.BeginInvoke(new System.Action(Close));
        }
    }
}
