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
    private interface ICipSession
    {
        Task<CipExplicitResponse> SendExplicitAsync(byte service, uint classId, uint instanceId, uint attributeId,
            ReadOnlyMemory<byte> requestData, CancellationToken cancellationToken);
    }

    private sealed class CipSession : ICommunicationAdapter, ICipSession
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
}

