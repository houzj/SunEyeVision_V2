using System;
using System.Windows.Threading;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class CommunicationManagerViewModel
{
    private DispatcherTimer? _connectionPresentationTimer;

    private void InitializeConnectionPresentation()
    {
        if (IsConnectionViewInactive || _connectionPresentationTimer != null) return;
        RefreshConnectionPresentation();
        _connectionPresentationTimer = new DispatcherTimer(DispatcherPriority.Background)
        {
            Interval = TimeSpan.FromSeconds(1)
        };
        _connectionPresentationTimer.Tick += ConnectionPresentationTick;
        _connectionPresentationTimer.Start();
    }

    private void ConnectionPresentationTick(object? sender, EventArgs args) => RefreshConnectionPresentation();

    private void RefreshConnectionPresentation()
    {
        if (IsConnectionViewInactive) return;
        foreach (var device in Communications) RefreshConnectionPresentation(device);
        OnPropertyChanged(nameof(IsSelectedConnected));
    }

    private void RefreshConnectionPresentation(Communication device)
    {
        if (IsConnectionViewInactive) return;
        var state = _communicationService.GetConnectionPresentation(device.Id);
        device.IsConnectionEnabled = state.IsConnectionEnabled;
        device.RuntimeStatus = MapStatus(state.ActualState);
        device.IsWaitingForReconnect = state.IsWaitingForReconnect;
        device.RuntimeConnectionDescription = state.StatusText;
        device.RuntimeConnectionError = state.FailureReason;
    }

    private void DisposeConnectionPresentation()
    {
        if (_connectionPresentationTimer == null) return;
        _connectionPresentationTimer.Stop();
        _connectionPresentationTimer.Tick -= ConnectionPresentationTick;
        _connectionPresentationTimer = null;
    }
}
