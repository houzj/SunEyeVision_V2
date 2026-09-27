using System;
using System.Windows;
using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class ModbusAddressPointDialog : ThemeDialogWindow
{
    private readonly ModbusAddressPointEditorViewModel _viewModel;

    public ModbusAddressPointDialog(ModbusAddressPointEditorViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel ?? throw new ArgumentNullException(nameof(viewModel));
        DataContext = _viewModel;
        _viewModel.RequestClose += OnRequestClose;
    }

    private void OnRequestClose(object? sender, EventArgs e)
    {
        DialogResult = _viewModel.Result != null;
        Close();
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.RequestClose -= OnRequestClose;
        base.OnClosed(e);
    }
}
