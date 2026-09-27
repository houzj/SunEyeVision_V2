using System.Buffers.Binary;
using System.Net;
using System.Net.Sockets;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class ModbusRuntimeTests
{
    [Fact]
    public async Task Serial_requires_a_port_name_before_opening_a_device()
    {
        await using var runtime = new CommunicationRuntimeManager();
        var communication = new Communication("Serial test", CommunicationType.Serial)
        {
            ProtocolKey = "serial",
            SerialPort = string.Empty
        };

        await Assert.ThrowsAsync<ArgumentException>(() => runtime.ConnectAsync(communication));
        Assert.False(runtime.IsConnected(communication.Id));
    }

    [Fact]
    public async Task Opc_ua_rejects_a_non_opc_endpoint_before_connecting()
    {
        await using var runtime = new CommunicationRuntimeManager();
        var communication = new Communication("OPC UA test", CommunicationType.OPCUA)
        {
            ProtocolKey = "opcua",
            EndpointUrl = "http://127.0.0.1:4840"
        };

        await Assert.ThrowsAsync<ArgumentException>(() => runtime.ConnectAsync(communication));
        Assert.False(runtime.IsConnected(communication.Id));
    }

    [Fact]
    public async Task Modbus_tcp_executes_typed_register_read_and_write_requests()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
        var simulator = RunModbusSimulatorAsync(listener, timeout.Token);

        await using var runtime = new CommunicationRuntimeManager();
        var communication = new Communication("Modbus TCP test", CommunicationType.ModbusTCP)
        {
            ProtocolKey = "modbus",
            ModbusMode = "TcpClient",
            Host = IPAddress.Loopback.ToString(),
            Port = port,
            UnitId = 7,
            RequestTimeout = 2000
        };

        await runtime.ConnectAsync(communication, timeout.Token);
        var registers = await runtime.ReadModbusHoldingRegistersAsync(communication.Id, 12, 2, timeout.Token);
        Assert.Equal(new ushort[] { 0x1234, 0xABCD }, registers);

        await runtime.WriteModbusRegisterAsync(communication.Id, 20, 0x55AA, timeout.Token);
        await runtime.WriteModbusCoilsAsync(communication.Id, 30, new[] { true, false, true }, timeout.Token);
        await runtime.WriteModbusRegistersAsync(communication.Id, 40, new ushort[] { 10, 20, 0xABCD }, timeout.Token);
        await simulator;
    }

    private static async Task RunModbusSimulatorAsync(TcpListener listener, CancellationToken cancellationToken)
    {
        using var client = await listener.AcceptTcpClientAsync(cancellationToken);
        var stream = client.GetStream();
        for (var requestIndex = 0; requestIndex < 4; requestIndex++)
        {
            var header = new byte[7];
            await stream.ReadExactlyAsync(header, cancellationToken);
            var length = BinaryPrimitives.ReadUInt16BigEndian(header.AsSpan(4, 2));
            var requestData = new byte[length - 1];
            await stream.ReadExactlyAsync(requestData, cancellationToken);
            Assert.Equal(7, header[6]);

            byte[] responseData;
            if (requestData[0] == 3)
            {
                Assert.Equal((ushort)12, BinaryPrimitives.ReadUInt16BigEndian(requestData.AsSpan(1, 2)));
                Assert.Equal((ushort)2, BinaryPrimitives.ReadUInt16BigEndian(requestData.AsSpan(3, 2)));
                responseData = new byte[] { 3, 4, 0x12, 0x34, 0xAB, 0xCD };
            }
            else if (requestData[0] == 6)
            {
                Assert.Equal((ushort)20, BinaryPrimitives.ReadUInt16BigEndian(requestData.AsSpan(1, 2)));
                Assert.Equal((ushort)0x55AA, BinaryPrimitives.ReadUInt16BigEndian(requestData.AsSpan(3, 2)));
                responseData = requestData;
            }
            else if (requestData[0] == 15)
            {
                Assert.Equal((ushort)30, BinaryPrimitives.ReadUInt16BigEndian(requestData.AsSpan(1, 2)));
                Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16BigEndian(requestData.AsSpan(3, 2)));
                Assert.Equal(new byte[] { 1, 0b00000101 }, requestData.AsSpan(5).ToArray());
                responseData = new byte[] { 15, 0, 30, 0, 3 };
            }
            else
            {
                Assert.Equal((byte)16, requestData[0]);
                Assert.Equal((ushort)40, BinaryPrimitives.ReadUInt16BigEndian(requestData.AsSpan(1, 2)));
                Assert.Equal((ushort)3, BinaryPrimitives.ReadUInt16BigEndian(requestData.AsSpan(3, 2)));
                Assert.Equal(new byte[] { 6, 0, 10, 0, 20, 0xAB, 0xCD }, requestData.AsSpan(5).ToArray());
                responseData = new byte[] { 16, 0, 40, 0, 3 };
            }

            var response = new byte[7 + responseData.Length];
            header.AsSpan(0, 4).CopyTo(response);
            BinaryPrimitives.WriteUInt16BigEndian(response.AsSpan(4, 2), (ushort)(responseData.Length + 1));
            response[6] = header[6];
            responseData.CopyTo(response, 7);
            await stream.WriteAsync(response, cancellationToken);
        }
    }
}
