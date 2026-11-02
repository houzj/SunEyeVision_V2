using System.Collections.Generic;
using SunEyeVision.Plugin.Contracts;
namespace SunEyeVision.Workflow;
public sealed partial class RuntimeToolConfiguration
{
    private readonly IReadOnlyDictionary<string,ParameterReferenceBehavior> _referenceBehaviors;
    /// <summary>Frozen tool-declared parameter behavior. Inputs and undeclared settings stay strict.</summary>
    public ParameterReferenceBehavior GetParameterReferenceBehavior(string fieldId)
        =>_referenceBehaviors.TryGetValue(fieldId,out var behavior)?behavior:ParameterReferenceBehavior.StrictExactlyOne;
}
