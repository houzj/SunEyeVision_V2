using SunEyeVision.Plugin.Contracts;

[assembly: PluginContractAttribute(FoundationContract.Major, FoundationContract.Minor)]

namespace ExternalRuntimePlugin;

public sealed record RuntimePluginSettings : IToolSettings<RuntimePluginSettings>
{
    public string Text { get; init; } = string.Empty;
    public RuntimePluginSettings Freeze() => this;
}

public sealed class RuntimePluginOutput : IToolOutput
{
    public string Text { get; init; } = string.Empty;
    public IReadOnlyDictionary<string, object?> ToValues() => new Dictionary<string, object?> { ["output.text"] = Text };
}

public sealed class RuntimePluginDefinition : IToolDefinition<RuntimePluginSettings, RuntimePluginOutput>
{
    public const string Id = "ExternalRuntimePlugin";
    public string ToolId => Id;
    public ToolSchema Describe() => new(
        new[] { new Port("input.text", TypeRef.ScalarOf(ScalarKind.Text), Required: false, Nullable: true) },
        new[] { new Port("output.text", TypeRef.ScalarOf(ScalarKind.Text)) });
    public IReadOnlyList<Diagnostic> Validate(RuntimePluginSettings settings) => Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities() => ToolCapabilities.None;
    public IToolSession<RuntimePluginSettings, RuntimePluginOutput> CreateSession() => new Session();

    private sealed class Session : IToolSession<RuntimePluginSettings, RuntimePluginOutput>
    {
        public ValueTask<ToolOutcome<RuntimePluginOutput>> ExecuteAsync(ToolInvocation<RuntimePluginSettings> invocation)
        {
            var text = invocation.Inputs.Contains("input.text") ? invocation.Inputs.Get<string>("input.text") ?? string.Empty : invocation.Settings.Text;
            invocation.Context.Log("ExternalRuntimePlugin executed.");
            return ValueTask.FromResult(ToolOutcome<RuntimePluginOutput>.Completed(new RuntimePluginOutput { Text = text }));
        }
        public ValueTask DisposeAsync() => ValueTask.CompletedTask;
    }
}
