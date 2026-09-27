using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using Microsoft.Win32;
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

        private void AddModbusAddressPoint_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is CommunicationManagerViewModel manager && manager.SelectedWorkspace != null)
                manager.SelectedWorkspace.AddModbusAddressPoint();
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

        private void ImportOpcUaCertificate_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CommunicationManagerViewModel manager || manager.SelectedCommunication?.IsOpcUa != true)
                return;

            var picker = new OpenFileDialog
            {
                Title = "选择 OPC UA 服务器证书",
                Filter = "X.509 证书 (*.der;*.cer;*.crt;*.pem)|*.der;*.cer;*.crt;*.pem|所有文件 (*.*)|*.*",
                CheckFileExists = true,
                Multiselect = false
            };
            if (picker.ShowDialog(this) != true) return;

            try
            {
                var certificate = manager.InspectOpcUaServerCertificate(picker.FileName);
                var decision = MessageBox.Show(this,
                    $"请核对证书来源后再信任。导入后此证书将被作为 OPC UA 服务器对端证书信任：\n\n主题：{certificate.Subject}\n指纹：{certificate.Thumbprint}\n有效期至（UTC）：{certificate.ExpiresUtc:yyyy-MM-dd HH:mm:ss}\n\n是否明确加入信任列表？",
                    "确认信任 OPC UA 服务器证书", MessageBoxButton.YesNo, MessageBoxImage.Warning);
                if (decision != MessageBoxResult.Yes) return;

                manager.TrustOpcUaServerCertificate(picker.FileName);
                MessageBox.Show(this, "证书已加入信任列表。请断开并重新连接 OPC UA 设备后生效。", "证书信任已更新", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"无法导入证书：{ex.Message}", "OPC UA 证书导入失败", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void RemoveOpcUaCertificate_Click(object sender, RoutedEventArgs e)
        {
            if (DataContext is not CommunicationManagerViewModel manager || manager.SelectedTrustedOpcUaCertificate is not { } certificate)
                return;

            var decision = MessageBox.Show(this,
                $"撤销此证书信任？新连接将不再信任该证书。\n\n{certificate.Subject}\n{certificate.Thumbprint}",
                "撤销 OPC UA 证书信任", MessageBoxButton.YesNo, MessageBoxImage.Warning);
            if (decision != MessageBoxResult.Yes) return;

            try
            {
                manager.RemoveTrustedOpcUaServerCertificate(certificate.Thumbprint);
                MessageBox.Show(this, "已撤销信任。现有会话不会自动断开；新信任状态在下一次连接时生效。", "证书信任已撤销", MessageBoxButton.OK, MessageBoxImage.Information);
            }
            catch (Exception ex)
            {
                MessageBox.Show(this, $"无法撤销证书信任：{ex.Message}", "OPC UA 证书操作失败", MessageBoxButton.OK, MessageBoxImage.Error);
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
    }
}
