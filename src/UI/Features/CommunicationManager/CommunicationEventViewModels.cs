using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.UI.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.UI.Features.CommunicationManager;

public enum CommunicationEventProcessingMethod
{
    Text,
    ByteMatch,
    Script
}

public enum CommunicationEventType
{
    ProtocolParse,
    ProtocolCompare
}

public sealed class CommunicationReceiveEventViewModel : ViewModelBase
{
    public Guid EventId { get; set; } = Guid.NewGuid();
    internal Dictionary<Guid, (string Signature, bool Value)> BooleanEdgeHistory { get; } = new();
    private string _selectedDeviceId = string.Empty;
    private string _selectedAddress = string.Empty;
    private string _separator = ",";
    private bool _characterLengthCompare;
    private int _characterLength = 1;
    private bool _isMessageExpanded;
    private bool _showMessageAsHex;
    private bool _isAssemblyConfigTab;
    private bool _echoToDevice;
    private string _name = string.Empty;
    private string _dataMode = "Text";
    private string _byteOrder = "BigEndian";
    private int _byteOffset;
    private bool _byteLengthCompare;
    private int _byteLength = 1;
    private bool _asciiData;
    private string _byteMatchLogic = "与";

    public CommunicationEventProcessingMethod ProcessingMethod { get; set; }
    public CommunicationEventType EventType { get; set; }
    public bool IsProtocolCompare => EventType == CommunicationEventType.ProtocolCompare && ProcessingMethod != CommunicationEventProcessingMethod.Script;
    public bool IsProtocolParse => !IsProtocolCompare;
    public bool IsByteMatch => ProcessingMethod == CommunicationEventProcessingMethod.ByteMatch;
    public bool IsNotByteMatch => !IsByteMatch;
    public int ProcessingMethodIndex
    {
        get => (int)ProcessingMethod;
        set
        {
            ProcessingMethod = (CommunicationEventProcessingMethod)value;
            if (ProcessingMethod == CommunicationEventProcessingMethod.ByteMatch)
                EventType = CommunicationEventType.ProtocolCompare;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(AvailableEventTypeNames));
            OnPropertyChanged(nameof(SelectedEventTypeName));
            OnPropertyChanged(nameof(HasEventType));
            OnPropertyChanged(nameof(ProcessingDescription));
            OnPropertyChanged(nameof(IsProtocolCompare));
            OnPropertyChanged(nameof(IsProtocolParse));
            OnPropertyChanged(nameof(IsByteMatch));
            OnPropertyChanged(nameof(IsNotByteMatch));
        }
    }
    public int EventTypeIndex
    {
        get => (int)EventType;
        set
        {
            EventType = (CommunicationEventType)value;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(IsProtocolCompare));
            OnPropertyChanged(nameof(IsProtocolParse));
        }
    }
    public string DisplayName => string.IsNullOrWhiteSpace(_name)
        ? ProcessingMethod == CommunicationEventProcessingMethod.Script ? ProcessingMethodDisplayName : $"{ProcessingMethodDisplayName}-{EventTypeDisplayName}"
        : _name;
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value);
    }
    public IReadOnlyList<string> AvailableEventTypeNames => ProcessingMethod == CommunicationEventProcessingMethod.ByteMatch
        ? new[] { "协议比对" }
        : new[] { "协议解析", "协议比对" };
    public bool HasEventType => ProcessingMethod != CommunicationEventProcessingMethod.Script;
    public string SelectedEventTypeName
    {
        get => EventType == CommunicationEventType.ProtocolCompare ? "协议比对" : "协议解析";
        set
        {
            EventType = value == "协议比对" ? CommunicationEventType.ProtocolCompare : CommunicationEventType.ProtocolParse;
            OnPropertyChanged(nameof(DisplayName));
            OnPropertyChanged(nameof(IsProtocolCompare));
            OnPropertyChanged(nameof(IsProtocolParse));
        }
    }
    public string ProcessingDescription => ProcessingMethod switch
    {
        CommunicationEventProcessingMethod.Script => "说明：对输入数据进行解析和组装，方式为 Python 脚本",
        CommunicationEventProcessingMethod.ByteMatch => "说明：按照配置的字节规则比对收到的数据",
        _ => "说明：按照配置的规则解析收到的数据"
    };
    public string ProcessingMethodDisplayName => ProcessingMethod switch
    {
        CommunicationEventProcessingMethod.ByteMatch => "字节匹配",
        CommunicationEventProcessingMethod.Script => "脚本",
        _ => "文本"
    };
    public string EventTypeDisplayName => EventType == CommunicationEventType.ProtocolCompare ? "协议比对" : "协议解析";
    public string SelectedDeviceId { get => _selectedDeviceId; set => SetProperty(ref _selectedDeviceId, value); }
    public string SelectedAddress { get => _selectedAddress; set => SetProperty(ref _selectedAddress, value); }
    public string DataMode
    {
        get => _dataMode;
        set
        {
            if (!SetProperty(ref _dataMode, value)) return;
            OnPropertyChanged(nameof(IsBinaryMode));
        }
    }
    public bool IsBinaryMode => string.Equals(DataMode, "Binary", StringComparison.OrdinalIgnoreCase);
    public string ByteOrder { get => _byteOrder; set => SetProperty(ref _byteOrder, value); }
    public int ByteOffset { get => _byteOffset; set => SetProperty(ref _byteOffset, Math.Max(0, value)); }
    public bool ByteLengthCompare { get => _byteLengthCompare; set => SetProperty(ref _byteLengthCompare, value); }
    public int ByteLength { get => _byteLength; set => SetProperty(ref _byteLength, Math.Max(1, value)); }
    public bool AsciiData { get => _asciiData; set => SetProperty(ref _asciiData, value); }
    public string ByteMatchLogic { get => _byteMatchLogic; set => SetProperty(ref _byteMatchLogic, value is "或" ? "或" : "与"); }
    public ObservableCollection<CommunicationSeparatorOption> SeparatorOptions { get; } = CommunicationSeparators.CreateOptions();
    public string Separator
    {
        get => _separator;
        set
        {
            if (value == null) return;
            CommunicationSeparators.EnsureValue(SeparatorOptions, value);
            SetProperty(ref _separator, value);
        }
    }
    public bool CharacterLengthCompare { get => _characterLengthCompare; set => SetProperty(ref _characterLengthCompare, value); }
    public int CharacterLength { get => _characterLength; set => SetProperty(ref _characterLength, value < 1 ? 1 : value); }
    public bool IsMessageExpanded
    {
        get => _isMessageExpanded;
        set
        {
            if (!SetProperty(ref _isMessageExpanded, value)) return;
            OnPropertyChanged(nameof(MessagePanelHeader));
        }
    }
    public string MessagePanelHeader => IsMessageExpanded ? "收起 〉" : "打开消息";
    public bool ShowMessageAsHex
    {
        get => _showMessageAsHex;
        set
        {
            if (!SetProperty(ref _showMessageAsHex, value)) return;
            RefreshParseHistoryText();
        }
    }
    public ObservableCollection<CommunicationParseHistoryEntry> ParseHistory { get; } = new();
    public string ParseHistoryText => string.Join(Environment.NewLine, ParseHistory.Select(entry =>
        ShowMessageAsHex ? entry.HexLine : entry.ResultLine));
    public ObservableCollection<CommunicationEventOutputViewModel> Outputs { get; } = new();
    public ObservableCollection<CommunicationCompareInputViewModel> CompareInputs { get; } = new();
    public ObservableCollection<AvailableDataSource> CompareInputDataSources { get; } = new();
    public ObservableCollection<CommunicationAssemblyOutputViewModel> AssemblyOutputs { get; } = new();
    public bool IsAssemblyConfigTab
    {
        get => _isAssemblyConfigTab;
        set
        {
            if (!SetProperty(ref _isAssemblyConfigTab, value)) return;
            OnPropertyChanged(nameof(IsInputConfigTab));
        }
    }
    public bool IsInputConfigTab => !IsAssemblyConfigTab;
    public bool EchoToDevice { get => _echoToDevice; set => SetProperty(ref _echoToDevice, value); }

    public void AddParseHistory(DateTimeOffset timestamp, byte[] data, string summary, bool success, string? failureReason)
    {
        var sequence = ParseHistory.Count == 0 ? 1 : ParseHistory[^1].Sequence + 1;
        var prefix = $"#{sequence} [{timestamp:yyyy-MM-dd HH:mm:ss.fff}] {(success ? "OK" : "NG")}";
        ParseHistory.Add(new CommunicationParseHistoryEntry
        {
            Sequence = sequence,
            Timestamp = timestamp,
            IsSuccess = success,
            FailureReason = failureReason ?? string.Empty,
            ResultLine = success ? $"{prefix} {summary}" : $"{prefix} {failureReason}；原始：{summary}",
            HexLine = success ? $"{prefix} {Convert.ToHexString(data)}" : $"{prefix} {failureReason}；{Convert.ToHexString(data)}"
        });
        while (ParseHistory.Count > 200)
            ParseHistory.RemoveAt(0);
        RefreshParseHistoryText();
    }

    public void ClearParseHistory()
    {
        ParseHistory.Clear();
        RefreshParseHistoryText();
    }

    private void RefreshParseHistoryText()
    {
        OnPropertyChanged(nameof(ParseHistoryText));
    }
}

