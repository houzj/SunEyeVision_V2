using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

public sealed class CommunicationSendParameterViewModel : ViewModelBase
{
    public Guid FieldId { get; set; } = Guid.NewGuid();
    private string _name = string.Empty, _dataType = "string", _content = string.Empty, _result = string.Empty, _status = "未发送";
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string DataType { get => _dataType; set { if (!string.IsNullOrWhiteSpace(value)) SetProperty(ref _dataType, value); } }
    public string Content { get => _content; set => SetProperty(ref _content, value); }
    public string Result { get => _result; set => SetProperty(ref _result, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
}

public sealed class CommunicationSendEventViewModel : ViewModelBase
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    private string _name = string.Empty, _deviceId = string.Empty, _targetId = string.Empty;
    private string _mode = "Direct", _separator = ";", _terminator = string.Empty;
    private string _previewText = string.Empty, _status = "未发送";
    private bool _isBusy, _showHistoryAsHex, _refreshing, _isAssemblyConfigTab, _isMessageExpanded;
    public event EventHandler? ConfigurationChanged;
    public ObservableCollection<CommunicationSendParameterViewModel> Parameters { get; } = new();
    public ObservableCollection<CommunicationAssemblyOutputViewModel> AssemblyOutputs { get; } = new();
    public ObservableCollection<AvailableDataSource> InputSources { get; } = new();
    public ObservableCollection<CommunicationParseHistoryEntry> History { get; } = new();
    public static IReadOnlyList<string> DataTypes { get; } = new[] { "string", "int", "float", "Boolean", "Double", "Int16", "UInt16", "Int32", "UInt32", "Float32" };
    public static IReadOnlyList<string> Modes { get; } = new[] { "直接输出", "组装输出" };
    public ObservableCollection<CommunicationSeparatorOption> SeparatorOptions { get; } = CommunicationSeparators.CreateOptions();
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string DisplayName => string.IsNullOrWhiteSpace(Name) ? $"文本-{ModeDisplayName}" : Name;
    public string DeviceId
    {
        get => _deviceId;
        set { if (SetProperty(ref _deviceId, value ?? string.Empty)) TargetId = string.Empty; }
    }
    public string TargetId { get => _targetId; set => SetProperty(ref _targetId, value ?? string.Empty); }
    public string Mode
    {
        get => _mode;
        set
        {
            if (!SetProperty(ref _mode, value)) return;
            OnPropertyChanged(nameof(ModeDisplayName)); OnPropertyChanged(nameof(IsAssembly)); OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(IsDirect)); OnPropertyChanged(nameof(IsInputConfigTab));
            OnPropertyChanged(nameof(ShowAssemblyConfig)); OnPropertyChanged(nameof(ProcessingDescription));
        }
    }
    public string ModeDisplayName { get => Mode == "Assembly" ? "组装输出" : "直接输出"; set => Mode = value == "组装输出" ? "Assembly" : "Direct"; }
    public bool IsAssembly => Mode == "Assembly";
    public bool IsDirect => !IsAssembly;
    public bool IsAssemblyConfigTab
    {
        get => _isAssemblyConfigTab;
        set { if (SetProperty(ref _isAssemblyConfigTab, value)) { OnPropertyChanged(nameof(IsInputConfigTab)); OnPropertyChanged(nameof(ShowAssemblyConfig)); } }
    }
    public bool IsInputConfigTab => !IsAssembly || !IsAssemblyConfigTab;
    public bool ShowAssemblyConfig => IsAssembly && IsAssemblyConfigTab;
    public string ProcessingDescription => IsAssembly ? "说明：按照组装列表组合输入参数后输出至外部设备" : "说明：将输入参数按列表顺序输出至外部设备";
    public bool IsMessageExpanded { get => _isMessageExpanded; set { if (SetProperty(ref _isMessageExpanded, value)) OnPropertyChanged(nameof(MessagePanelHeader)); } }
    public string MessagePanelHeader => IsMessageExpanded ? "收起 〉" : "打开消息";
    public string Separator
    {
        get => _separator;
        set
        {
            // Keep the option collection stable so refreshing options cannot clear the selection.
            if (value == null) return;
            CommunicationSeparators.EnsureValue(SeparatorOptions, value);
            SetProperty(ref _separator, value);
        }
    }
    public string Terminator { get => _terminator; set => SetProperty(ref _terminator, value ?? string.Empty); }
    public string PreviewText { get => _previewText; set => SetProperty(ref _previewText, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public bool IsBusy { get => _isBusy; set { if (SetProperty(ref _isBusy, value)) OnPropertyChanged(nameof(IsNotBusy)); } }
    public bool IsNotBusy => !IsBusy;
    public bool ShowHistoryAsHex { get => _showHistoryAsHex; set { if (SetProperty(ref _showHistoryAsHex, value)) OnPropertyChanged(nameof(HistoryText)); } }
    public string HistoryText => string.Join(Environment.NewLine, History.Select(item => ShowHistoryAsHex ? item.HexLine : item.ResultLine));

    public CommunicationSendEventViewModel()
    {
        PropertyChanged += Configuration_PropertyChanged;
        Parameters.CollectionChanged += Fields_CollectionChanged;
        AssemblyOutputs.CollectionChanged += Fields_CollectionChanged;
    }

    private void Fields_CollectionChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.OldItems != null) foreach (INotifyPropertyChanged field in e.OldItems) field.PropertyChanged -= Configuration_PropertyChanged;
        if (e.NewItems != null) foreach (INotifyPropertyChanged field in e.NewItems) field.PropertyChanged += Configuration_PropertyChanged;
        RefreshSources(); NotifyConfigurationChanged();
    }

