using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;
using SunEyeVision.DeviceDriver.Communications;

namespace SunEyeVision.UI.Features.CommunicationManager;

/// <summary>
/// Transient communication-console state owned by one device. Nothing in this type is persisted.
/// </summary>
public sealed class CommunicationDeviceWorkspaceViewModel : ViewModelBase
{
    public event EventHandler? AddressConfigurationChanged;
    private const int MaxHistoryRecords = 512;
    private readonly List<TrafficRecord> _receivedRecords = new();
    private readonly List<TrafficRecord> _sentRecords = new();
    private readonly Dictionary<int, ushort> _registerValues = new();
    private string _sendText = string.Empty;
    private string _receivedText = string.Empty;
    private string _sentText = string.Empty;
    private bool _isBusy;
    private bool _isReceiveHexDisplay;
    private bool _isSendHexInput;
    private string _modbusOperation = "读取保持寄存器";
    private int _modbusAddress;
    private int _modbusCount = 1;
    private int _modbusRegisterStartAddress;
    private int _modbusRegisterCount = 50;
    private string _addressRangeWarning = string.Empty;
    private string _modbusWriteValue = "0";
    private string _modbusResultText = string.Empty;
    private string _s7Area = "DataBlock";
    private int _s7DataBlock = 1;
    private int _s7StartByte;
    private int _s7ByteCount = 1;
    private string _s7WriteHex = string.Empty;
    private string _s7ResultText = string.Empty;
    private string _cipService = "0E";
    private string _cipClassId = "1";
    private string _cipInstanceId = "1";
    private string _cipAttributeId = "1";
    private string _cipRequestDataHex = string.Empty;
    private string _cipResultText = string.Empty;

    public CommunicationDeviceWorkspaceViewModel(Communication communication)
    {
        Communication = communication ?? throw new ArgumentNullException(nameof(communication));
        OpcUaPoints.CollectionChanged += (_, _) => OnPropertyChanged(nameof(HasNoOpcUaPoints));
        foreach (var point in Communication.ModbusAddressPoints)
            ModbusAddressPoints.Add(point);
        foreach (var point in Communication.OpcUaDataPoints ?? new())
            OpcUaPoints.Add(new OpcUaPointRowViewModel(point));
        _modbusRegisterStartAddress = communication.ModbusRegisterStartAddress;
        _modbusRegisterCount = Math.Clamp(communication.ModbusRegisterCount, 1,
            ushort.MaxValue + 1 - _modbusRegisterStartAddress);
        // Legacy plans did not persist table bounds. Keep every allocated block visible.
        if (!communication.Settings.ContainsKey("modbusRegisterCount") && ModbusAddressPoints.Count > 0)
        {
            var last = ModbusAddressPoints.Max(point => point.EndAddress);
            _modbusRegisterCount = Math.Min(ushort.MaxValue + 1 - _modbusRegisterStartAddress,
                Math.Max(_modbusRegisterCount, last - _modbusRegisterStartAddress + 1));
        }
        RefreshAddressSpaceRows();
    }

    public Communication Communication { get; }

    public ObservableCollection<string> ModbusOperations { get; } = new(new[]
    {
        "读取离散输入", "读取保持寄存器", "读取输入寄存器", "写单寄存器", "写多个寄存器"
    });

    /// <summary>
    /// 当前设备的 Modbus 寄存器地址块。配置写回 Communication，随方案保存。
    /// </summary>
    public ObservableCollection<ModbusAddressPoint> ModbusAddressPoints { get; } = new();
    public ObservableCollection<ModbusAddressSpaceRow> AddressSpaceRows { get; } = new();
    public ObservableCollection<OpcUaPointRowViewModel> OpcUaPoints { get; } = new();
    public bool HasNoOpcUaPoints => OpcUaPoints.Count == 0;
    private OpcUaPointRowViewModel? _selectedOpcUaPoint;
    public OpcUaPointRowViewModel? SelectedOpcUaPoint
    {
        get => _selectedOpcUaPoint;
        set
        {
            if (SetProperty(ref _selectedOpcUaPoint, value))
                OnPropertyChanged(nameof(SelectedOpcUaNodeId));
        }
    }
    public string SelectedOpcUaNodeId => SelectedOpcUaPoint?.NodeId ?? "未选择数据点";

