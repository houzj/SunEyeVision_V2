using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Features.WorkflowUI.Canvas.Adapters;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public partial class CommunicationManagerViewModel
{
    public ObservableCollection<CommunicationHeartbeatViewModel> Heartbeats { get; } = new();
    public ICommand AddHeartbeatCommand { get; private set; } = null!;
    private bool _refreshingHeartbeats;
    private bool _heartbeatEditorInitialized;

    private void InitializeHeartbeats()
    {
        foreach (var definition in _solutionManager.CurrentSolution?.CommunicationHeartbeats ?? new())
            TrackHeartbeat(CommunicationHeartbeatViewModel.FromDefinition(definition));
        AddHeartbeatCommand = new RelayCommand(() =>
        {
            TrackHeartbeat(new CommunicationHeartbeatViewModel());
            HeartbeatConfigurationChanged(null, EventArgs.Empty);
        });
        ServiceInitializer.CommunicationService.Heartbeats.Runner.StatusChanged += HeartbeatStatusChanged;
        _heartbeatEditorInitialized = true;
        RefreshHeartbeatTargets();
    }

    private void TrackHeartbeat(CommunicationHeartbeatViewModel row)
    {
        row.ConfigurationChanged += HeartbeatConfigurationChanged;
        Heartbeats.Add(row);
    }

    private void HeartbeatConfigurationChanged(object? sender, EventArgs e)
    {
        if (_refreshingHeartbeats || !_heartbeatEditorInitialized) return;
        RefreshAutomationChoices();
        RefreshHeartbeatTargets();
        if (_solutionManager.CurrentSolution is not { } solution) return;
        solution.CommunicationHeartbeats = Heartbeats.Select(row => row.Capture()).ToList();
        ServiceInitializer.CommunicationService.RefreshAutomationConfiguration(solution);
        try { ServiceInitializer.CommunicationService.RefreshHeartbeatConfiguration(solution); }
        catch (Exception ex)
        {
            foreach (var row in Heartbeats.Where(item => item.Enabled)) row.Status = ex.Message;
            LogError($"心跳配置未生效：{ex.Message}");
        }
    }

    private void HeartbeatStatusChanged(Guid id, string status)
    {
        var dispatcher = Application.Current?.Dispatcher;
        if (dispatcher == null) return;
        dispatcher.BeginInvoke(new Action(() =>
        {
            var row = Heartbeats.FirstOrDefault(item => item.Id == id);
            if (row?.Enabled == true) row.Status = status;
        }));
    }

    public void RemoveHeartbeat(CommunicationHeartbeatViewModel row)
    {
        if (!Heartbeats.Remove(row)) return;
        row.ConfigurationChanged -= HeartbeatConfigurationChanged;
        HeartbeatConfigurationChanged(null, EventArgs.Empty);
    }

    public void RefreshHeartbeatTargets()
    {
        if (_refreshingHeartbeats) return;
        _refreshingHeartbeats = true;
        var typeChanged = false;
        try
        {
            foreach (var row in Heartbeats)
            {
                row.Targets.Clear();
                row.Targets.Add(new("清除选择", "", "", true));
                row.TargetLabel = "请选择";
                foreach (var device in Communications)
                {
                    var ordinal = Communications.IndexOf(device) + 1;
                    if (device.IsRawProtocol)
                    {
                        var name = $"{ordinal} {device.Name}";
                        if (row.DeviceId == device.Id) { row.TargetLabel = name; row.DataTypes = new[] { "string" }; row.ConfigureType(false, null); }
                        if (!Heartbeats.Any(item => item != row && item.DeviceId == device.Id))
                            row.Targets.Add(new(name, device.Id, "", true));
                    }
                    else if (device.IsModbus || device.IsOpcUa)
                    {
                        var parent = new HeartbeatTargetNode($"{ordinal} {device.Name}", device.Id, "", false);
                        var points = device.IsModbus
                            ? device.ModbusAddressPoints.Select(point => (point.Id, point.Name))
                            : (device.OpcUaDataPoints ?? new()).Select(point => (point.Id, Name: point.TagName));
                        var pointOrdinal = 0;
                        foreach (var point in points)
                        {
                            var name = $"{ordinal} {device.Name}-{++pointOrdinal} {point.Name}";
                            if (row.DeviceId == device.Id && row.TargetId == point.Id)
                            {
                                row.TargetLabel = name;
                                row.DataTypes = device.IsModbus ? new[] { "int", "float", "string" } : CommunicationSendEventViewModel.DataTypes;
                                typeChanged |= ConfigureHeartbeatType(row, device);
                            }
                            if (!Heartbeats.Any(item => item != row && item.DeviceId == device.Id && item.TargetId == point.Id))
                                parent.Children.Add(new($"{pointOrdinal} {point.Name}", device.Id, point.Id, true) { FullName = name });
                        }
                        if (parent.Children.Count > 0) row.Targets.Add(parent);
                    }
                }
                if (row.DeviceId != "" && row.TargetLabel == "请选择") row.TargetLabel = "绑定目标已失效";
            }
        }
        finally { _refreshingHeartbeats = false; }
        if (typeChanged) HeartbeatConfigurationChanged(null, EventArgs.Empty);
    }

    private bool ConfigureHeartbeatType(CommunicationHeartbeatViewModel row, Communication device)
    {
        if (!device.IsOpcUa) return row.ConfigureType(false, null);
        var point = device.OpcUaDataPoints?.FirstOrDefault(p => p.Id == row.TargetId);
        string? nativeType = null;
        if (point != null && _runtimeManager.TryGetLatestOpcUaPoint(device.Id, point.NodeId, out var latest) &&
            latest != null && latest.IsSupportedScalar)
        {
            nativeType = latest.BuiltInType.ToString() switch
            {
                "Float" => "float", "Int32" => "int",
                "String" => "string", "Boolean" => "Boolean", "Double" => "Double",
                "Int16" => "Int16", "UInt16" => "UInt16", "UInt32" => "UInt32", _ => null
            };
        }
        var changed = row.ConfigureType(true, nativeType);
        if (!row.TypeResolved) row.Status = "节点类型未确认或暂不支持，请连接设备并刷新数据点。";
        else if (row.Status.StartsWith("节点类型未确认")) row.Status = row.Enabled ? "等待周期" : "未启用";
        return changed;
    }

    private void RefreshHeartbeatPointType(string deviceId, string pointId)
    {
        var device = Communications.FirstOrDefault(d => d.Id == deviceId);
        if (device == null) return;
        var changed = false;
        foreach (var row in Heartbeats.Where(r => r.DeviceId == deviceId && r.TargetId == pointId))
            changed |= ConfigureHeartbeatType(row, device);
        // Value updates must not rebuild an open tree or restart unchanged heartbeat jobs.
        if (changed) HeartbeatConfigurationChanged(null, EventArgs.Empty);
    }

    public void SelectHeartbeatTarget(CommunicationHeartbeatViewModel row, HeartbeatTargetNode target)
    {
        if (!target.Selectable) return;
        row.ConfigureType(false, null);
        var device = Communications.FirstOrDefault(item => item.Id == target.DeviceId);
        var types = device?.IsModbus == true ? new[] { "int", "float", "string" }
            : device?.IsOpcUa == true ? CommunicationSendEventViewModel.DataTypes : new[] { "string" };
        var defaultType = device?.IsModbus == true ? "int" : "string";
        if (device?.IsOpcUa == true)
        {
            var point = device.OpcUaDataPoints?.FirstOrDefault(item => item.Id == target.TargetId);
            if (point != null && _runtimeManager.TryGetLatestOpcUaPoint(device.Id, point.NodeId, out var latest) && latest != null)
                defaultType = latest.BuiltInType.ToString() switch { "Float" => "float", "Int32" => "int", var native => types.Contains(native) ? native : "string" };
        }
        row.SelectTarget(target, types, defaultType);
        if (device?.IsOpcUa == true)
        {
            if (ConfigureHeartbeatType(row, device)) HeartbeatConfigurationChanged(null, EventArgs.Empty);
        }
    }

    private void DisposeHeartbeatEditor()
    {
        _heartbeatEditorInitialized = false;
        ServiceInitializer.CommunicationService.Heartbeats.Runner.StatusChanged -= HeartbeatStatusChanged;
        foreach (var row in Heartbeats) row.ConfigurationChanged -= HeartbeatConfigurationChanged;
    }
}
