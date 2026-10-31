using System.IO;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Tool.ProtocolAssemble;
using SunEyeVision.Tool.ReceiveData;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class GlobalVariableValueShapeTests
{
    [Theory]
    [InlineData("String", -1)] [InlineData("String", 0)] [InlineData("String", 1)] [InlineData("String", 3)]
    [InlineData("Integer", -1)] [InlineData("Integer", 0)] [InlineData("Integer", 1)] [InlineData("Integer", 3)]
    [InlineData("Double", -1)] [InlineData("Double", 0)] [InlineData("Double", 1)] [InlineData("Double", 3)]
    [InlineData("Boolean", -1)] [InlineData("Boolean", 0)] [InlineData("Boolean", 1)] [InlineData("Boolean", 3)]
    [InlineData("Binary", -1)] [InlineData("Binary", 0)] [InlineData("Binary", 1)] [InlineData("Binary", 3)]
    public void Repository_keeps_actual_scalar_or_one_dimensional_array_shape_with_existing_type_family(string family, int count)
    {
        using var files = new Files();
        var value = MakeValue(family, count);
        var solution = new Solution();
        solution.GlobalVariables.Add(new GlobalVariable("value", value, family));
        var repository = new SolutionRepository(files.Recovery);
        var path = files.Path("first.solution");
        Assert.True(repository.Save(solution, path), repository.LastError);
        AssertJsonShape(path, family, count);
        var candidate = repository.LoadCandidate(path);
        Assert.False(candidate.HasErrors);
        var reopened = Assert.IsType<Solution>(candidate.Candidate);
        var actual = Assert.Single(reopened.GlobalVariables).Value;
        AssertExactValue(value, actual);
        var again = files.Path("second.solution");
        Assert.True(repository.Save(reopened, again), repository.LastError);
        AssertJsonShape(again, family, count);
        AssertExactValue(value, Assert.Single(repository.Load(again)!.GlobalVariables).Value);
    }

    [Fact]
    public async Task Reopened_typed_producer_and_string_consumer_execute_without_arrayizing_design_or_produced_global()
    {
        using var files = new Files();
        ToolRegistry.RegisterTool(typeof(ProtocolAssembleTool)); ToolRegistry.RegisterTool(typeof(ReceiveDataTool));
        var source = new ProtocolAssembleParameters { Parts = new() { new() { InputText = "CURRENT" } } };
        var input = new ReceiveDataParameters { SourceKind = "Variable", Variables = new() { new() { VariableId = "code-id" } } };
        var flow = new SunEyeVision.Workflow.Workflow("flow", "variables");
        flow.Nodes.Add(new WorkflowNodeBase("receiver", "接收", "接收", "ReceiveData") { Parameters = input });
        flow.Nodes.Add(new WorkflowNodeBase("producer", "组装", "组装", "ProtocolAssemble") { Parameters = source });
        var binding = BindingAdaptationB2Tests.Producer(flow.Id, "producer", source, ProtocolAssembleParameters.TextOutput.BindingKey);
        var variable = new GlobalVariable("code", "DESIGN", "String") { Id = "code-id", ProducerBinding = binding };
        var solution = new Solution { Workflows = new() { flow }, GlobalVariables = new() { variable } };
        var repository = new SolutionRepository(files.Recovery); var path = files.Path("producer.solution");
        Assert.True(repository.Save(solution, path), repository.LastError);
        var reopened = Assert.IsType<Solution>(repository.Load(path));
        var stored = Assert.Single(reopened.GlobalVariables);
        Assert.Equal("DESIGN", Assert.IsType<string>(stored.Value));
        Assert.Equal(binding.ReferenceExpression, stored.ProducerBinding!.ReferenceExpression);
        Assert.Equal(flow.Id, stored.ProducerBinding.SourceReference.ScopeId);
        Assert.Equal("producer", stored.ProducerBinding.SourceReference.NodeId);
        Assert.Equal("field:" + ProtocolAssembleParameters.TextOutput.FieldId.ToString("N"), stored.ProducerBinding.SourceReference.PortId);
        var capture = new NodeConfigurationEditHost(reopened).Capture();
        var frozen = capture.CopyConfiguration(); var frozenVariable = Assert.Single(frozen.GlobalVariables);
        var validation = GlobalProducerBindingRules.Validate(frozen, frozenVariable.ProducerBinding!, frozenVariable);
        Assert.True(validation.State is SunEyeVision.Plugin.Contracts.BindingCompatibility.Compatible or SunEyeVision.Plugin.Contracts.BindingCompatibility.Conditional, validation.Reason);
        Assert.True(GlobalProducerBindingRules.TryDescribeTarget(frozenVariable.Type, out var target, out var clr, out var reason), reason);
        using var values = new WorkflowGlobalRunValues(frozen.Id,frozen.GlobalVariables,Array.Empty<KeyValuePair<string, object?>>());
        var service = new WorkflowGlobalVariableReceiveService(new CommunicationReceiveService(), values, GlobalVariable.CaptureDefinitions(frozen.GlobalVariables));
        var catalog = new WorkflowEngine(VisionLogger.Instance); catalog.RegisterWorkflow(reopened.Workflows.Single());
        var engine = new WorkflowExecutionEngine(catalog, new PluginManager(VisionLogger.Instance), VisionLogger.Instance);
        using var result = await engine.ExecuteWorkflowAsync(new WorkflowRunRequest(flow.Id, communicationInputs: service)
        {
            Configuration = WorkflowRunConfiguration.Capture(frozen.Workflows, capture.Version), ExecutionOwner = reopened.ExecutionOwner,
            GlobalProducers = new[] { new WorkflowGlobalProducerOperation(frozenVariable.Id, frozenVariable.Name, frozenVariable.ProducerBinding!.SourceReference, target, clr) },
            ProducedGlobals = values
        });
        Assert.True(result.Success, string.Join(";", result.Errors));
        Assert.Equal("CURRENT", Assert.IsType<string>(values.Capture()[stored.Name]));
        Assert.Equal("DESIGN", Assert.IsType<string>(stored.Value));
        var received = Assert.IsType<CommunicationToolResults>(result.NodeResults["receiver"].Result);
        var receivedParameters = Assert.IsType<ReceiveDataParameters>(frozen.Workflows.Single().Nodes.Single(node => node.Id == "receiver").Parameters);
        var output = received.GetOutputVariables().Single(field => field.Name == receivedParameters.Variables[0].Definition().BindingKey);
        Assert.Equal("CURRENT", Assert.Single(Assert.IsType<string[]>(output.RuntimeValue)));
    }

    private static object MakeValue(string family, int count)
    {
        object Scalar(int index) => family switch
        {
            "String" => "text-" + index, "Integer" => (long)index + 17,
            "Double" => index + 2.5, "Boolean" => index % 2 == 0,
            "Binary" => new BinaryPayload(new byte[] { 0, 7, (byte)index, 255 }), _ => throw new ArgumentException(family)
        };
        if (count < 0) return Scalar(0);
        return family switch
        {
            "String" => Enumerable.Range(0, count).Select(index => (string)Scalar(index)).ToArray(),
            "Integer" => Enumerable.Range(0, count).Select(index => (long)Scalar(index)).ToArray(),
            "Double" => Enumerable.Range(0, count).Select(index => (double)Scalar(index)).ToArray(),
            "Boolean" => Enumerable.Range(0, count).Select(index => (bool)Scalar(index)).ToArray(),
            "Binary" => Enumerable.Range(0, count).Select(index => (BinaryPayload)Scalar(index)).ToArray(),
            _ => throw new ArgumentException(family)
        };
    }
    private static void AssertJsonShape(string path, string family, int count)
    {
        using var json = JsonDocument.Parse(File.ReadAllText(path));
        var variable = Assert.Single(json.RootElement.GetProperty("GlobalVariables").EnumerateArray());
        var wrapper = variable.GetProperty("Value");
        Assert.Equal(family, wrapper.GetProperty("Type").GetString());
        var actual = wrapper.GetProperty("Value");
        if (count >= 0) { Assert.Equal(JsonValueKind.Array, actual.ValueKind); Assert.Equal(count, actual.GetArrayLength()); }
        else Assert.Equal(family switch { "String" or "Binary" => JsonValueKind.String, "Boolean" => JsonValueKind.True, _ => JsonValueKind.Number }, actual.ValueKind);
        if (family == "Binary")
        {
            var expected = count < 0 ? new[] { (BinaryPayload)MakeValue(family, -1) } : (BinaryPayload[])MakeValue(family, count);
            var payloads = count < 0 ? new[] { actual } : actual.EnumerateArray().ToArray();
            Assert.Equal(expected.Select(payload => payload.ToBase64()), payloads.Select(payload => payload.GetString()));
        }
    }
    private static void AssertExactValue(object expected, object? actual)
    {
        Assert.NotNull(actual); Assert.Equal(expected.GetType(), actual!.GetType());
        if (expected is BinaryPayload payload) Assert.Equal(payload.ToArray(), Assert.IsType<BinaryPayload>(actual).ToArray());
        else if (expected is BinaryPayload[] payloads)
            Assert.Equal(payloads.Select(value => value.ToBase64()), Assert.IsType<BinaryPayload[]>(actual).Select(value => value.ToBase64()));
        else if (expected is Array array)
            Assert.Equal(array.Cast<object?>(), Assert.IsAssignableFrom<Array>(actual).Cast<object?>());
        else Assert.Equal(expected, actual);
        // Empty arrays may share Array.Empty<T>(); they have no mutable elements to alias.
        if (expected is Array {Length:>0} or BinaryPayload) Assert.NotSame(expected, actual);
    }
    private sealed class Files : IDisposable
    {
        private readonly string _root = System.IO.Path.GetFullPath(System.IO.Path.Combine(
            Environment.GetEnvironmentVariable("S2_CASE_ROOT")??AppContext.BaseDirectory,"global-value-shape"));
        private readonly string _directory;
        internal Files() { _directory = System.IO.Path.Combine(_root, Guid.NewGuid().ToString("N")); Directory.CreateDirectory(_directory); }
        internal string Recovery => System.IO.Path.Combine(_directory, "recovery");
        internal string Path(string file) => System.IO.Path.Combine(_directory, file);
        public void Dispose()
        {
            var target = System.IO.Path.GetFullPath(_directory);
            if (!target.StartsWith(_root + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("测试清理超出专用目录。");
            if (Directory.Exists(target)) Directory.Delete(target, true);
        }
    }
}
