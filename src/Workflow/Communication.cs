using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Workflow;

/// <summary>
/// 通讯类型枚举
/// </summary>
public enum CommunicationType
{
    /// <summary>
    /// HTTP
    /// </summary>
    HTTP,

    /// <summary>
    /// Modbus TCP
    /// </summary>
    ModbusTCP,

    /// <summary>
    /// Modbus RTU
    /// </summary>
    ModbusRTU,

    /// <summary>
    /// OPC UA
    /// </summary>
    OPCUA,

    /// <summary>
    /// TCP/IP
    /// </summary>
    TCP,

    /// <summary>
    /// UDP
    /// </summary>
    UDP,

    /// <summary>
    /// 串口
    /// </summary>
    Serial,

    /// <summary>
    /// 其他
    /// </summary>
    Other
}

/// <summary>
/// 通讯设备运行状态，由通讯运行时更新，不写入方案文件。
/// </summary>
public enum CommunicationRuntimeStatus
{
    Disconnected,
    Connecting,
    Connected,
    Faulted
}

/// <summary>
/// 通讯模型
/// </summary>
/// <remarks>
/// 用于管理解决方案中的通讯设备配置；具体运行行为由协议驱动实现。
/// </remarks>
public class Communication : ObservableObject
{
    private string _name = "";
    private bool _enabled = true;
    private CommunicationType _connectionType = CommunicationType.HTTP;
    private string _description = "";
    private CommunicationRuntimeStatus _runtimeStatus = CommunicationRuntimeStatus.Disconnected;
    private bool _isRuntimeOperationInProgress;

    /// <summary>
    /// 通讯ID
    /// </summary>
    public string Id { get; set; } = Guid.NewGuid().ToString();

    /// <summary>Persisted communication configuration schema version. Missing legacy values default to 1.</summary>
    public int SchemaVersion { get; set; } = 1;

    /// <summary>
    /// 通讯名称
    /// </summary>
    public string Name
    {
        get => _name;
        set => SetProperty(ref _name, value, "通讯名称");
    }

    /// <summary>
    /// 是否启用
    /// </summary>
    public bool IsEnabled
    {
        get => _enabled;
        set => SetProperty(ref _enabled, value, "是否启用");
    }

    /// <summary>
    /// 通讯类型
    /// </summary>
    public CommunicationType ConnectionType
    {
        get => _connectionType;
        set => SetProperty(ref _connectionType, value, "通讯类型");
    }

    /// <summary>
    /// 通讯描述
    /// </summary>
    public string Description
    {
        get => _description;
        set => SetProperty(ref _description, value, "通讯描述");
    }

    /// <summary>
    /// 当前运行时连接状态。该状态不写入方案文件。
    /// </summary>
    [JsonIgnore]
    public CommunicationRuntimeStatus RuntimeStatus
    {
        get => _runtimeStatus;
        set
        {
            if (SetProperty(ref _runtimeStatus, value, "通讯状态"))
            {
                OnPropertyChanged(nameof(IsRuntimeActive));
                OnPropertyChanged(nameof(ProtocolStatusText));
                OnPropertyChanged(nameof(RuntimeStatusText));
            }
        }
    }

    [JsonIgnore]
    public bool IsRuntimeActive => RuntimeStatus is CommunicationRuntimeStatus.Connecting or CommunicationRuntimeStatus.Connected;

    [JsonIgnore]
    public bool IsRuntimeOperationInProgress
    {
        get => _isRuntimeOperationInProgress;
        set
        {
            if (SetProperty(ref _isRuntimeOperationInProgress, value))
                OnPropertyChanged(nameof(CanToggleRuntime));
        }
    }

    [JsonIgnore]
    public bool CanToggleRuntime => SupportsRuntimeToggle && !IsRuntimeOperationInProgress;

    [JsonIgnore]
    public string ProtocolStatusText => $"{ProtocolDisplayName} · {RuntimeStatusText}";

