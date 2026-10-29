using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

public sealed record ReferenceBindingBatchResult(InputValues? Values,Diagnostic? Diagnostic,
    ImageFrameAddress? Frame=null,Func<bool>? IsCurrent=null)
{
    public bool Success=>Values is not null&&Diagnostic is null;
}

/// <summary>
/// Borrows one host-owned declaration/run snapshot and its result leases. The host supplies the
/// existing resolver; this adapter neither interprets indices nor reads workflow/global state.
/// </summary>
public sealed class ReferenceBindingBatch
{
    private readonly IBindingPlanCompiler _compiler;
    private readonly IBindingCompilationScope _scope;
    private readonly IBindingRunSnapshot _snapshot;
    private readonly Func<BindingPlan,IBindingRunSnapshot,BindingResolution> _resolve;
    private readonly Func<bool> _isCurrent;
    private readonly IReadOnlyDictionary<BindingNodeId,ImageFrameAddress> _frames;
    private bool _resolving;
    public ImageFrameAddress Frame {get;}
    public RunIdentity Identity=>_snapshot.ConsumerIdentity;

    public ReferenceBindingBatch(IBindingPlanCompiler compiler,IBindingCompilationScope scope,IBindingRunSnapshot snapshot,
        Func<BindingPlan,IBindingRunSnapshot,BindingResolution> resolve,ImageFrameAddress frame,
        IReadOnlyDictionary<BindingNodeId,ImageFrameAddress> sourceFrames,Func<bool> isCurrent)
    {
        ArgumentNullException.ThrowIfNull(compiler);ArgumentNullException.ThrowIfNull(scope);ArgumentNullException.ThrowIfNull(snapshot);
        ArgumentNullException.ThrowIfNull(resolve);ArgumentNullException.ThrowIfNull(frame);ArgumentNullException.ThrowIfNull(sourceFrames);ArgumentNullException.ThrowIfNull(isCurrent);
        _compiler=compiler;_scope=scope;_snapshot=snapshot;_resolve=resolve;Frame=frame;_isCurrent=isCurrent;
        _frames=new ReadOnlyDictionary<BindingNodeId,ImageFrameAddress>(sourceFrames.ToDictionary(pair=>pair.Key,pair=>pair.Value));
    }

    public ReferenceBindingBatchResult Resolve(IEnumerable<(Port Target,ParameterSetting Setting)> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        if(_resolving)return Failure("binding-reentrant","当前整组解析尚未结束。");
        _resolving=true;
        try
        {
            if(!_isCurrent())return Stale();
            if(_scope.Consumer!=_snapshot.Consumer||_scope.DocumentSessionId!=_snapshot.ConsumerIdentity.DocumentSessionId
                ||_scope.DocumentRevision!=_snapshot.DocumentRevision||_snapshot.ConsumerIdentity.RequestId==Guid.Empty)
                return Failure("document-changed","声明与运行快照不属于同一消费者及文档版本。");
            var all=fields.ToArray();
            if(all.Length==0||all.Any(field=>field.Target is null||field.Setting is null)
                ||all.Select(field=>field.Target.Id).Distinct(StringComparer.Ordinal).Count()!=all.Length)
                return Failure("batch-fields","整组字段必须非空且身份唯一。");
            // InputValues enforces complete required targets; never return a partly resolved group.
            var targets=all.Select(field=>field.Target with{Required=true}).ToArray();
            _=new ToolSchema(targets,Array.Empty<Port>());
            var values=new Dictionary<string,object?>(StringComparer.Ordinal);
            foreach(var (target,setting) in all)
            {
                if(!_isCurrent())return Stale();
                if(setting is ConstantSetting constant)
                {values.Add(target.Id,ValueRules.Freeze(target,constant.Value));continue;}
                if(setting is not ReferenceSetting reference)return Failure("binding-unset","整组参数尚未完成。",target.Id);
                if(!string.IsNullOrEmpty(reference.RebindingReason))return Failure("binding-reselection",reference.RebindingReason,target.Id);
                var compiled=_compiler.CompileBinding(reference.Reference,target,_scope,reference.Conversion);
                if(!_isCurrent())return Stale();
                if(compiled.Plan is not {} plan)return new(null,compiled.Diagnostic??new("binding-compile","来源无法编译。",target.Id));
                foreach(var dependency in plan.Dependencies)
                    if(!_frames.TryGetValue(dependency,out var frame)||frame!=Frame)
                        return Failure("frame-mismatch","来源及索引依赖必须属于同一物理帧。",target.Id);
                var resolved=_resolve(plan,_snapshot);
                if(!_isCurrent())return Stale();
                if(!resolved.Success)return new(null,resolved.Diagnostic??new("binding-resolve","来源未在当前快照产生可用值。",target.Id));
                values.Add(target.Id,ValueRules.Freeze(target,resolved.Value));
            }
            if(!_isCurrent())return Stale();
            return new(new InputValues(targets,values),null,Frame,_isCurrent);
        }
        catch(Exception error)when(error is ArgumentException or OverflowException)
        {return Failure("invalid-value",error.Message);}
        finally{_resolving=false;}
    }
    private static ReferenceBindingBatchResult Stale()=>Failure("binding-stale","整组解析期间宿主、运行、声明或图像已改变。");
    private static ReferenceBindingBatchResult Failure(string code,string message,string? field=null)=>new(null,new(code,message,field));
}
