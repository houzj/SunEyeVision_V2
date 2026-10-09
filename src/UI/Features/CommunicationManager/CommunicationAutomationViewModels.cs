using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public sealed record AutomationChoice(string Id, string Name, bool IsSupported = true);
public sealed record TriggerEventChoice(Guid? Id, string Name);
public sealed record TriggerFieldChoice(Guid? Id, string Name);

public sealed class CommunicationTriggerRowViewModel : ViewModelBase
{
    private readonly CommunicationManagerViewModel _owner;
    public CommunicationTriggerDefinition Definition { get; }
    public IReadOnlyList<AutomationChoice> Commands { get; } = new[]
    {
        new AutomationChoice("切换方案", "切换方案"), new("执行流程", "执行流程"),
        new("执行模块", "执行模块"), new("执行模块动作", "执行模块动作（暂不支持）", false)
    };
    public IReadOnlyList<string> MatchModes { get; } = new[] { "完全匹配", "部分匹配", "不匹配" };
    public IReadOnlyList<TriggerEventChoice> Events { get; private set; } = Array.Empty<TriggerEventChoice>();
    public IReadOnlyList<TriggerFieldChoice> Fields { get; private set; } = Array.Empty<TriggerFieldChoice>();
    public IReadOnlyList<AutomationChoice> Targets { get; private set; } = Array.Empty<AutomationChoice>();
    public CommunicationTriggerRowViewModel(CommunicationManagerViewModel owner, CommunicationTriggerDefinition definition)
    {
        _owner = owner; Definition = definition;
        definition.PropertyChanged += Changed;
        RefreshChoices();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(Definition.EventId)) Definition.FieldId = null;
        if (e.PropertyName == nameof(Definition.Command)) Definition.TargetId = "";
        if (e.PropertyName is nameof(Definition.EventId) or nameof(Definition.Command)) RefreshChoices();
        _owner.SyncAutomationConfiguration();
    }
    public void RefreshChoices()
    {
        Events = new[] { new TriggerEventChoice(null, "请选择") }.Concat(_owner.ReceiveEvents.Select((e, i) =>
            new TriggerEventChoice(e.EventId, $"{i} {e.DisplayName}"))).ToArray();
        if (Definition.EventId.HasValue && !Events.Any(e => e.Id == Definition.EventId))
            Events = Events.Append(new(Definition.EventId, "已失效的接收事件")).ToArray();
        var source = _owner.ReceiveEvents.FirstOrDefault(e => e.EventId == Definition.EventId);
        Fields = new[] { new TriggerFieldChoice(null, "无") }.Concat(source?.AssemblyOutputs.Select(f =>
            new TriggerFieldChoice(f.FieldId, f.Name)) ?? Enumerable.Empty<TriggerFieldChoice>()).ToArray();
        if (Definition.FieldId.HasValue && !Fields.Any(f => f.Id == Definition.FieldId))
            Fields = Fields.Append(new(Definition.FieldId, "已失效的输出字段")).ToArray();
        Targets = _owner.GetAutomationTargets(Definition.Command);
        if (!string.IsNullOrEmpty(Definition.TargetId) && !Targets.Any(t => t.Id == Definition.TargetId))
            Targets = Targets.Append(new(Definition.TargetId, "已失效的目标")).ToArray();
        OnPropertyChanged(nameof(Events)); OnPropertyChanged(nameof(Fields)); OnPropertyChanged(nameof(Targets));
    }
    public void Dispose() => Definition.PropertyChanged -= Changed;
}

