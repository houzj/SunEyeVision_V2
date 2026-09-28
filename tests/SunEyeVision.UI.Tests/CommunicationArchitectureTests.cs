using System;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.UI.Services.Communication;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class CommunicationArchitectureTests
{
    [Fact]
    public void Communication_configuration_round_trip_does_not_persist_runtime_values()
    {
        var point = new ModbusAddressPoint
        {
            Name = "温度", StartAddress = 5, Count = 2, CurrentValue = "1234"
        };
        point.ReceiveRecords.Add("runtime-only");
        var device = new Communication("PLC", CommunicationType.ModbusTCP)
        {
            ProtocolKey = "modbus",
            ModbusAddressPoints = [point], RuntimeStatus = CommunicationRuntimeStatus.Connected
        };

        var json = JsonSerializer.Serialize(device, WorkflowSerializationOptions.Default);
        Assert.DoesNotContain("CurrentValue", json);
        Assert.DoesNotContain("ReceiveRecords", json);
        Assert.DoesNotContain("RuntimeStatus", json);

        var restored = JsonSerializer.Deserialize<Communication>(json, WorkflowSerializationOptions.Default);
        Assert.NotNull(restored);
        Assert.Equal(device.Id, restored.Id);
        Assert.Equal("modbus", restored.ProtocolKey);
        Assert.Equal(point.Id, Assert.Single(restored.ModbusAddressPoints).Id);
        Assert.Equal(5, restored.ModbusAddressPoints[0].StartAddress);
        Assert.Equal(string.Empty, restored.ModbusAddressPoints[0].CurrentValue);
    }

    [Fact]
    public void Address_table_bounds_round_trip_and_cannot_hide_allocated_blocks()
    {
        var device = new Communication("PLC", CommunicationType.ModbusTCP) { ProtocolKey = "modbus" };
        var workspace = new CommunicationDeviceWorkspaceViewModel(device);
        workspace.ModbusRegisterStartAddress = 5;
        workspace.ModbusRegisterCount = 45;
        var point = workspace.AddModbusAddressPoint(10);
        point.Count = 2;

        workspace.ModbusRegisterCount = 5;
        Assert.Equal(45, workspace.ModbusRegisterCount);
        Assert.Equal(45, device.ModbusRegisterCount);
        Assert.Contains(point.Name, workspace.AddressRangeWarning);

        var json = JsonSerializer.Serialize(device, WorkflowSerializationOptions.Default);
        var restored = JsonSerializer.Deserialize<Communication>(json, WorkflowSerializationOptions.Default)!;
        var reopened = new CommunicationDeviceWorkspaceViewModel(restored);
        Assert.Equal(5, reopened.ModbusRegisterStartAddress);
        Assert.Equal(45, reopened.ModbusRegisterCount);
        Assert.Equal(10, Assert.Single(reopened.ModbusAddressPoints).StartAddress);

        reopened.ClearAllModbusAddressPoints();
        Assert.Empty(reopened.ModbusAddressPoints);
        Assert.Empty(restored.ModbusAddressPoints);
    }

    [Fact]
    public async Task Missing_protocol_driver_is_diagnostic_and_does_not_create_a_session()
    {
        await using var runtime = new CommunicationRuntimeManager();
        var device = new Communication("future protocol", CommunicationType.Other)
        {
            ProtocolKey = "not-installed"
        };

        var error = await Assert.ThrowsAsync<NotSupportedException>(
            () => runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(device)));
        Assert.Contains("驱动不可用", error.Message);
        Assert.False(runtime.IsConnected(device.Id));
    }

    [Fact]
    public void Repository_replaces_solution_with_recoverable_previous_version()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SunEyeVisionCommunicationTest", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var path = Path.Combine(directory, "test.solution");
            var repository = new SolutionRepository();
            var solution = new Solution { Version = "first" };
            var legacyDevice = new Communication("legacy", CommunicationType.TCP) { ProtocolKey = "tcp-client" };
            legacyDevice.Settings["autoConnect"] = false;
            solution.Communications.Add(legacyDevice);
            Assert.True(repository.Save(solution, path));
            solution.Version = "second";
            Assert.True(repository.Save(solution, path));

            using var current = JsonDocument.Parse(File.ReadAllText(path));
            using var backup = JsonDocument.Parse(File.ReadAllText(path + ".bak"));
            Assert.Equal("second", current.RootElement.GetProperty("Version").GetString());
            Assert.Equal("first", backup.RootElement.GetProperty("Version").GetString());
            Assert.DoesNotContain("autoConnect", File.ReadAllText(path));
            Assert.Empty(Directory.EnumerateFiles(directory, "*.tmp").ToArray());

            var overlapping = new Communication("bad", CommunicationType.ModbusTCP)
            {
                ProtocolKey = "modbus",
                ModbusAddressPoints =
                [
                    new ModbusAddressPoint { StartAddress = 1, Count = 2 },
                    new ModbusAddressPoint { StartAddress = 2, Count = 2 }
                ]
            };
            solution.Communications.Add(overlapping);
            Assert.False(repository.Save(solution, path));
            using var retained = JsonDocument.Parse(File.ReadAllText(path));
            Assert.Equal("second", retained.RootElement.GetProperty("Version").GetString());
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact(Timeout = 15000)]
    public async Task Solution_lifecycle_auto_connects_and_disconnects_without_opening_manager_window()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SunEyeVisionCommunicationTest", Guid.NewGuid().ToString("N"));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        Directory.CreateDirectory(directory);
        try
        {
            var manager = new SolutionManager(directory);
            await using var composition = new CommunicationRuntimeComposition();
            await using var service = new CommunicationApplicationService(manager, composition);
            var device = new Communication("loopback", CommunicationType.TCP)
            {
                ProtocolKey = "tcp-client", Host = "127.0.0.1", Port = port,
                IsEnabled = true
            };
            var disabled = new Communication("disabled", CommunicationType.TCP)
            {
                ProtocolKey = "tcp-client", Host = "127.0.0.1", Port = port, IsEnabled = false
            };
            // A legacy disabled autoConnect flag must not suppress list-based restore.
            device.Settings["autoConnect"] = false;
            var solution = new Solution { Communications = [device, disabled] };

            manager.SetCurrentSolution(solution);
            using var accepted = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await service.ReadyAsync();
            Assert.True(composition.Runtime.IsConnected(device.Id));
            Assert.False(composition.Runtime.IsConnected(disabled.Id));

            manager.CloseSolution();
            await service.ReadyAsync();
            Assert.False(composition.Runtime.IsConnected(device.Id));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }

    [Fact(Timeout = 15000)]
    public async Task Unexpected_tcp_disconnect_reconnects_when_policy_is_enabled()
    {
        var directory = Path.Combine(Path.GetTempPath(), "SunEyeVisionCommunicationTest", Guid.NewGuid().ToString("N"));
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        Directory.CreateDirectory(directory);
        try
        {
            var manager = new SolutionManager(directory);
            await using var composition = new CommunicationRuntimeComposition();
            await using var service = new CommunicationApplicationService(manager, composition);
            var device = new Communication("reconnect", CommunicationType.TCP)
            {
                ProtocolKey = "tcp-client", Host = "127.0.0.1",
                Port = ((IPEndPoint)listener.LocalEndpoint).Port,
                IsEnabled = true, AutoReconnect = true
            };
            manager.SetCurrentSolution(new Solution { Communications = [device] });
            var first = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(5));
            await service.ReadyAsync();
            first.Dispose();

            using var second = await listener.AcceptTcpClientAsync().WaitAsync(TimeSpan.FromSeconds(8));
            for (var attempt = 0; attempt < 30 && !composition.Runtime.IsConnected(device.Id); attempt++)
                await Task.Delay(50);
            Assert.True(composition.Runtime.IsConnected(device.Id));

            manager.CloseSolution();
            await service.ReadyAsync();
            Assert.False(composition.Runtime.IsConnected(device.Id));
        }
        finally
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
