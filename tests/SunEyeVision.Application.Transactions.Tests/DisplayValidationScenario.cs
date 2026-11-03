using System.Reflection;
using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.TestFixtures;

internal static class DisplayValidationScenario
{
    internal static void Run(string dataRoot)
    {
        static void Check(bool value, string message) { if (!value) throw new Exception(message); }
        var component = new ToolComponentRegistration(new ToolComponentMetadata("display-probe", "DisplayProbe",
            ToolComponentRole.Runtime, FoundationContract.Major, FoundationContract.Minor), typeof(DisplayProbeDefinition));
        var directory = (RuntimeDirectorySnapshot)typeof(RuntimeDirectorySnapshot).GetMethod("Prepare", BindingFlags.NonPublic | BindingFlags.Static)!
            .Invoke(null, new object[] { new[] { component } })!;
        using var manager = new DocumentSessionManager(directory);
        var session = manager.CreateNew(); var flow = session.GetWorkflows().Single();
        var graph = session.PrepareGraphEdit(flow.Id);
        var first = session.CreateNode(graph, "DisplayProbe", new CanvasPoint(0, 0));
        var second = session.CreateNode(graph, "DisplayProbe", new CanvasPoint(0, 100));
        session.CommitGraphEdit(graph, "显示测试节点");
        var selfOptions = session.CaptureReferenceSelections(flow.Id, second, "text");
        Check(selfOptions.FieldKind == ReferenceSelectionFieldKind.Display && selfOptions.Outputs.Count == 1 &&
            ReferenceEquals(selfOptions.Outputs.Single().Actual, second) && selfOptions.Variables.Count == 0,
            "显示选择器须包含自身且不提供变量来源。");
        Check(session.CaptureReferenceSelections(flow.Id, second, "input").Outputs.Count == 0,
            "执行输入不得把自身当来源。");
        void Apply(ParameterSetting content, ParameterSetting? x = null)
        {
            var draft = session.Editing.Host.BeginEdit(flow.Id, second.Id);
            draft.ApplyFields(new Dictionary<string, ParameterSetting> { ["text"] = content, ["x"] = x ?? new ConstantSetting(0d) });
            session.CommitEdit(DocumentEditProposal.Prepare(session.Editing.Host, nodes: new[] { draft }));
        }
        var self = new DisplayExpressionSetting("{count}", new[] { new DisplayExpressionSlot("count", new ResultFieldPath("count"), "F0") });
        Apply(self);
        Check(session.Undo() && session.Redo(), "自身显示表达式必须能统一回放，不能造自身执行环。");
        Apply(new DisplayExpressionSetting("{row}", new[] {
            new DisplayExpressionSlot("row", new ResultFieldPath("rows", new[] { "value" }), "F2") }));
        Apply(self, new ReferenceSetting(new OutputReference(second.Id, "items", TypeRef.SequenceOf(DataType.Integer), scopeId: flow.Id)));
        var source = new OutputReference(first.Id, "count", DataType.Integer, scopeId: flow.Id);
        var front = new DisplayExpressionSetting("{front}", new[] { new DisplayExpressionSlot("front", source) });
        void Reject(Action operation)
        {
            var prior = second.Configuration; var revision = session.ChangeRevision; var count = session.HistoryEntries.Count;
            try { operation(); throw new Exception("非法显示提案未拒绝。"); }
            catch (Exception error) when (error is InvalidOperationException or ArgumentException) { }
            Check(ReferenceEquals(prior, second.Configuration) && revision == session.ChangeRevision && count == session.HistoryEntries.Count,
                "拒绝显示提案不得改正式配置或历史。");
        }
        Reject(() => Apply(front));
        var connect = session.PrepareGraphEdit(flow.Id);
        connect.AddConnection(new ConnectionDefinition("front", first.Id, second.Id, "count", "input"));
        session.CommitGraphEdit(connect, "显示前序");
        Apply(front, new ReferenceSetting(source));
        var connectedOptions = session.CaptureReferenceSelections(flow.Id, second, "x");
        Check(connectedOptions.Outputs.Count == 2 && connectedOptions.Outputs.Any(option => ReferenceEquals(option.Actual, first)),
            "位置来源查询须与提交校验共用前序及自身声明。");
        var peer = new OutputReference(first.Id, "count", DataType.Integer, scopeId: "other-workflow");
        Reject(() => Apply(new DisplayExpressionSetting("{peer}", new[] { new DisplayExpressionSlot("peer", peer) })));
        var variable = new VariableReference(session.Document.Id, "not-an-index", DataType.Integer);
        var indexed = new OutputReference(first.Id, "items", TypeRef.SequenceOf(DataType.Integer),
            selection: new BindingSelection.AtVariable(variable), scopeId: flow.Id);
        Reject(() => Apply(new DisplayExpressionSetting("{index}", new[] { new DisplayExpressionSlot("index", indexed) })));
        Reject(() => Apply(new DisplayExpressionSetting("{missing}", new[] { new DisplayExpressionSlot("missing", new ResultFieldPath("missing")) })));
        var text = new OutputReference(first.Id, "name", DataType.Text, scopeId: flow.Id);
        Reject(() => Apply(new DisplayExpressionSetting("{name}", new[] { new DisplayExpressionSlot("name", text, "F2") })));
        Reject(() => Apply(front, new ReferenceSetting(text)));
        var types = new FrozenVariableTypeCatalog(Array.Empty<KeyValuePair<string, string>>());
        var path = Path.Combine(dataRoot, "display.solution");
        session.SaveAs(path, Path.Combine(dataRoot, "display-backup"), dataRoot, types, new System.Text.Json.JsonSerializerOptions(), _ => null);
        var reopened = manager.Open(path, _ => types, _ => null, new System.Text.Json.JsonSerializerOptions());
        Check(reopened.GetNodes(flow.Id).Single(node => node.Id == second.Id).Configuration.Fields.Read("text") is DisplayExpressionSetting,
            "显示表达式保存重开必须保留结构。");
        Console.WriteLine("PASS 显示前序与自身表达式、位置引用、拒跨流程/变量索引/非法自身/格式，统一历史及保存重开");
    }
}

