using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public sealed record HeartbeatTargetNode(string Name, string DeviceId, string TargetId, bool Selectable)
{
    public ObservableCollection<HeartbeatTargetNode> Children { get; } = new();
    public string FullName { get; init; } = Name;
}

public sealed class CommunicationHeartbeatViewModel : ViewModelBase
{
    public Guid Id { get; set; } = Guid.NewGuid();
    private string _deviceId = "", _targetId = "", _mode = "Single", _type = "string", _a = "", _b = "";
    private string _label = "请选择", _status = "未启用";
    private int _interval = 1000;
    private bool _enabled, _popup, _selecting, _typeLocked;
    public bool TypeLocked => _typeLocked;
    public bool TypeResolved => !_typeLocked || !string.IsNullOrEmpty(DataType);
    public string TypeLabel => TypeResolved ? DataType : "未知类型";
    public bool ShowTypeSelector => ShowType && !TypeLocked;
    public event EventHandler? ConfigurationChanged;
    public ObservableCollection<HeartbeatTargetNode> Targets { get; } = new();
    public IReadOnlyList<string> Modes { get; } = new[] { "单数据", "多数据" };
    private IReadOnlyList<string> _types = new[] { "string" };
    public IReadOnlyList<string> DataTypes { get => _types; set => SetProperty(ref _types, value); }
    public bool ShowType => DataTypes.Count != 1 || DataTypes[0] != "string";
    public string DeviceId { get => _deviceId; set => SetProperty(ref _deviceId, value); }
    public string TargetId { get => _targetId; set => SetProperty(ref _targetId, value); }
    public string Mode { get => _mode; set { if (SetProperty(ref _mode, value)) { OnPropertyChanged(nameof(ModeDisplay)); OnPropertyChanged(nameof(IsAlternate)); } } }
    public string ModeDisplay { get => Mode == "Alternate" ? "多数据" : "单数据"; set => Mode = value == "多数据" ? "Alternate" : "Single"; }
    public bool IsAlternate => Mode == "Alternate";
    public string DataType { get => _type; set { if (!TypeLocked) SetProperty(ref _type, value); } }
    public string ContentA { get => _a; set => SetProperty(ref _a, value); }
    public string ContentB { get => _b; set => SetProperty(ref _b, value); }
    public int IntervalMs { get => _interval; set => SetProperty(ref _interval, value); }
    public bool Enabled { get => _enabled; set { if (SetProperty(ref _enabled, value)) Status = value ? "等待周期" : "未启用"; } }
    public string TargetLabel { get => _label; set => SetProperty(ref _label, value); }
    public string Status { get => _status; set { if (SetProperty(ref _status, value)) { OnPropertyChanged(nameof(HasStatusIssue)); OnPropertyChanged(nameof(StatusMessage)); } } }
    public bool HasStatusIssue => Status is not ("未启用" or "未绑定目标" or "等待周期" or "发送成功");
    public string StatusMessage => TargetLabel + "：" + Status;
    public bool IsTargetOpen { get => _popup; set => SetProperty(ref _popup, value); }

    public CommunicationHeartbeatViewModel()
    {
        PropertyChanged += (_, e) =>
        {
            if (!_selecting && e.PropertyName is ("DeviceId" or "TargetId" or "Mode" or "DataType" or "ContentA" or "ContentB" or "IntervalMs" or "Enabled"))
                ConfigurationChanged?.Invoke(this, EventArgs.Empty);
            if (e.PropertyName == nameof(DataTypes)) { OnPropertyChanged(nameof(ShowType)); OnPropertyChanged(nameof(ShowTypeSelector)); }
            if (e.PropertyName == nameof(DataType)) { OnPropertyChanged(nameof(TypeResolved)); OnPropertyChanged(nameof(TypeLabel)); }
            if (e.PropertyName == nameof(TargetLabel)) OnPropertyChanged(nameof(StatusMessage));
        };
    }

    public bool ConfigureType(bool locked, string? nativeType)
    {
        var changed = locked && _type != (nativeType ?? "");
        _typeLocked = locked;
        _selecting = true;
        try
        {
            if (locked) SetProperty(ref _type, nativeType ?? "", nameof(DataType));
            OnPropertyChanged(nameof(TypeLocked)); OnPropertyChanged(nameof(TypeResolved));
            OnPropertyChanged(nameof(TypeLabel)); OnPropertyChanged(nameof(ShowTypeSelector));
        }
        finally { _selecting = false; }
        return changed;
    }

    public void SelectTarget(HeartbeatTargetNode target, IReadOnlyList<string> types, string defaultType)
    {
        _selecting = true;
        try
        {
            if (DeviceId != target.DeviceId || TargetId != target.TargetId)
                Status = Enabled ? "等待周期" : "未启用";
            DeviceId = target.DeviceId; TargetId = target.TargetId; TargetLabel = target.FullName;
            DataTypes = types; DataType = defaultType; IsTargetOpen = false;
            if (target.DeviceId.Length == 0) { Enabled = false; Status = "未绑定目标"; }
        }
        finally { _selecting = false; }
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public CommunicationHeartbeatDefinition Capture() => new()
    {
        Id = Id, DeviceId = DeviceId, TargetId = TargetId, Mode = Mode, DataType = DataType,
        ContentA = ContentA, ContentB = ContentB, IntervalMs = IntervalMs, Enabled = Enabled
    };

    public static CommunicationHeartbeatViewModel FromDefinition(CommunicationHeartbeatDefinition definition) => new()
    {
        Id = definition.Id, DeviceId = definition.DeviceId, TargetId = definition.TargetId, Mode = definition.Mode,
        DataType = definition.DataType, ContentA = definition.ContentA, ContentB = definition.ContentB,
        IntervalMs = definition.IntervalMs, Enabled = definition.Enabled
    };
}
