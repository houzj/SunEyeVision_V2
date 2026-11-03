using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Application;

/// <summary>Immutable declarations only; never owns a result reader or a native resource.</summary>
public sealed record NodeResultPresentation(ToolSchema Schema, ResultProjectionDescriptor? Projection,
    ResultDisplayDescriptor? Display, ResultDisplayAppearance? Appearance);

public sealed partial class DocumentSession
{
    public NodeResultPresentation GetNodeResultPresentation(string workflowId, NodeDefinition node,
        NodeConfigurationEditDraft? draft = null)
    {
        ArgumentNullException.ThrowIfNull(node);
        if (Environment.CurrentManagedThreadId != _ownerThread) throw new InvalidOperationException("呈现声明须由会话所属线程读取。");
        lock (_gate)
        {
            var scope = OpenScope(workflowId);
            var view = RequireNode(scope, node.Id);
            if (!ReferenceEquals(view.Node, node)) throw new InvalidOperationException("呈现查询的节点实例已退出。");
            var configuration = view.Configuration;
            if (draft is not null)
            {
                if (!ReferenceEquals(draft.Session, this) || !ReferenceEquals(draft.Node, node) || draft.WorkflowId != workflowId)
                    throw new InvalidOperationException("呈现草稿不属于该会话实际节点。");
                configuration = draft.CapturePresentationCandidate();
            }
            if (_admitting) throw new InvalidOperationException("呈现声明查询不能重入准入。");
            _admitting = true;
            try
            {
                using var invocation = _directory.AcquireInvocation();
                var definition = configuration.Definition;
                var provider = definition as IToolResultPresentationProvider;
                var schema = provider?.ResultSchema ?? configuration.Schema;
                var projection = provider?.ResultProjection;
                var diagnostics = projection?.Validate(schema);
                if (diagnostics is { Count: > 0 }) throw new InvalidOperationException(diagnostics[0].Message);
                var display = (definition as IToolResultDisplayProvider)?.ResultDisplay;
                display?.Validate(schema);
                var appearance = ResultDisplayFields.Capture(definition, configuration.Configuration.Fields);
                ValidateScopeCore(scope);
                if (!ReferenceEquals(node.Configuration, view.Configuration.Configuration))
                    throw new InvalidOperationException("呈现声明读取期间正式节点配置已改变。");
                return new(schema, projection, display, appearance);
            }
            finally { _admitting = false; }
        }
    }
}
