using System;
using System.Collections.Generic;
using System.Net;
using SunEyeVision.Plugin.SDK.Devices.Communication;

namespace SunEyeVision.DeviceDriver.Communications;

public sealed partial class CommunicationRuntimeManager
{
    private sealed class BuiltInFactory : ICommunicationProtocolFactory
    {
        private readonly Func<CommunicationDeviceSnapshot, ICommunicationAdapter> _create;

        public BuiltInFactory(string protocolId, CommunicationCapabilities capabilities,
            Func<CommunicationDeviceSnapshot, ICommunicationAdapter> create)
        {
            ProtocolId = protocolId;
            Capabilities = capabilities;
            _create = create;
        }

        public string ProtocolId { get; }
        public CommunicationCapabilities Capabilities { get; }
        public void Validate(CommunicationDeviceSnapshot configuration) => ValidateSupportedSettings(configuration);
        public ICommunicationAdapter Create(CommunicationDeviceSnapshot configuration) => _create(configuration);
    }

    private static IReadOnlyDictionary<string, ICommunicationProtocolFactory> BuildFactoryRegistry(
        IEnumerable<ICommunicationProtocolFactory> factories)
    {
        var registry = new Dictionary<string, ICommunicationProtocolFactory>(StringComparer.OrdinalIgnoreCase);
        foreach (var factory in factories)
        {
            if (string.IsNullOrWhiteSpace(factory.ProtocolId) || !registry.TryAdd(factory.ProtocolId, factory))
                throw new InvalidOperationException($"重复或无效的通讯协议 ID：{factory.ProtocolId}");
        }
        return registry;
    }

    private static IEnumerable<ICommunicationProtocolFactory> CreateDefaultFactories(string certificateStoreRoot)
    {
        const CommunicationCapabilities raw = CommunicationCapabilities.RawSend | CommunicationCapabilities.RawReceive;
        const CommunicationCapabilities readWrite = CommunicationCapabilities.Read | CommunicationCapabilities.Write;

        yield return new BuiltInFactory("tcp-client", raw,
            config => new TcpClientSession(config.Id, config.Host, config.Port, GetTimeout(config)));
        yield return new BuiltInFactory("tcp-server", raw,
            config => new TcpServerSession(config.Id, ParseBindAddress(config.LocalHost), config.LocalPort));
        yield return new BuiltInFactory("udp", raw,
            config => new UdpSession(config.Id, ParseBindAddress(config.LocalHost), config.LocalPort,
                config.RemoteHost, config.RemotePort));
        yield return new BuiltInFactory("serial", raw, config => new SerialSession(config));
        yield return new BuiltInFactory("modbus", readWrite, config => new ModbusSession(config));
        yield return new BuiltInFactory("modbus-tcp", readWrite, config => new ModbusSession(config));
        yield return new BuiltInFactory("modbus-rtu", readWrite, config => new ModbusSession(config));
        yield return new BuiltInFactory("s7", readWrite, config => new S7Session(config));
        yield return new BuiltInFactory("cip", readWrite, config => new CipSession(config));
        yield return new BuiltInFactory("opcua", readWrite | CommunicationCapabilities.Browse |
            CommunicationCapabilities.Subscribe, config => new OpcUaSession(config, certificateStoreRoot));
    }
}
