using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text.Json;
using System.Threading.Channels;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using SunEyeVision.Workflow;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.CommunicationManager;
using SunEyeVision.UI.Services.Communication;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class OpcUaRuntimeTests
{
    [Fact]
    public void New_opc_ua_devices_default_to_local_kepserverex_endpoint()
    {
        const string expectedEndpoint = "opc.tcp://127.0.0.1:49320";
        var editor = new CommunicationDeviceEditorViewModel();
        var communication = new Communication("OPC UA default", CommunicationType.OPCUA)
        {
            ProtocolKey = "opcua"
        };

        Assert.Equal(expectedEndpoint, editor.EndpointUrl);
        Assert.Equal(expectedEndpoint, communication.EndpointUrl);
        editor.SelectedProtocol = editor.Protocols.Single(option => option.Key == "opcua");
        Assert.Equal("127.0.0.1", editor.Host);
        Assert.Equal(49320, editor.Port);
        Assert.True(editor.AutoReconnect);
        Assert.Equal(expectedEndpoint, editor.OpcUaEndpointPreview);
        editor.CreateCommand.Execute(null);
        Assert.Equal(expectedEndpoint, editor.Result?.EndpointUrl);
        Assert.Equal("127.0.0.1", editor.Result?.OpcUaHost);
        Assert.Equal(49320, editor.Result?.OpcUaPort);
    }

    [Fact]
    public void Legacy_endpoint_wins_over_unrelated_host_port_and_split_fields_preserve_path()
    {
        var communication = new Communication("Existing", CommunicationType.OPCUA) { ProtocolKey = "opcua" };
        communication.Host = "unrelated-host";
        communication.Port = 7920;
        communication.EndpointUrl = "opc.tcp://127.0.0.1:49321/SunEyeVision.Test";
        Assert.Equal("127.0.0.1", communication.OpcUaHost);
        Assert.Equal(49321, communication.OpcUaPort);

        communication.OpcUaHost = "localhost";
        communication.OpcUaPort = 49322;
        Assert.Equal("opc.tcp://localhost:49322/SunEyeVision.Test", communication.EndpointUrl);
        communication.OpcUaDataPoints.Add(new OpcUaDataPoint { NamespaceIndex = 2, TagName = "通道 1.设备 1.test1" });
        var restored = JsonSerializer.Deserialize<Communication>(JsonSerializer.Serialize(communication));
        Assert.NotNull(restored);
        Assert.Equal(communication.EndpointUrl, restored.EndpointUrl);
        Assert.Equal("ns=2;s=通道 1.设备 1.test1", Assert.Single(restored.OpcUaDataPoints).NodeId);
        Assert.Equal("test1", Assert.Single(communication.Clone().OpcUaDataPoints).Name);

        var invalidLegacy = new Communication("Invalid legacy", CommunicationType.OPCUA) { ProtocolKey = "opcua" };
        invalidLegacy.EndpointUrl = "bad-old-endpoint";
        Assert.Equal("bad-old-endpoint", invalidLegacy.EndpointUrl);
        Assert.Equal("", invalidLegacy.OpcUaHost);
        Assert.Equal(0, invalidLegacy.OpcUaPort);
        invalidLegacy.OpcUaHost = "127.0.0.1";
        Assert.Equal("", invalidLegacy.EndpointUrl);
    }

    [Fact]
    public void Workspace_shows_only_selected_node_and_disables_writes_for_stale_or_complex_values()
    {
        var first = new OpcUaDataPoint { NamespaceIndex = 2, TagName = "通道 1.设备 1.test1" };
        var second = new OpcUaDataPoint { NamespaceIndex = 2, TagName = "通道 1.设备 2.test1" };
        var device = new Communication("Monitor", CommunicationType.OPCUA)
        {
            ProtocolKey = "opcua", OpcUaDataPoints = new List<OpcUaDataPoint> { first, second }
        };
        var workspace = new CommunicationDeviceWorkspaceViewModel(device);
        Assert.Equal("未选择数据点", workspace.SelectedOpcUaNodeId);
        var rows = workspace.OpcUaPoints.ToArray();
        Assert.Equal("test1", rows[0].Name);
        Assert.Equal("test1", rows[1].Name);
        workspace.SelectedOpcUaPoint = rows[1];
        Assert.Equal(second.NodeId, workspace.SelectedOpcUaNodeId);
        workspace.SelectedOpcUaPoint = null;
        Assert.Equal("未选择数据点", workspace.SelectedOpcUaNodeId);

        rows[0].Apply(new OpcUaPointValueEventArgs(device.Id, first.NodeId, false,
            BuiltInType.Boolean, true, StatusCodes.Good, DateTimeOffset.UtcNow));
        Assert.True(rows[0].CanEdit);
        rows[0].MarkDisconnected();
        Assert.False(rows[0].CanEdit);
        rows[0].Apply(new OpcUaPointValueEventArgs(device.Id, first.NodeId, new[] { 1, 2 },
            BuiltInType.Int32, true, StatusCodes.Good, DateTimeOffset.UtcNow, isSupportedScalar: false));
        Assert.False(rows[0].CanEdit);
    }

    [Fact]
    public async Task Opc_ua_anonymous_client_discovers_browses_reads_writes_and_subscribes_against_a_local_server()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var endpointUrl = $"opc.tcp://127.0.0.1:{port}/SunEyeVision.Test";
        var testRoot = Path.Combine(Path.GetTempPath(), "SunEyeVision-OpcUa-Test", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);

        var serverPkiRoot = Path.Combine(testRoot, "server-pki");
        var clientPkiRoot = Path.Combine(testRoot, "client-pki");
        await using var serverHost = await OpcUaTestServer.StartAsync(endpointUrl, serverPkiRoot);
        await using var runtime = new CommunicationRuntimeManager(clientPkiRoot);
        var communication = new Communication("OPC UA integration test", CommunicationType.OPCUA)
        {
            ProtocolKey = "opcua",
            EndpointUrl = endpointUrl,
            RequestTimeout = 5000
        };

        var stage = "Connect";
        try
        {
            await runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(communication));

            stage = "Browse Objects";
            var objects = await runtime.BrowseOpcUaAsync(communication.Id, "i=85");
            var testRootNode = Assert.Single(objects, item => item.DisplayName == "TestRoot");
            stage = "Browse TestRoot";
            var children = await runtime.BrowseOpcUaAsync(communication.Id, testRootNode.NodeId);
            var valueNode = Assert.Single(children, item => item.DisplayName == "Value");

            stage = "Read initial value";
            var initialValue = await runtime.ReadOpcUaValueAsync(communication.Id, valueNode.NodeId);
            Assert.Equal(7, initialValue.Value);
            var point = await runtime.RefreshOpcUaPointAsync(communication.Id, valueNode.NodeId);
            Assert.Equal(BuiltInType.Int32, point.BuiltInType);
            Assert.True(point.CanWrite);
            Assert.Equal(7, point.Value);

            stage = "Write value";
            await runtime.WriteOpcUaValueAsync(communication.Id, valueNode.NodeId, 42);
            stage = "Read written value";
            var writtenValue = await runtime.ReadOpcUaValueAsync(communication.Id, valueNode.NodeId);
            Assert.Equal(42, writtenValue.Value);

            stage = "Subscribe and receive notification";
            var typedNotifications = Channel.CreateUnbounded<OpcUaPointValueEventArgs>();
            runtime.OpcUaPointValueChanged += (_, args) =>
            {
                if (args.CommunicationId == communication.Id && args.NodeId == valueNode.NodeId)
                    typedNotifications.Writer.TryWrite(args);
            };
            await runtime.SubscribeOpcUaValueAsync(communication.Id, valueNode.NodeId, 50);
            await runtime.WriteOpcUaValueAsync(communication.Id, valueNode.NodeId, 43);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            OpcUaPointValueEventArgs typedUpdate;
            do { typedUpdate = await typedNotifications.Reader.ReadAsync(timeout.Token); }
            while (!Equals(typedUpdate.Value, 43));
            Assert.Equal(BuiltInType.Int32, typedUpdate.BuiltInType);

            stage = "Unsubscribe and verify notifications stop";
            await runtime.UnsubscribeOpcUaValueAsync(communication.Id, valueNode.NodeId);
            while (typedNotifications.Reader.TryRead(out _)) { }
            await runtime.WriteOpcUaValueAsync(communication.Id, valueNode.NodeId, 44);
            using var noNotificationTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await typedNotifications.Reader.ReadAsync(noNotificationTimeout.Token));
        }
        catch (Exception exception)
        {
            var serverTrace = Path.Combine(testRoot, "server.log");
            if (File.Exists(serverTrace))
                throw new Xunit.Sdk.XunitException($"Failure during {stage}:{Environment.NewLine}{exception}{Environment.NewLine}OPC UA server trace:{Environment.NewLine}{File.ReadAllText(serverTrace)}");
            throw;
        }
        finally
        {
            await runtime.DisconnectAsync(communication.Id);
            await serverHost.StopAsync();
            try { Directory.Delete(testRoot, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    [Fact(Timeout = 20000)]
    public async Task Solution_lifecycle_connects_and_monitors_configured_opc_ua_points_without_opening_manager()
    {
        using var listener = new TcpListener(IPAddress.Loopback, 0);
        listener.Start();
        var port = ((IPEndPoint)listener.LocalEndpoint).Port;
        listener.Stop();
        var endpointUrl = $"opc.tcp://127.0.0.1:{port}/SunEyeVision.Test";
        var testRoot = Path.Combine(Path.GetTempPath(), "SunEyeVision-OpcUa-Lifecycle", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(testRoot);
        try
        {
            await using var server = await OpcUaTestServer.StartAsync(endpointUrl, Path.Combine(testRoot, "server-pki"));
            var manager = new SolutionManager(testRoot);
            await using var composition = new CommunicationRuntimeComposition(Path.Combine(testRoot, "client-pki"));
            await using var service = new CommunicationApplicationService(manager, composition);
            var point = new OpcUaDataPoint { NamespaceIndex = 2, TagName = "Value" };
            var device = new Communication("OPC UA lifecycle", CommunicationType.OPCUA)
            {
                ProtocolKey = "opcua", EndpointUrl = endpointUrl, RequestTimeout = 5000,
                OpcUaDataPoints = new List<OpcUaDataPoint> { point }, IsEnabled = true
            };
            manager.SetCurrentSolution(new Solution { Communications = [device] });
            await service.ReadyAsync();
            Assert.True(composition.Runtime.IsConnected(device.Id));
            Assert.True(composition.Runtime.TryGetLatestOpcUaPoint(device.Id, point.NodeId, out var latest));
            Assert.Equal(7, latest?.Value);
            Assert.True(latest?.CanWrite);
            manager.CloseSolution();
            await service.ReadyAsync();
            Assert.False(composition.Runtime.IsConnected(device.Id));
            await server.StopAsync();
        }
        finally
        {
            try { Directory.Delete(testRoot, recursive: true); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }
    }

    private sealed class OpcUaTestServer : IAsyncDisposable
    {
        private readonly ApplicationInstance _application;
        private readonly TestServer _server;
        private int _stopped;

        private OpcUaTestServer(ApplicationInstance application, TestServer server)
        {
            _application = application;
            _server = server;
        }

        public static async Task<OpcUaTestServer> StartAsync(string endpointUrl, string pkiRoot)
        {
            var applicationUri = $"urn:{Dns.GetHostName()}:SunEyeVision.OpcUa.Test";
            var application = new ApplicationInstance();
            var builder = application.Build("SunEyeVision OPC UA Test Server", applicationUri);
            var concreteBuilder = (ApplicationConfigurationBuilder)builder;
            var certificates = ApplicationConfigurationBuilder.CreateDefaultApplicationCertificates(
                "CN=SunEyeVision OPC UA Test",
                CertificateStoreType.Directory,
                pkiRoot);
            concreteBuilder.ApplicationConfiguration.ApplicationName = "SunEyeVision OPC UA Test Server";
            concreteBuilder.ApplicationConfiguration.ApplicationUri = applicationUri;
            concreteBuilder.ApplicationConfiguration.TraceConfiguration = new TraceConfiguration
            {
                OutputFilePath = Path.Combine(Path.GetDirectoryName(pkiRoot)!, "server.log"),
                TraceMasks = (int)Utils.TraceMasks.All
            };
            var configuration = await builder
                .AsServer(new[] { endpointUrl }, Array.Empty<string>())
                .AddUnsecurePolicyNone(true)
                .AddUserTokenPolicy(UserTokenType.Anonymous)
                .AddSecurityConfiguration(certificates, Path.Combine(pkiRoot, "trusted"), Path.Combine(pkiRoot, "rejected"))
                .SetAutoAcceptUntrustedCertificates(true)
                .SetAddAppCertToTrustedStore(true)
                .CreateAsync(CancellationToken.None);

            await configuration.ValidateAsync(ApplicationType.Server);
            await application.CheckApplicationInstanceCertificatesAsync(true, 2048, CancellationToken.None);
            var server = new TestServer();
            await application.StartAsync(server);
            return new OpcUaTestServer(application, server);
        }

        public async Task StopAsync()
        {
            if (Interlocked.Exchange(ref _stopped, 1) != 0)
                return;
            try { await _application.StopAsync(); }
            finally { _server.Dispose(); }
        }

        public async ValueTask DisposeAsync() => await StopAsync();
    }

    private sealed class TestServer : StandardServer
    {
        protected override MasterNodeManager CreateMasterNodeManager(IServerInternal server, ApplicationConfiguration configuration) =>
            new(server, configuration, null, new INodeManager[] { new TestNodeManager(server, configuration) });
    }

    private sealed class TestNodeManager : CustomNodeManager2
    {
        private const string NamespaceUri = "urn:SunEyeVision:OpcUaIntegrationTest";

        public TestNodeManager(IServerInternal server, ApplicationConfiguration configuration)
            : base(server, configuration, NamespaceUri)
        {
        }

        public override void CreateAddressSpace(IDictionary<NodeId, IList<IReference>> externalReferences)
        {
            base.CreateAddressSpace(externalReferences);
            var root = new FolderState(null)
            {
                NodeId = new NodeId("TestRoot", NamespaceIndex),
                BrowseName = new QualifiedName("TestRoot", NamespaceIndex),
                DisplayName = "TestRoot",
                TypeDefinitionId = ObjectTypeIds.FolderType
            };
            var value = new BaseDataVariableState(root)
            {
                NodeId = new NodeId("Value", NamespaceIndex),
                BrowseName = new QualifiedName("Value", NamespaceIndex),
                DisplayName = "Value",
                TypeDefinitionId = VariableTypeIds.BaseDataVariableType,
                DataType = DataTypeIds.Int32,
                ValueRank = ValueRanks.Scalar,
                AccessLevel = (byte)(AccessLevels.CurrentRead | AccessLevels.CurrentWrite),
                UserAccessLevel = (byte)(AccessLevels.CurrentRead | AccessLevels.CurrentWrite),
                Value = 7,
                StatusCode = StatusCodes.Good,
                Timestamp = DateTime.UtcNow
            };
            root.AddChild(value);
            AddPredefinedNode(SystemContext, root);

            if (!externalReferences.TryGetValue(ObjectIds.ObjectsFolder, out var references))
                externalReferences[ObjectIds.ObjectsFolder] = references = new List<IReference>();
            references.Add(new NodeStateReference(ReferenceTypeIds.Organizes, false, root.NodeId));
        }
    }
}
