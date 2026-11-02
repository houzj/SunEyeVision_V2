using System;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Workflow;

/// <summary>Node declarations are read only from the captured document, workflow and admitted generation.</summary>
public sealed class ScopedNodeInfoProvider : INodeInfoProvider
{
    private readonly Solution _document;
    private readonly Workflow _flow;
    private readonly RuntimeDirectorySnapshot _directory;
    public ScopedNodeInfoProvider(Solution document,string workflowId)
    {
        _document=document??throw new ArgumentNullException(nameof(document));
        ArgumentException.ThrowIfNullOrWhiteSpace(workflowId);
        _directory=document.RuntimeDirectory??throw new InvalidOperationException("方案缺少已准入运行目录。");
        if(!_directory.IsActive)throw new InvalidOperationException("方案运行目录已退役。");
        _flow=document.Workflows.SingleOrDefault(flow=>flow.Id==workflowId)
            ??throw new InvalidOperationException("流程不属于当前方案。");
    }
    private WorkflowNodeBase? Find(string nodeId)
    {
        if(!ReferenceEquals(_document.RuntimeDirectory,_directory)||!_directory.IsActive)
            throw new InvalidOperationException("方案运行目录已变更或退役。");
        if(_document.Workflows.Count(flow=>ReferenceEquals(flow,_flow))!=1||_document.Workflows.Count(flow=>flow.Id==_flow.Id)!=1)
            throw new InvalidOperationException("流程已退出当前方案或身份不唯一。");
        var nodes=_flow.Nodes.Where(node=>node.Id==nodeId).ToArray();
        if(nodes.Length>1)throw new InvalidOperationException("节点身份不唯一。");
        var node=nodes.SingleOrDefault();
        if(node is not null)RuntimeNodeAdmission.Require(_directory,node);
        return node;
    }
    public string GetNodeName(string nodeId)=>Find(nodeId)?.Name??nodeId;
    public string GetNodeType(string nodeId)=>Find(nodeId)?.ToolType??"Unknown";
    public string? GetNodeIcon(string nodeId)=>Find(nodeId)?.Icon;
    public bool NodeExists(string nodeId)=>Find(nodeId) is not null;
    public ToolSchema? GetSchema(string nodeId)
    {
        var node=Find(nodeId);
        if(node is null)return null;
        return node.RuntimeConfiguration!.Schema;
    }
}
