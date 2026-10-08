using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using System.Text;
using SunEyeVision.Workflow;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.CommunicationManager;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class TcpServerBroadcastTests
{
    [Fact]
    public async Task Tcp_client_assembles_split_and_combined_terminated_messages()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        await using var runtime = new CommunicationRuntimeManager();
        var communication = new Communication
        {
            ProtocolKey = "tcp-client", Host = IPAddress.Loopback.ToString(), Port = port,
            EnableReceiveTerminator = true, ReceiveTerminator = "\\r\\n"
        };
        var received = Channel.CreateUnbounded<byte[]>();
        runtime.DataReceived += (_, args) => received.Writer.TryWrite(args.Data);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(communication), timeout.Token);
        using var peer = await listener.AcceptTcpClientAsync(timeout.Token);
        await peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes("ABC,"), timeout.Token);
        await peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes("2\r"), timeout.Token);
        await peer.GetStream().WriteAsync(Encoding.UTF8.GetBytes("\n4,5\r\n"), timeout.Token);

        Assert.Equal("ABC,2", Encoding.UTF8.GetString(await received.Reader.ReadAsync(timeout.Token)));
        Assert.Equal("4,5", Encoding.UTF8.GetString(await received.Reader.ReadAsync(timeout.Token)));
    }

    [Fact]
    public async Task Tcp_server_keeps_terminated_message_buffers_separate_for_each_client()
    {
        using var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();
        await using var runtime = new CommunicationRuntimeManager();
        var communication = new Communication
        {
            ProtocolKey = "tcp-server", LocalHost = IPAddress.Loopback.ToString(), LocalPort = port,
            EnableReceiveTerminator = true, ReceiveTerminator = "\n"
        };
        var received = Channel.CreateUnbounded<byte[]>();
        runtime.DataReceived += (_, args) => received.Writer.TryWrite(args.Data);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(communication), timeout.Token);
        using var clientA = new TcpClient();
        using var clientB = new TcpClient();
        await clientA.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
        await clientB.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
        await clientA.GetStream().WriteAsync(Encoding.UTF8.GetBytes("A"), timeout.Token);
        await clientB.GetStream().WriteAsync(Encoding.UTF8.GetBytes("B\n"), timeout.Token);
        Assert.Equal("B", Encoding.UTF8.GetString(await received.Reader.ReadAsync(timeout.Token)));
        await clientA.GetStream().WriteAsync(Encoding.UTF8.GetBytes("1\n"), timeout.Token);
        Assert.Equal("A1", Encoding.UTF8.GetString(await received.Reader.ReadAsync(timeout.Token)));
    }

    [Fact]
    public async Task Send_broadcasts_to_every_connected_client()
    {
        using var portProbe = new TcpListener(IPAddress.Loopback, 0);
        portProbe.Start();
        var port = ((IPEndPoint)portProbe.LocalEndpoint).Port;
        portProbe.Stop();

        await using var runtime = new CommunicationRuntimeManager();
        var communication = new Communication
        {
            ProtocolKey = "tcp-server",
            LocalHost = IPAddress.Loopback.ToString(),
            LocalPort = port
        };
        var received = Channel.CreateUnbounded<byte[]>();
        runtime.DataReceived += (_, args) => received.Writer.TryWrite(args.Data);
        using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        await runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(communication), timeout.Token);
        using var clientA = new TcpClient();
        using var clientB = new TcpClient();
        await clientA.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
        await clientA.GetStream().WriteAsync(new byte[] { 0xA1 }, timeout.Token);
        Assert.Equal(new byte[] { 0xA1 }, await received.Reader.ReadAsync(timeout.Token));

        await clientB.ConnectAsync(IPAddress.Loopback, port, timeout.Token);
        await clientB.GetStream().WriteAsync(new byte[] { 0xB2 }, timeout.Token);
        Assert.Equal(new byte[] { 0xB2 }, await received.Reader.ReadAsync(timeout.Token));

        var payload = new byte[] { 0x11, 0x22, 0x33 };
        var deliveredClientCount = await runtime.SendBytesAsync(communication.Id, payload, timeout.Token);
        Assert.Equal(2, deliveredClientCount);

        var receivedByA = new byte[payload.Length];
        var receivedByB = new byte[payload.Length];
        await Task.WhenAll(
            clientA.GetStream().ReadExactlyAsync(receivedByA, timeout.Token).AsTask(),
            clientB.GetStream().ReadExactlyAsync(receivedByB, timeout.Token).AsTask());

        Assert.Equal(payload, receivedByA);
        Assert.Equal(payload, receivedByB);
    }
}