public sealed class DisplayProbeDefinition : IToolDefinition<WorkspaceProbeSettings, WorkspaceProbeOutput>, IToolSettingsFields,
    IToolCatalogProvider, IToolResultDisplayProvider
{
    private static readonly TypeRef Row = TypeRef.RecordOf("display-row", new[] {
        new Port("id", DataType.Integer), new Port("quad", TypeRef.SequenceOf(TypeRef.GeometryOf(GeometryKind.Point, WorkspaceProbeDefinition.Coordinates), CountConstraint.Exactly(4))),
        new Port("region", TypeRef.ResourceOf(ResourceKind.Region)), new Port("value", DataType.Number), new Port("passed", DataType.Boolean) });
    private static readonly ToolSchema Schema = new(new[] { new Port("input", DataType.Integer, Required: false, EditModes: FieldEditModes.Reference) },
        new[] { new Port("count", DataType.Integer), new Port("name", DataType.Text), new Port("items", TypeRef.SequenceOf(DataType.Integer)), new Port("rows", TypeRef.SequenceOf(Row)) });
    public string ToolId => "DisplayProbe";
    public ToolCatalogDescriptor Catalog { get; } = new("显示测试", "核验显示引用", "generic", "测试", 0);
    public ToolSchema Describe() => Schema;
    public ToolSchema SettingsSchema { get; } = new(Array.Empty<Port>(), Array.Empty<Port>());
    public ToolSchema DisplaySettingsSchema { get; } = new(new[] {
        new Port("text", DataType.Text, EditModes: FieldEditModes.Constant | FieldEditModes.DisplayExpression),
        new Port("x", DataType.Number, EditModes: FieldEditModes.Constant | FieldEditModes.Reference) }, Array.Empty<Port>());
    public ResultDisplayDescriptor ResultDisplay { get; } = new(new("rows"), new("rows", new[] { "id" }), new("rows", new[] { "quad" }),
        new("rows", new[] { "region" }), new("rows", new[] { "value" }), new("rows", new[] { "passed" }));
    public DisplayProbeDefinition() => ResultDisplay.Validate(Schema);
    public InputValues CreateDisplaySettings() => new(DisplaySettingsSchema.Inputs, new Dictionary<string, object?> { ["text"] = "", ["x"] = 0d });
    public ResultDisplayAppearance CaptureResultDisplay(FieldSettings fields)
    {
        var content = fields.Read("text"); var expression = content as DisplayExpressionSetting;
        var text = expression?.Text ?? (string)((ConstantSetting)content).Value!;
        var color = new ResultDisplayColor(255, 128, 0);
        return new(new(false, color, color, 1, 1), new(true, text, color, color, 12, 1, 0, 0, expression, fields.Read("x")));
    }
    public InputValues ReadSettings(object settings) => new(SettingsSchema.Inputs, new Dictionary<string, object?>());
    public object MaterializeSettings(InputValues values) => new WorkspaceProbeSettings();
    public IReadOnlyList<Diagnostic> Validate(WorkspaceProbeSettings settings) => Array.Empty<Diagnostic>();
    public ToolCapabilities Capabilities() => ToolCapabilities.None;
    public IToolSession<WorkspaceProbeSettings, WorkspaceProbeOutput> CreateSession() => throw new NotSupportedException("显示编辑声明测试，不执行算法。");
}