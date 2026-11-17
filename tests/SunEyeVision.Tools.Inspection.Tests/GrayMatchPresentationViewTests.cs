using System.Runtime.Versioning;
using SunEyeVision.Tool.GrayMatch.Runtime;
using SunEyeVision.Tool.GrayMatch.Wpf.Design;
using SunEyeVision.Tool.GrayMatch.Wpf.Views;
using Xunit;

namespace SunEyeVision.Tools.Inspection.Tests;

/// <summary>
/// 呈现层契约的端到端验证：参数页必须能按正式声明组装（输入区、图形分组完整消费、文本坐标注册），
/// 且 `EagerlyComposePages` 打开时**全部页签**（模板参数 / 运行参数 / 结果显示）都能构建；
/// 结果页必须能用正式输出声明构建。历史上这几类缺一项都会在宿主里抛异常导致参数页组装失败。
/// </summary>
[SupportedOSPlatform("windows")]
public sealed class GrayMatchPresentationViewTests
{
    private static void OnSta(Action action) => WpfPresentationHost.Run(action);

    [Fact]
    public void Parameter_view_binds_the_formal_draft_without_throwing()
    {
        OnSta(() =>
        {
            var context = GrayMatchDesignData.CreateParameterContext();
            var editor = new GrayMatchParameterEditor(context.Draft, context.Options);
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
    public void Parameter_view_composes_every_page_when_all_pages_are_requested()
    {
        OnSta(() =>
        {
            var context = GrayMatchDesignData.CreateParameterContext();
            var options = context.Options with { EagerlyComposePages = true };
            var editor = new GrayMatchParameterEditor(context.Draft, options);
            try
            {
                Assert.True(editor.ReadyTask.IsCompleted);
                Assert.NotNull(editor.View);
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
            var tool = new GrayMatchTool();
            var view = new GrayMatchResultView(tool.ResultSchema, tool.ResultProjection);
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
