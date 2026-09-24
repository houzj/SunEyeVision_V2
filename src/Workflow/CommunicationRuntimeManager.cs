using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Buffers.Binary;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.IO.Ports;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NModbus;
using NModbus.IO;
using S7.Net;
using InvalidDataException = System.IO.InvalidDataException;
using Opc.Ua;
using Opc.Ua.Client;
using Opc.Ua.Configuration;

namespace SunEyeVision.Workflow;

/// <summary>当前进程内的通讯连接生命周期管理。协议驱动不依赖 WPF/UI。</summary>
public sealed class CommunicationRuntimeManager : IAsyncDisposable
{
    private readonly ConcurrentDictionary<string, ICommunicationSession> _sessions = new();
    private readonly string _opcUaCertificateStoreRoot;

    public CommunicationRuntimeManager(string? opcUaCertificateStoreRoot = null)
    {
        _opcUaCertificateStoreRoot = string.IsNullOrWhiteSpace(opcUaCertificateStoreRoot)
            ? Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SunEyeVision", "OPC Foundation", "CertificateStores")
            : Path.GetFullPath(opcUaCertificateStoreRoot);
    }

    public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
    public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;

    public IReadOnlyList<TrustedOpcUaCertificate> GetTrustedOpcUaCertificates() =>
        new OpcUaTrustStore(_opcUaCertificateStoreRoot).List();

    public TrustedOpcUaCertificate TrustOpcUaServerCertificate(string certificatePath) =>
        new OpcUaTrustStore(_opcUaCertificateStoreRoot).TrustFile(certificatePath);

    public bool RemoveTrustedOpcUaServerCertificate(string thumbprint) =>
        new OpcUaTrustStore(_opcUaCertificateStoreRoot).Remove(thumbprint);

    public async Task ConnectAsync(Communication communication, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(communication);
        ValidateSupportedSettings(communication);
        ICommunicationSession session = communication.ProtocolKey switch
        {
            "tcp-client" => new TcpClientSession(communication.Id, communication.Host, communication.Port, GetTimeout(communication)),
            "tcp-server" => new TcpServerSession(communication.Id, ParseBindAddress(communication.LocalHost), communication.LocalPort),
            "udp" => new UdpSession(communication.Id, ParseBindAddress(communication.LocalHost), communication.LocalPort,
                communication.RemoteHost, communication.RemotePort),
            "serial" => new SerialSession(communication),
            "modbus" or "modbus-tcp" or "modbus-rtu" => new ModbusSession(communication),
            "s7" => new S7Session(communication),
            "opcua" => new OpcUaSession(communication, _opcUaCertificateStoreRoot),
            "cip" => new CipSession(communication),
            _ => throw new NotSupportedException($"{communication.ProtocolDisplayName} 的真实协议驱动尚未接入。")
        };
        if (!_sessions.TryAdd(communication.Id, session))
        {
            await session.DisposeAsync().ConfigureAwait(false);
            throw new InvalidOperationException("该通讯设备已经处于连接状态。");
        }

        session.DataReceived += OnDataReceived;
        session.ConnectionClosed += OnConnectionClosed;
        try
        {
            await session.ConnectAsync(cancellationToken).ConfigureAwait(false);
        }
        catch
        {
            _sessions.TryRemove(communication.Id, out _);
            session.DataReceived -= OnDataReceived;
            session.ConnectionClosed -= OnConnectionClosed;
            await session.DisposeAsync().ConfigureAwait(false);
            throw;
        }
    }

    public async Task DisconnectAsync(string communicationId)
    {
        if (!_sessions.TryRemove(communicationId, out var session)) return;
        session.DataReceived -= OnDataReceived;
        session.ConnectionClosed -= OnConnectionClosed;
        await session.DisposeAsync().ConfigureAwait(false);
    }

    public async Task SendAsync(string communicationId, string text, CancellationToken cancellationToken = default) =>
        _ = await SendBytesAsync(communicationId, Encoding.UTF8.GetBytes(text), cancellationToken).ConfigureAwait(false);

    public async Task<int> SendBytesAsync(string communicationId, ReadOnlyMemory<byte> payload, CancellationToken cancellationToken = default)
    {
        if (!_sessions.TryGetValue(communicationId, out var session))
            throw new InvalidOperationException("设备尚未连接。");
        return await session.SendAsync(payload, cancellationToken).ConfigureAwait(false);
    }

