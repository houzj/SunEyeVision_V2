using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Workflow;

/// <summary>Pure declaration checks for global producers. Does not admit or execute a run,
/// read a result, acquire a resource, or mutate the document.</summary>
public static class GlobalProducerBindingRules
{
    public static bool TryDescribeTarget(string vocabulary, out Port target, out Type clrType, out string reason)
    {
        target=null!;clrType=null!;reason=string.Empty;
        // Current variable declarations express scalar and sequence cardinality explicitly.
        // Do not inherit historical SDK carrier aliases (Integer[] -> int[]) here.
        var resolved=vocabulary switch {
            "Integer[]"=>typeof(long[]),"Double[]"=>typeof(double[]),
            "String[]"=>typeof(string[]),"Boolean[]"=>typeof(bool[]),
            _=>SdkTypeVocabulary.ToSystemType(vocabulary)};
        if(resolved is null)
        {reason="全局变量类型没有正式存储声明。";return false;}
        if(!BindingClrAdapter.TryDescribe(resolved,out var type,out var storage)
            ||!(type.Shape==ValueShape.Scalar||type.Shape==ValueShape.Sequence&&type.Element!.Shape==ValueShape.Scalar))
        {reason="全局变量尚无资源、组合或几何的正式存储及租约合同，不能建立此生产绑定。";return false;}
        target=new Port("value",type,Storage:storage);clrType=resolved;return true;
    }

    public static BindingValidation Validate(Solution solution,ProducerBinding producer,GlobalVariable variable)
        =>Validate(solution,producer,variable,node=>new OutputBindingChangeService().ReadContract(node));

    // Reconciliation borrows its already captured contracts, including its actual host reader.
    // It must not re-read a mutable provider halfway through building one atomic change plan.
    internal static BindingValidation Validate(Solution solution,ProducerBinding producer,GlobalVariable variable,
        Func<WorkflowNodeBase,IReadOnlyList<OutputVariable>?> readContract)
    {
        ArgumentNullException.ThrowIfNull(solution);ArgumentNullException.ThrowIfNull(producer);ArgumentNullException.ThrowIfNull(variable);
        BindingValidation Reject(string message)=>new(BindingCompatibility.Incompatible,message);
        try
        {
            var expression=producer.ReferenceExpression;
            var vocabulary=variable.Type;var variableId=variable.Id;
            var reference=producer.SourceReference;
            if(string.IsNullOrWhiteSpace(reference.ScopeId))return Reject("全局变量生产来源必须声明所属流程作用域。");
            if(reference.Selection is not BindingSelection.AllItems)
                return Reject("全局变量生产绑定当前仅支持声明字段及集合All，不开放未经原件确认的单项或动态索引。");
            if(!TryDescribeTarget(vocabulary,out var target,out var clrType,out var reason))return Reject(reason);
            var flows=solution.Workflows.Where(flow=>flow.Id==reference.ScopeId).ToArray();
            if(flows.Length!=1)return Reject("全局变量生产来源流程已删除或作用域身份不唯一。");
            var sources=flows[0].Nodes.Where(node=>node.Id==reference.NodeId).ToArray();
            if(sources.Length!=1)return Reject("全局变量生产节点已删除或身份不唯一，请重新绑定。");
            var flow=flows[0];var source=sources[0];
            var parameters=source.Parameters;var runtime=source.RuntimeConfiguration;
            bool Current()=>producer.ReferenceExpression==expression&&variable.Type==vocabulary&&variable.Id==variableId
                &&flow.Id==reference.ScopeId&&source.Id==reference.NodeId
                &&solution.Workflows.Count(candidate=>ReferenceEquals(candidate,flow))==1
                &&flow.Nodes.Count(candidate=>ReferenceEquals(candidate,source))==1
                &&ReferenceEquals(source.Parameters,parameters)&&ReferenceEquals(source.RuntimeConfiguration,runtime);
            var contract=readContract(source);
            if(!Current())return Reject("读取生产来源声明期间配置或节点归属已改变，请重新选择当前来源。");
            if(contract is null)return new(BindingCompatibility.Unresolved,"生产来源正式输出声明暂不可用，等待校验。");
            var outputs=contract.Where(output=>output.Kind==SunEyeVision.Plugin.SDK.Execution.Results.OutputVariableKind.Data).ToArray();
            var selected=outputs.Where(output=>BindingClrAdapter.OutputPortId(output)==reference.PortId).ToArray();
            if(selected.Length!=1)return Reject("全局变量原输出身份已删除或不唯一，请重新绑定。");
            var output=selected[0];
            // A dynamic output may exist before its type declaration is complete. Keep the
            // explicit source pending; only a removed port or a known conflict breaks a binding.
            var declared=output.DeclaredType??SdkTypeVocabulary.ToSystemType(output.VariableType);
            if(output.ValueDeclaration is null&&(declared is null||declared==typeof(object)))
                return new(BindingCompatibility.Unresolved,"生产字段的正式类型声明尚不完整，等待校验。");
            var port=BindingClrAdapter.DescribeOutput(output);
            // Only the selected formal output is needed for this declaration-only operation.
            // Other unsupported output carriers do not justify guessing or rejecting this port.
            var sourceSchema=new ToolSchema(Array.Empty<Port>(),new[]{port});
            var node=new BindingNodeId(reference.ScopeId,reference.NodeId);
            // This is a design-only scope. No RunIdentity or expected invocation is fabricated.
            var scope=new BindingScopeSnapshot(new(reference.ScopeId,"global-producer:"+variable.Id),Guid.Empty,0,
                new[]{new BindingSource(node,Guid.Empty,sourceSchema,0)},new[]{node},
                Array.Empty<KeyValuePair<BindingNodeId,IReadOnlyList<BindingNodeId>>>());
            var compiled=new ParameterBindingService().CompileBinding(reference,target,scope);
            if(!Current())return Reject("编译生产来源期间配置或节点归属已改变，请重新选择当前来源。");
            return compiled.Plan is null
                ?Reject(compiled.Diagnostic?.Message??"全局变量生产来源声明不符合目标存储类型。")
                :new(compiled.Compatibility,"正式生产来源声明可绑定；运行时仍须当前批次准入与值校验。",clrType);
        }
        catch(Exception failure)when(failure is JsonException or ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {return Reject("全局变量生产绑定无效："+failure.Message);}
    }
}
