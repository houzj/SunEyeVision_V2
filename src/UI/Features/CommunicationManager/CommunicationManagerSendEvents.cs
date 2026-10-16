using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Input;
using SunEyeVision.UI.Core;
using SunEyeVision.UI.Services.Communication;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public sealed record CommunicationSendTarget(string Id, string Name);

public partial class CommunicationManagerViewModel
{
    public ObservableCollection<CommunicationSendEventViewModel> SendEvents { get; } = new();
    public ObservableCollection<CommunicationSendTarget> SendEventTargets { get; } = new();
    private CommunicationSendEventViewModel? _selectedSendEvent;
    private bool _refreshingSendTargets;
    public CommunicationSendEventViewModel? SelectedSendEvent
    {
        get => _selectedSendEvent;
        set
        {
            if (!SetProperty(ref _selectedSendEvent, value)) return;
            OnPropertyChanged(nameof(HasSelectedSendEvent));
            RefreshSendTargets(); CommandManager.InvalidateRequerySuggested();
        }
    }
    public bool HasSelectedSendEvent => SelectedSendEvent != null;
    public Communication? SelectedSendDevice => Communications.FirstOrDefault(item => item.Id == SelectedSendEvent?.DeviceId);
    public bool HasSendTarget => SelectedSendDevice?.IsModbus == true || SelectedSendDevice?.IsOpcUa == true;
    public bool IsSendRaw => SelectedSendDevice?.IsRawProtocol == true;
    public string SendTargetLabel => SelectedSendDevice?.IsOpcUa == true ? "绑定数据点" : "绑定地址";
    public string SendCapabilityText => SelectedSendDevice switch
    {
        { IsModbus: true } => "按地址块的位数和字节顺序编码；同一地址块输出类型须一致，超出容量不发送。",
        { IsOpcUa: true } => "写入原生数据点；最终输出仅一条，类型与数据点匹配且必须可写。",
        { IsRawProtocol: true } => "UTF-8 文本按参数顺序发送；组装输出按组装列表顺序发送。",
        null => "请选择绑定设备。",
        _ => "该协议暂未接入发送事件。"
    };
    public ICommand AddSendParameterCommand { get; private set; } = null!;
    public ICommand AddSendAssemblyCommand { get; private set; } = null!;
    public ICommand TestSendEventCommand { get; private set; } = null!;
    public ICommand ClearSendEventHistoryCommand { get; private set; } = null!;

    private void InitializeSendEvents(IEnumerable<CommunicationSendEventDefinition>? definitions)
    {
        foreach (var definition in definitions ?? Enumerable.Empty<CommunicationSendEventDefinition>())
            TrackSendEvent(CommunicationSendEventViewModel.FromDefinition(definition));
        AddSendParameterCommand = new RelayCommand(AddSendParameter, CanEditSendEvent);
        AddSendAssemblyCommand = new RelayCommand(AddSendAssembly, CanEditSendEvent);
        TestSendEventCommand = new RelayCommand(async () => await ExecuteSelectedSendEventAsync(), CanEditSendEvent);
        ClearSendEventHistoryCommand = new RelayCommand(() => SelectedSendEvent?.ClearHistory(), CanEditSendEvent);
        SelectedSendEvent = SendEvents.FirstOrDefault();
    }

    private bool CanEditSendEvent() => SelectedSendEvent?.IsNotBusy == true;
    private void TrackSendEvent(CommunicationSendEventViewModel definition)
    {
        definition.ConfigurationChanged += SendEvent_ConfigurationChanged;
        definition.PropertyChanged += SendEvent_PropertyChanged;
        SendEvents.Add(definition);
    }

