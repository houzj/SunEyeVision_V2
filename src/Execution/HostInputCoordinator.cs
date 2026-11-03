using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Execution;

/// <summary>One common admission/ownership path for formal and debug host input.</summary>
internal static class HostInputCoordinator
{
    internal static async ValueTask<PreparedHostInvocation> PrepareAsync(ExecutionNodeSnapshot configuration,
        IHostInputPreparer preparer, object settings, ToolExecutionIdentity identity,
        string nodeId, IToolExecutionContext baseContext)
    {
        if (ToolHostDeclarationRules.ValidateAndFingerprint(configuration.Definition) != configuration.HostDeclarations.Fingerprint)
            throw new InvalidOperationException("宿主输入声明与冻结执行快照不一致。");
        var declaration=configuration.Directory.GetHostInputs(configuration.Configuration.ToolId)
            ?? throw new InvalidOperationException("工具未声明宿主输入能力。");
        if(preparer.CapabilityId!=declaration.CapabilityId || preparer.CapabilityVersion!=declaration.Version)
            throw new InvalidOperationException("宿主输入能力与准入目录不一致。");
        var services=preparer.Services?.ToArray() ?? throw new InvalidOperationException("宿主输入服务声明缺失。");
        if(services.Any(string.IsNullOrWhiteSpace)||services.Distinct(StringComparer.Ordinal).Count()!=services.Length||
            services.Any(service=>!declaration.Services.Contains(service,StringComparer.Ordinal)))
            throw new InvalidOperationException("宿主输入使用了未声明或重复的服务。");
        if(configuration.Definition is not IToolSettingsFields fields)
            throw new InvalidOperationException("宿主输入要求正式设置声明。");
        var expected=identity.ToInvocationIdentity(nodeId);
        if(baseContext.Identity!=expected || baseContext.Cancellation!=identity.CancellationToken)
            throw new InvalidOperationException("基础执行上下文与当前调用身份不一致。");
        IPreparedHostInputs? lease=null;
        try
        {
            EnsureCurrent(identity);
            var context=new HostInputPreparationContext(declaration,fields.ReadSettings(settings),expected,
                identity.CancellationToken,new InvocationGate(identity));
            lease=await preparer.PrepareAsync(context).ConfigureAwait(false)
                ?? throw new InvalidOperationException("宿主输入没有返回租约。");
            EnsureCurrent(identity);
            var declared=declaration.Ports.ToDictionary(port=>port.PortId,StringComparer.Ordinal);
            var schema=configuration.Schema.Inputs.ToDictionary(port=>port.Id,StringComparer.Ordinal);
            var values=new Dictionary<string,object?>(StringComparer.Ordinal);
            foreach(var pair in lease.Values ?? throw new InvalidOperationException("宿主输入没有返回值集合。"))
            {
                if(!declared.TryGetValue(pair.Key,out var declaredPort)||!schema.TryGetValue(pair.Key,out var port)||declaredPort.Type!=port.Type)
                    throw new InvalidOperationException("宿主输入返回未声明或类型不符的端口："+pair.Key);
                values.Add(pair.Key,ValueRules.Freeze(port,pair.Value));
            }
            var bound=lease.BindContext(baseContext) ?? throw new InvalidOperationException("宿主输入没有返回执行上下文。");
            if(bound.Identity!=expected||bound.Cancellation!=baseContext.Cancellation||!ReferenceEquals(bound.Resources,baseContext.Resources))
                throw new InvalidOperationException("宿主输入不得替换调用身份、取消令牌或输出资源域。");
            var prepared=new PreparedHostInvocation(new ReadOnlyDictionary<string,object?>(values),bound,lease);
            lease=null;return prepared;
        }
        catch(Exception failure)
        {
            if(lease is not null)
                try{await lease.DisposeAsync().ConfigureAwait(false);}catch(Exception cleanup){throw new AggregateException("宿主输入准备和资源释放均失败。",failure,cleanup);}
            throw;
        }
    }

    private static void EnsureCurrent(ToolExecutionIdentity identity)
    {
        identity.CancellationToken.ThrowIfCancellationRequested();
        if(!identity.IsCurrent)throw new InvalidOperationException("宿主输入的执行身份已退役。 ");
    }

    private sealed class InvocationGate(ToolExecutionIdentity identity):IHostInvocationGate
    { public bool TryInvoke(Action callback)=>identity.TryInvokeCallback(callback); }
}

internal sealed class PreparedHostInvocation(IReadOnlyDictionary<string,object?> values,IToolExecutionContext context,IPreparedHostInputs lease):IAsyncDisposable
{
    private IPreparedHostInputs? _lease=lease;
    internal IReadOnlyDictionary<string,object?> Values {get;}=values;
    internal IToolExecutionContext Context {get;}=context;
    public async ValueTask DisposeAsync()
    {var current=Interlocked.Exchange(ref _lease,null);if(current is not null)await current.DisposeAsync().ConfigureAwait(false);}
}
