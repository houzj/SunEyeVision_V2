using System.Runtime.Versioning;
using SunEyeVision.Tool.ContourMatch.Runtime;
using SunEyeVision.Tool.ContourMatch.Wpf.Design;
using SunEyeVision.Tool.ContourMatch.Wpf.Views;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>
/// 呈现层契约的端到端验证：参数页必须能按正式声明组装（输入区、图形分组完整消费、文本坐标注册），
/// 结果页必须能用正式输出声明构建。历史上这三类缺一项都会在宿主里抛异常导致参数页组装失败。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class ContourMatchPresentationViewTests
{
    private static void OnSta(Action action) => WpfPresentationHost.Run(action);

    [Fact]
    public void Parameter_view_binds_the_formal_draft_without_throwing()
    {
        OnSta(() =>
        {
            var context = ContourMatchDesignData.CreateParameterContext();
            var editor = new ContourMatchParameterEditor(context.Draft, context.Options);
            try
            {
                Assert.True(editor.ReadyTask.IsCompleted);
                Assert.NotNull(editor.View);
                Assert.False(editor.HasInvalidInput);
            }
            finally
            {
                editor.Dispose();
            }
        });
    }

    [Fact]
    public void Result_view_builds_from_the_formal_output_declaration()
    {
        OnSta(() =>
        {
            var tool = new ContourMatchTool();
            var view = new ContourMatchResultView(tool.ResultSchema, tool.ResultProjection);
            try
            {
                Assert.NotNull(view.View);
                Assert.NotEmpty(view.Sources);
            }
            finally
            {
                view.Dispose();
            }
        });
    }
}
