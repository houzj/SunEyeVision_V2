using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Controls.References.Variables.Design;

internal static class VariableSelectionDesignData
{
    internal static IReadOnlyList<VariableSelectionItem> Items {get;}=Enumerable.Range(1,16).Select(index=>
        new VariableSelectionItem("参数"+index,index<=12?"检测参数":"运行参数",index,"设计时示例",index.ToString(),
            new VariableReference("design-solution","variable-"+index,TypeRef.ScalarOf(ScalarKind.Int64)))).ToArray();
}
