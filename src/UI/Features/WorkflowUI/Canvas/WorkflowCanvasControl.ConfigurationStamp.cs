using System.Text.Json;
using SunEyeVision.Workflow;
namespace SunEyeVision.UI.Features.WorkflowUI.Canvas;
public partial class WorkflowCanvasControl
{
    // 只在手势起点/提交点读取持久化配置，不在每次鼠标移动时序列化。
    private string? VmConfigurationStamp(WorkflowTabViewModel tab)
    {
        try
        {
            return string.Join("\n",tab.WorkflowNodes.Select(n=>
                (n.Parameters==null?"null":JsonSerializer.Serialize(n.Parameters,n.Parameters.GetType(),WorkflowSerializationOptions.Compact))+
                "|"+JsonSerializer.Serialize(n.ParamSettings,WorkflowSerializationOptions.Compact)));
        }
        catch (JsonException) { return null; }
        catch (NotSupportedException) { return null; }
    }
}