    private void SendEvent_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (sender == SelectedSendEvent && e.PropertyName == nameof(CommunicationSendEventViewModel.DeviceId))
        {
            if (SelectedSendDevice?.IsRawProtocol != true) SelectedSendEvent!.Terminator = string.Empty;
            RefreshSendTargets();
        }
    }

    private void SendEvent_ConfigurationChanged(object? sender, EventArgs e)
    {
        if (IsConnectionViewInactive || _refreshingSendTargets) return;
        if (_solutionManager.CurrentSolution is { } solution) solution.CommunicationSendEvents = CaptureSendEvents();
        // Like receive events, configuration is shared in memory and persisted by solution save.
        // Do not trigger the separate OPC UA device save/discard prompt.
    }

    private List<CommunicationSendEventDefinition> CaptureSendEvents() => SendEvents.Select(item => item.Capture()).ToList();

    public void RefreshSendTargets()
    {
        if (_refreshingSendTargets) return;
        _refreshingSendTargets = true;
        try
        {
            var target = SelectedSendEvent?.TargetId;
            SendEventTargets.Clear();
            if (SelectedSendDevice is { IsModbus: true } modbus)
                foreach (var point in modbus.ModbusAddressPoints) SendEventTargets.Add(new(point.Id, point.Name));
            if (SelectedSendDevice is { IsOpcUa: true } opcUa)
                foreach (var point in opcUa.OpcUaDataPoints ?? new()) SendEventTargets.Add(new(point.Id, point.TagName));
            if (SelectedSendEvent != null)
                SelectedSendEvent.TargetId = SendEventTargets.Any(item => item.Id == target) ? target! : string.Empty;
            OnPropertyChanged(nameof(SelectedSendDevice)); OnPropertyChanged(nameof(HasSendTarget));
            OnPropertyChanged(nameof(IsSendRaw)); OnPropertyChanged(nameof(SendTargetLabel)); OnPropertyChanged(nameof(SendCapabilityText));
        }
        finally { _refreshingSendTargets = false; }
        if (_solutionManager.CurrentSolution is { } solution) solution.CommunicationSendEvents = CaptureSendEvents();
    }

    private void SendTargets_CollectionChanged(object? sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
    {
        RefreshSendTargets();
        HeartbeatConfigurationChanged(null, EventArgs.Empty);
    }

    public void AddSendEvent(CommunicationSendEventViewModel definition)
    {
        TrackSendEvent(definition); SelectedSendEvent = definition;
        ActivePage = "SendEvents"; SendEvent_ConfigurationChanged(definition, EventArgs.Empty);
    }

    private void AddSendParameter()
    {
        if (SelectedSendEvent == null) return;
        SelectedSendEvent.Parameters.Add(new CommunicationSendParameterViewModel
        {
            Name = GetNextIndexedName(SelectedSendEvent.Parameters.Select(item => item.Name), "in"),
            DataType = DefaultSendDataType()
        });
    }

    private void AddSendAssembly()
    {
        if (SelectedSendEvent == null) return;
        SelectedSendEvent.AssemblyOutputs.Add(new CommunicationAssemblyOutputViewModel
        {
            Name = GetNextIndexedName(SelectedSendEvent.AssemblyOutputs.Select(item => item.Name), "out"),
            DataType = DefaultSendDataType(),
            AvailableDataSources = SelectedSendEvent.InputSources
        });
    }

    private string DefaultSendDataType()
    {
        if (SelectedSendDevice?.IsModbus == true) return "int";
        if (SelectedSendDevice is { IsOpcUa: true } device)
        {
            var point = device.OpcUaDataPoints?.FirstOrDefault(item => item.Id == SelectedSendEvent?.TargetId);
            if (point != null && _runtimeManager.TryGetLatestOpcUaPoint(device.Id, point.NodeId, out var value) && value != null)
                return value.BuiltInType switch
                {
                    Opc.Ua.BuiltInType.Boolean => "Boolean", Opc.Ua.BuiltInType.Double => "Double",
                    Opc.Ua.BuiltInType.Float => "float", Opc.Ua.BuiltInType.Int32 => "int",
                    Opc.Ua.BuiltInType.Int16 => "Int16", Opc.Ua.BuiltInType.UInt16 => "UInt16",
                    Opc.Ua.BuiltInType.UInt32 => "UInt32", _ => "string"
                };
        }
        return "string";
    }

    public void RemoveSendEvent(CommunicationSendEventViewModel definition)
    {
        if (definition.IsBusy) return;
        var index = SendEvents.IndexOf(definition);
        if (!SendEvents.Remove(definition)) return;
        definition.ConfigurationChanged -= SendEvent_ConfigurationChanged;
        definition.PropertyChanged -= SendEvent_PropertyChanged;
        SelectedSendEvent = SendEvents.Count == 0 ? null : SendEvents[Math.Min(index, SendEvents.Count - 1)];
        SendEvent_ConfigurationChanged(null, EventArgs.Empty);
    }

    public async Task ExecuteSelectedSendEventAsync()
    {
        var definition = SelectedSendEvent;
        if (IsConnectionViewInactive || definition == null || definition.IsBusy) return;
        definition.IsBusy = true; CommandManager.InvalidateRequerySuggested();
        CommunicationSendPlan? plan = null;
        try
        {
            var device = SelectedSendDevice ?? throw new InvalidOperationException("请选择绑定设备。");
            var token = _communicationService.GetDeviceOperationToken(_viewSolution!, device.Id);
            var status = _runtimeManager.GetStatus(new(device.Id));
            var service = new CommunicationSendEventService(_runtimeManager);
            plan = service.Prepare(definition.Capture(), device);
            await service.SendAsync(plan, token, status);
            if (IsConnectionViewInactive) return;
            foreach (var value in plan.Message.Inputs)
            {
                var input = definition.Parameters.First(item => item.FieldId == value.FieldId);
                input.Result = value.Display; input.Status = "OK";
            }
            foreach (var value in plan.Message.Outputs)
            {
                var output = definition.AssemblyOutputs.FirstOrDefault(item => item.FieldId == value.FieldId);
                if (output != null) output.Result = value.Display;
            }
            definition.PreviewText = plan.Protocol == "Modbus"
                ? $"寄存器 {plan.StartAddress} 起，{plan.Registers!.Length} 个；数据 HEX：{Convert.ToHexString(plan.PayloadBytes)}"
                : plan.Message.Text;
            definition.Status = "发送成功";
            definition.AddHistory(definition.PreviewText, plan.PayloadBytes, true);
            if (_deviceWorkspaces.TryGetValue(device.Id, out var workspace))
                workspace.AppendSent(plan.PayloadBytes, DateTimeOffset.Now, !device.IsRawProtocol);
        }
        catch (Exception ex)
        {
            definition.PreviewText = string.Empty; definition.Status = ex.Message;
            foreach (var input in definition.Parameters) { input.Result = string.Empty; input.Status = "NG"; }
            foreach (var output in definition.AssemblyOutputs) output.Result = string.Empty;
            definition.AddHistory(ex.Message, plan?.PayloadBytes ?? Array.Empty<byte>(), false);
            LogError($"发送事件 {definition.DisplayName}：{ex.Message}");
        }
        finally { definition.IsBusy = false; CommandManager.InvalidateRequerySuggested(); }
    }
}