    public int ModbusRegisterStartAddress
    {
        get => _modbusRegisterStartAddress;
        set
        {
            var next = Math.Clamp(value, 0, ushort.MaxValue);
            var nextCount = Math.Min(_modbusRegisterCount, ushort.MaxValue + 1 - next);
            var affected = ModbusAddressPoints.Where(point => point.StartAddress < next || point.EndAddress >= next + nextCount).ToArray();
            if (affected.Length > 0)
            {
                AddressRangeWarning = $"调整会影响已分配地址：{string.Join("、", affected.Select(point => $"{point.Name}({point.RangeText})"))}。请先删除相关地址块或清空地址空间。";
                return;
            }
            AddressRangeWarning = string.Empty;
            if (SetProperty(ref _modbusRegisterStartAddress, next))
            {
                OnPropertyChanged(nameof(MaxModbusRegisterCount));
                SetProperty(ref _modbusRegisterCount, nextCount, nameof(ModbusRegisterCount));
                Communication.ModbusRegisterStartAddress = next;
                Communication.ModbusRegisterCount = nextCount;
                RefreshAddressSpaceRows();
            }
        }
    }

    public int MaxModbusRegisterCount => ushort.MaxValue + 1 - ModbusRegisterStartAddress;

    public string AddressRangeWarning
    {
        get => _addressRangeWarning;
        private set => SetProperty(ref _addressRangeWarning, value);
    }

    public int ModbusRegisterCount
    {
        get => _modbusRegisterCount;
        set
        {
            var next = Math.Clamp(value, 1, MaxModbusRegisterCount);
            var affected = ModbusAddressPoints.Where(point => point.StartAddress < ModbusRegisterStartAddress ||
                point.EndAddress >= ModbusRegisterStartAddress + next).ToArray();
            if (affected.Length > 0)
            {
                AddressRangeWarning = $"调整会影响已分配地址：{string.Join("、", affected.Select(point => $"{point.Name}({point.RangeText})"))}。请先删除相关地址块或清空地址空间。";
                return;
            }
            AddressRangeWarning = string.Empty;
            if (SetProperty(ref _modbusRegisterCount, next))
            {
                Communication.ModbusRegisterCount = next;
                RefreshAddressSpaceRows();
            }
        }
    }

    public ModbusAddressPoint AddModbusAddressPoint(int? startAddress = null)
    {
        var nextAddress = ModbusAddressPoints.Count == 0
            ? ModbusRegisterStartAddress
            : ModbusAddressPoints.Max(point => point.EndAddress + 1);
        var point = new ModbusAddressPoint
        {
            Name = $"地址{ModbusAddressPoints.Count + 1}",
            StartAddress = Math.Clamp(startAddress ?? nextAddress, ModbusRegisterStartAddress,
                ModbusRegisterStartAddress + ModbusRegisterCount - 1)
        };
        Communication.ModbusAddressPoints.Add(point);
        ModbusAddressPoints.Add(point);
        RefreshAddressSpaceRows();
        AddressConfigurationChanged?.Invoke(this, EventArgs.Empty);
        return point;
    }

