using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Opc.Ua;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.Workflow;
using ConfiguredCommunication = SunEyeVision.Workflow.Communication;

namespace SunEyeVision.UI.Services.Communication;

public sealed record CommunicationSendPlan(string DeviceId, string Protocol,
    CommunicationSendMessage Message, byte[] PayloadBytes, ushort[]? Registers = null,
    ushort StartAddress = 0, string? NodeId = null, object? Scalar = null);

/// <summary>Prepares all data before any device write. No WPF or workflow scheduling.</summary>
public sealed class CommunicationSendEventService
{
    private readonly CommunicationRuntimeManager _runtime;
    public CommunicationSendEventService(CommunicationRuntimeManager runtime) => _runtime = runtime;

    public CommunicationSendPlan Prepare(CommunicationSendEventDefinition definition,
        ConfiguredCommunication device, IReadOnlyDictionary<Guid, object?>? parameterSnapshot = null)
    {
        if (device.Id != definition.DeviceId) throw new InvalidOperationException("发送事件绑定设备不存在。");
        var message = CommunicationSendEventFormatter.Build(definition, parameterSnapshot);
        if (device.IsRawProtocol)
        {
            if (message.Utf8Bytes.Length == 0) throw new FormatException("发送内容为空，请配置参数内容或结束符。");
            return new CommunicationSendPlan(device.Id, "Raw", message, message.Utf8Bytes);
        }
        if (!string.IsNullOrEmpty(definition.Terminator))
            throw new FormatException("寄存器和原生数据点写入不使用文本结束符，请清空结束符。");
        if (device.IsModbus)
        {
            var point = device.ModbusAddressPoints.FirstOrDefault(item => item.Id == definition.TargetId)
                ?? throw new InvalidOperationException("请选择有效的 Modbus 地址块。");
            var types = message.Outputs.Select(item => item.DataType.ToLowerInvariant()).Distinct().ToArray();
            if (types.Length != 1 || types[0] is not ("int" or "float" or "string"))
                throw new FormatException("同一 Modbus 地址块的输出须统一为 int、float 或 string。");
            var content = types[0] == "string" ? message.Text : string.Join(";", message.Outputs.Select(item => item.Display));
            var payload = ModbusSendTestEncoder.EncodeDetailed(point, types[0], content);
            if (!string.IsNullOrEmpty(payload.TruncationMessage))
                throw new FormatException("发送内容超出地址块容量，未执行写入。");
            var bytes = new byte[payload.Registers.Length * 2];
            for (var index = 0; index < payload.Registers.Length; index++)
                BinaryPrimitives.WriteUInt16BigEndian(bytes.AsSpan(index * 2, 2), payload.Registers[index]);
            if (payload.Registers.Length > 123) throw new FormatException("单次 FC16 写入最多 123 个寄存器。");
            return new CommunicationSendPlan(device.Id, "Modbus", message, bytes,
                payload.Registers, checked((ushort)point.StartAddress));
        }
        if (device.IsOpcUa)
        {
            var point = device.OpcUaDataPoints?.FirstOrDefault(item => item.Id == definition.TargetId)
                ?? throw new InvalidOperationException("请选择有效的 OPC UA 数据点。");
            if (message.Outputs.Count != 1) throw new FormatException("OPC UA 单个数据点应配置一条最终输出。");
            if (!_runtime.IsConnected(device.Id) ||
                !_runtime.TryGetLatestOpcUaPoint(device.Id, point.NodeId, out var latest) || latest == null)
                throw new InvalidOperationException("请先连接 OPC UA 设备并刷新数据点，以确认类型和写权限。");
            if (!StatusCode.IsGood(latest.StatusCode) || !latest.CanWrite || !latest.IsSupportedScalar)
                throw new InvalidOperationException("OPC UA 数据点质量无效、只读或类型暂不支持写入。");
            var value = message.Outputs[0].Value;
            if (!CommunicationSendEventFormatter.MatchesType(value, latest.BuiltInType.ToString()))
                throw new FormatException($"OPC UA 数据点为 {latest.BuiltInType}，输出为 {value.GetType().Name}，类型不匹配。");
            return new CommunicationSendPlan(device.Id, "OpcUa", message, message.Utf8Bytes,
                NodeId: point.NodeId, Scalar: value);
        }
        throw new NotSupportedException("该设备尚未接入发送事件；当前支持 TCP、UDP、串口、Modbus 和 OPC UA。");
    }

    public async Task<int> SendAsync(CommunicationSendPlan plan, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        switch (plan.Protocol)
        {
            case "Raw": return await _runtime.SendBytesAsync(plan.DeviceId, plan.PayloadBytes, cancellationToken).ConfigureAwait(false);
            case "Modbus":
                await _runtime.WriteModbusRegistersAsync(plan.DeviceId, plan.StartAddress, plan.Registers!, cancellationToken).ConfigureAwait(false);
                return 1;
            case "OpcUa":
                await _runtime.WriteOpcUaValueAsync(plan.DeviceId, plan.NodeId!, plan.Scalar!, cancellationToken).ConfigureAwait(false);
                return 1;
            default: throw new NotSupportedException("无效的发送计划。");
        }
    }
}