    private void Configuration_PropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (_refreshing || e.PropertyName is not ("Name" or "DataType" or "Content" or "Mode" or "DeviceId" or "TargetId"
            or "Separator" or "Terminator" or "BindingSource" or "BindingDisplayText" or "IsContentBound" or "SourceFieldId")) return;
        if (sender is CommunicationSendParameterViewModel && e.PropertyName is ("Name" or "DataType")) RefreshSources();
        if (sender == this && e.PropertyName == nameof(Name)) OnPropertyChanged(nameof(DisplayName));
        NotifyConfigurationChanged();
    }

    private void NotifyConfigurationChanged()
    {
        PreviewText = string.Empty; Status = "未发送";
        foreach (var input in Parameters) { input.Result = string.Empty; input.Status = "未发送"; }
        foreach (var output in AssemblyOutputs) output.Result = string.Empty;
        ConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void RefreshSources()
    {
        if (_refreshing) return;
        _refreshing = true;
        try
        {
            InputSources.Clear();
            foreach (var input in Parameters) InputSources.Add(new AvailableDataSource
            {
                SourceNodeId = "SendEventInputs", SourceNodeName = "参数列表", PropertyName = input.Name,
                DisplayName = input.Name, LeafDisplayName = input.Name, SourceFieldId = input.FieldId,
                PropertyType = typeof(object)
            });
            foreach (var output in AssemblyOutputs)
            {
                if (output.AvailableDataSources == null) output.AvailableDataSources = InputSources;
                if (!output.IsContentBound || !output.SourceFieldId.HasValue) continue;
                var source = InputSources.FirstOrDefault(item => item.SourceFieldId == output.SourceFieldId);
                if (source == null) { output.BindingDisplayText = "字段已删除"; continue; }
                var fieldId = output.SourceFieldId;
                output.ApplyBindingSelection(source.GetBindingPath(), source.GetBindingDisplayName(source.SourceNodeName));
                // Display paths may temporarily collide while names are edited. Preserve the stable identity.
                output.SourceFieldId = fieldId;
            }
        }
        finally { _refreshing = false; }
    }

    public CommunicationSendEventDefinition Capture() => new()
    {
        EventId = EventId, Name = Name, DeviceId = DeviceId, TargetId = TargetId, Mode = Mode,
        Separator = Separator, Terminator = Terminator,
        Parameters = Parameters.Select(item => new CommunicationSendParameterDefinition
        { FieldId = item.FieldId, Name = item.Name, DataType = item.DataType, Content = item.Content }).ToList(),
        AssemblyOutputs = AssemblyOutputs.Select(item => new CommunicationAssemblyOutputDefinition
        {
            FieldId = item.FieldId, Name = item.Name, DataType = item.DataType, Content = item.Content,
            IsContentBound = item.IsContentBound, SourceFieldId = item.SourceFieldId,
            BindingSource = item.BindingSource, BindingDisplayText = item.BindingDisplayText,
            SourceNodeId = item.SourceNodeId, SourceNameSnapshot = item.SourceNameSnapshot
        }).ToList()
    };

    public static CommunicationSendEventViewModel FromDefinition(CommunicationSendEventDefinition definition)
    {
        var result = new CommunicationSendEventViewModel
        {
            EventId = definition.EventId, Name = definition.Name, DeviceId = definition.DeviceId,
            TargetId = definition.TargetId, Mode = definition.Mode, Separator = definition.Separator, Terminator = definition.Terminator
        };
        foreach (var item in definition.Parameters) result.Parameters.Add(new CommunicationSendParameterViewModel
        { FieldId = item.FieldId, Name = item.Name, DataType = item.DataType, Content = item.Content });
        foreach (var item in definition.AssemblyOutputs) result.AssemblyOutputs.Add(new CommunicationAssemblyOutputViewModel
        {
            AvailableDataSources = result.InputSources, FieldId = item.FieldId, Name = item.Name, DataType = item.DataType,
            Content = item.Content, BindingSource = item.BindingSource, BindingDisplayText = item.BindingDisplayText,
            IsContentBound = item.IsContentBound, SourceFieldId = item.SourceFieldId,
            SourceNodeId = item.SourceNodeId, SourceNameSnapshot = item.SourceNameSnapshot
        });
        result.RefreshSources();
        return result;
    }

    public void AddHistory(string message, byte[] bytes, bool success)
    {
        var sequence = History.Count == 0 ? 1 : History[^1].Sequence + 1;
        var timestamp = DateTimeOffset.Now;
        var prefix = $"#{sequence} [{timestamp:yyyy-MM-dd HH:mm:ss.fff}] {(success ? "OK" : "NG")}";
        History.Add(new CommunicationParseHistoryEntry { Sequence = sequence, Timestamp = timestamp, IsSuccess = success,
            ResultLine = $"{prefix} {message}", HexLine = $"{prefix} {Convert.ToHexString(bytes)} {(!success ? message : string.Empty)}" });
        while (History.Count > 200) History.RemoveAt(0);
        OnPropertyChanged(nameof(HistoryText));
    }
    public void ClearHistory() { History.Clear(); OnPropertyChanged(nameof(HistoryText)); }
}
