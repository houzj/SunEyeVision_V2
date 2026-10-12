using System;
using System.Threading;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls.Communication;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

public sealed class CommunicationAnalysisControlTests
{
    [Fact]
    public void Host_analysis_uses_same_result_and_replaces_previous_diagnostics()
    {
        Exception? failure = null;
        var thread = new Thread(() =>
        {
            try
            {
                var result = new CommunicationToolResults { ToolName = "协议解析" };
                result.SetAnalysis(new[] { new CommunicationAnalysisRow(null, "out0", "00 5C", "[0,1]", "诊断92", true) });
                result.SetSuccess(0.5);
                var control = new CommunicationDataAnalysisControl();
                control.OnResultUpdated(result);
                Assert.Same(result, control.CurrentResult);
                var grid = Assert.IsType<DataGrid>(control.FindName("AnalysisGrid"));
                Assert.Single(grid.Items);
                Assert.True(grid.IsReadOnly);
                Assert.Equal(5, grid.Columns.Count);
                var next = new CommunicationToolResults(); next.SetError("输入不可用");
                control.OnResultUpdated(next);
                Assert.Empty(grid.Items);
                Assert.Same(next, control.CurrentResult);
                Assert.Contains("输入不可用", Assert.IsType<TextBlock>(control.FindName("ExecutionSummary")).Text);
            }
            catch (Exception ex) { failure = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA); thread.IsBackground = true; thread.Start();
        Assert.True(thread.Join(TimeSpan.FromSeconds(15)), "通信分析控件测试超时。");
        if (failure != null) System.Runtime.ExceptionServices.ExceptionDispatchInfo.Capture(failure).Throw();
    }
}