    public void RemoveModbusAddressPoint(ModbusAddressPoint point)
    {
        if (point == null) return;
        for (var address = point.StartAddress; address <= point.EndAddress; address++)
            _registerValues.Remove(address);
        Communication.ModbusAddressPoints.Remove(point);
        ModbusAddressPoints.Remove(point);
        RefreshAddressSpaceRows();
        AddressConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public void ClearAllModbusAddressPoints()
    {
        if (ModbusAddressPoints.Count == 0) return;
        Communication.ModbusAddressPoints.Clear();
        ModbusAddressPoints.Clear();
        _registerValues.Clear();
        AddressRangeWarning = string.Empty;
        RefreshAddressSpaceRows();
        AddressConfigurationChanged?.Invoke(this, EventArgs.Empty);
    }

    public bool TrySaveModbusAddressPoint(ModbusAddressPoint draft, out string error)
    {
        error = string.Empty;
        if (draft.StartAddress < 0 || draft.Count < 1 || (long)draft.StartAddress + draft.Count - 1 > ushort.MaxValue)
        {
            error = "地址范围必须在 0 到 65535 之间。";
            return false;
        }

        if (draft.StartAddress < ModbusRegisterStartAddress || draft.EndAddress >=
            ModbusRegisterStartAddress + ModbusRegisterCount)
        {
            error = $"地址块必须位于当前地址空间 {ModbusRegisterStartAddress}～{ModbusRegisterStartAddress + ModbusRegisterCount - 1} 内。";
            return false;
        }

        var conflict = ModbusAddressPoints.FirstOrDefault(point =>
            point.Id != draft.Id &&
            draft.StartAddress <= point.EndAddress &&
            draft.EndAddress >= point.StartAddress);
        if (conflict != null)
        {
            error = $"地址范围与“{conflict.Name}”（{conflict.RangeText}）重叠。";
            return false;
        }

        var existing = ModbusAddressPoints.FirstOrDefault(point => point.Id == draft.Id);
        if (existing == null)
        {
            Communication.ModbusAddressPoints.Add(draft);
            ModbusAddressPoints.Add(draft);
        }
        else
        {
            if (existing.StartAddress != draft.StartAddress || existing.Count != draft.Count)
            {
                for (var address = existing.StartAddress; address <= existing.EndAddress; address++)
                    _registerValues.Remove(address);
                existing.HasReceivedData = false;
            }
            existing.Name = draft.Name;
            existing.StartAddress = draft.StartAddress;
            existing.Count = draft.Count;
            existing.IntBits = draft.IntBits;
            existing.AccessMode = draft.AccessMode;
            existing.ByteOrder = draft.ByteOrder;
            existing.PollEnabled = draft.PollEnabled;
            existing.TimeoutMs = draft.TimeoutMs;
            existing.CurrentValue = draft.CurrentValue;
            existing.ReceiveRecords = draft.ReceiveRecords;
        }

        RefreshAddressSpaceRows();
        AddressConfigurationChanged?.Invoke(this, EventArgs.Empty);
        return true;
    }

    public void RefreshAddressSpaceRows()
    {
        AddressSpaceRows.Clear();
        var visibleCount = ModbusRegisterCount;
        for (var offset = 0; offset < visibleCount; offset += 10)
        {
            var row = new ModbusAddressSpaceRow(offset + ModbusRegisterStartAddress);
            var rowLength = Math.Min(10, visibleCount - offset);
            for (var column = 0; column < 10; column++)
            {
                var address = row.StartAddress + column;
                var isInRange = column < rowLength;
                var point = isInRange
                    ? ModbusAddressPoints.FirstOrDefault(item => address >= item.StartAddress && address <= item.EndAddress)
                    : null;
                row.Cells.Add(new ModbusAddressSpaceCell(address, point, point?.StartAddress == address,
                    point != null && point.EndAddress == address,
                    isInRange && _registerValues.TryGetValue(address, out var value) ? $"[{value >> 8:X2}] [{value & 0xFF:X2}]" : null,
                    isInRange));
            }
            var rowEnd = row.StartAddress + rowLength - 1;
            foreach (var point in ModbusAddressPoints.Where(item => item.StartAddress <= rowEnd && item.EndAddress >= row.StartAddress))
            {
                var first = Math.Max(point.StartAddress, row.StartAddress);
                var last = Math.Min(point.EndAddress, rowEnd);
                row.Bars.Add(new ModbusAddressBarSegment(point, first - row.StartAddress, last - first + 1,
                    last == point.EndAddress));
            }
            AddressSpaceRows.Add(row);
        }
    }

    /// <summary>
    /// 自动轮询结果仅接受仍启用轮询的地址块。
    /// </summary>
    public int ApplyModbusPollResults(IReadOnlyList<ModbusPollUpdate> updates) =>
        ApplyModbusReadResults(updates, requirePollEnabled: true);

    /// <summary>手动数据同步与轮询使能无关，只要求地址块仍存在且配置未变。</summary>
    public int ApplyModbusSyncResults(IReadOnlyList<ModbusPollUpdate> updates) =>
        ApplyModbusReadResults(updates, requirePollEnabled: false);

    private int ApplyModbusReadResults(IReadOnlyList<ModbusPollUpdate> updates, bool requirePollEnabled)
    {
        var appliedCount = 0;
        foreach (var update in updates)
        {
            var point = update.Point;
            var values = update.Values;
            if (!ModbusAddressPoints.Contains(point) ||
                (requirePollEnabled && !point.PollEnabled) ||
                point.AccessMode == "只写" ||
                point.StartAddress != update.StartAddress ||
                values.Length != point.Count)
                continue;

            for (var index = 0; index < values.Length; index++)
                _registerValues[point.StartAddress + index] = values[index];

            point.CurrentValue = string.Join(" ", values.Select(value => $"[{value >> 8:X2}][{value & 0xFF:X2}]"));
            point.HasReceivedData = true;
            point.ReceiveRecords.Add($"#{point.ReceiveRecords.Count + 1} [{update.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] {point.CurrentValue}");
            if (point.ReceiveRecords.Count > MaxHistoryRecords)
                point.ReceiveRecords.RemoveAt(0);
            UpdateAddressSpaceValues(point);
            appliedCount++;
        }

        return appliedCount;
    }

    public void ClearModbusPollStatus()
    {
        foreach (var point in ModbusAddressPoints)
            point.HasReceivedData = false;
    }

    private void UpdateAddressSpaceValues(ModbusAddressPoint point)
    {
        foreach (var row in AddressSpaceRows)
        {
            foreach (var cell in row.Cells)
            {
                if (cell.Point?.Id != point.Id)
                    continue;

                cell.UpdateReceivedValue(_registerValues.TryGetValue(cell.Address, out var value)
                    ? $"[{value >> 8:X2}] [{value & 0xFF:X2}]"
                    : null);
            }
        }
    }

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

public readonly record struct ModbusPollUpdate(ModbusAddressPoint Point, ushort[] Values, DateTimeOffset Timestamp, int StartAddress);

public sealed class ModbusAddressSpaceRow
{
    public ModbusAddressSpaceRow(int startAddress) => StartAddress = startAddress;
    public int StartAddress { get; }
    public ObservableCollection<ModbusAddressSpaceCell> Cells { get; } = new();
    public ObservableCollection<ModbusAddressBarSegment> Bars { get; } = new();
}

public sealed class ModbusAddressBarSegment
{
    public ModbusAddressBarSegment(ModbusAddressPoint point, int startColumn, int columnSpan, bool isLast)
    {
        Point = point;
        StartColumn = startColumn;
        ColumnSpan = columnSpan;
        IsLast = isLast;
    }

    public ModbusAddressPoint Point { get; }
    public int StartColumn { get; }
    public int ColumnSpan { get; }
    public bool IsLast { get; }
    public string BlockLabel => Point.Name;
}

public sealed class ModbusAddressSpaceCell : ObservableObject
{
    public ModbusAddressSpaceCell(int address, ModbusAddressPoint? point, bool isFirst, bool isLast, string? receivedValue, bool isInRange = true)
    {
        Address = address;
        Point = point;
        IsFirst = isFirst;
        IsLast = isLast;
        IsInRange = isInRange;
        _receivedValue = receivedValue;
    }

    public int Address { get; }
    public ModbusAddressPoint? Point { get; }
    public bool IsConfigured => Point != null;
    public bool IsFirst { get; }
    public bool IsLast { get; }
    public bool IsInRange { get; }
    private string? _receivedValue;

    public string? ReceivedValue => _receivedValue;
    public string BlockLabel => IsFirst ? Point?.Name ?? string.Empty : string.Empty;
    public string ValueText => Point == null ? string.Empty : ReceivedValue ?? "0000";
    public string DisplayText => ValueText;

    public void UpdateReceivedValue(string? value)
    {
        if (SetProperty(ref _receivedValue, value))
            OnPropertyChanged(nameof(ValueText));
    }
}
