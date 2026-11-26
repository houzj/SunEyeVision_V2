using System.Collections;
using SunEyeVision.Application;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.Wpf.Presenters.Display;
using SunEyeVision.Plugin.Wpf.Regions;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>
/// 节点级常驻 ROI 投影服务。对应原厂参数树 ROISelecter 常驻、ROIList 一直绑在
/// 结果视图上（ModuleDisplayDecorator.cs:512）；窗口开合只切可见性，不销毁投影。
/// 本方无参数树常驻对象模型，改由本服务按选中节点持有常驻投影，会话仅临时挂载。
/// </summary>
internal sealed class NodeRoiEditingService : IDisposable
{
    private readonly Func<DocumentSession?> _session;
    private readonly RuntimeDirectorySnapshot _directory;
    private readonly NativeResultViewport _viewport;
    private NodeBinding? _binding;
    private bool _disposed;

    internal NodeRoiEditingService(Func<DocumentSession?> session, RuntimeDirectorySnapshot directory, NativeResultViewport viewport)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        _directory = directory ?? throw new ArgumentNullException(nameof(directory));
        _viewport = viewport ?? throw new ArgumentNullException(nameof(viewport));
    }

    /// <summary>选中节点或当前帧变化时调用；同一节点+同一尺寸保持投影实例不变。</summary>
    internal void Sync(NodeDefinition? node)
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
        _viewport.View.Dispatcher.VerifyAccess();
        var session = _session();
        var size = _viewport.ResultImageSize;
        if (node is null || session is null || size is null ||
            !session.Document.Workflows.Any(flow => flow.Nodes.Any(candidate => ReferenceEquals(candidate, node))))
        {
            Retire();
            return;
        }
        var declaration = _directory.TryCreateDefinition(node.ToolType)?.RoiEdit;
        if (declaration is null || declaration.Inheritance is null)
        {
            // 无本地 ROI 声明的工具没有常驻 ROIList（原厂同理：无 ROISelecter 的节点无 ROI）。
            Retire();
            return;
        }
        var width = size.Value.Width;
        var height = size.Value.Height;
        var workflowId = session.Document.Workflows.First(flow => flow.Nodes.Any(candidate => ReferenceEquals(candidate, node))).Id;
        // 提交会替换 NodeDefinition 实例但保持身份；按 (workflowId,nodeId) 去重，常驻投影不重建。
        if (_binding is {} current && current.WorkflowId == workflowId && current.NodeId == node.Id
            && current.Width == width && current.Height == height)
        {
            current.Node = node;
            return;
        }
        Retire();
        var access = new NodeRoiDocumentAccess(() => _session() ?? throw new InvalidOperationException("方案会话已失效。"),
            () => node, declaration.IsRoiField, _ => { });
        var store = new RoiDocumentStore(access, declaration.DocumentFieldId,
            declaration.Groups.Select(group => new NativeRoiEditGroup(group.Group, group.Title, group.IsMask, group.MaximumCount, group.EmptyCoverage)).ToArray(),
            declaration.ImageFieldId, declaration.Inheritance,
            declaration.MaskInheritance is {} mask ? new MaskInheritanceSpec(mask.ModeFieldId, mask.SourceFieldId, mask.MaximumInherited) : null,
            declaration.ItemNames);
        var document = store.Read();
        var rows = new List<object>();
        foreach (var group in store.Groups)
            foreach (var item in RoiDocumentValue.Items(document, group.Name))
                rows.Add(NativeRoiProjection.CreateResident(
                    new RoiEditItem(group.Name, Guid.ParseExact(item.Get<string>("id"), "N"), item.Get<string>("name"),
                        RoiShapeProjection.ToNative(item.Get<RecordValue>("shape")), item.Get<bool>("enabled"), item.Get<bool>("visible")),
                    width, height));
        var resident = (IList)Array.AsReadOnly(rows.ToArray());
        _binding = new NodeBinding(workflowId, node.Id, width, height, resident) { Node = node };
        _viewport.SetResidentRoiRows(resident);
    }

    /// <summary>节点切换或方案退役：回收常驻投影（原厂只在节点退出时销毁 ROISelecter）。</summary>
    internal void Retire()
    {
        if (_binding is null) return;
        var binding = _binding;
        _binding = null;
        _viewport.ClearResidentRoiRows(binding.Rows);
    }

    public void Dispose() { if (_disposed) return; _disposed = true; Retire(); }

    private sealed class NodeBinding(string workflowId, string nodeId, int width, int height, IList rows)
    {
        internal string WorkflowId { get; } = workflowId;
        internal string NodeId { get; } = nodeId;
        internal int Width { get; } = width;
        internal int Height { get; } = height;
        internal IList Rows { get; } = rows;
        internal NodeDefinition Node { get; set; } = null!;
    }
}
