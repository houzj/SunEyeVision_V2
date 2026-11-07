using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Plugin.Editing.Wpf.Execution.Parameters;

public partial class ParameterResolver
{
    private readonly ParameterBindingService _compiledBindings=new();
    public BindingCompilation CompileBinding(OutputReference reference,Port target,IBindingCompilationScope scope,BindingConversion conversion=BindingConversion.Automatic,ParameterReferenceBehavior behavior=ParameterReferenceBehavior.StrictExactlyOne)
        =>_compiledBindings.CompileBinding(reference,target,scope,conversion,behavior);
    public BindingResolution ResolveBinding(BindingPlan plan,IBindingRunSnapshot snapshot)
        =>_compiledBindings.ResolveBinding(plan,snapshot);
}
