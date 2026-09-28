using System.Net;
using System.Net.Sockets;
using System.IO;
using System.Text;
using System.Threading.Channels;
using Opc.Ua;
using Opc.Ua.Configuration;
using Opc.Ua.Server;
using SunEyeVision.Workflow;
using SunEyeVision.DeviceDriver.Communications;
using SunEyeVision.UI.Features.CommunicationManager;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class OpcUaRuntimeTests
{
    [Fact]
    public async Task Opc_ua_username_identity_is_rejected_as_not_yet_supported()
    {
        await using var runtime = new CommunicationRuntimeManager(Path.Combine(Path.GetTempPath(), Guid.NewGuid().ToString("N")));
        var communication = new Communication("OPC UA unsupported identity", CommunicationType.OPCUA)
        {
            ProtocolKey = "opcua",
            EndpointUrl = "opc.tcp://127.0.0.1:4840",
            OpcUaIdentityMode = "用户名密码"
        };

        var exception = await Assert.ThrowsAsync<NotSupportedException>(() => runtime.ConnectAsync(CommunicationDeviceSnapshotFactory.Create(communication)));

        Assert.Contains("仅支持匿名身份", exception.Message, StringComparison.Ordinal);
        Assert.False(runtime.IsConnected(communication.Id));
    }

    [Theory]
    [InlineData("Sign")]
    [InlineData("SignAndEncrypt")]
    public async Task Opc_ua_client_discovers_browses_reads_writes_and_subscribes_against_a_local_server(string securityMode)
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
        var trustStore = new OpcUaTrustStore(clientPkiRoot);
        foreach (var serverCertificate in Directory.GetFiles(Path.Combine(serverPkiRoot, "certs"), "*.der"))
            trustStore.TrustFile(serverCertificate);
        await using var runtime = new CommunicationRuntimeManager(clientPkiRoot);
        var communication = new Communication("OPC UA integration test", CommunicationType.OPCUA)
        {
            ProtocolKey = "opcua",
            EndpointUrl = endpointUrl,
            SecurityMode = securityMode,
            OpcUaIdentityMode = "匿名",
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

            stage = "Write value";
            await runtime.WriteOpcUaValueAsync(communication.Id, valueNode.NodeId, 42);
            stage = "Read written value";
            var writtenValue = await runtime.ReadOpcUaValueAsync(communication.Id, valueNode.NodeId);
            Assert.Equal(42, writtenValue.Value);

            stage = "Subscribe and receive notification";
            var notifications = Channel.CreateUnbounded<string>();
            runtime.DataReceived += (_, args) =>
            {
                if (args.CommunicationId == communication.Id)
                    notifications.Writer.TryWrite(Encoding.UTF8.GetString(args.Data));
            };
            await runtime.SubscribeOpcUaValueAsync(communication.Id, valueNode.NodeId, 50);
            await runtime.WriteOpcUaValueAsync(communication.Id, valueNode.NodeId, 43);
            using var timeout = new CancellationTokenSource(TimeSpan.FromSeconds(5));
            string update = await notifications.Reader.ReadAsync(timeout.Token);
            Assert.Contains("43", update, StringComparison.Ordinal);

            stage = "Unsubscribe and verify notifications stop";
            await runtime.UnsubscribeOpcUaValueAsync(communication.Id, valueNode.NodeId);
            while (notifications.Reader.TryRead(out _)) { }
            await runtime.WriteOpcUaValueAsync(communication.Id, valueNode.NodeId, 44);
            using var noNotificationTimeout = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));
            await Assert.ThrowsAnyAsync<OperationCanceledException>(async () =>
                await notifications.Reader.ReadAsync(noNotificationTimeout.Token));
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
                .AddSignPolicies(true)
                .AddSignAndEncryptPolicies(true)
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