    public Task<bool[]> ReadModbusCoilsAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).ReadCoilsAsync(address, count, cancellationToken);

    public Task<bool[]> ReadModbusInputsAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).ReadInputsAsync(address, count, cancellationToken);

    public Task<ushort[]> ReadModbusHoldingRegistersAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).ReadHoldingRegistersAsync(address, count, cancellationToken);

    public Task<ushort[]> ReadModbusInputRegistersAsync(string communicationId, ushort address, ushort count, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).ReadInputRegistersAsync(address, count, cancellationToken);

    public Task WriteModbusCoilAsync(string communicationId, ushort address, bool value, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).WriteCoilAsync(address, value, cancellationToken);

    public Task WriteModbusRegisterAsync(string communicationId, ushort address, ushort value, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).WriteRegisterAsync(address, value, cancellationToken);

    public Task WriteModbusCoilsAsync(string communicationId, ushort address, bool[] values, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).WriteCoilsAsync(address, values, cancellationToken);

    public Task WriteModbusRegistersAsync(string communicationId, ushort address, ushort[] values, CancellationToken cancellationToken = default) =>
        RequireModbusSession(communicationId).WriteRegistersAsync(address, values, cancellationToken);

    public Task<byte[]> ReadS7BytesAsync(string communicationId, DataType area, int dataBlock, int startByte, int count,
        CancellationToken cancellationToken = default) => RequireS7Session(communicationId).ReadBytesAsync(area, dataBlock, startByte, count, cancellationToken);

    public Task WriteS7BytesAsync(string communicationId, DataType area, int dataBlock, int startByte, ReadOnlyMemory<byte> value,
        CancellationToken cancellationToken = default) => RequireS7Session(communicationId).WriteBytesAsync(area, dataBlock, startByte, value, cancellationToken);

    public Task<IReadOnlyList<OpcUaBrowseItem>> BrowseOpcUaAsync(string communicationId, string nodeId, CancellationToken cancellationToken = default) =>
        RequireOpcUaSession(communicationId).BrowseAsync(nodeId, cancellationToken);

    public Task<DataValue> ReadOpcUaValueAsync(string communicationId, string nodeId, CancellationToken cancellationToken = default) =>
        RequireOpcUaSession(communicationId).ReadValueAsync(nodeId, cancellationToken);

    public Task WriteOpcUaValueAsync(string communicationId, string nodeId, object value, CancellationToken cancellationToken = default) =>
        RequireOpcUaSession(communicationId).WriteValueAsync(nodeId, value, cancellationToken);

    public Task SubscribeOpcUaValueAsync(string communicationId, string nodeId, int samplingIntervalMs, CancellationToken cancellationToken = default) =>
        RequireOpcUaSession(communicationId).SubscribeAsync(nodeId, samplingIntervalMs, cancellationToken);

    public Task UnsubscribeOpcUaValueAsync(string communicationId, string nodeId, CancellationToken cancellationToken = default) =>
        RequireOpcUaSession(communicationId).UnsubscribeAsync(nodeId, cancellationToken);

    public Task<CipExplicitResponse> SendCipExplicitAsync(string communicationId, byte service, uint classId, uint instanceId, uint attributeId,
        ReadOnlyMemory<byte> requestData, CancellationToken cancellationToken = default) =>
        RequireCipSession(communicationId).SendExplicitAsync(service, classId, instanceId, attributeId, requestData, cancellationToken);

    public bool IsConnected(string communicationId) =>
        _sessions.TryGetValue(communicationId, out var session) && session.IsConnected;

    public async ValueTask DisposeAsync()
    {
        foreach (var id in _sessions.Keys)
            await DisconnectAsync(id).ConfigureAwait(false);
    }

    private void OnDataReceived(object? sender, CommunicationDataReceivedEventArgs e) =>
        DataReceived?.Invoke(this, e);

    private void OnConnectionClosed(object? sender, CommunicationConnectionClosedEventArgs e)
    {
        if (_sessions.TryRemove(e.CommunicationId, out var session))
        {
            session.DataReceived -= OnDataReceived;
            session.ConnectionClosed -= OnConnectionClosed;
        }
        ConnectionClosed?.Invoke(this, e);
    }

    private static void ValidateSupportedSettings(Communication communication)
    {
        if (communication.ProtocolKey is not ("tcp-client" or "tcp-server" or "udp" or "serial" or "modbus" or "modbus-tcp" or "modbus-rtu" or "s7" or "opcua" or "cip"))
            throw new NotSupportedException($"{communication.ProtocolDisplayName} 的真实协议驱动尚未接入。");
        if (communication.ProtocolKey == "opcua")
        {
            if (!Uri.TryCreate(communication.EndpointUrl, UriKind.Absolute, out var endpoint) || endpoint.Scheme != "opc.tcp")
                throw new ArgumentException("OPC UA 服务端点必须是有效的 opc.tcp:// URL。", nameof(communication));
            if (communication.OpcUaIdentityMode != "匿名")
                throw new NotSupportedException("当前版本的 OPC UA 客户端仅支持匿名身份；用户名/密码凭据按当前需求暂不接入。请将服务器配置为允许匿名访问。");
        }
        else if (communication.ProtocolKey == "cip")
        {
            if (string.IsNullOrWhiteSpace(communication.Host)) throw new ArgumentException("EtherNet/IP CIP 目标 IP 不能为空。", nameof(communication));
            ValidatePort(communication.Port, "EtherNet/IP CIP 目标端口");
        }
        else if (communication.ProtocolKey == "tcp-client")
        {
            if (string.IsNullOrWhiteSpace(communication.Host)) throw new ArgumentException("目标 IP 不能为空。", nameof(communication));
            ValidatePort(communication.Port, "目标端口");
        }
        else if (communication.ProtocolKey == "s7")
        {
            if (string.IsNullOrWhiteSpace(communication.Host)) throw new ArgumentException("S7 目标 IP 不能为空。", nameof(communication));
            ValidatePort(communication.Port, "S7 目标端口");
            if (communication.Rack is < 0 or > 7) throw new ArgumentOutOfRangeException(nameof(communication.Rack), "机架号必须为 0 到 7。");
            if (communication.Slot is < 0 or > 31) throw new ArgumentOutOfRangeException(nameof(communication.Slot), "槽号必须为 0 到 31。");
            if (!Enum.TryParse<CpuType>(communication.S7CpuType, true, out var s7CpuType) || !Enum.IsDefined(s7CpuType))
                throw new ArgumentException($"不支持的 S7 CPU 类型：{communication.S7CpuType}。", nameof(communication));
        }
        else
        {
            if (communication.ProtocolKey == "serial")
            {
                ValidateSerialSettings(communication);
                return;
            }
            if (communication.IsModbus && !communication.IsModbusRtu)
            {
                if (string.IsNullOrWhiteSpace(communication.Host)) throw new ArgumentException("Modbus 目标 IP 不能为空。", nameof(communication));
                ValidatePort(communication.Port, "Modbus 目标端口");
                return;
            }
            if (communication.IsModbusRtu)
            {
                ValidateSerialSettings(communication);
                return;
            }
            _ = ParseBindAddress(communication.LocalHost);
            ValidatePort(communication.LocalPort, "本机端口");
            if (communication.ProtocolKey == "udp")
            {
                if (string.IsNullOrWhiteSpace(communication.RemoteHost)) throw new ArgumentException("UDP 目标 IP 不能为空。", nameof(communication));
                ValidatePort(communication.RemotePort, "UDP 目标端口");
            }
        }
    }

    private static void ValidateSerialSettings(Communication communication)
    {
        if (string.IsNullOrWhiteSpace(communication.SerialPort))
            throw new ArgumentException("串口号不能为空。", nameof(communication));
        if (communication.BaudRate is < 50 or > 4_000_000)
            throw new ArgumentOutOfRangeException(nameof(communication.BaudRate), "波特率超出有效范围。");
        if (communication.DataBits is < 5 or > 8)
            throw new ArgumentOutOfRangeException(nameof(communication.DataBits), "数据位必须为 5 到 8。");
        if (communication.StopBits is not (1 or 2))
            throw new ArgumentOutOfRangeException(nameof(communication.StopBits), "停止位必须为 1 或 2。");
    }

    private ModbusSession RequireModbusSession(string communicationId) =>
        _sessions.TryGetValue(communicationId, out var session) && session is ModbusSession modbus
            ? modbus
            : throw new InvalidOperationException("Modbus 设备尚未连接，或当前设备不是 Modbus 协议。");

    private S7Session RequireS7Session(string communicationId) =>
        _sessions.TryGetValue(communicationId, out var session) && session is S7Session s7
            ? s7 : throw new InvalidOperationException("S7 设备尚未连接，或当前设备不是 S7 协议。");

    private OpcUaSession RequireOpcUaSession(string communicationId) =>
        _sessions.TryGetValue(communicationId, out var session) && session is OpcUaSession opcua
            ? opcua : throw new InvalidOperationException("OPC UA 设备尚未连接，或当前设备不是 OPC UA 协议。");

    private CipSession RequireCipSession(string communicationId) =>
        _sessions.TryGetValue(communicationId, out var session) && session is CipSession cip
            ? cip : throw new InvalidOperationException("EtherNet/IP CIP 设备尚未连接，或当前设备不是 CIP 协议。");

    private static TimeSpan GetTimeout(Communication communication) =>
        TimeSpan.FromMilliseconds(Math.Clamp(communication.RequestTimeout, 250, 120_000));

    private static IPAddress ParseBindAddress(string value) =>
        string.IsNullOrWhiteSpace(value) ? IPAddress.Any : IPAddress.TryParse(value, out var address)
            ? address : throw new ArgumentException($"本机 IP 地址格式无效：{value}");

    private static void ValidatePort(int port, string name)
    {
        if (port is < 1 or > 65535) throw new ArgumentOutOfRangeException(name, "端口必须在 1 到 65535 之间。");
    }

    private interface ICommunicationSession : IAsyncDisposable
    {
        bool IsConnected { get; }
        event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        Task ConnectAsync(CancellationToken cancellationToken);
        Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken);
    }

    private interface IModbusSession
    {
        Task<bool[]> ReadCoilsAsync(ushort address, ushort count, CancellationToken cancellationToken);
        Task<bool[]> ReadInputsAsync(ushort address, ushort count, CancellationToken cancellationToken);
        Task<ushort[]> ReadHoldingRegistersAsync(ushort address, ushort count, CancellationToken cancellationToken);
        Task<ushort[]> ReadInputRegistersAsync(ushort address, ushort count, CancellationToken cancellationToken);
        Task WriteCoilAsync(ushort address, bool value, CancellationToken cancellationToken);
        Task WriteRegisterAsync(ushort address, ushort value, CancellationToken cancellationToken);
        Task WriteCoilsAsync(ushort address, bool[] values, CancellationToken cancellationToken);
        Task WriteRegistersAsync(ushort address, ushort[] values, CancellationToken cancellationToken);
    }

    private interface IS7Session
    {
        Task<byte[]> ReadBytesAsync(DataType area, int dataBlock, int startByte, int count, CancellationToken cancellationToken);
        Task WriteBytesAsync(DataType area, int dataBlock, int startByte, ReadOnlyMemory<byte> value, CancellationToken cancellationToken);
    }

    private sealed class S7Session : ICommunicationSession, IS7Session
    {
        private readonly Communication _communication;
        private readonly SemaphoreSlim _operationGate = new(1, 1);
        private Plc? _plc;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => _plc?.IsConnected == true;

        public S7Session(Communication communication) { _communication = communication; }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            if (!Enum.TryParse<CpuType>(_communication.S7CpuType, true, out var cpuType) || !Enum.IsDefined(cpuType))
                throw new ArgumentException($"不支持的 S7 CPU 类型：{_communication.S7CpuType}。");
            var plc = new Plc(cpuType, _communication.Host, _communication.Port, (short)_communication.Rack, (short)_communication.Slot);
            try { await plc.OpenAsync(cancellationToken).ConfigureAwait(false); _plc = plc; }
            catch { plc.Close(); throw; }
        }

        public async Task<byte[]> ReadBytesAsync(DataType area, int dataBlock, int startByte, int count, CancellationToken cancellationToken)
        {
            if (count is < 1 or > 2000) throw new ArgumentOutOfRangeException(nameof(count), "单次 S7 读取长度必须为 1 到 2000 字节。");
            if (dataBlock < 0 || startByte < 0) throw new ArgumentOutOfRangeException(nameof(startByte), "数据块与起始字节不能为负数。");
            var plc = _plc ?? throw new InvalidOperationException("S7 会话未连接。");
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { return await plc.ReadBytesAsync(area, dataBlock, startByte, count, cancellationToken).ConfigureAwait(false); }
            finally { _operationGate.Release(); }
        }

        public async Task WriteBytesAsync(DataType area, int dataBlock, int startByte, ReadOnlyMemory<byte> value, CancellationToken cancellationToken)
        {
            if (value.IsEmpty || value.Length > 2000) throw new ArgumentOutOfRangeException(nameof(value), "单次 S7 写入长度必须为 1 到 2000 字节。");
            if (dataBlock < 0 || startByte < 0) throw new ArgumentOutOfRangeException(nameof(startByte), "数据块与起始字节不能为负数。");
            var plc = _plc ?? throw new InvalidOperationException("S7 会话未连接。");
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await plc.WriteBytesAsync(area, dataBlock, startByte, value, cancellationToken).ConfigureAwait(false); }
            finally { _operationGate.Release(); }
        }

        public Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException("S7 是类型化 PLC 协议，请使用 S7 数据区读写操作，不支持原始字节发送。");

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0) { _plc?.Close(); _plc = null; _operationGate.Dispose(); }
            return ValueTask.CompletedTask;
        }
    }

    private interface IOpcUaSession
    {
        Task<IReadOnlyList<OpcUaBrowseItem>> BrowseAsync(string nodeId, CancellationToken cancellationToken);
        Task<DataValue> ReadValueAsync(string nodeId, CancellationToken cancellationToken);
        Task WriteValueAsync(string nodeId, object value, CancellationToken cancellationToken);
        Task SubscribeAsync(string nodeId, int samplingIntervalMs, CancellationToken cancellationToken);
        Task UnsubscribeAsync(string nodeId, CancellationToken cancellationToken);
    }

    private sealed class OpcUaSession : ICommunicationSession, IOpcUaSession
    {
        private readonly Communication _communication;
        private readonly string _pkiRoot;
        private readonly SemaphoreSlim _serviceGate = new(1, 1);
        private readonly List<Subscription> _subscriptions = new();
        private readonly Dictionary<string, (Subscription Subscription, MonitoredItem Item, SubscriptionNotificationGate Gate)> _monitoredItems = new(StringComparer.Ordinal);
        private Session? _session;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => _session?.Connected == true;

        public OpcUaSession(Communication communication, string pkiRoot)
        {
            _communication = communication;
            _pkiRoot = pkiRoot;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            var appName = "SunEyeVision";
            var appUri = $"urn:{Dns.GetHostName()}:SunEyeVision";
            var configuration = new ApplicationConfiguration
            {
                ApplicationName = appName,
                ApplicationUri = appUri,
                ApplicationType = ApplicationType.Client,
                SecurityConfiguration = new SecurityConfiguration
                {
                    ApplicationCertificate = new CertificateIdentifier { StoreType = "Directory", StorePath = Path.Combine(_pkiRoot, "MachineDefault"), SubjectName = "CN=SunEyeVision" },
                    TrustedPeerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(_pkiRoot, "UA Applications") },
                    TrustedIssuerCertificates = new CertificateTrustList { StoreType = "Directory", StorePath = Path.Combine(_pkiRoot, "UA Certificate Authorities") },
                    RejectedCertificateStore = new CertificateStoreIdentifier { StoreType = "Directory", StorePath = Path.Combine(_pkiRoot, "RejectedCertificates") },
                    AutoAcceptUntrustedCertificates = false,
                    AddAppCertToTrustedStore = true,
                    RejectSHA1SignedCertificates = true,
                    MinimumCertificateKeySize = 2048
                },
                TransportConfigurations = new TransportConfigurationCollection(),
                TransportQuotas = new TransportQuotas { OperationTimeout = _communication.RequestTimeout },
                ClientConfiguration = new ClientConfiguration { DefaultSessionTimeout = Math.Clamp(_communication.RequestTimeout * 10, 10_000, 600_000) },
                TraceConfiguration = new TraceConfiguration()
            };
            await configuration.Validate(ApplicationType.Client).ConfigureAwait(false);
            var application = new ApplicationInstance(configuration);
            await application.CheckApplicationInstanceCertificatesAsync(true, 2048, cancellationToken).ConfigureAwait(false);

            var requestedMode = _communication.SecurityMode;
            var requiredMode = requestedMode switch
            {
                "None" => MessageSecurityMode.None,
                "Sign" => MessageSecurityMode.Sign,
                "SignAndEncrypt" => MessageSecurityMode.SignAndEncrypt,
                _ => throw new ArgumentException($"不支持的 OPC UA 安全模式：{requestedMode}。", nameof(_communication))
            };
            using var discovery = await DiscoveryClient.CreateAsync(configuration, new Uri(_communication.EndpointUrl),
                EndpointConfiguration.Create(configuration), DiagnosticsMasks.None, cancellationToken).ConfigureAwait(false);
            var discoveredEndpoints = await discovery.GetEndpointsAsync(null, cancellationToken).ConfigureAwait(false);
            var selectedEndpoint = discoveredEndpoints
                .Where(endpoint => endpoint.SecurityMode == requiredMode && endpoint.UserIdentityTokens.Any(token => token.TokenType == UserTokenType.Anonymous))
                .OrderByDescending(endpoint => endpoint.SecurityLevel)
                .FirstOrDefault();
            if (selectedEndpoint == null)
                throw new InvalidOperationException($"服务器未发现同时满足安全模式 {requestedMode} 与匿名身份的 OPC UA Endpoint。");

            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(configuration));
            _session = await Session.Create(configuration, endpoint, false, _communication.Name,
                (uint)Math.Clamp(_communication.RequestTimeout * 10, 10_000, 600_000), new UserIdentity(), null, cancellationToken).ConfigureAwait(false);
        }

        public async Task<IReadOnlyList<OpcUaBrowseItem>> BrowseAsync(string nodeId, CancellationToken cancellationToken)
        {
            var session = RequireSession();
            var parsedNode = NodeId.Parse(nodeId);
            await _serviceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                session.Browse(null, null, parsedNode, 0u, BrowseDirection.Forward, ReferenceTypeIds.HierarchicalReferences,
                    true, (uint)(NodeClass.Object | NodeClass.Variable | NodeClass.Method), out _, out var references);
                return references.Select(reference => new OpcUaBrowseItem(reference.NodeId.ToString(), reference.DisplayName.Text, reference.NodeClass.ToString())).ToArray();
            }
            finally { _serviceGate.Release(); }
        }

        public async Task<DataValue> ReadValueAsync(string nodeId, CancellationToken cancellationToken)
        {
            var session = RequireSession();
            await _serviceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { cancellationToken.ThrowIfCancellationRequested(); return session.ReadValue(NodeId.Parse(nodeId)); }
            finally { _serviceGate.Release(); }
        }

        public async Task WriteValueAsync(string nodeId, object value, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(value);
            var session = RequireSession();
            await _serviceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var write = new WriteValue { NodeId = NodeId.Parse(nodeId), AttributeId = Attributes.Value, Value = new DataValue(new Variant(value)) };
                session.Write(null, new WriteValueCollection { write }, out var results, out _);
                if (results.Count != 1)
                    throw new ServiceResultException(StatusCodes.BadUnexpectedError, $"OPC UA 写入响应数量异常：请求 1 项，服务端返回 {results.Count} 项。");
                if (StatusCode.IsBad(results[0])) throw new ServiceResultException(results[0]);
            }
            finally { _serviceGate.Release(); }
        }

        public async Task SubscribeAsync(string nodeId, int samplingIntervalMs, CancellationToken cancellationToken)
        {
            if (samplingIntervalMs is < 10 or > 600_000) throw new ArgumentOutOfRangeException(nameof(samplingIntervalMs), "OPC UA 采样间隔范围为 10–600000 ms。");
            var parsedNodeId = NodeId.Parse(nodeId);
            var key = parsedNodeId.ToString();
            var session = RequireSession();
            await _serviceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (_monitoredItems.ContainsKey(key))
                    throw new InvalidOperationException($"NodeId {key} 已有活动订阅，请先取消订阅再重新设置采样间隔。");
                var subscription = new Subscription(session.DefaultSubscription) { PublishingInterval = samplingIntervalMs };
                var notificationGate = new SubscriptionNotificationGate();
                var item = new MonitoredItem(subscription.DefaultItem)
                {
                    StartNodeId = parsedNodeId,
                    AttributeId = Attributes.Value,
                    SamplingInterval = samplingIntervalMs,
                    DisplayName = nodeId
                };
                item.Notification += (_, args) =>
                {
                    if (notificationGate.IsActive && args.NotificationValue is MonitoredItemNotification notification)
                    {
                        var text = $"{item.DisplayName}: {notification.Value.Value} [{notification.Value.StatusCode}]";
                        DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communication.Id, Encoding.UTF8.GetBytes(text), DateTimeOffset.Now));
                    }
                };
                subscription.AddItem(item);
                session.AddSubscription(subscription);
                subscription.Create();
                subscription.ApplyChanges();
                _subscriptions.Add(subscription);
                _monitoredItems.Add(key, (subscription, item, notificationGate));
            }
            finally { _serviceGate.Release(); }
        }

        public async Task UnsubscribeAsync(string nodeId, CancellationToken cancellationToken)
        {
            var key = NodeId.Parse(nodeId).ToString();
            var session = RequireSession();
            await _serviceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (!_monitoredItems.TryGetValue(key, out var monitored))
                    return;

                monitored.Gate.IsActive = false;
                monitored.Subscription.RemoveItem(monitored.Item);
                try
                {
                    monitored.Subscription.ApplyChanges();
                    _monitoredItems.Remove(key);
                    if (!monitored.Subscription.MonitoredItems.Any())
                    {
                        monitored.Subscription.Delete(true);
                        session.RemoveSubscription(monitored.Subscription);
                        _subscriptions.Remove(monitored.Subscription);
                    }
                }
                catch
                {
                    monitored.Subscription.AddItem(monitored.Item);
                    monitored.Gate.IsActive = true;
                    throw;
                }
            }
            finally { _serviceGate.Release(); }
        }

        private Session RequireSession() => _session is { Connected: true } session ? session : throw new InvalidOperationException("OPC UA 会话尚未连接。");

        private sealed class SubscriptionNotificationGate
        {
            public volatile bool IsActive = true;
        }

        public Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException("OPC UA 是类型化服务协议，请使用浏览、读取、写入或订阅操作，不支持原始字节发送。");

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            await _serviceGate.WaitAsync().ConfigureAwait(false);
            try
            {
                foreach (var subscription in _subscriptions)
                {
                    try { subscription.Delete(true); } catch { }
                }
                _subscriptions.Clear();
                _monitoredItems.Clear();
                if (_session != null)
                {
                    try { await _session.CloseAsync().ConfigureAwait(false); } catch { }
                    _session.Dispose();
                    _session = null;
                }
            }
            finally { _serviceGate.Release(); _serviceGate.Dispose(); }
        }
    }

    private interface ICipSession
    {
        Task<CipExplicitResponse> SendExplicitAsync(byte service, uint classId, uint instanceId, uint attributeId,
            ReadOnlyMemory<byte> requestData, CancellationToken cancellationToken);
    }

    private sealed class CipSession : ICommunicationSession, ICipSession
    {
        private readonly Communication _communication;
        private readonly SemaphoreSlim _requestGate = new(1, 1);
        private TcpClient? _client;
        private NetworkStream? _stream;
        private uint _sessionHandle;
        private long _senderContext;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => _client?.Connected == true;

        public CipSession(Communication communication) => _communication = communication;

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            var client = new TcpClient();
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(GetTimeout(_communication));
            try
            {
                await client.ConnectAsync(_communication.Host, _communication.Port, timeout.Token).ConfigureAwait(false);
                _client = client;
                _stream = client.GetStream();
                var response = await ExchangeEncapsulationAsync(0x0065, 0, new byte[] { 1, 0, 0, 0 }, timeout.Token).ConfigureAwait(false);
                if (response.Status != 0 || response.Data.Length < 4 || BinaryPrimitives.ReadUInt16LittleEndian(response.Data) != 1)
                    throw new InvalidDataException("EtherNet/IP RegisterSession 响应无效。");
                _sessionHandle = response.SessionHandle;
            }
            catch
            {
                client.Dispose();
                _client = null;
                _stream = null;
                throw;
            }
        }

        public async Task<CipExplicitResponse> SendExplicitAsync(byte service, uint classId, uint instanceId, uint attributeId,
            ReadOnlyMemory<byte> requestData, CancellationToken cancellationToken)
        {
            if (service == 0 || service >= 0x80) throw new ArgumentOutOfRangeException(nameof(service), "CIP 请求服务码必须为 0x01–0x7F。");
            if (classId > ushort.MaxValue || instanceId > ushort.MaxValue || attributeId > ushort.MaxValue)
                throw new ArgumentOutOfRangeException(nameof(classId), "当前 CIP 显式消息路径的 Class/Instance/Attribute 最大为 65535。");
            var session = _sessionHandle;
            if (session == 0 || _stream == null) throw new InvalidOperationException("EtherNet/IP CIP 会话未注册。");

            var cipRequest = BuildCipRequest(service, classId, instanceId, attributeId, requestData.Span);
            var cpf = new byte[16 + cipRequest.Length];
            BinaryPrimitives.WriteUInt32LittleEndian(cpf, 0); // Interface handle: CIP
            BinaryPrimitives.WriteUInt16LittleEndian(cpf.AsSpan(4), 0); // timeout
            BinaryPrimitives.WriteUInt16LittleEndian(cpf.AsSpan(6), 2); // item count
            BinaryPrimitives.WriteUInt16LittleEndian(cpf.AsSpan(8), 0x0000); // Null Address Item
            BinaryPrimitives.WriteUInt16LittleEndian(cpf.AsSpan(10), 0);
            BinaryPrimitives.WriteUInt16LittleEndian(cpf.AsSpan(12), 0x00B2); // Unconnected Data Item
            BinaryPrimitives.WriteUInt16LittleEndian(cpf.AsSpan(14), checked((ushort)cipRequest.Length));
            cipRequest.CopyTo(cpf, 16);

            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            timeout.CancelAfter(GetTimeout(_communication));
            await _requestGate.WaitAsync(timeout.Token).ConfigureAwait(false);
            try
            {
                var response = await ExchangeEncapsulationAsync(0x006F, session, cpf, timeout.Token).ConfigureAwait(false);
                if (response.Status != 0) throw new IOException($"EtherNet/IP 封装层返回状态 0x{response.Status:X8}。");
                var cipResponse = ExtractCipResponse(response.Data);
                if (cipResponse.Service != (byte)(service | 0x80)) throw new InvalidDataException("CIP 响应服务码与请求不匹配。");
                return cipResponse;
            }
            catch
            {
                _client?.Dispose();
                _client = null;
                _stream = null;
                _sessionHandle = 0;
                throw;
            }
            finally { _requestGate.Release(); }
        }

        private async Task<EncapsulationResponse> ExchangeEncapsulationAsync(ushort command, uint sessionHandle, byte[] payload, CancellationToken cancellationToken)
        {
            var stream = _stream ?? throw new InvalidOperationException("EtherNet/IP TCP 流尚未打开。");
            var header = new byte[24];
            BinaryPrimitives.WriteUInt16LittleEndian(header, command);
            BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2), checked((ushort)payload.Length));
            BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), sessionHandle);
            var context = unchecked((ulong)Interlocked.Increment(ref _senderContext));
            BinaryPrimitives.WriteUInt64LittleEndian(header.AsSpan(12), context);
            await stream.WriteAsync(header, cancellationToken).ConfigureAwait(false);
            if (payload.Length > 0) await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);

            var responseHeader = new byte[24];
            await stream.ReadExactlyAsync(responseHeader, cancellationToken).ConfigureAwait(false);
            var responseLength = BinaryPrimitives.ReadUInt16LittleEndian(responseHeader.AsSpan(2));
            if (BinaryPrimitives.ReadUInt16LittleEndian(responseHeader) != command)
                throw new InvalidDataException("EtherNet/IP 响应命令与请求不匹配。");
            var responseData = new byte[responseLength];
            if (responseLength > 0) await stream.ReadExactlyAsync(responseData, cancellationToken).ConfigureAwait(false);
            return new EncapsulationResponse(
                BinaryPrimitives.ReadUInt32LittleEndian(responseHeader.AsSpan(4)),
                BinaryPrimitives.ReadUInt32LittleEndian(responseHeader.AsSpan(8)), responseData);
        }

        private static byte[] BuildCipRequest(byte service, uint classId, uint instanceId, uint attributeId, ReadOnlySpan<byte> data)
        {
            using var path = new MemoryStream();
            WriteLogicalSegment(path, 0x20, 0x21, classId);
            WriteLogicalSegment(path, 0x24, 0x25, instanceId);
            WriteLogicalSegment(path, 0x30, 0x31, attributeId);
            if ((path.Length & 1) != 0) path.WriteByte(0);
            var pathBytes = path.ToArray();
            if (pathBytes.Length / 2 > byte.MaxValue) throw new ArgumentOutOfRangeException(nameof(classId), "CIP 路径长度超出限制。");
            var request = new byte[2 + pathBytes.Length + data.Length];
            request[0] = service;
            request[1] = (byte)(pathBytes.Length / 2);
            pathBytes.CopyTo(request, 2);
            data.CopyTo(request.AsSpan(2 + pathBytes.Length));
            return request;
        }

        private static void WriteLogicalSegment(Stream stream, byte compactSegment, byte extendedSegment, uint value)
        {
            if (value <= byte.MaxValue)
            {
                stream.WriteByte(compactSegment);
                stream.WriteByte((byte)value);
            }
            else
            {
                stream.WriteByte(extendedSegment);
                stream.WriteByte(0);
                Span<byte> encoded = stackalloc byte[2];
                BinaryPrimitives.WriteUInt16LittleEndian(encoded, (ushort)value);
                stream.Write(encoded);
            }
        }

        private static CipExplicitResponse ExtractCipResponse(byte[] cpf)
        {
            if (cpf.Length < 8) throw new InvalidDataException("EtherNet/IP CPF 响应过短。");
            var itemCount = BinaryPrimitives.ReadUInt16LittleEndian(cpf.AsSpan(6));
            var offset = 8;
            for (var index = 0; index < itemCount; index++)
            {
                if (offset + 4 > cpf.Length) throw new InvalidDataException("EtherNet/IP CPF 项头不完整。");
                var type = BinaryPrimitives.ReadUInt16LittleEndian(cpf.AsSpan(offset));
                var length = BinaryPrimitives.ReadUInt16LittleEndian(cpf.AsSpan(offset + 2));
                offset += 4;
                if (offset + length > cpf.Length) throw new InvalidDataException("EtherNet/IP CPF 项数据不完整。");
                if (type == 0x00B2)
                {
                    var cip = cpf.AsSpan(offset, length);
                    if (cip.Length < 4) throw new InvalidDataException("CIP 响应报文过短。");
                    var additionalStatusWords = cip[3];
                    var dataOffset = 4 + additionalStatusWords * 2;
                    if (dataOffset > cip.Length) throw new InvalidDataException("CIP 附加状态长度超出报文。");
                    var additional = new ushort[additionalStatusWords];
                    for (var statusIndex = 0; statusIndex < additional.Length; statusIndex++)
                        additional[statusIndex] = BinaryPrimitives.ReadUInt16LittleEndian(cip.Slice(4 + statusIndex * 2, 2));
                    return new CipExplicitResponse(cip[0], cip[2], additional, cip[dataOffset..].ToArray());
                }
                offset += length;
            }
            throw new InvalidDataException("EtherNet/IP 响应中没有 Unconnected Data Item。");
        }

        public Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException("EtherNet/IP CIP 是类型化显式消息协议，请使用 CIP 服务/路径操作，不支持原始字节发送。");

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            await _requestGate.WaitAsync().ConfigureAwait(false);
            try
            {
                var session = _sessionHandle;
                if (session != 0 && _stream != null)
                {
                    try
                    {
                        var unregister = new byte[24];
                        BinaryPrimitives.WriteUInt16LittleEndian(unregister, 0x0066);
                        BinaryPrimitives.WriteUInt32LittleEndian(unregister.AsSpan(4), session);
                        BinaryPrimitives.WriteUInt64LittleEndian(unregister.AsSpan(12), unchecked((ulong)Interlocked.Increment(ref _senderContext)));
                        await _stream.WriteAsync(unregister, CancellationToken.None).ConfigureAwait(false);
                    }
                    catch { }
                }
                _client?.Dispose();
                _client = null;
                _stream = null;
                _sessionHandle = 0;
            }
            finally { _requestGate.Release(); _requestGate.Dispose(); }
        }

        private sealed record EncapsulationResponse(uint SessionHandle, uint Status, byte[] Data);
    }

    public sealed record CipExplicitResponse(byte Service, byte GeneralStatus, ushort[] AdditionalStatus, byte[] Data);

    public sealed record OpcUaBrowseItem(string NodeId, string DisplayName, string NodeClass);

    private sealed class SerialSession : ICommunicationSession
    {
        private readonly string _communicationId;
        private readonly SerialPort _port;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly SemaphoreSlim _writeGate = new(1, 1);
        private Task? _receiveTask;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => _port.IsOpen;

        public SerialSession(Communication communication)
        {
            _communicationId = communication.Id;
            _port = new SerialPort(communication.SerialPort, communication.BaudRate,
                ParseParity(communication.Parity), communication.DataBits, ParseStopBits(communication.StopBits))
            {
                ReadTimeout = Math.Max(100, communication.CharacterTimeout),
                WriteTimeout = Math.Max(100, communication.RequestTimeout)
            };
        }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _port.Open();
            _receiveTask = Task.Run(() => ReceiveLoopAsync(_lifetime.Token), CancellationToken.None);
            return Task.CompletedTask;
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            var buffer = new byte[4096];
            try
            {
                while (!cancellationToken.IsCancellationRequested && _port.IsOpen)
                {
                    try
                    {
                        var count = _port.Read(buffer, 0, buffer.Length);
                        if (count <= 0) continue;
                        var payload = new byte[count];
                        Buffer.BlockCopy(buffer, 0, payload, 0, count);
                        DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, payload, DateTimeOffset.Now));
                    }
                    catch (TimeoutException) when (!cancellationToken.IsCancellationRequested) { }
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (InvalidOperationException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) when (cancellationToken.IsCancellationRequested) { }
            catch (Exception) when (!cancellationToken.IsCancellationRequested) { }
            finally
            {
                ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
            }
            await Task.CompletedTask;
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            if (!_port.IsOpen) throw new InvalidOperationException("串口尚未打开。");
            await _writeGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                await Task.Run(() => _port.Write(payload.ToArray(), 0, payload.Length), cancellationToken).ConfigureAwait(false);
                return 1;
            }
            finally { _writeGate.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _lifetime.Cancel();
            if (_port.IsOpen) _port.Close();
            if (_receiveTask is not null)
            {
                try { await _receiveTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            _port.Dispose();
            _writeGate.Dispose();
            _lifetime.Dispose();
        }
    }

    private sealed class ModbusSession : ICommunicationSession, IModbusSession
    {
        private readonly Communication _communication;
        private readonly SemaphoreSlim _operationGate = new(1, 1);
        private TcpClient? _tcpClient;
        private SerialPort? _serialPort;
        private PortStreamResource? _serialResource;
        private IModbusMaster? _master;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => _master != null && (_tcpClient?.Connected ?? _serialPort?.IsOpen ?? false);

        public ModbusSession(Communication communication) => _communication = communication;

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            var factory = new ModbusFactory();
            if (!_communication.IsModbusRtu)
            {
                _tcpClient = new TcpClient();
                using var timeout = new CancellationTokenSource(GetTimeout(_communication));
                using var linked = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, timeout.Token);
                await _tcpClient.ConnectAsync(_communication.Host, _communication.Port, linked.Token).ConfigureAwait(false);
                _master = factory.CreateMaster(_tcpClient);
            }
            else
            {
                _serialPort = new SerialPort(_communication.SerialPort, _communication.BaudRate,
                    ParseParity(_communication.Parity), _communication.DataBits, ParseStopBits(_communication.StopBits))
                {
                    ReadTimeout = (int)GetTimeout(_communication).TotalMilliseconds,
                    WriteTimeout = (int)GetTimeout(_communication).TotalMilliseconds
                };
                _serialPort.Open();
                _serialResource = new PortStreamResource(_serialPort);
                _master = factory.CreateRtuMaster(_serialResource);
            }
            _master.Transport.ReadTimeout = (int)GetTimeout(_communication).TotalMilliseconds;
            _master.Transport.WriteTimeout = (int)GetTimeout(_communication).TotalMilliseconds;
            _master.Transport.Retries = 0;
        }

        public Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken) =>
            throw new NotSupportedException("Modbus 必须通过功能码进行类型化读写，不能发送任意原始数据。");

        public Task<bool[]> ReadCoilsAsync(ushort address, ushort count, CancellationToken cancellationToken) =>
            ExecuteAsync(master => master.ReadCoilsAsync(GetUnitId(), address, count), cancellationToken);

        public Task<bool[]> ReadInputsAsync(ushort address, ushort count, CancellationToken cancellationToken) =>
            ExecuteAsync(master => master.ReadInputsAsync(GetUnitId(), address, count), cancellationToken);

        public Task<ushort[]> ReadHoldingRegistersAsync(ushort address, ushort count, CancellationToken cancellationToken) =>
            ExecuteAsync(master => master.ReadHoldingRegistersAsync(GetUnitId(), address, count), cancellationToken);

        public Task<ushort[]> ReadInputRegistersAsync(ushort address, ushort count, CancellationToken cancellationToken) =>
            ExecuteAsync(master => master.ReadInputRegistersAsync(GetUnitId(), address, count), cancellationToken);

        public Task WriteCoilAsync(ushort address, bool value, CancellationToken cancellationToken) =>
            ExecuteAsync(master => master.WriteSingleCoilAsync(GetUnitId(), address, value), cancellationToken);

        public Task WriteRegisterAsync(ushort address, ushort value, CancellationToken cancellationToken) =>
            ExecuteAsync(master => master.WriteSingleRegisterAsync(GetUnitId(), address, value), cancellationToken);

        public Task WriteCoilsAsync(ushort address, bool[] values, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(values);
            if (values.Length is < 1 or > 1968) throw new ArgumentOutOfRangeException(nameof(values), "FC15 一次写入数量必须为 1 到 1968 个线圈。");
            if (address > ushort.MaxValue - values.Length + 1) throw new ArgumentOutOfRangeException(nameof(address), "线圈地址范围超出 Modbus 地址空间。");
            return ExecuteAsync(master => master.WriteMultipleCoilsAsync(GetUnitId(), address, values), cancellationToken);
        }

        public Task WriteRegistersAsync(ushort address, ushort[] values, CancellationToken cancellationToken)
        {
            ArgumentNullException.ThrowIfNull(values);
            if (values.Length is < 1 or > 123) throw new ArgumentOutOfRangeException(nameof(values), "FC16 一次写入数量必须为 1 到 123 个寄存器。");
            if (address > ushort.MaxValue - values.Length + 1) throw new ArgumentOutOfRangeException(nameof(address), "寄存器地址范围超出 Modbus 地址空间。");
            return ExecuteAsync(master => master.WriteMultipleRegistersAsync(GetUnitId(), address, values), cancellationToken);
        }

        private async Task<T> ExecuteAsync<T>(Func<IModbusMaster, Task<T>> operation, CancellationToken cancellationToken)
        {
            var master = _master ?? throw new InvalidOperationException("Modbus 会话尚未建立。");
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { return await operation(master).WaitAsync(cancellationToken).ConfigureAwait(false); }
            finally { _operationGate.Release(); }
        }

        private async Task ExecuteAsync(Func<IModbusMaster, Task> operation, CancellationToken cancellationToken)
        {
            var master = _master ?? throw new InvalidOperationException("Modbus 会话尚未建立。");
            await _operationGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try { await operation(master).WaitAsync(cancellationToken).ConfigureAwait(false); }
            finally { _operationGate.Release(); }
        }

        private byte GetUnitId() => _communication.UnitId is >= 0 and <= 255
            ? (byte)_communication.UnitId
            : throw new ArgumentOutOfRangeException(nameof(_communication.UnitId), "Modbus 站号必须在 0 到 255 之间。");

        public ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return ValueTask.CompletedTask;
            _master?.Dispose();
            _serialResource?.Dispose();
            _serialPort?.Dispose();
            _tcpClient?.Dispose();
            _operationGate.Dispose();
            return ValueTask.CompletedTask;
        }
    }

    private sealed class PortStreamResource : IStreamResource, IDisposable
    {
        private readonly SerialPort _port;
        public int InfiniteTimeout => Timeout.Infinite;
        public int ReadTimeout { get => _port.ReadTimeout; set => _port.ReadTimeout = value; }
        public int WriteTimeout { get => _port.WriteTimeout; set => _port.WriteTimeout = value; }

        public PortStreamResource(SerialPort port) => _port = port;
        public void DiscardInBuffer() => _port.DiscardInBuffer();
        public int Read(byte[] buffer, int offset, int count) => _port.Read(buffer, offset, count);
        public void Write(byte[] buffer, int offset, int count) => _port.Write(buffer, offset, count);
        public void Dispose() { }
    }

    private static Parity ParseParity(string value) => value switch
    {
        "奇校验" or "Odd" or "ODD PARITY" => Parity.Odd,
        "偶校验" or "Even" or "EVEN PARITY" => Parity.Even,
        "标记校验" or "Mark" or "MARK PARITY" => Parity.Mark,
        "空格校验" or "Space" or "SPACE PARITY" => Parity.Space,
        _ => Parity.None
    };

    private static StopBits ParseStopBits(int value) => value switch
    {
        1 => StopBits.One,
        2 => StopBits.Two,
        _ => throw new ArgumentOutOfRangeException(nameof(value), "停止位必须为 1 或 2。")
    };

    private sealed class TcpClientSession : ICommunicationSession
    {
        private readonly string _communicationId;
        private readonly string _host;
        private readonly int _port;
        private readonly TimeSpan _connectTimeout;
        private readonly TcpClient _client = new();
        private readonly CancellationTokenSource _lifetime = new();
        private NetworkStream? _stream;
        private Task? _receiveTask;
        private int _disposed;
        private int _connected;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => Volatile.Read(ref _connected) == 1;

        public TcpClientSession(string communicationId, string host, int port, TimeSpan connectTimeout)
        {
            _communicationId = communicationId;
            _host = host;
            _port = port;
            _connectTimeout = connectTimeout;
        }

        public async Task ConnectAsync(CancellationToken cancellationToken)
        {
            using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken, _lifetime.Token);
            timeout.CancelAfter(_connectTimeout);
            await _client.ConnectAsync(_host, _port, timeout.Token).ConfigureAwait(false);
            _stream = _client.GetStream();
            Volatile.Write(ref _connected, 1);
            _receiveTask = ReceiveLoopAsync(_stream, _lifetime.Token);
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            var stream = _stream ?? throw new InvalidOperationException("TCP 连接尚未建立。");
            await stream.WriteAsync(payload, cancellationToken).ConfigureAwait(false);
            await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
            return 1;
        }

        private async Task ReceiveLoopAsync(NetworkStream stream, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (count == 0) break;
                    var data = new byte[count];
                    Buffer.BlockCopy(buffer, 0, data, 0, count);
                    DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, data, DateTimeOffset.Now));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) { }
            catch (SocketException) { }
            finally
            {
                Volatile.Write(ref _connected, 0);
                ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
                _stream?.Dispose();
                _client.Dispose();
                _lifetime.Cancel();
                _lifetime.Dispose();
            }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _lifetime.Cancel();
            Volatile.Write(ref _connected, 0);
            _client.Close();
            if (_receiveTask is not null)
            {
                try { await _receiveTask.ConfigureAwait(false); }
                catch (OperationCanceledException) { }
            }
            _stream?.Dispose();
            _lifetime.Dispose();
            _client.Dispose();
        }
    }

    private sealed class TcpServerSession : ICommunicationSession
    {
        private readonly string _communicationId;
        private readonly TcpListener _listener;
        private readonly CancellationTokenSource _lifetime = new();
        private readonly ConcurrentDictionary<int, TcpClient> _clients = new();
        private readonly ConcurrentDictionary<int, Task> _receiveTasks = new();
        private readonly SemaphoreSlim _sendLock = new(1, 1);
        private Task? _acceptTask;
        private int _clientSequence;
        private int _running;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => Volatile.Read(ref _running) == 1;

        public TcpServerSession(string communicationId, IPAddress address, int port)
        {
            _communicationId = communicationId;
            _listener = new TcpListener(address, port);
        }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _listener.Start();
            Volatile.Write(ref _running, 1);
            _acceptTask = AcceptLoopAsync(_lifetime.Token);
            return Task.CompletedTask;
        }

        private async Task AcceptLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var client = await _listener.AcceptTcpClientAsync(cancellationToken).ConfigureAwait(false);
                    var id = Interlocked.Increment(ref _clientSequence);
                    _clients[id] = client;
                    _receiveTasks[id] = ReceiveClientAsync(id, client, cancellationToken);
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                Volatile.Write(ref _running, 0);
                if (!_lifetime.IsCancellationRequested)
                    ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
            }
        }

        private async Task ReceiveClientAsync(int id, TcpClient client, CancellationToken cancellationToken)
        {
            var buffer = new byte[8192];
            try
            {
                var stream = client.GetStream();
                while (!cancellationToken.IsCancellationRequested)
                {
                    var count = await stream.ReadAsync(buffer, cancellationToken).ConfigureAwait(false);
                    if (count == 0) break;
                    var data = new byte[count];
                    Buffer.BlockCopy(buffer, 0, data, 0, count);
                    DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, data, DateTimeOffset.Now));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (IOException) { }
            catch (SocketException) { }
            finally
            {
                _clients.TryRemove(id, out _);
                client.Dispose();
            }
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            var clients = _clients.ToArray();
            if (clients.Length == 0)
                throw new InvalidOperationException("TCP 服务端尚无已连接客户端，暂不能发送。");

            await _sendLock.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var delivered = 0;
                foreach (var (id, client) in clients)
                {
                    try
                    {
                        await client.GetStream().WriteAsync(payload, cancellationToken).ConfigureAwait(false);
                        delivered++;
                    }
                    catch (Exception ex) when (ex is IOException or SocketException or ObjectDisposedException or InvalidOperationException)
                    {
                        _clients.TryRemove(id, out _);
                        client.Dispose();
                    }
                }

                if (delivered == 0)
                    throw new IOException("发送失败：所有 TCP 客户端连接均已断开。");

                return delivered;
            }
            finally { _sendLock.Release(); }
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            Volatile.Write(ref _running, 0);
            _lifetime.Cancel();
            _listener.Stop();
            foreach (var client in _clients.Values) client.Dispose();
            if (_acceptTask != null)
            {
                try { await _acceptTask.ConfigureAwait(false); }
                catch (SocketException) { }
            }
            try { await Task.WhenAll(_receiveTasks.Values).ConfigureAwait(false); }
            catch (IOException) { }
            catch (ObjectDisposedException) { }
            catch (SocketException) { }
            _sendLock.Dispose();
            _lifetime.Dispose();
            ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
        }
    }

    private sealed class UdpSession : ICommunicationSession
    {
        private readonly string _communicationId;
        private readonly UdpClient _client;
        private readonly string _remoteHost;
        private readonly int _remotePort;
        private readonly CancellationTokenSource _lifetime = new();
        private Task? _receiveTask;
        private int _disposed;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<CommunicationConnectionClosedEventArgs>? ConnectionClosed;
        public bool IsConnected => !_lifetime.IsCancellationRequested;

        public UdpSession(string communicationId, IPAddress address, int localPort, string remoteHost, int remotePort)
        {
            _communicationId = communicationId;
            _client = new UdpClient(new IPEndPoint(address, localPort));
            _remoteHost = remoteHost;
            _remotePort = remotePort;
        }

        public Task ConnectAsync(CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            _receiveTask = ReceiveLoopAsync(_lifetime.Token);
            return Task.CompletedTask;
        }

        private async Task ReceiveLoopAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    var datagram = await _client.ReceiveAsync(cancellationToken).ConfigureAwait(false);
                    DataReceived?.Invoke(this, new CommunicationDataReceivedEventArgs(_communicationId, datagram.Buffer, DateTimeOffset.Now));
                }
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
            catch (ObjectDisposedException) when (cancellationToken.IsCancellationRequested) { }
            catch (SocketException) when (cancellationToken.IsCancellationRequested) { }
            finally
            {
                if (!_lifetime.IsCancellationRequested)
                    ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
            }
        }

        public async Task<int> SendAsync(ReadOnlyMemory<byte> payload, CancellationToken cancellationToken)
        {
            var addresses = await Dns.GetHostAddressesAsync(_remoteHost, cancellationToken).ConfigureAwait(false);
            addresses = Array.FindAll(addresses, address => address.AddressFamily == _client.Client.AddressFamily);
            if (addresses.Length == 0) throw new SocketException((int)SocketError.HostNotFound);
            await _client.SendAsync(payload.ToArray(), new IPEndPoint(addresses[0], _remotePort), cancellationToken).ConfigureAwait(false);
            return 1;
        }

        public async ValueTask DisposeAsync()
        {
            if (Interlocked.Exchange(ref _disposed, 1) != 0) return;
            _lifetime.Cancel();
            _client.Dispose();
            if (_receiveTask != null)
            {
                try { await _receiveTask.ConfigureAwait(false); }
                catch (SocketException) { }
            }
            _lifetime.Dispose();
            ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communicationId));
        }
    }
}

public sealed class CommunicationDataReceivedEventArgs : EventArgs
{
    public string CommunicationId { get; }
    public byte[] Data { get; }
    public DateTimeOffset Timestamp { get; }

    public CommunicationDataReceivedEventArgs(string communicationId, byte[] data, DateTimeOffset timestamp)
    {
        CommunicationId = communicationId;
        Data = data;
        Timestamp = timestamp;
    }
}

public sealed class CommunicationConnectionClosedEventArgs : EventArgs
{
    public string CommunicationId { get; }
    public CommunicationConnectionClosedEventArgs(string communicationId) => CommunicationId = communicationId;
}
