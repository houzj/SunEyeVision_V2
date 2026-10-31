using System;
using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Workflow;

public sealed partial class NodeConfigurationEditHost
{
    public ConstantSetting? ReadCurrentParameter(NodeConfigurationEditDraft editor,string fieldId)
    {
        ArgumentNullException.ThrowIfNull(editor);
        lock(_gate)
        {
            if(editor.Owner!=_owner||editor.Consumed)throw new InvalidOperationException("参数当前值的编辑草稿已退役。");
            var formal=Find(_store.Capture().CopyConfiguration(),editor.Identity);
            if(NodeEditSignature(formal)!=editor.Signature)throw new InvalidOperationException("正式参数关系已改变，请重新确认编辑草稿。");
            var candidate=editor.Node.RuntimeConfiguration??throw new InvalidOperationException("编辑草稿没有Runtime参数。");
            if(editor.RuntimeValueBaseline is not {} opening||candidate.CurrentValueChangedFrom(fieldId,opening))
                return candidate.ReadCurrentParameter(fieldId);
            return formal.RuntimeConfiguration?.ReadCurrentParameter(fieldId);
        }
    }

    internal System.Collections.Generic.IReadOnlyDictionary<WorkflowNodeBase,RuntimeToolConfiguration?> CaptureParameterConfigurations()
    {
        lock(_gate)
        {
            var snapshot=new System.Collections.Generic.Dictionary<WorkflowNodeBase,RuntimeToolConfiguration?>(
                System.Collections.Generic.ReferenceEqualityComparer.Instance);
            // Canvas history can add a node before configuration scopes synchronize. Capture
            // actual document identities without accepting drafts or causing a synchronization.
            foreach(var node in Nodes(_design).Values)snapshot.Add(node,node.RuntimeConfiguration);
            return snapshot;
        }
    }

    internal RuntimeParameterValueAcceptance CaptureParameterValueAcceptance(BindingConfigurationSnapshot snapshot)
    {
        lock(_gate)
        {
            if(!ReferenceEquals(snapshot,_store.Capture()))
                throw new InvalidOperationException("参数值接纳入口必须对应当前正式配置快照。");
            var owner=_design.ExecutionOwner;
            var instances=_nodeInstances.ToDictionary(pair=>pair.Key,pair=>pair.Value);
            var configurations=Nodes(snapshot.CopyConfiguration()).Where(pair=>pair.Value.RuntimeConfiguration is not null)
                .ToDictionary(pair=>pair.Key,pair=>pair.Value.RuntimeConfiguration!.ConfigurationIdentity);
            return new((workflowId,nodeId,identity,configuration,fieldId,value)=>
            {
                var key=new ScopedNodeIdentity(workflowId,nodeId);
                lock(_gate)
                {
                    // Structure and configuration validation happen before acquiring the execution
                    // identity gate. Inside that gate the prepared root is installed without observers.
                    if(!identity.HasStartedExecution||!ReferenceEquals(identity.Owner,owner)||identity.NodeId!=workflowId+"/"+nodeId
                        ||!instances.TryGetValue(key,out var captured)||!_nodeInstances.TryGetValue(key,out var current)
                        ||!ReferenceEquals(captured,current)||!_design.Workflows.Any(flow=>flow.Id==workflowId
                            &&flow.Nodes.Any(node=>ReferenceEquals(node,current)))
                        ||!configurations.TryGetValue(key,out var admitted)||configuration.ConfigurationIdentity!=admitted)
                        throw new InvalidOperationException("参数写入不属于当前方案、节点实例或冻结配置。");
                    var baseline=_store.Capture();
                    var draft=_store.BeginEdit();
                    var node=Find(draft.Configuration,key);
                    var prior=current.RuntimeConfiguration;
                    if(prior is null||node.RuntimeConfiguration?.ConfigurationIdentity!=admitted||prior.ConfigurationIdentity!=admitted)
                        throw new InvalidOperationException("参数关系或声明已改变，旧运行不得写入当前值。");
                    node.RuntimeConfiguration=node.RuntimeConfiguration.WithAcceptedParameterValue(fieldId,value);
                    // This is an immutable candidate; all validation/copying precedes identity admission.
                    RuntimeToolConfiguration? prepared=null;
                    _store.AcceptRuntimeValues(draft,
                        _=>current.RuntimeConfiguration=prepared!,
                        candidate=>
                        {
                            if(BindingConfigurationStore.ConfigurationSignature(candidate)!=BindingConfigurationStore.ConfigurationSignature(baseline.CopyConfiguration()))
                                throw new InvalidOperationException("参数值接纳不能修改配置关系或声明。");
                            prepared=Find(candidate,key).RuntimeConfiguration
                                ??throw new InvalidOperationException("参数值候选丢失Runtime配置。");
                        },
                        install=>identity.TryInvokeCallback(()=>
                        {
                            // Admission itself can reenter the host. Never install a verdict or root
                            // prepared before an admission callback changed its publication/instance.
                            if(!ReferenceEquals(_store.Capture(),baseline)||!ReferenceEquals(current.RuntimeConfiguration,prior)
                                ||!_nodeInstances.TryGetValue(key,out var admittedNode)||!ReferenceEquals(admittedNode,current))
                                throw new InvalidOperationException("参数接纳期间正式状态已改变，拒绝旧候选。");
                            install();
                        }));
                }
            });
        }
    }
}
