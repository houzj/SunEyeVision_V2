using System;
using System.Linq;
using System.Windows;
using SunEyeVision.UI.Shell.Windows;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class ModbusSendTestDialog : ThemeDialogWindow
{
    private readonly CommunicationManagerViewModel _manager;
    private readonly CommunicationDeviceWorkspaceViewModel _workspace;
    private readonly ModbusAddressPoint[] _addressPoints;

    public ModbusSendTestDialog(CommunicationManagerViewModel manager,
        CommunicationDeviceWorkspaceViewModel workspace)
    {
        InitializeComponent();
        _manager = manager;
        _workspace = workspace;
        _addressPoints = workspace.ModbusAddressPoints.ToArray();
        AddressCombo.ItemsSource = _addressPoints
            .Select((point, index) => $"{index + 1} {point.Name}")
            .ToArray();
        var writableIndex = Array.FindIndex(_addressPoints, point => point.AccessMode != "只读");
        AddressCombo.SelectedIndex = writableIndex >= 0 ? writableIndex : 0;
        DataTypeCombo.ItemsSource = new[] { "int", "float", "string" };
        DataTypeCombo.SelectedItem = "string";
    }

    private void Clear_Click(object sender, RoutedEventArgs e)
    {
        PayloadTextBox.Clear();
        StatusText.Text = string.Empty;
        PayloadTextBox.Focus();
    }

    private async void Send_Click(object sender, RoutedEventArgs e)
    {
        if (AddressCombo.SelectedIndex < 0 || AddressCombo.SelectedIndex >= _addressPoints.Length ||
            DataTypeCombo.SelectedItem is not string dataType)
        {
            StatusText.Text = "请选择地址块和发送数据类型。";
            return;
        }
        SendButton.IsEnabled = false;
        StatusText.Text = "正在发送…";
        try
        {
            await _manager.SendModbusTestAsync(_workspace, _addressPoints[AddressCombo.SelectedIndex], dataType, PayloadTextBox.Text);
            StatusText.Text = _workspace.ModbusResultText;
        }
        catch (Exception ex)
        {
            StatusText.Text = $"发送失败：{ex.Message}";
        }
        finally
        {
            SendButton.IsEnabled = true;
        }
    }

}