    /// <summary>
    /// 设备端点地址。使用 Settings 保存，兼容旧的 host/ipAddress 键。
    /// </summary>
    [JsonIgnore]
    public string Host
    {
        get
        {
            if (Settings.TryGetValue("host", out var host)) return Convert.ToString(host) ?? "";
            return Settings.TryGetValue("ipAddress", out var ip) ? Convert.ToString(ip) ?? "" : "";
        }
        set
        {
            var normalized = value?.Trim() ?? "";
            Settings["host"] = normalized;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConnectionString));
        }
    }

    /// <summary>
    /// 设备端口。使用 Settings 保存。
    /// </summary>
    [JsonIgnore]
    public int Port
    {
        get
        {
            if (!Settings.TryGetValue("port", out var port)) return 0;
            return port switch
            {
                int value => value,
                long value => (int)value,
                _ when int.TryParse(Convert.ToString(port), out var value) => value,
                _ => 0
            };
        }
        set
        {
            Settings["port"] = value;
            OnPropertyChanged();
            OnPropertyChanged(nameof(ConnectionString));
        }
    }

    [JsonIgnore]
    public bool AutoReconnect
    {
        get => GetBoolSetting("autoReconnect");
        set { Settings["autoReconnect"] = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public bool EnableDataUpload
    {
        get => GetBoolSetting("enableDataUpload");
        set { Settings["enableDataUpload"] = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public bool EnableReceiveTerminator
    {
        get => GetBoolSetting("enableReceiveTerminator");
        set { Settings["enableReceiveTerminator"] = value; OnPropertyChanged(); }
    }

    [JsonIgnore]
    public string ReceiveTerminator
    {
        get => Settings.TryGetValue("receiveTerminator", out var value) ? Convert.ToString(value) ?? "" : "";
        set { Settings["receiveTerminator"] = value ?? ""; OnPropertyChanged(); }
    }

    [JsonIgnore] public string ProtocolKey
    {
        get
        {
            if (Settings.TryGetValue("protocolKey", out var value)) return Convert.ToString(value) ?? "";
            return ConnectionType switch
            {
                CommunicationType.UDP => "udp",
                CommunicationType.Serial => "serial",
                CommunicationType.ModbusTCP or CommunicationType.ModbusRTU => "modbus",
                CommunicationType.OPCUA => "opcua",
                CommunicationType.HTTP => "http",
                _ => "tcp-client"
            };
        }
        set { Settings["protocolKey"] = value ?? ""; OnPropertyChanged(); OnPropertyChanged(nameof(ProtocolStatusText)); OnPropertyChanged(nameof(IsTcpClient)); OnPropertyChanged(nameof(IsTcpServer)); OnPropertyChanged(nameof(IsUdp)); OnPropertyChanged(nameof(SupportsRuntimeToggle)); OnPropertyChanged(nameof(CanToggleRuntime)); OnPropertyChanged(nameof(IsModbus)); OnPropertyChanged(nameof(IsPlainSerial)); OnPropertyChanged(nameof(IsS7)); OnPropertyChanged(nameof(IsCip)); OnPropertyChanged(nameof(IsOpcUa)); OnPropertyChanged(nameof(IsHttp)); OnPropertyChanged(nameof(IsRawProtocol)); }
    }

    [JsonIgnore] public string ProtocolDisplayName => Settings.TryGetValue("protocolDisplayName", out var value) ? Convert.ToString(value) ?? ConnectionType.ToString() : ProtocolKey == "modbus" ? "Modbus通信" : ConnectionType switch
    {
        CommunicationType.TCP => "TCP客户端", CommunicationType.UDP => "UDP", CommunicationType.Serial => "串口",
        CommunicationType.ModbusTCP => "Modbus TCP", CommunicationType.ModbusRTU => "Modbus RTU", CommunicationType.OPCUA => "OPC UA",
        CommunicationType.HTTP => "HTTP", _ => ConnectionType.ToString()
    };

    [JsonIgnore] public bool IsTcpClient => ProtocolKey == "tcp-client";
    [JsonIgnore] public bool IsTcpServer => ProtocolKey == "tcp-server";
    [JsonIgnore] public bool IsUdp => ProtocolKey == "udp";
    [JsonIgnore] public bool SupportsRuntimeToggle => IsTcpClient || IsTcpServer || IsUdp || IsPlainSerial || IsModbus || IsS7 || IsOpcUa || IsCip;
    [JsonIgnore] public bool IsModbus => ProtocolKey is "modbus" or "modbus-tcp" or "modbus-rtu";
    [JsonIgnore] public bool IsPlainSerial => ProtocolKey == "serial";
    [JsonIgnore] public bool IsS7 => ProtocolKey == "s7";
    [JsonIgnore] public bool IsCip => ProtocolKey == "cip";
    [JsonIgnore] public string S7Area { get => GetStringSetting("s7Area", "DataBlock"); set => SetSetting("s7Area", value); }
    [JsonIgnore] public int S7DataBlock { get => GetIntSetting("s7DataBlock", 1); set => SetSetting("s7DataBlock", value); }
    [JsonIgnore] public bool IsOpcUa => ProtocolKey == "opcua";
    [JsonIgnore] public bool IsHttp => ProtocolKey == "http";
    [JsonIgnore] public bool IsRawProtocol => ProtocolKey is "tcp-client" or "tcp-server" or "udp" or "serial";

    [JsonIgnore] public string LocalHost { get => GetStringSetting("localHost", "127.0.0.1"); set => SetSetting("localHost", value); }
    [JsonIgnore] public int LocalPort { get => GetIntSetting("localPort", 7930); set => SetSetting("localPort", value); }
    [JsonIgnore] public string RemoteHost { get => GetStringSetting("remoteHost", "127.0.0.1"); set => SetSetting("remoteHost", value); }
    [JsonIgnore] public int RemotePort { get => GetIntSetting("remotePort", 7920); set => SetSetting("remotePort", value); }
    [JsonIgnore] public string SerialPort { get => GetStringSetting("serialPort"); set => SetSetting("serialPort", value); }
    [JsonIgnore] public int BaudRate { get => GetIntSetting("baudRate", 115200); set => SetSetting("baudRate", value); }
    [JsonIgnore] public int DataBits { get => GetIntSetting("dataBits", 8); set => SetSetting("dataBits", value); }
    [JsonIgnore] public string Parity { get => GetStringSetting("parity", "无校验"); set => SetSetting("parity", value); }
    [JsonIgnore] public int StopBits { get => GetIntSetting("stopBits", 1); set => SetSetting("stopBits", value); }
    [JsonIgnore] public int CharacterTimeout { get => GetIntSetting("characterTimeout", 10); set => SetSetting("characterTimeout", value); }
    [JsonIgnore] public int PollInterval { get => GetIntSetting("pollInterval", 10); set => SetSetting("pollInterval", value); }
    [JsonIgnore] public int ModbusRegisterStartAddress
    {
        get => GetIntSetting("modbusRegisterStartAddress", 0);
        set => SetSetting("modbusRegisterStartAddress", Math.Clamp(value, 0, ushort.MaxValue));
    }
    [JsonIgnore] public int ModbusRegisterCount
    {
        get => GetIntSetting("modbusRegisterCount", 50);
        set => SetSetting("modbusRegisterCount", Math.Clamp(value, 1, ushort.MaxValue + 1 - ModbusRegisterStartAddress));
    }
    [JsonIgnore] public int UnitId { get => GetIntSetting("unitId", 1); set => SetSetting("unitId", value); }
    [JsonIgnore] public int Rack { get => GetIntSetting("rack", 0); set => SetSetting("rack", value); }
    [JsonIgnore] public int Slot { get => GetIntSetting("slot", 0); set => SetSetting("slot", value); }
    [JsonIgnore] public string S7CpuType { get => GetStringSetting("s7CpuType", "S71200"); set => SetSetting("s7CpuType", value); }
    [JsonIgnore] public string ModbusMode
    {
        get => GetStringSetting("modbusMode", ProtocolKey == "modbus-rtu" ? "串口" : "TcpClient");
        set
        {
            SetSetting("modbusMode", value);
            OnPropertyChanged(nameof(IsModbusTcp));
            OnPropertyChanged(nameof(IsModbusRtu));
        }
    }
    /// <summary>Legacy endpointUrl is authoritative until this OPC UA device is edited with split fields.</summary>
    [JsonIgnore]
    public string OpcUaHost
    {
        get => HasOpcUaEndpointParts ? GetStringSetting("opcUaHost", "") : ReadLegacyOpcUaEndpoint()?.Host ?? (Settings.ContainsKey("endpointUrl") ? "" : "127.0.0.1");
        set
        {
            var normalized = value?.Trim() ?? "";
            if (string.Equals(OpcUaHost, normalized, StringComparison.Ordinal) &&
                (HasOpcUaEndpointParts || Settings.ContainsKey("endpointUrl"))) return;
            EnsureOpcUaEndpointParts(); SetSetting("opcUaHost", normalized); OnPropertyChanged(nameof(EndpointUrl));
        }
    }

    [JsonIgnore]
    public int OpcUaPort
    {
        get => HasOpcUaEndpointParts ? GetIntSetting("opcUaPort", 0) : ReadLegacyOpcUaEndpoint()?.Port ?? (Settings.ContainsKey("endpointUrl") ? 0 : 49320);
        set
        {
            if (OpcUaPort == value) return;
            EnsureOpcUaEndpointParts(); SetSetting("opcUaPort", value); OnPropertyChanged(nameof(EndpointUrl));
        }
    }

    [JsonIgnore]
    public string EndpointUrl
    {
        get
        {
            if (!HasOpcUaEndpointParts) return GetStringSetting("endpointUrl", "opc.tcp://127.0.0.1:49320");
            if (string.IsNullOrWhiteSpace(OpcUaHost) || OpcUaPort is < 1 or > 65535) return string.Empty;
            try
            {
                var builder = new UriBuilder("opc.tcp", OpcUaHost, OpcUaPort, GetStringSetting("opcUaEndpointPath", "/"))
                {
                    Query = GetStringSetting("opcUaEndpointQuery"),
                    Fragment = GetStringSetting("opcUaEndpointFragment")
                };
                var endpoint = builder.Uri.ToString();
                return endpoint.EndsWith('/') && GetStringSetting("opcUaEndpointPath", "/") == "/" ? endpoint[..^1] : endpoint;
            }
            catch (UriFormatException) { return string.Empty; }
            catch (ArgumentException) { return string.Empty; }
        }
        set { SetSetting("endpointUrl", value); OnPropertyChanged(nameof(OpcUaHost)); OnPropertyChanged(nameof(OpcUaPort)); }
    }

    [JsonIgnore] private bool HasOpcUaEndpointParts => GetIntSetting("opcUaEndpointPartsVersion", 0) >= 1;

    private Uri? ReadLegacyOpcUaEndpoint() =>
        Uri.TryCreate(GetStringSetting("endpointUrl", "opc.tcp://127.0.0.1:49320"), UriKind.Absolute, out var uri) &&
        uri.Scheme == "opc.tcp" && uri.Port is > 0 and <= 65535 ? uri : null;

    private void EnsureOpcUaEndpointParts()
    {
        if (HasOpcUaEndpointParts) return;
        var legacy = ReadLegacyOpcUaEndpoint();
        Settings["opcUaHost"] = legacy?.Host ?? (Settings.ContainsKey("endpointUrl") ? "" : "127.0.0.1");
        Settings["opcUaPort"] = legacy?.Port ?? (Settings.ContainsKey("endpointUrl") ? 0 : 49320);
        Settings["opcUaEndpointPath"] = legacy?.AbsolutePath ?? "/";
        Settings["opcUaEndpointQuery"] = legacy?.Query ?? "";
        Settings["opcUaEndpointFragment"] = legacy?.Fragment ?? "";
        Settings["opcUaEndpointPartsVersion"] = 1;
    }
    [JsonIgnore] public int RequestTimeout { get => GetIntSetting("requestTimeout", 3000); set => SetSetting("requestTimeout", value); }
    [JsonIgnore] public string HttpMethod { get => GetStringSetting("httpMethod", "POST"); set => SetSetting("httpMethod", value); }

    [JsonIgnore] public bool IsModbusRtu => IsModbus && (ModbusMode is "串口" or "Serial" or "Rtu");
    [JsonIgnore] public bool IsModbusTcp => IsModbus && !IsModbusRtu;

    private string GetStringSetting(string key, string fallback = "") => Settings.TryGetValue(key, out var value) ? Convert.ToString(value) ?? fallback : fallback;
    private int GetIntSetting(string key, int fallback = 0)
    {
        if (!Settings.TryGetValue(key, out var value)) return fallback;
        return value switch { int integer => integer, long longValue => (int)longValue, _ when int.TryParse(Convert.ToString(value), out var parsed) => parsed, _ => fallback };
    }
    private void SetSetting(string key, object? value)
    {
        Settings[key] = value ?? "";
        OnPropertyChanged(key);
        OnPropertyChanged(key switch
        {
            "localHost" => nameof(LocalHost),
            "localPort" => nameof(LocalPort),
            "remoteHost" => nameof(RemoteHost),
            "remotePort" => nameof(RemotePort),
            "serialPort" => nameof(SerialPort),
            "baudRate" => nameof(BaudRate),
            "dataBits" => nameof(DataBits),
            "parity" => nameof(Parity),
            "stopBits" => nameof(StopBits),
            "characterTimeout" => nameof(CharacterTimeout),
            "pollInterval" => nameof(PollInterval),
            "modbusRegisterStartAddress" => nameof(ModbusRegisterStartAddress),
            "modbusRegisterCount" => nameof(ModbusRegisterCount),
            "unitId" => nameof(UnitId),
            "rack" => nameof(Rack),
            "slot" => nameof(Slot),
            "s7CpuType" => nameof(S7CpuType),
            "modbusMode" => nameof(ModbusMode),
            "endpointUrl" => nameof(EndpointUrl),
            "requestTimeout" => nameof(RequestTimeout),
            "httpMethod" => nameof(HttpMethod),
            _ => key
        });
    }

    [JsonIgnore]
    public string ConnectionString => GetConnectionString();

    [JsonIgnore]
    public string RuntimeStatusText => RuntimeStatus switch
    {
        CommunicationRuntimeStatus.Connected => "已连接",
        CommunicationRuntimeStatus.Connecting => "连接中",
        CommunicationRuntimeStatus.Faulted => "故障",
        _ => "已断开"
    };

    private bool GetBoolSetting(string key)
    {
        if (!Settings.TryGetValue(key, out var value)) return false;
        return value switch
        {
            bool boolean => boolean,
            _ when bool.TryParse(Convert.ToString(value), out var parsed) => parsed,
            _ => false
        };
    }

    /// <summary>
    /// 通讯设置（JSON格式）
    /// </summary>
    public Dictionary<string, object> Settings { get; set; } = new();

    /// <summary>
    /// 数据映射配置
    /// </summary>
    public Dictionary<string, object> Mapping { get; set; } = new();

    /// <summary>
    /// Modbus 地址空间配置。每项代表一个连续的寄存器地址块。
    /// </summary>
    public List<ModbusAddressPoint> ModbusAddressPoints { get; set; } = new();

    /// <summary>Configured OPC UA string NodeIds. Runtime values are intentionally not persisted.</summary>
    public List<OpcUaDataPoint> OpcUaDataPoints { get; set; } = new();

    /// <summary>
    /// 创建时间
    /// </summary>
    public DateTime CreatedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 修改时间
    /// </summary>
    public DateTime ModifiedTime { get; set; } = DateTime.Now;

    /// <summary>
    /// 无参构造函数
    /// </summary>
    public Communication()
    {
    }

    /// <summary>
    /// 构造函数
    /// </summary>
    public Communication(string name, CommunicationType connectionType)
    {
        Name = name;
        ConnectionType = connectionType;
    }

    /// <summary>
    /// 克隆通讯配置
    /// </summary>
    public Communication Clone()
    {
        return new Communication
        {
            Id = Guid.NewGuid().ToString(),
            SchemaVersion = SchemaVersion,
            Name = Name,
            IsEnabled = IsEnabled,
            ConnectionType = ConnectionType,
            Description = Description,
            Settings = new Dictionary<string, object>(Settings),
            Mapping = new Dictionary<string, object>(Mapping),
            ModbusAddressPoints = ModbusAddressPoints.Select(point => point.Clone()).ToList(),
            OpcUaDataPoints = (OpcUaDataPoints ?? new()).Select(point => point.Clone()).ToList(),
            CreatedTime = CreatedTime,
            ModifiedTime = DateTime.Now
        };
    }

    /// <summary>
    /// 验证通讯配置
    /// </summary>
    public (bool IsValid, List<string> Errors) Validate()
    {
        var errors = new List<string>();

        if (string.IsNullOrWhiteSpace(Name))
        {
            errors.Add("通讯名称不能为空");
        }

        if (Settings.Count == 0)
        {
            errors.Add("通讯设置不能为空");
        }

        return (errors.Count == 0, errors);
    }

    /// <summary>
    /// 获取连接字符串
    /// </summary>
    public string GetConnectionString()
    {
        if (IsOpcUa) return EndpointUrl;
        if (ProtocolKey == "tcp-server") return $"{LocalHost}:{LocalPort} (listen)";
        if (ProtocolKey == "udp") return $"{LocalHost}:{LocalPort} → {RemoteHost}:{RemotePort}";
        if (IsModbusRtu || IsPlainSerial) return $"{SerialPort} · {BaudRate},{DataBits},{Parity},{StopBits}";
        if (Settings.TryGetValue("host", out var host) && Settings.TryGetValue("port", out var port))
        {
            return $"{host}:{port}";
        }
        else if (Settings.TryGetValue("ipAddress", out var ipAddress) && Settings.TryGetValue("port", out port))
        {
            return $"{ipAddress}:{port}";
        }
        else
        {
            return Name;
        }
    }
}
