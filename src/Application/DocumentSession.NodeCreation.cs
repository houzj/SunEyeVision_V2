using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    /// <summary>Create and admit one private node into this graph proposal. It becomes
    /// a document member only when the whole proposal commits successfully.</summary>
    public NodeDefinition CreateNode(GraphEditDraft draft, string toolId, CanvasPoint position)
    {
        ArgumentNullException.ThrowIfNull(draft);
        ArgumentException.ThrowIfNullOrWhiteSpace(toolId);
        EnsureOwnerCanEdit();
        using var mutation = _owner.BeginDocumentMutation();
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            var scope = OpenScope(draft.Workflow.Id);
            if (!ReferenceEquals(scope.Flow, draft.Workflow) || draft.OpeningHistoryRevision != History.Revision ||
                !draft.Opening.MatchesCurrent(scope.Flow)) throw new InvalidOperationException("节点创建草稿不属于当前流程或已过期。");
            _preparing = true;
            try
            {
                var definition = _directory.CreateDefinition(toolId);
                var settings = definition as IToolSettingsFields ?? throw new InvalidOperationException("工具没有正式参数声明。");
                var defaults = settings.ReadSettings(definition.CreateSettings()).CopyValues()
                    .ToDictionary(pair => pair.Key, pair => (ParameterSetting)new ConstantSetting(pair.Value), StringComparer.Ordinal);
                if (definition is IToolResultDisplayProvider display)
                    foreach (var pair in display.CreateDisplaySettings().CopyValues()) defaults.Add(pair.Key, new ConstantSetting(pair.Value));
                var declarations = definition.Describe().Inputs.Concat(settings.SettingsSchema.Inputs)
                    .Concat(ResultDisplayFields.Schema(definition).Inputs).ToArray();
                var configuration = new ToolConfiguration(toolId, new FieldSettings(declarations, defaults));
                var node = new NodeDefinition(Guid.NewGuid().ToString("N"), string.Empty, toolId, configuration);
                _ = AdmitConfiguration(node, configuration);
                ValidateScopeCore(scope);
                if (draft.OpeningHistoryRevision != History.Revision || !draft.Opening.MatchesCurrent(scope.Flow))
                    throw new InvalidOperationException("工具准入期间流程已变化。");
                static int FirstFree(IEnumerable<int> allocated)
                {
                    var occupied = allocated.Where(value => value > 0).ToHashSet();
                    var candidate = 1;
                    while (occupied.Contains(candidate)) candidate = checked(candidate + 1);
                    return candidate;
                }
                var otherNodes = Document.Workflows.Where(flow => !ReferenceEquals(flow, scope.Flow)).SelectMany(flow => flow.Nodes);
                var global = FirstFree(otherNodes.Concat(draft.Nodes).Select(item => item.GlobalIndex));
                var local = FirstFree(draft.Nodes.Where(item => item.ToolType == toolId).Select(item => item.LocalIndex));
                var displayName = (_directory.GetCatalog(toolId)?.DisplayName ?? toolId) + local;
                node.GlobalIndex = global; node.LocalIndex = local; node.DispName = displayName;
                node.Name = $"{global} {displayName}";
                node.PositionX = position.X; node.PositionY = position.Y;
                // AddNode checks the proposal's owner thread and sealed state. No mutable
                // sequence counter or second document is advanced on rejection/cancellation.
                draft.AddNode(node);
                return node;
            }
            finally { _preparing = false; }
        }
    }
}
