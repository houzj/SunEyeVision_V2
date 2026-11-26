using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>
/// 节点级常驻 ROI 文档的宿主字段读写。对应原厂参数树 ROISelecter 常驻：
/// 读直接取节点配置（免参数页控件），写立即经 DocumentSession 提交并进撤销。
/// </summary>
internal sealed class NodeRoiDocumentAccess : IRoiInputAccess
{
    private readonly Func<DocumentSession> _session;
    private readonly Func<NodeDefinition> _node;
    private readonly Func<string, bool> _isRoiField;
    private readonly Action<DocumentEditReceipt> _published;

    internal NodeRoiDocumentAccess(Func<DocumentSession> session, Func<NodeDefinition> node,
        Func<string, bool> isRoiField, Action<DocumentEditReceipt> published)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _node = node ?? throw new ArgumentNullException(nameof(node));
        _isRoiField = isRoiField ?? throw new ArgumentNullException(nameof(isRoiField));
        _published = published ?? throw new ArgumentNullException(nameof(published));
    }

    public ParameterSetting Read(string fieldId)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        if (!_isRoiField(fieldId))
            throw new InvalidOperationException($"字段 {fieldId} 不属于 ROI 编辑声明，不能由 ROI 文档读写。");
        var fields = _node().Configuration.Fields;
        if (!fields.Declarations.ContainsKey(fieldId))
            throw new InvalidOperationException($"节点配置缺少 ROI 字段 {fieldId}，不能静默回退。");
        return fields.Read(fieldId);
    }

    public void Write(string fieldId, ParameterSetting setting)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(fieldId);
        if (!_isRoiField(fieldId))
            throw new InvalidOperationException($"字段 {fieldId} 不属于 ROI 编辑声明，不能由 ROI 文档读写。");
        var session = _session();
        var node = _node();
        // 原厂 SetValue 即时写参数对象（ROISelecter.cs:2483）；本方落到 draft→proposal→commit，
        // 保持与 DocumentSession 撤销/发布合同一致（MainWindowRootWorkspace.cs:52-55）。
        var draft = session.Editing.Host.BeginEdit(WorkflowId(session, node), node.Id);
        draft.ApplyFields(new Dictionary<string, ParameterSetting>(StringComparer.Ordinal) { [fieldId] = setting });
        var proposal = DocumentEditProposal.Prepare(session.Editing.Host, nodes: new[] { draft });
        session.ValidateEdit(proposal);
        var receipt = session.CommitEdit(proposal);
        _published(receipt);
    }

    private static string WorkflowId(DocumentSession session, NodeDefinition node)
        => session.Document.Workflows
               .FirstOrDefault(flow => flow.Nodes.Any(candidate => ReferenceEquals(candidate, node)))?.Id
           ?? throw new InvalidOperationException("节点不属于当前方案的任何流程。");
}