public sealed class CommunicationParseHistoryEntry
{
    public int Sequence { get; init; }
    public DateTimeOffset Timestamp { get; init; }
    public bool IsSuccess { get; init; }
    public string FailureReason { get; init; } = string.Empty;
    public string ResultLine { get; init; } = string.Empty;
    public string HexLine { get; init; } = string.Empty;
}

public sealed class CommunicationEventOutputViewModel : ViewModelBase
{
    public Guid FieldId { get; set; } = Guid.NewGuid();
    private string _name = string.Empty;
    private string _dataType = "int";
    private string _result = string.Empty;
    private string _status = "未接收";
    private object? _value;
    private string _compareRule = "不比较";
    private int _minimum;
    private int _maximum = 1;
    private string _compareValue = string.Empty;
    public ObservableCollection<string> CompareRuleOptions { get; } = new() { "不比较", "范围内", "范围外" };

    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string DataType
    {
        get => _dataType;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!SetProperty(ref _dataType, value)) return;
            CompareRuleOptions.Clear();
            CompareRuleOptions.Add("不比较");
            if (string.Equals(value, "string", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(value, "Boolean", StringComparison.OrdinalIgnoreCase))
            {
                CompareRuleOptions.Add("等于");
                CompareRuleOptions.Add("不等于");
                if (string.Equals(value, "Boolean", StringComparison.OrdinalIgnoreCase))
                {
                    CompareRuleOptions.Add("上升沿");
                    CompareRuleOptions.Add("下降沿");
                }
            }
            else
            {
                CompareRuleOptions.Add("范围内");
                CompareRuleOptions.Add("范围外");
            }
            if (!CompareRuleOptions.Contains(CompareRule)) CompareRule = "不比较";
        }
    }
    public string CompareRule
    {
        get => _compareRule;
        set
        {
            var normalized = value == "范围" ? "范围内" : value;
            // An unsupported saved rule must fail comparison, not silently pass as 不比较.
            SetProperty(ref _compareRule, normalized ?? string.Empty);
        }
    }
    public int Minimum { get => _minimum; set => SetProperty(ref _minimum, value); }
    public int Maximum { get => _maximum; set => SetProperty(ref _maximum, value); }
    public string CompareValue { get => _compareValue; set => SetProperty(ref _compareValue, value); }
    public string Result { get => _result; set => SetProperty(ref _result, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public object? Value { get => _value; set => SetProperty(ref _value, value); }
}

public sealed class CommunicationCompareInputViewModel : ViewModelBase
{
    public Guid FieldId { get; set; } = Guid.NewGuid();
    private string _name = string.Empty;
    private string _dataType = "int";
    private string _compareRule = "不比较";
    private int _minimum;
    private int _maximum = 1;
    private string _compareValue = string.Empty;
    private string _result = string.Empty;
    private string _status = "未接收";
    private object? _value;
    private int _byteStart;
    private int _byteEnd = 1;
    private string _byteOrder = "ABCD";
    private bool _isByteRule;
    public ObservableCollection<string> CompareRuleOptions { get; } = new() { "不比较", "范围内", "范围外" };
    public int ByteStart { get => _byteStart; set => SetProperty(ref _byteStart, Math.Max(0, value)); }
    public int ByteEnd { get => _byteEnd; set => SetProperty(ref _byteEnd, Math.Max(0, value)); }
    public string ByteOrder { get => _byteOrder; set => SetProperty(ref _byteOrder, value); }
    public bool IsByteRule
    {
        get => _isByteRule;
        set
        {
            if (!SetProperty(ref _isByteRule, value)) return;
            RefreshCompareRuleOptions();
        }
    }
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string DataType
    {
        get => _dataType;
        set
        {
            if (string.IsNullOrWhiteSpace(value)) return;
            if (!SetProperty(ref _dataType, value)) return;
            RefreshCompareRuleOptions();
            OnPropertyChanged(nameof(IsString));
            OnPropertyChanged(nameof(IsBoolean));
            OnPropertyChanged(nameof(IsNumericRange));
        }
    }
    public bool IsString => string.Equals(DataType, "string", System.StringComparison.OrdinalIgnoreCase);
    public bool IsBoolean => string.Equals(DataType, "Boolean", System.StringComparison.OrdinalIgnoreCase);
    public bool IsNumericRange => !IsString && !IsBoolean;
    private void RefreshCompareRuleOptions()
    {
        CompareRuleOptions.Clear();
        CompareRuleOptions.Add("不比较");
        if (IsString || IsBoolean)
        {
            CompareRuleOptions.Add("等于");
            CompareRuleOptions.Add("不等于");
            if (IsBoolean)
            {
                CompareRuleOptions.Add("上升沿");
                CompareRuleOptions.Add("下降沿");
            }
        }
        else
        {
            if (IsByteRule) CompareRuleOptions.Add("等于");
            CompareRuleOptions.Add("范围内");
            CompareRuleOptions.Add("范围外");
            if (IsByteRule)
            {
                CompareRuleOptions.Add("上升沿");
                CompareRuleOptions.Add("下降沿");
            }
        }
        if (!CompareRuleOptions.Contains(CompareRule))
            CompareRule = "不比较";
    }
    public string CompareRule
    {
        get => _compareRule;
        set
        {
            var normalized = value == "范围" ? "范围内" : value;
            // Preserve unknown saved rules so evaluation reports NG instead of a false OK.
            SetProperty(ref _compareRule, normalized ?? string.Empty);
        }
    }
    public int Minimum { get => _minimum; set => SetProperty(ref _minimum, value); }
    public int Maximum { get => _maximum; set => SetProperty(ref _maximum, value); }
    public string CompareValue { get => _compareValue; set => SetProperty(ref _compareValue, value); }
    public string Result { get => _result; set => SetProperty(ref _result, value); }
    public string Status { get => _status; set => SetProperty(ref _status, value); }
    public object? Value { get => _value; set => SetProperty(ref _value, value); }
}

public sealed class CommunicationAssemblyOutputViewModel : ViewModelBase
{
    public Guid FieldId { get; set; } = Guid.NewGuid();
    private string _name = string.Empty;
    private string _dataType = "int";
    private string _content = string.Empty;
    private string _result = string.Empty;
    private string _bindingSource = string.Empty;
    private string _bindingDisplayText = string.Empty;
    private bool _isContentBound;
    private Guid? _sourceFieldId;
    private string _sourceNodeId = string.Empty;
    private string _sourceNameSnapshot = string.Empty;
    private ObservableCollection<AvailableDataSource>? _availableDataSources;
    public string Name { get => _name; set => SetProperty(ref _name, value); }
    public string DataType { get => _dataType; set => SetProperty(ref _dataType, value); }
    public string Content { get => _content; set => SetProperty(ref _content, value); }
    public string Result { get => _result; set => SetProperty(ref _result, value); }
    public string BindingSource
    {
        get => _bindingSource;
        set
        {
            var normalized = NormalizeBindingPath(value);
            if (!SetProperty(ref _bindingSource, normalized))
            {
                ResolveBindingIdentity();
                return;
            }
            SourceFieldId = null;
            SourceNodeId = string.Empty;
            SourceNameSnapshot = string.Empty;
            IsContentBound = !string.IsNullOrWhiteSpace(normalized);
            ResolveBindingIdentity();
        }
    }
    public string BindingDisplayText { get => _bindingDisplayText; set => SetProperty(ref _bindingDisplayText, value); }
    public bool IsContentBound { get => _isContentBound; set => SetProperty(ref _isContentBound, value); }
    public Guid? SourceFieldId { get => _sourceFieldId; set => SetProperty(ref _sourceFieldId, value); }
    public string SourceNodeId { get => _sourceNodeId; set => SetProperty(ref _sourceNodeId, value); }
    public string SourceNameSnapshot { get => _sourceNameSnapshot; set => SetProperty(ref _sourceNameSnapshot, value); }
    public ObservableCollection<AvailableDataSource>? AvailableDataSources
    {
        get => _availableDataSources;
        set
        {
            if (!SetProperty(ref _availableDataSources, value)) return;
            BindingSource = BindingSource;
        }
    }

    public void ApplyBindingSelection(string? bindingSource, string? displayText)
    {
        BindingSource = string.IsNullOrWhiteSpace(bindingSource) ? displayText ?? string.Empty : bindingSource;
        BindingDisplayText = displayText ?? string.Empty;
        IsContentBound = !string.IsNullOrWhiteSpace(BindingSource);
        ResolveBindingIdentity();
    }

    internal void ResolveBindingIdentity()
    {
        if (string.IsNullOrWhiteSpace(BindingSource) || AvailableDataSources == null)
            return;

        foreach (var source in AvailableDataSources)
        {
            if (!string.Equals(source.GetBindingPath(), BindingSource, StringComparison.Ordinal))
                continue;

            SourceFieldId = source.SourceFieldId;
            SourceNodeId = source.SourceNodeId;
            SourceNameSnapshot = source.PropertyName;
            if (string.IsNullOrWhiteSpace(BindingDisplayText))
                BindingDisplayText = source.GetDisplayText();
            return;
        }
    }

    private string NormalizeBindingPath(string? path)
    {
        var value = path ?? string.Empty;
        if (AvailableDataSources == null) return value;
        foreach (var source in AvailableDataSources)
            if (string.Equals(source.GetBindingPath(), value, StringComparison.Ordinal) ||
                string.Equals(source.GetBindingDisplayName(source.SourceNodeName), value, StringComparison.Ordinal))
                return source.GetBindingPath();
        return value;
    }
}
