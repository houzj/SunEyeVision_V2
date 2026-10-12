using System.Diagnostics;
using System.Globalization;
using System.Text;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.UI.Controls.Communication;

namespace SunEyeVision.Tool.ProtocolAssemble;

[Tool("ProtocolAssemble", "协议组装", Icon = "assemble", Order = 30, Category = SunEyeVision.Plugin.SDK.Core.ToolCategories.Communication, Description = "按配置顺序和分隔符组装文本，不发送设备", HasDebugWindow = true)]
public sealed class ProtocolAssembleTool : IToolPlugin<ProtocolAssembleParameters, CommunicationToolResults>, IToolEditorFactory
{
    IReadOnlyList<OutputVariable> IToolPlugin.OutputVariables => new CommunicationToolResults(ProtocolAssembleParameters.OutputFields).GetOutputVariables();
    public System.Windows.FrameworkElement CreateDebugControl(IToolPlugin tool) => new Views.ProtocolAssembleEditor();
    public System.Windows.FrameworkElement CreateDataAnalysisControl() => new CommunicationDataAnalysisControl();

    public CommunicationToolResults Run(ProtocolAssembleParameters parameters)
    {
        var result = new CommunicationToolResults(ProtocolAssembleParameters.OutputFields) { ToolId = "ProtocolAssemble", ToolName = "协议组装" };
        var watch = Stopwatch.StartNew();
        var rows = new List<CommunicationAnalysisRow>();
        try
        {
            var parts = parameters.Parts?.Select(p => (p.PartId, p.InputText)).ToArray()
                ?? throw new FormatException("组装列表不存在。");
            var separator = parameters.Separator ?? throw new FormatException("分隔符不能为null。");
            if (parts.Length == 0) throw new FormatException("请至少配置一项组装内容。");
            if (parts.Any(p => p.PartId == Guid.Empty) || parts.Select(p => p.PartId).Distinct().Count() != parts.Length)
                throw new FormatException("组装项身份无效或重复。");
            var textParts = new List<string>();
            foreach (var part in parts)
            {
                parameters.Context?.CancellationToken.ThrowIfCancellationRequested();
                var value = ProtocolBindingInput.Resolve(part.InputText, parameters.NodeResults, parameters.Context?.GlobalVariables);
                var text = Format(value);
                textParts.Add(text);
                rows.Add(new(part.PartId, $"第{textParts.Count}项", part.InputText, value.GetType().Name, text, true));
            }
            var textResult = string.Join(separator, textParts);
            var bytes = new BinaryPayload(new UTF8Encoding(false, true).GetBytes(textResult));
            parameters.Context?.CancellationToken.ThrowIfCancellationRequested();
            result.PublishOutputs(new Dictionary<Guid, object?>
            {
                [ProtocolAssembleParameters.TextOutput.FieldId] = textResult,
                [ProtocolAssembleParameters.BytesOutput.FieldId] = bytes
            });
            result.SetSuccess(watch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        {
            result.ClearOutputs(); result.Status = ExecutionStatus.Cancelled; result.ErrorMessage = "组装已取消。";
        }
        catch (Exception ex)
        {
            result.ClearOutputs(); result.SetError(ex.Message);
            rows.Add(new(null, "组装失败", "", "", ex.Message, false));
        }
        result.ExecutionTimeMs = watch.Elapsed.TotalMilliseconds;
        result.SetAnalysis(rows);
        return result;
    }

    private static string Format(object value) => value switch
    {
        string text => text,
        long number => number.ToString(CultureInfo.InvariantCulture),
        int number => number.ToString(CultureInfo.InvariantCulture),
        double number when double.IsFinite(number) => number.ToString("R", CultureInfo.InvariantCulture),
        float number when float.IsFinite(number) => number.ToString("R", CultureInfo.InvariantCulture),
        _ => throw new FormatException($"不支持自动文本组装类型：{value.GetType().Name}。")
    };
}
