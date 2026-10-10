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
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Devices.Communication;
using Communication = SunEyeVision.DeviceDriver.Communications.CommunicationDeviceSnapshot;

namespace SunEyeVision.DeviceDriver.Communications;

public sealed partial class CommunicationRuntimeManager
{
    private interface IOpcUaSession
    {
        Task<IReadOnlyList<OpcUaBrowseItem>> BrowseAsync(string nodeId, CancellationToken cancellationToken);
        Task<DataValue> ReadValueAsync(string nodeId, CancellationToken cancellationToken);
        Task<OpcUaPointValueEventArgs> ReadPointAsync(string nodeId, CancellationToken cancellationToken);
        Task WriteValueAsync(string nodeId, object value, CancellationToken cancellationToken);
        Task SubscribeAsync(string nodeId, int samplingIntervalMs, CancellationToken cancellationToken);
        Task UnsubscribeAsync(string nodeId, CancellationToken cancellationToken);
    }

    private sealed class OpcUaSession : ICommunicationAdapter, IOpcUaSession
    {
        private readonly Communication _communication;
        private readonly string _pkiRoot;
        private readonly SemaphoreSlim _serviceGate = new(1, 1);
        private readonly List<Subscription> _subscriptions = new();
        private readonly Dictionary<string, (Subscription Subscription, MonitoredItem Item, SubscriptionNotificationGate Gate)> _monitoredItems = new(StringComparer.Ordinal);
        private readonly ConcurrentDictionary<string, (BuiltInType Type, bool CanWrite, bool IsSupportedScalar)> _pointMetadata = new(StringComparer.Ordinal);
        private Session? _session;
        private int _disposed;
        private int _connectionClosedSignaled;

        public event EventHandler<CommunicationDataReceivedEventArgs>? DataReceived;
        public event EventHandler<OpcUaPointValueEventArgs>? PointValueChanged;
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

            using var discovery = await DiscoveryClient.CreateAsync(configuration, new Uri(_communication.EndpointUrl),
                EndpointConfiguration.Create(configuration), DiagnosticsMasks.None, cancellationToken).ConfigureAwait(false);
            var discoveredEndpoints = await discovery.GetEndpointsAsync(null, cancellationToken).ConfigureAwait(false);
            var selectedEndpoint = discoveredEndpoints
                .Where(endpoint => endpoint.SecurityMode == MessageSecurityMode.None && endpoint.UserIdentityTokens.Any(token => token.TokenType == UserTokenType.Anonymous))
                .OrderByDescending(endpoint => endpoint.SecurityLevel)
                .FirstOrDefault();
            if (selectedEndpoint == null)
                throw new InvalidOperationException("服务器未发现同时支持 None 安全模式与匿名身份的 OPC UA Endpoint。");

            var endpoint = new ConfiguredEndpoint(null, selectedEndpoint, EndpointConfiguration.Create(configuration));
            _session = await Session.Create(configuration, endpoint, false, _communication.Name,
                (uint)Math.Clamp(_communication.RequestTimeout * 10, 10_000, 600_000), new UserIdentity(), null, cancellationToken).ConfigureAwait(false);
            _session.KeepAlive += Session_KeepAlive;
        }

        private void Session_KeepAlive(ISession session, KeepAliveEventArgs e)
        {
            if (e.Status == null || !StatusCode.IsBad(e.Status.StatusCode) ||
                Interlocked.CompareExchange(ref _connectionClosedSignaled, 1, 0) != 0 || Volatile.Read(ref _disposed) != 0)
                return;
            ConnectionClosed?.Invoke(this, new CommunicationConnectionClosedEventArgs(_communication.Id));
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
            try { return await Task.Run(() => { cancellationToken.ThrowIfCancellationRequested(); return session.ReadValue(NodeId.Parse(nodeId)); }, cancellationToken).ConfigureAwait(false); }
            finally { _serviceGate.Release(); }
        }

