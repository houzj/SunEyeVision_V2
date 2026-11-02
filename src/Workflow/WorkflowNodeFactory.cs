using System;
namespace SunEyeVision.Workflow;
/// <summary>Optional UI presentation materializer; runtime node creation uses the admitted directory.</summary>
public static class WorkflowNodeFactory
{
    public static Func<WorkflowNodeBase,WorkflowNodeBase>? TypeConverter {get;set;}
}
