using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

public partial class ParameterResolver
{
    private readonly ParameterBindingService _compiledBindings=new();
    public BindingCompilation CompileBinding(OutputReference reference,Port target,IBindingCompilationScope scope,BindingConversion conversion=BindingConversion.Automatic)
        =>_compiledBindings.CompileBinding(reference,target,scope,conversion);
    public BindingResolution ResolveBinding(BindingPlan plan,IBindingRunSnapshot snapshot)
        =>_compiledBindings.ResolveBinding(plan,snapshot);
}
