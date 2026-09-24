using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CipRuntimeTests
{
    [Fact]
    public async Task Cip_client_registers_encapsulation_session_and_executes_explicit_request()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var simulator = RunDeviceSimulatorAsync(listener, timeout.Token);

        await using var runtime = new CommunicationRuntimeManager();
        var communication = new Communication("CIP test", CommunicationType.TCP)
        {
            ProtocolKey = "cip",
            Host = IPAddress.Loopback.ToString(),
            Port = port
        };

        await runtime.ConnectAsync(communication, timeout.Token);
        var response = await runtime.SendCipExplicitAsync(communication.Id, 0x0E, 1, 1, 1, ReadOnlyMemory<byte>.Empty, timeout.Token);
        Assert.Equal((byte)0x8E, response.Service);
        Assert.Equal((byte)0, response.GeneralStatus);
        Assert.Equal(new byte[] { 0x34, 0x12 }, response.Data);
        await runtime.DisconnectAsync(communication.Id);
        await simulator;
    }

    private static async Task RunDeviceSimulatorAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        var stream = client.GetStream();
        var registerHeader = await ReadEncapsulationHeaderAsync(stream, cancellationToken);
        Assert.Equal((ushort)0x0065, BinaryPrimitives.ReadUInt16LittleEndian(registerHeader));
        Assert.Equal((ushort)1, BinaryPrimitives.ReadUInt16LittleEndian(registerHeader.AsSpan(24)));
        await WriteEncapsulationAsync(stream, 0x0065, 0x12345678, new byte[] { 1, 0, 0, 0 }, cancellationToken);

        var requestHeader = await ReadEncapsulationHeaderAsync(stream, cancellationToken);
        Assert.Equal((ushort)0x006F, BinaryPrimitives.ReadUInt16LittleEndian(requestHeader));
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(requestHeader.AsSpan(4)));
        var requestCpf = requestHeader.AsSpan(24);
        Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16LittleEndian(requestCpf.Slice(6, 2)));
        var requestLength = BinaryPrimitives.ReadUInt16LittleEndian(requestCpf.Slice(14, 2));
        var requestCip = requestCpf.Slice(16, requestLength);
        Assert.Equal(new byte[] { 0x0E, 3, 0x20, 1, 0x24, 1, 0x30, 1 }, requestCip.ToArray());

        var cipResponse = new byte[] { 0x8E, 0, 0, 0, 0x34, 0x12 };
        var responseCpf = new byte[16 + cipResponse.Length];
        BinaryPrimitives.WriteUInt16LittleEndian(responseCpf.AsSpan(6), 2);
        BinaryPrimitives.WriteUInt16LittleEndian(responseCpf.AsSpan(12), 0x00B2);
        BinaryPrimitives.WriteUInt16LittleEndian(responseCpf.AsSpan(14), (ushort)cipResponse.Length);
        cipResponse.CopyTo(responseCpf, 16);
        await WriteEncapsulationAsync(stream, 0x006F, 0x12345678, responseCpf, cancellationToken);

        var unregisterHeader = new byte[24];
        await stream.ReadExactlyAsync(unregisterHeader, cancellationToken);
        Assert.Equal((ushort)0x0066, BinaryPrimitives.ReadUInt16LittleEndian(unregisterHeader));
        Assert.Equal(0x12345678u, BinaryPrimitives.ReadUInt32LittleEndian(unregisterHeader.AsSpan(4)));
    }

    private static async Task<byte[]> ReadEncapsulationHeaderAsync(NetworkStream stream, CancellationToken cancellationToken)
    {
        var header = new byte[24];
        await stream.ReadExactlyAsync(header, cancellationToken);
        var payloadLength = BinaryPrimitives.ReadUInt16LittleEndian(header.AsSpan(2));
        Array.Resize(ref header, 24 + payloadLength);
        if (payloadLength > 0) await stream.ReadExactlyAsync(header.AsMemory(24), cancellationToken);
        return header;
    }

    private static async Task WriteEncapsulationAsync(NetworkStream stream, ushort command, uint sessionHandle, byte[] payload, CancellationToken cancellationToken)
    {
        var header = new byte[24];
        BinaryPrimitives.WriteUInt16LittleEndian(header, command);
        BinaryPrimitives.WriteUInt16LittleEndian(header.AsSpan(2), (ushort)payload.Length);
        BinaryPrimitives.WriteUInt32LittleEndian(header.AsSpan(4), sessionHandle);
        if (payload.Length > 0)
        {
            await stream.WriteAsync(header, cancellationToken);
            await stream.WriteAsync(payload, cancellationToken);
        }
        else await stream.WriteAsync(header, cancellationToken);
    }
}
