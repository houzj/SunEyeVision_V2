using System.Linq;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Workflow;

public sealed partial class RuntimeToolConfiguration
{
    public ToolSchema DisplaySettingsSchema=>ResultDisplayFields.Schema(_definition);
    public ToolSchema EditorSettingsSchema=>ResultDisplayFields.EditorSchema(_definition);
    public ResultDisplayDescriptor? ResultDisplay=>(_definition as IToolResultDisplayProvider)?.ResultDisplay;
    public ResultDisplayAppearance? CaptureResultDisplay()=>ResultDisplayFields.Capture(_definition,Fields);
    public bool IsDisplayField(string id)=>DisplaySettingsSchema.Inputs.Any(port=>port.Id==id);
}
