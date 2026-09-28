using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using SunEyeVision.UI.Core;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Features.CommunicationManager;

/// <summary>
/// 第一阶段通讯设备编辑器。只负责配置，不创建真实连接。
/// </summary>
public sealed class CommunicationDeviceEditorViewModel : ViewModelBase
{
    private string _deviceName = string.Empty;
    private bool _deviceNameFollowsProtocol = true;
    private bool _isApplyingDefaultDeviceName;
    private CommunicationProtocolOption? _selectedProtocol;
    private string _host = "127.0.0.1";
    private int _port = 7920;
    private string _localHost = "127.0.0.1";
    private int _localPort = 7930;
    private string _remoteHost = "127.0.0.1";
    private int _remotePort = 7920;
    private string _serialPort = string.Empty;
    private int _baudRate = 115200;
    private int _dataBits = 8;
    private string _parity = "无校验";
    private int _stopBits = 1;
    private int _characterTimeout = 10;
    private int _pollInterval = 10;
    private int _unitId = 1;
    private int _rack;
    private int _slot;
    private string _s7CpuType = "S71200";
    private string _modbusMode = "TcpClient";
    private string _endpointUrl = "opc.tcp://127.0.0.1:49320";
    private int _requestTimeout = 3000;
    private string _httpMethod = "POST";
    private bool _enableDataUpload = true;
    private bool _autoReconnect;
    private bool _enableReceiveTerminator;
    private string _receiveTerminator = string.Empty;
    private string _validationMessage = string.Empty;

    public ObservableCollection<CommunicationProtocolOption> Protocols { get; } =
        new(CommunicationProtocolOption.CreateDefaults());
    public ObservableCollection<string> SerialPortOptions { get; } = new();
    public ObservableCollection<int> BaudRateOptions { get; } =
        new(SerialPortCatalog.GetBaudRates(115200));

    public CommunicationProtocolOption? SelectedProtocol
    {
        get => _selectedProtocol;
        set
        {
            if (SetProperty(ref _selectedProtocol, value))
            {
                if (value != null && (_deviceNameFollowsProtocol || string.IsNullOrWhiteSpace(DeviceName)))
                {
                    _isApplyingDefaultDeviceName = true;
                    try { DeviceName = value.DisplayName; }
                    finally { _isApplyingDefaultDeviceName = false; }
                }
                if (value?.Key == "s7" && Port == 7920) Port = 102;
                if (value?.Key == "cip" && Port == 7920) Port = 44818;
                if (value?.Key == "opcua" && Port == 7920) Port = 49320;
                if (value?.Key == "opcua") AutoReconnect = true;
                OnPropertyChanged(nameof(IsNetworkProtocol));
                OnPropertyChanged(nameof(IsSerialProtocol));
                OnPropertyChanged(nameof(IsPlainSerial));
                OnPropertyChanged(nameof(IsTcpClient));
                OnPropertyChanged(nameof(IsTcpServer));
                OnPropertyChanged(nameof(IsUdp));
                OnPropertyChanged(nameof(IsModbus));
                OnPropertyChanged(nameof(IsModbusTcp));
                OnPropertyChanged(nameof(IsModbusRtu));
                OnPropertyChanged(nameof(IsOpcUa));
                OnPropertyChanged(nameof(IsS7));
                OnPropertyChanged(nameof(IsCip));
                OnPropertyChanged(nameof(IsHttp));
                OnPropertyChanged(nameof(IsRawProtocol));
            }
        }
    }

    public string DeviceName
    {
        get => _deviceName;
        set
        {
            if (!_isApplyingDefaultDeviceName && !string.Equals(_deviceName, value, StringComparison.Ordinal))
                _deviceNameFollowsProtocol = false;
            SetProperty(ref _deviceName, value, "设备名称");
        }
    }

    public string Host
    {
        get => _host;
        set { if (SetProperty(ref _host, value)) OnPropertyChanged(nameof(OpcUaEndpointPreview)); }
    }

    public int Port
    {
        get => _port;
        set { if (SetProperty(ref _port, value)) OnPropertyChanged(nameof(OpcUaEndpointPreview)); }
    }

    public string OpcUaEndpointPreview
    {
        get
        {
            if (string.IsNullOrWhiteSpace(Host) || Port is < 1 or > 65535) return "请输入有效的服务器地址和端口";
            try { return new UriBuilder("opc.tcp", Host.Trim(), Port).Uri.ToString().TrimEnd('/'); }
            catch (UriFormatException) { return "服务器地址格式无效"; }
            catch (ArgumentException) { return "服务器地址格式无效"; }
        }
    }

    public string LocalHost { get => _localHost; set => SetProperty(ref _localHost, value); }
    public int LocalPort { get => _localPort; set => SetProperty(ref _localPort, value); }
    public string RemoteHost { get => _remoteHost; set => SetProperty(ref _remoteHost, value); }
    public int RemotePort { get => _remotePort; set => SetProperty(ref _remotePort, value); }

