using SunEyeVision.Plugin.Contracts;

[assembly: PluginContractAttribute(FoundationContract.Major, FoundationContract.Minor)]

namespace SamplePlugin;

public sealed record SampleSettings : IToolSettings<SampleSettings>
{
    public string Text { get; init; } = "sample";
    public SampleSettings Freeze() => this;
}

public sealed class SampleOutput : IToolOutput
{
    public string Text { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> ToValues() => new Dictionary<string, object?> { ["output.text"] = Text };
}

public sealed class SampleTool : IToolDefinition<SampleSettings, SampleOutput>
{
    public const string Id = "SampleTool";
    public string ToolId => Id;
    public ToolSchema Describe() => new(Array.Empty<Port>(), new[] { new Port("output.text", TypeRef.ScalarOf(ScalarKind.Text)) });
    public IReadOnlyList<Diagnostic> Validate(SampleSettings settings) => Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities() => ToolCapabilities.None;
    public IToolSession<SampleSettings, SampleOutput> CreateSession() => new Session();

    private sealed class Session : IToolSession<SampleSettings, SampleOutput>
    {
        public ValueTask<ToolOutcome<SampleOutput>> ExecuteAsync(ToolInvocation<SampleSettings> invocation)
            => ValueTask.FromResult(ToolOutcome<SampleOutput>.Completed(new SampleOutput { Text = invocation.Settings.Text }));
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
