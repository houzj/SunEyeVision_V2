using System.Runtime.Versioning;
using System.Collections;
using System.Windows;
using SunEyeVision.Tool.ContourMatch.Runtime;
using SunEyeVision.Tool.ContourMatch.Wpf.Design;
using SunEyeVision.Tool.ContourMatch.Wpf.Views;
using SunEyeVision.Plugin.Contracts;
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

    /// <summary>
    /// 模板配置窗口的数据模型必须能从正式草稿加载，枚举字段按枚举名解析。
    /// 历史问题：枚举字段用属性名查声明（传入的是 field:GUID 标识）会抛"未映射的字段"，导致创建模板直接崩溃。
    /// </summary>
    [Fact]
    public void Template_config_data_loads_enum_fields_from_formal_draft()
    {
        OnSta(() =>
        {
            var context = ContourMatchDesignData.CreateParameterContext();
            var data = new TemplateConfigData(context.Draft, null, () => { }, () => { }, () => { },
                SunEyeVision.Plugin.Contracts.RoiDocumentValue.Empty, null);
            // 测试直接驱动命令接缝，不能让建模失败框弹进测试宿主（会阻塞并弹到桌面）。
            data.SuppressDialogs = true;
            Assert.NotNull(data.ItemsData);
            Assert.InRange(data.PyramidScaleFlag, 0, 1);
            Assert.InRange(data.EdgeThresholdFlag, 0, 1);
        });
    }

    [Fact]
    public void Clearing_template_masks_marks_both_layers_as_explicit_current_state()
    {
        OnSta(() =>
        {
            var context = ContourMatchDesignData.CreateParameterContext();
            var data = new TemplateConfigData(context.Draft, null, () => { }, () => { }, () => { },
                SunEyeVision.Plugin.Contracts.RoiDocumentValue.Empty, null);

            data.ClearEraseMask();
            var snapshot = data.CaptureMaskSnapshot();

            Assert.True(snapshot.EraseMaskSpecified);
            Assert.True(snapshot.PaintMaskSpecified);
            Assert.Null(snapshot.EraseMask);
            Assert.Null(snapshot.PaintMask);
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

    [Fact]
    public void Template_commands_cover_roi_list_clear_history_visibility_and_lock_contract()
    {
        OnSta(() =>
        {
            var context = ContourMatchDesignData.CreateParameterContext();
            RecordValue? accepted = null;
            var data = new TemplateConfigData(context.Draft, null, () => { }, () => { }, () => { },
                SunEyeVision.Plugin.Contracts.RoiDocumentValue.Empty, null,
                document => accepted = document);
            data.SuppressDialogs = true;

            // 原厂工具栏的每条命令都必须有可执行接缝。
            Assert.NotNull(data.SelectCurImageCommand);
            Assert.NotNull(data.SelectOtherImageCommand);
            Assert.NotNull(data.SaveCommand);
            Assert.NotNull(data.MatchCommand);
            Assert.NotNull(data.ClearCommand);
            Assert.NotNull(data.BackStepCommand);
            Assert.NotNull(data.ForwardStepCommand);
            Assert.NotNull(data.RadioButtonCommand);
            Assert.NotNull(data.IsUsePenCommand);
            Assert.NotNull(data.RubberCommand);
            Assert.NotNull(data.Rubber2Command);
            Assert.NotNull(data.AddROICommand);
            Assert.NotNull(data.DeleteCommand);
            Assert.NotNull(data.GirdMouseDownCommand);
            Assert.NotNull(data.ZoomOrgBtnCommand);
            Assert.NotNull(data.ZoomFullBtnCommand);
            Assert.NotNull(data.ConfirmCommand);

            data.RadioButtonCommand.Execute("hikFrame");
            var roi = new RectRoiItem { CenterPoint = new Point(20, 10), Width = 10, Height = 8 };
            data.AddROICommand.Execute(roi);
            Assert.Single((IList)data.ItemsData);
            Assert.Equal(20, data.FixPointX);
            Assert.Equal(10, data.FixPointY);

            // Eye/Sign 改动必须进入确认文档；这是原厂列表两个可编辑列的持久化合同。
            roi.Visible = false;
            data.ConfirmCommand.Execute(null);
            var acceptedDocument = Assert.IsType<RecordValue>(accepted);
            var inspectionItem = SunEyeVision.Plugin.Contracts.RoiDocumentValue.Items(acceptedDocument, SunEyeVision.Plugin.Contracts.RoiDocumentValue.Inspection);
            Assert.Single(inspectionItem);
            Assert.False(inspectionItem[0].Get<bool>("visible"));
            // Sign 的切换仍要能改变列表语义；放在确认后验证，避免把唯一 ROI 变成
            // 原厂禁止建模的“只有屏蔽样本”状态，测试不应弹出模态错误框。
            roi.SignIndex = 1;
            Assert.Equal(1, roi.SignIndex);
            // 渲染用的 Sign 是 bool?（原厂 IROISign），必须跟着列表下标同步。
            Assert.False(roi.Sign);

            // 清空和撤销/重做作用于同一份 ROI 文档历史。
            data.ClearCommand.Execute(null);
            Assert.Empty((IList)data.ItemsData);
            data.BackStepCommand.Execute(null);
            Assert.Single((IList)data.ItemsData);
            data.ForwardStepCommand.Execute(null);
            Assert.Empty((IList)data.ItemsData);

            data.IsUsePen = true;
            data.IsUsePenCommand.Execute(null);
            Assert.True(data.IsUsePen);
            Assert.Empty((IList)data.ItemsData);
        });
    }
}