    public string SerialPort
    {
        get => _serialPort;
        set => SetProperty(ref _serialPort, value);
    }

    public int BaudRate
    {
        get => _baudRate;
        set => SetProperty(ref _baudRate, value);
    }

    public int DataBits { get => _dataBits; set => SetProperty(ref _dataBits, value); }
    public string Parity { get => _parity; set => SetProperty(ref _parity, value); }
    public int StopBits { get => _stopBits; set => SetProperty(ref _stopBits, value); }
    public int CharacterTimeout { get => _characterTimeout; set => SetProperty(ref _characterTimeout, value); }
    public int PollInterval { get => _pollInterval; set => SetProperty(ref _pollInterval, value); }
    public int UnitId { get => _unitId; set => SetProperty(ref _unitId, value); }
    public int Rack { get => _rack; set => SetProperty(ref _rack, value); }
    public int Slot { get => _slot; set => SetProperty(ref _slot, value); }
    public string S7CpuType { get => _s7CpuType; set => SetProperty(ref _s7CpuType, value); }
    public string ModbusMode
    {
        get => _modbusMode;
        set
        {
            if (SetProperty(ref _modbusMode, value))
            {
                OnPropertyChanged(nameof(IsModbusTcp));
                OnPropertyChanged(nameof(IsModbusRtu));
                CommandManager.InvalidateRequerySuggested();
            }
        }
    }
    public string EndpointUrl { get => _endpointUrl; set => SetProperty(ref _endpointUrl, value); }
    public int RequestTimeout { get => _requestTimeout; set => SetProperty(ref _requestTimeout, value); }
    public string HttpMethod { get => _httpMethod; set => SetProperty(ref _httpMethod, value); }

    public ObservableCollection<string> ParityOptions { get; } =
        new(new[] { "无校验", "奇校验", "偶校验", "标记校验", "空格校验" });
    public ObservableCollection<int> DataBitsOptions { get; } = new(new[] { 7, 8 });
    public ObservableCollection<int> StopBitsOptions { get; } = new(new[] { 1, 2 });
    public ObservableCollection<string> ModbusModeOptions { get; } = new(new[] { "TcpClient", "串口" });
    public ObservableCollection<string> S7CpuTypeOptions { get; } = new(new[] { "S7200", "S7200Smart", "S7300", "S7400", "S71200", "S71500" });
    public ObservableCollection<string> HttpMethodOptions { get; } = new(new[] { "GET", "POST", "PUT" });

    public bool EnableDataUpload
    {
        get => _enableDataUpload;
        set => SetProperty(ref _enableDataUpload, value);
    }

    public bool AutoReconnect
    {
        get => _autoReconnect;
        set => SetProperty(ref _autoReconnect, value);
    }

    public bool EnableReceiveTerminator
    {
        get => _enableReceiveTerminator;
        set => SetProperty(ref _enableReceiveTerminator, value);
    }

    public string ReceiveTerminator
    {
        get => _receiveTerminator;
        set => SetProperty(ref _receiveTerminator, value);
    }

    public bool IsNetworkProtocol => SelectedProtocol?.IsNetwork ?? true;
    public bool IsSerialProtocol => SelectedProtocol?.IsSerial ?? false;
    public bool IsPlainSerial => SelectedProtocol?.Key == "serial";
    public bool IsTcpClient => SelectedProtocol?.Key == "tcp-client";
    public bool IsTcpServer => SelectedProtocol?.Key == "tcp-server";
    public bool IsUdp => SelectedProtocol?.Key == "udp";
    public bool IsModbus => SelectedProtocol?.Key == "modbus";
    public bool IsModbusTcp => IsModbus && ModbusMode == "TcpClient";
    public bool IsModbusRtu => IsModbus && ModbusMode == "串口";
    public bool IsOpcUa => SelectedProtocol?.Key == "opcua";
    public bool IsS7 => SelectedProtocol?.Key == "s7";
    public bool IsCip => SelectedProtocol?.Key == "cip";
    public bool IsHttp => SelectedProtocol?.Key == "http";
    public bool IsRawProtocol => SelectedProtocol?.Key is "tcp-client" or "tcp-server" or "udp" or "serial";
    public string ValidationMessage
    {
        get => _validationMessage;
        private set => SetProperty(ref _validationMessage, value);
    }

    public Communication? Result { get; private set; }
    public ICommand CreateCommand { get; }
    public ICommand CancelCommand { get; }

    public event EventHandler? RequestClose;

    public CommunicationDeviceEditorViewModel()
    {
        RefreshSerialPorts();
        SelectedProtocol = Protocols.First();
        CreateCommand = new RelayCommand(Create, CanCreate, "CreateCommunicationDevice");
        CancelCommand = new RelayCommand(() => RequestClose?.Invoke(this, EventArgs.Empty));
    }

