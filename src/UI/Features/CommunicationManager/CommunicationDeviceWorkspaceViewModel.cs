using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

/// <summary>
/// Transient communication-console state owned by one device. Nothing in this type is persisted.
/// </summary>
public sealed class CommunicationDeviceWorkspaceViewModel : ViewModelBase
{
    private const int MaxHistoryRecords = 512;
    private readonly List<TrafficRecord> _receivedRecords = new();
    private readonly List<TrafficRecord> _sentRecords = new();
    private string _sendText = string.Empty;
    private string _receivedText = string.Empty;
    private string _sentText = string.Empty;
    private bool _isBusy;
    private bool _isReceiveHexDisplay;
    private bool _isSendHexInput;
    private string _modbusOperation = "读取保持寄存器";
    private int _modbusAddress;
    private int _modbusCount = 1;
    private string _modbusWriteValue = "0";
    private string _modbusResultText = string.Empty;
    private string _s7Area = "DataBlock";
    private int _s7DataBlock = 1;
    private int _s7StartByte;
    private int _s7ByteCount = 1;
    private string _s7WriteHex = string.Empty;
    private string _s7ResultText = string.Empty;
    private string _opcUaNodeId = "i=85";
    private string _opcUaValueText = string.Empty;
    private int _opcUaSamplingInterval = 100;
    private string _opcUaResultText = string.Empty;
    private string _cipService = "0E";
    private string _cipClassId = "1";
    private string _cipInstanceId = "1";
    private string _cipAttributeId = "1";
    private string _cipRequestDataHex = string.Empty;
    private string _cipResultText = string.Empty;

    public CommunicationDeviceWorkspaceViewModel(Communication communication)
    {
        Communication = communication ?? throw new ArgumentNullException(nameof(communication));
    }

    public Communication Communication { get; }

    public ObservableCollection<string> ModbusOperations { get; } = new(new[]
    {
        "读取线圈", "读取离散输入", "读取保持寄存器", "读取输入寄存器", "写单线圈", "写单寄存器", "写多个线圈", "写多个寄存器"
    });

    public string ModbusOperation { get => _modbusOperation; set => SetProperty(ref _modbusOperation, value); }
    public int ModbusAddress { get => _modbusAddress; set => SetProperty(ref _modbusAddress, value); }
    public int ModbusCount { get => _modbusCount; set => SetProperty(ref _modbusCount, value); }
    public string ModbusWriteValue { get => _modbusWriteValue; set => SetProperty(ref _modbusWriteValue, value); }
    public string ModbusResultText { get => _modbusResultText; set => SetProperty(ref _modbusResultText, value); }

    public ObservableCollection<string> S7Areas { get; } = new(new[] { "DataBlock", "Marker", "Input", "Output" });
    public string S7Area { get => _s7Area; set => SetProperty(ref _s7Area, value); }
    public int S7DataBlock { get => _s7DataBlock; set => SetProperty(ref _s7DataBlock, value); }
    public int S7StartByte { get => _s7StartByte; set => SetProperty(ref _s7StartByte, value); }
    public int S7ByteCount { get => _s7ByteCount; set => SetProperty(ref _s7ByteCount, value); }
    public string S7WriteHex { get => _s7WriteHex; set => SetProperty(ref _s7WriteHex, value); }
    public string S7ResultText { get => _s7ResultText; set => SetProperty(ref _s7ResultText, value); }

    public string OpcUaNodeId { get => _opcUaNodeId; set => SetProperty(ref _opcUaNodeId, value); }
    public string OpcUaValueText { get => _opcUaValueText; set => SetProperty(ref _opcUaValueText, value); }
    public int OpcUaSamplingInterval { get => _opcUaSamplingInterval; set => SetProperty(ref _opcUaSamplingInterval, value); }
    public string OpcUaResultText { get => _opcUaResultText; set => SetProperty(ref _opcUaResultText, value); }
    public ObservableCollection<CommunicationRuntimeManager.OpcUaBrowseItem> OpcUaBrowseItems { get; } = new();

    public string CipService { get => _cipService; set => SetProperty(ref _cipService, value); }
    public string CipClassId { get => _cipClassId; set => SetProperty(ref _cipClassId, value); }
    public string CipInstanceId { get => _cipInstanceId; set => SetProperty(ref _cipInstanceId, value); }
    public string CipAttributeId { get => _cipAttributeId; set => SetProperty(ref _cipAttributeId, value); }
    public string CipRequestDataHex { get => _cipRequestDataHex; set => SetProperty(ref _cipRequestDataHex, value); }
    public string CipResultText { get => _cipResultText; set => SetProperty(ref _cipResultText, value); }