public sealed class CommunicationResponseRowViewModel : ViewModelBase
{
    private readonly CommunicationManagerViewModel _owner;
    public CommunicationResponseDefinition Definition { get; }
    public string Title { get; }
    public string FirstLabel { get; }
    public string SecondLabel { get; }
    public bool IsLoaded { get; }
    public bool IsState => !IsLoaded;
    public bool ShowType => _owner.Communications.FirstOrDefault(d => d.Id == Definition.DeviceId)?.IsRawProtocol == false;
    public bool ShowSeparator => IsState || ShowType && Definition.DataType != "string";
    public bool ShowTerminator => string.Equals(Definition.DataType, "string", StringComparison.OrdinalIgnoreCase);
    public string TargetLabel { get; private set; } = "请选择";
    private bool _targetOpen, _selecting;
    public bool IsTargetOpen { get => _targetOpen; set => SetProperty(ref _targetOpen, value); }
    public IReadOnlyList<string> DataTypes { get; private set; } = new[] { "string" };
    public ObservableCollection<HeartbeatTargetNode> Targets { get; } = new();
    public ObservableCollection<CommunicationSeparatorOption> Separators { get; } = CommunicationSeparators.CreateOptions();
    public string Explanation => IsLoaded ? "方案加载并完成通信初始化后发送；延迟单位为秒。"
        : "发送内容＋分隔符＋编号。Modbus 数值逐段编码，编号为最后一项；OPC UA 单个数值节点不支持多项，校验失败不写入。";
    public CommunicationResponseRowViewModel(CommunicationManagerViewModel owner, CommunicationResponseDefinition definition,
        string title, string first, string second, bool loaded = false)
    {
        _owner = owner; Definition = definition; Title = title; FirstLabel = first; SecondLabel = second; IsLoaded = loaded;
        definition.PropertyChanged += Changed;
        RefreshTargets();
    }
    private void Changed(object? sender, PropertyChangedEventArgs e)
    {
        if (_selecting) return;
        if (e.PropertyName == nameof(Definition.Enabled) && !Definition.Enabled) IsTargetOpen = false;
        if (e.PropertyName == nameof(Definition.DataType) && !ShowTerminator) Definition.Terminator = "";
        if (e.PropertyName is nameof(Definition.DeviceId) or nameof(Definition.TargetId) or nameof(Definition.DataType)) RefreshTargets();
        CommunicationSeparators.EnsureValue(Separators, Definition.Separator);
        CommunicationSeparators.EnsureValue(Separators, Definition.Terminator);
        _owner.SyncAutomationConfiguration();
    }
    public void RefreshTargets()
    {
        Targets.Clear(); Targets.Add(new("请选择", "", "", true)); TargetLabel = "请选择";
        foreach (var device in _owner.Communications)
        {
            var name = $"{_owner.Communications.IndexOf(device) + 1} {device.Name}";
            if (device.IsRawProtocol)
            {
                Targets.Add(new(name, device.Id, "", true));
                if (Definition.DeviceId == device.Id) TargetLabel = name;
            }
            else if (device.IsModbus || device.IsOpcUa)
            {
                var parent = new HeartbeatTargetNode(name, device.Id, "", false);
                var points = device.IsModbus ? device.ModbusAddressPoints.Select(p => (p.Id, p.Name))
                    : (device.OpcUaDataPoints ?? new()).Select(p => (p.Id, Name: p.TagName));
                var ordinal = 0;
                foreach (var point in points)
                {
                    var label = $"{name}.{++ordinal} {point.Name}";
                    parent.Children.Add(new(label, device.Id, point.Id, true));
                    if (Definition.DeviceId == device.Id && Definition.TargetId == point.Id) TargetLabel = label;
                }
                Targets.Add(parent);
            }
        }
        var selected = _owner.Communications.FirstOrDefault(d => d.Id == Definition.DeviceId);
        DataTypes = selected?.IsModbus == true ? new[] { "int", "float", "string" }
            : selected?.IsOpcUa == true ? CommunicationSendEventViewModel.DataTypes : new[] { "string" };
        if (Definition.DeviceId != "" && TargetLabel == "请选择") TargetLabel = "绑定目标已失效";
        CommunicationSeparators.EnsureValue(Separators, Definition.Separator);
        CommunicationSeparators.EnsureValue(Separators, Definition.Terminator);
        OnPropertyChanged(nameof(TargetLabel)); OnPropertyChanged(nameof(DataTypes)); OnPropertyChanged(nameof(ShowType));
        OnPropertyChanged(nameof(ShowSeparator)); OnPropertyChanged(nameof(ShowTerminator));
    }
    public void SelectTarget(HeartbeatTargetNode target)
    {
        if (!target.Selectable) return;
        _selecting = true;
        try
        {
            Definition.DeviceId = target.DeviceId; Definition.TargetId = target.TargetId;
            var device = _owner.Communications.FirstOrDefault(d => d.Id == target.DeviceId);
            Definition.DataType = device?.IsModbus == true ? "int" : "string";
            Definition.Terminator = ""; IsTargetOpen = false;
        }
        finally { _selecting = false; }
        RefreshTargets(); _owner.SyncAutomationConfiguration();
    }
    public void Dispose() => Definition.PropertyChanged -= Changed;
}
