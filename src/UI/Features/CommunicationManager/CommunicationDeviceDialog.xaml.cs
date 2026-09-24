using System;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.UI.Shell.Windows;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class CommunicationDeviceDialog : ThemeDialogWindow
{
    private readonly CommunicationDeviceEditorViewModel _viewModel;

    public CommunicationDeviceDialog(CommunicationDeviceEditorViewModel viewModel)
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

    private void ReceiveTerminatorShortcut_Click(object sender, RoutedEventArgs e)
    {
        if (sender is Button { Tag: string value })
            _viewModel.ReceiveTerminator = value;
    }

    protected override void OnClosed(EventArgs e)
    {
        _viewModel.RequestClose -= OnRequestClose;
        base.OnClosed(e);
    }
}