    public string SendText
    {
        get => _sendText;
        set => SetProperty(ref _sendText, value);
    }

    public string ReceivedText
    {
        get => _receivedText;
        private set => SetProperty(ref _receivedText, value);
    }

    public string SentText
    {
        get => _sentText;
        private set => SetProperty(ref _sentText, value);
    }

    public bool IsBusy
    {
        get => _isBusy;
        set => SetProperty(ref _isBusy, value);
    }

    public bool IsReceiveHexDisplay
    {
        get => _isReceiveHexDisplay;
        set
        {
            if (SetProperty(ref _isReceiveHexDisplay, value))
                ReceivedText = FormatHistory(_receivedRecords, value);
        }
    }

    public bool IsSendHexInput
    {
        get => _isSendHexInput;
        set => SetProperty(ref _isSendHexInput, value);
    }

    public void AppendReceived(byte[] payload, DateTimeOffset timestamp)
    {
        AddRecord(_receivedRecords, payload, timestamp, isHex: false);
        ReceivedText = FormatHistory(_receivedRecords, IsReceiveHexDisplay);
    }

    public void AppendSent(byte[] payload, DateTimeOffset timestamp, bool isHex)
    {
        AddRecord(_sentRecords, payload, timestamp, isHex);
        SentText = FormatHistory(_sentRecords, displayHex: null);
    }

    public void ClearReceivedHistory()
    {
        _receivedRecords.Clear();
        ReceivedText = string.Empty;
    }

    public void ClearSentHistory()
    {
        _sentRecords.Clear();
        SentText = string.Empty;
    }

    private static void AddRecord(List<TrafficRecord> records, byte[] payload, DateTimeOffset timestamp, bool isHex)
    {
        ArgumentNullException.ThrowIfNull(payload);
        records.Add(new TrafficRecord((byte[])payload.Clone(), timestamp, isHex));
        if (records.Count > MaxHistoryRecords)
            records.RemoveAt(0);
    }

    private static string FormatHistory(IReadOnlyList<TrafficRecord> records, bool? displayHex)
    {
        if (records.Count == 0) return string.Empty;

        var builder = new StringBuilder();
        foreach (var record in records)
        {
            var isHex = displayHex ?? record.IsHex;
            var mode = isHex ? "HEX" : "UTF-8";
            var direction = displayHex.HasValue ? "RECV" : "SEND";
            var payload = isHex
                ? CommunicationPayloadCodec.FormatHex(record.Payload)
                : FormatTextPayload(record.Payload);
            builder.Append('[')
                .Append(record.Timestamp.ToLocalTime().ToString("yyyy-MM-dd HH:mm:ss.fff"))
                .Append("]# ").Append(direction).Append(' ').Append(mode).Append('>')
                .AppendLine().Append(payload).AppendLine().AppendLine();
        }

        var formatted = builder.ToString();
        return formatted.Length > 500_000 ? formatted[^400_000..] : formatted;
    }

    private static string FormatTextPayload(byte[] payload)
    {
        if (payload.Length == 0)
            return "（空数据）";

        var text = Encoding.UTF8.GetString(payload);
        if (text.Contains('\uFFFD'))
            return $"（数据无法按 UTF-8 解码，原始 HEX：{CommunicationPayloadCodec.FormatHex(payload)}；切换 HEX 查看）";

        if (text.Length > 0 && text.All(character => char.IsControl(character) || char.IsWhiteSpace(character) || character == '\uFEFF'))
            return $"（收到不可显示数据，原始 HEX：{CommunicationPayloadCodec.FormatHex(payload)}；切换 HEX 查看）";

        var builder = new StringBuilder(text.Length);
        foreach (var character in text)
        {
            switch (character)
            {
                case '\0': builder.Append("\\0"); break;
                case '\r': builder.Append("\\r"); break;
                case '\n': builder.Append("\\n"); break;
                case '\t': builder.Append("\\t"); break;
                default:
                    if (char.IsControl(character))
                    {
                        if (character <= 0xFF)
                            builder.Append("\\x").Append(((int)character).ToString("X2"));
                        else
                            builder.Append("\\u").Append(((int)character).ToString("X4"));
                    }
                    else
                    {
                        builder.Append(character);
                    }
                    break;
            }
        }

        return builder.ToString();
    }

    private sealed record TrafficRecord(byte[] Payload, DateTimeOffset Timestamp, bool IsHex);
}
