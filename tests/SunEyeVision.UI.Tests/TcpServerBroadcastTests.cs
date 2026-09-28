using System.Net;
using System.Net.Sockets;
using System.Threading.Channels;
using SunEyeVision.Workflow;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.CommunicationManager;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class TcpServerBroadcastTests
{
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
