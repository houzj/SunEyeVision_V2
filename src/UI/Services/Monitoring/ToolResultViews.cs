using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.Wpf.Presenters.Results;

namespace SunEyeVision.UI.Services.Monitoring;

/// <summary>UI publication boundary. Views borrow declared values during Update,
/// while the host retains the published resource scope; no tool result-class conversion.</summary>
internal static class ToolResultViews
{
    public static void Update(IToolResultView view,ToolResults? result)
    {
        if(result is not {IsSuccess:true}){view.Update(null);return;}
        using var lease=result.Resources?.Retain();
        using var reader=result.DeclaredOutput?.AcquireReader();
        if(reader?.Values is {} values){view.Update(values);return;}
        // A direct debug result can arrive before workflow publication. Read its
        // declared output catalog, exactly as the normal publication boundary does.
        var outputs=result.GetOutputVariables().Where(output=>output.Kind==OutputVariableKind.Data)
            .ToDictionary(BindingClrAdapter.OutputPortId,StringComparer.Ordinal);
        var data=new Dictionary<string,object?>(StringComparer.Ordinal);
        foreach(var port in view.Sources)
        {
            if(!outputs.TryGetValue(port.Id,out var output))throw new InvalidOperationException("Missing declared result view source: "+port.Id);
            var value=result.TryGetDeclaredOutputValue(port.Id,out var canonical)?canonical
                :BindingClrAdapter.NormalizeValue(BindingClrAdapter.DescribeOutput(output),output.RuntimeValue);
            data.Add(port.Id,value);
        }
        // No image frame is invented and no resource is captured/registered by the UI.
        view.Update(new InputValues(view.Sources,data));
    }
}