    public void RefreshSerialPorts()
    {
        var current = SerialPort;
        SerialPortOptions.Clear();
        foreach (var port in SerialPortCatalog.GetPorts(current))
            SerialPortOptions.Add(port);
    }

    private bool CanCreate()
    {
        return !string.IsNullOrWhiteSpace(DeviceName) &&
               SelectedProtocol != null &&
               (SelectedProtocol.Key == "opcua"
                   ? Uri.TryCreate(OpcUaEndpointPreview, UriKind.Absolute, out var opcUaEndpoint) && opcUaEndpoint.Scheme == "opc.tcp" && RequestTimeout is >= 250 and <= 120000
                   : SelectedProtocol.Key == "modbus"
                   ? (IsModbusRtu ? !string.IsNullOrWhiteSpace(SerialPort) : !string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535)
                   : SelectedProtocol.IsSerial
                   ? !string.IsNullOrWhiteSpace(SerialPort)
                   : !string.IsNullOrWhiteSpace(Host) && Port is > 0 and <= 65535);
    }

    private void Create()
    {
        ValidationMessage = string.Empty;
        if (!CanCreate())
        {
            ValidationMessage = SelectedProtocol?.Key == "modbus" && IsModbusRtu || SelectedProtocol?.IsSerial == true
                ? "请输入设备名称和串口号"
                : "请输入设备名称、目标地址和有效端口（1-65535）";
            return;
        }

        var communication = new Communication(DeviceName.Trim(), SelectedProtocol!.ConnectionType)
        {
            IsEnabled = true,
            RuntimeStatus = CommunicationRuntimeStatus.Disconnected
        };

        // 通过 Communication 的类型化属性写入初始值。直接操作 Settings 字典会绕过
        // 属性通知，创建后详情页的绑定可能先拿到空值，尤其是 ComboBox 的串口/波特率。
        communication.ProtocolKey = SelectedProtocol.Key;
        communication.Settings["protocolDisplayName"] = SelectedProtocol.DisplayName;
        communication.Host = Host.Trim();
        communication.Port = Port;
        communication.LocalHost = LocalHost.Trim();
        communication.LocalPort = LocalPort;
        communication.RemoteHost = RemoteHost.Trim();
        communication.RemotePort = RemotePort;
        communication.SerialPort = SerialPort.Trim();
        communication.BaudRate = BaudRate;
        communication.DataBits = DataBits;
        communication.Parity = Parity;
        communication.StopBits = StopBits;
        communication.CharacterTimeout = CharacterTimeout;
        communication.PollInterval = PollInterval;
        communication.UnitId = UnitId;
        communication.Rack = Rack;
        communication.Slot = Slot;
        communication.S7CpuType = S7CpuType;
        communication.ModbusMode = ModbusMode;
        if (communication.IsOpcUa)
        {
            communication.OpcUaHost = Host.Trim();
            communication.OpcUaPort = Port;
        }
        else
            communication.EndpointUrl = EndpointUrl.Trim();
        communication.RequestTimeout = RequestTimeout;
        communication.HttpMethod = HttpMethod;
        communication.AutoReconnect = AutoReconnect;
        communication.EnableDataUpload = EnableDataUpload;
        communication.EnableReceiveTerminator = EnableReceiveTerminator;
        communication.ReceiveTerminator = ReceiveTerminator.Trim();

        Result = communication;
        RequestClose?.Invoke(this, EventArgs.Empty);
    }
}

/// <summary>
/// 协议类型仅用于第一阶段 UI 选择。具体协议实现后续接入。
/// </summary>
public sealed class CommunicationProtocolOption
{
    public string Key { get; }
    public string DisplayName { get; }
    public CommunicationType ConnectionType { get; }
    public bool IsNetwork { get; }
    public bool IsSerial { get; }

    private CommunicationProtocolOption(string key, string displayName, CommunicationType connectionType, bool isNetwork, bool isSerial)
    {
        Key = key;
        DisplayName = displayName;
        ConnectionType = connectionType;
        IsNetwork = isNetwork;
        IsSerial = isSerial;
    }

    public static CommunicationProtocolOption[] CreateDefaults() =>
    [
        new("tcp-client", "TCP客户端", CommunicationType.TCP, true, false),
        new("tcp-server", "TCP服务端", CommunicationType.TCP, true, false),
        new("udp", "UDP", CommunicationType.UDP, true, false),
        new("serial", "串口", CommunicationType.Serial, false, true),
        new("modbus", "Modbus通信", CommunicationType.ModbusTCP, true, false),
        new("s7", "西门子S7", CommunicationType.TCP, true, false),
        new("cip", "EtherNet/IP CIP", CommunicationType.TCP, true, false),
        new("opcua", "OPC UA", CommunicationType.OPCUA, true, false)
    ];
}
