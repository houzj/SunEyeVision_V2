using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Workflow;

/// <summary>
/// Modbus 地址空间中的一个连续寄存器地址块。
/// 线圈地址暂不纳入当前 UI 方案，保留模型扩展空间供后续启用。
/// </summary>
public sealed class ModbusAddressPoint : ObservableObject
{
    private string _name = "地址";
    private int _startAddress;
    private int _count = 1;
    private string _intBits = "16bit";
    private string _accessMode = "读写";
    private string _byteOrder = "ABCD";
    private bool _pollEnabled;
    private int _timeoutMs = 200;
    private string _currentValue = string.Empty;
    private bool _hasReceivedData;

    public string Id { get; set; } = Guid.NewGuid().ToString("N");

    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value ?? string.Empty);
    }

    public int StartAddress
    {
        get => _startAddress;
        set
        {
            if (SetProperty(ref _startAddress, value))
            {
                OnPropertyChanged(nameof(EndAddress));
                OnPropertyChanged(nameof(RangeText));
                OnPropertyChanged(nameof(CellAddresses));
            }
        }
    }

    public int Count
    {
        get => _count;
        set
        {
            if (SetProperty(ref _count, value))
            {
                OnPropertyChanged(nameof(EndAddress));
                OnPropertyChanged(nameof(RangeText));
                OnPropertyChanged(nameof(CellAddresses));
            }
        }
    }

    /// <summary>VM 地址配置中的整数位数。一个 16bit 值占一个寄存器，32bit 值占两个寄存器。</summary>
    public string IntBits
    {
        get => _intBits;
        set => SetProperty(ref _intBits, value is "32bit" ? "32bit" : "16bit");
    }

    /// <summary>
    /// 兼容旧方案保存的 DataType 字段。读取旧配置时转换为 IntBits，保存新配置时不再写入旧字段。
    /// </summary>
    [JsonPropertyName("DataType")]
    [JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)]
    public string? LegacyDataType
    {
        get => null;
        set
        {
            if (value is "UInt32" or "Int32" or "Float32")
                IntBits = "32bit";
            else if (!string.IsNullOrWhiteSpace(value))
                IntBits = "16bit";
        }
    }

    public string AccessMode
    {
        get => _accessMode;
        set => SetProperty(ref _accessMode, value ?? "读写");
    }

    public string ByteOrder
    {
        get => _byteOrder;
        set => SetProperty(ref _byteOrder, value ?? "ABCD");
    }

    public bool PollEnabled
    {
        get => _pollEnabled;
        set => SetProperty(ref _pollEnabled, value);
    }

    /// <summary>
    /// 单个地址块 Modbus 请求超时时间（毫秒）。轮询周期属于设备级通信参数。
    /// </summary>
    public int TimeoutMs
    {
        get => _timeoutMs;
        set => SetProperty(ref _timeoutMs, Math.Clamp(value, 50, 120000));
    }

    public string CurrentValue
    {
        get => _currentValue;
        set => SetProperty(ref _currentValue, value ?? string.Empty);
    }

    /// <summary>
    /// 地址级接收记录。必须使用可通知集合，因为记录窗口可能在轮询期间保持打开状态。
    /// </summary>
    public ObservableCollection<string> ReceiveRecords { get; set; } = new();

    [JsonIgnore]
    public bool HasReceivedData
    {
        get => _hasReceivedData;
        set => SetProperty(ref _hasReceivedData, value);
    }

    [JsonIgnore]
    public int EndAddress => StartAddress + Math.Max(Count, 1) - 1;

    [JsonIgnore]
    public string RangeText => Count <= 1 ? StartAddress.ToString() : $"{StartAddress}–{EndAddress}";

    [JsonIgnore]
    public IEnumerable<int> CellAddresses => Enumerable.Range(StartAddress, Math.Max(Count, 1));

    public ModbusAddressPoint Clone() => new()
    {
        Id = Guid.NewGuid().ToString("N"),
        Name = Name,
        StartAddress = StartAddress,
        Count = Count,
        IntBits = IntBits,
        AccessMode = AccessMode,
        ByteOrder = ByteOrder,
        PollEnabled = PollEnabled,
        TimeoutMs = TimeoutMs,
        CurrentValue = CurrentValue,
        ReceiveRecords = new ObservableCollection<string>(ReceiveRecords)
    };
}