        public async Task<OpcUaPointValueEventArgs> ReadPointAsync(string nodeId, CancellationToken cancellationToken)
        {
            var session = RequireSession();
            var parsedNode = NodeId.Parse(nodeId);
            var key = parsedNode.ToString();
            await _serviceGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                cancellationToken.ThrowIfCancellationRequested();
                var reads = new ReadValueIdCollection
                {
                    new ReadValueId { NodeId = parsedNode, AttributeId = Attributes.Value },
                    new ReadValueId { NodeId = parsedNode, AttributeId = Attributes.UserAccessLevel },
                    new ReadValueId { NodeId = parsedNode, AttributeId = Attributes.AccessLevel }
                };
                return await Task.Run(() =>
                {
                    session.Read(null, 0, TimestampsToReturn.Both, reads, out var values, out _);
                    if (values.Count < 3) throw new ServiceResultException(StatusCodes.BadUnexpectedError);
                    var value = values[0];
                    if (StatusCode.IsBad(value.StatusCode)) throw new ServiceResultException(value.StatusCode);
                    var type = value.WrappedValue.TypeInfo?.BuiltInType ?? BuiltInType.Null;
                    var accessValue = StatusCode.IsGood(values[1].StatusCode) ? values[1] : values[2];
                    var access = StatusCode.IsGood(accessValue.StatusCode) ? Convert.ToByte(accessValue.Value) : (byte)0;
                    var serverAccess = StatusCode.IsGood(values[2].StatusCode) ? Convert.ToByte(values[2].Value) : access;
                    var writable = (access & AccessLevels.CurrentWrite) != 0 && (serverAccess & AccessLevels.CurrentWrite) != 0;
                    var supported = value.Value is not Array and not ExtensionObject && type is
                        BuiltInType.Boolean or BuiltInType.SByte or BuiltInType.Byte or BuiltInType.Int16 or
                        BuiltInType.UInt16 or BuiltInType.Int32 or BuiltInType.UInt32 or BuiltInType.Int64 or
                        BuiltInType.UInt64 or BuiltInType.Float or BuiltInType.Double or BuiltInType.String;
                    _pointMetadata[key] = (type, writable, supported);
                    return new OpcUaPointValueEventArgs(_communication.Id, key, value.Value, type, writable,
                        value.StatusCode, value.SourceTimestamp == DateTime.MinValue ? DateTimeOffset.UtcNow : new DateTimeOffset(value.SourceTimestamp.ToUniversalTime()),
                        isSupportedScalar: supported);
                }, cancellationToken).ConfigureAwait(false);
            }
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
                await Task.Run(() =>
                {
                    session.Write(null, new WriteValueCollection { write }, out var results, out _);
                    if (results.Count != 1)
                        throw new ServiceResultException(StatusCodes.BadUnexpectedError, $"OPC UA 写入响应数量异常：请求 1 项，服务端返回 {results.Count} 项。");
                    if (StatusCode.IsBad(results[0])) throw new ServiceResultException(results[0]);
                }, cancellationToken).ConfigureAwait(false);
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
                        var metadata = _pointMetadata.TryGetValue(key, out var known) ? known : (BuiltInType.Null, false, false);
                        try
                        {
                            PointValueChanged?.Invoke(this, new OpcUaPointValueEventArgs(_communication.Id, key,
                                notification.Value.Value, metadata.Item1, metadata.Item2, notification.Value.StatusCode,
                                notification.Value.SourceTimestamp == DateTime.MinValue ? DateTimeOffset.UtcNow :
                                    new DateTimeOffset(notification.Value.SourceTimestamp.ToUniversalTime()),
                                isSupportedScalar: metadata.Item3));
                        }
                        catch (Exception ex)
                        {
                            VisionLogger.Instance.Log(LogLevel.Warning, $"OPC UA 数据点通知处理失败：{ex.Message}",
                                "CommunicationRuntime", ex, LogTarget.File);
                        }
                    }
                };
                await Task.Run(() =>
                {
                    subscription.AddItem(item);
                    session.AddSubscription(subscription);
                    subscription.Create();
                    subscription.ApplyChanges();
                    _subscriptions.Add(subscription);
                    _monitoredItems.Add(key, (subscription, item, notificationGate));
                }, cancellationToken).ConfigureAwait(false);
            }
            finally { _serviceGate.Release(); }
        }

        public async Task<(double Sampling, double Publishing)?> GetMonitoringIntervalsAsync(string nodeId, CancellationToken token)
        {
            await _serviceGate.WaitAsync(token).ConfigureAwait(false);
            try
            {
                return _monitoredItems.TryGetValue(NodeId.Parse(nodeId).ToString(), out var monitored)
                    ? (monitored.Item.SamplingInterval, monitored.Subscription.PublishingInterval) : null;
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

                await Task.Run(() =>
                {
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
                }, cancellationToken).ConfigureAwait(false);
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
                    _session.KeepAlive -= Session_KeepAlive;
                    try { await _session.CloseAsync().ConfigureAwait(false); } catch { }
                    _session.Dispose();
                    _session = null;
                }
            }
            finally { _serviceGate.Release(); _serviceGate.Dispose(); }
        }
    }
}
