using System.Diagnostics;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.UI.Controls.Communication;

namespace SunEyeVision.Tool.SendData;

[Tool("SendData", "发送数据", Icon = "send", Order = 40, Category = ToolCategories.Communication,
    Description = "将本次工作流数据提交到设备、变量或发送事件", HasDebugWindow = true)]
public sealed class SendDataTool : IToolPlugin<SendDataParameters, SendDataResults>, IAsyncToolPlugin, IToolEditorFactory, IToolExecutionProfileProvider
{
    public System.Windows.FrameworkElement CreateDebugControl(IToolPlugin tool) => new Views.SendDataEditor();
    public System.Windows.FrameworkElement CreateDataAnalysisControl() => new CommunicationDataAnalysisControl();
    public ToolExecutionProfile DescribeExecution(ToolParameters parameters) => ToolExecutionProfile.LegacyExclusive;
    public SendDataResults Run(SendDataParameters p) => SendAsync(p, p.Context?.CommunicationOutputs, p.Context?.NodeId,
        p.NodeResults, p.Context?.GlobalVariables, p.Context?.CancellationToken ?? default).GetAwaiter().GetResult();
    public async ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request) =>
        await SendAsync((SendDataParameters)request.Parameters, request.Context.CommunicationOutputs ?? request.CommunicationOutputs,
            request.NodeId, request.NodeResults, request.GlobalVariables, request.CancellationToken).ConfigureAwait(false);

    private static async Task<SendDataResults> SendAsync(SendDataParameters p, ICommunicationSendService? service, string? nodeId,
        IDictionary<string, ToolResults>? results, IDictionary<string, object?>? globals, CancellationToken token)
    {
        var result = new SendDataResults(); var watch = Stopwatch.StartNew(); var analysis = new List<CommunicationAnalysisRow>();
        var submittedValues = new Dictionary<Guid, object?>();
        try
        {
            result = new(p.VariableFields());
            if (service == null) throw new InvalidOperationException("宿主未提供发送服务。");
            if (p.Rows.Count == 0 || p.Rows.Any(r => r.FieldId == Guid.Empty) || p.Rows.Select(r => r.FieldId).Distinct().Count() != p.Rows.Count)
                throw new FormatException("发送列表为空或字段身份重复。");
            var targetCatalog = service.GetTargets();
            var raw = p.DestinationKind == "Device" && targetCatalog.Any(t => t.Kind == "Device" && t.DeviceId == p.DeviceId && t.Protocol == "Raw");
            var atomic = raw || p.DestinationKind is "Event" or "Variable";
            if (p.DestinationKind == "Variable" && p.Rows.Select(r => r.TargetId).Distinct().Count() != p.Rows.Count)
                throw new FormatException("同一变量不能重复提交。");
            var groups = raw || p.DestinationKind == "Event" ? new[] { p.Rows.ToArray() } : p.Rows.Select(r => new[] { r }).ToArray();
            var prepared = new List<(SendDataRow[] Rows, PreparedCommunicationSend Plan)>();
            foreach (var rows in groups)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    if (p.DestinationKind == "Device" && !raw && string.IsNullOrWhiteSpace(rows[0].TargetId))
                        throw new FormatException("请选择发送目标。");
                    var inputs = rows.Select(r => new CommunicationSendInput(p.DestinationKind == "Event" ? r.EventFieldId : r.FieldId,
                        r.TargetId, ResolveInput(r, p.DestinationKind, results, globals), r.DataType)).ToArray();
                    var options = p.DestinationKind == "Device" ? new CommunicationSendOptions(p.Hex, p.Separator, p.Terminator) : new();
                    var plan = await service.PrepareAsync(new(p.DestinationKind, p.DeviceId, p.EventId, inputs, options, nodeId), token).ConfigureAwait(false);
                    prepared.Add((rows, plan));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    var label = targetCatalog.FirstOrDefault(t => t.Kind == p.DestinationKind && t.Id == rows[0].TargetId && (p.DestinationKind != "Device" || t.DeviceId == p.DeviceId))?.Name ?? rows[0].Name;
                    var message = $"发送项 {label}（发送类型 {rows[0].DataType}）：{e.Message}";
                    analysis.Add(new(rows[0].FieldId, rows[0].Name, rows[0].InputText, "预检失败，未提交", message, false));
                    if (!p.IgnoreErrors || atomic) { result.Failed++; throw new FormatException(message, e); }
                    result.Skipped++;
                }
            }
            if (prepared.Count == 0) throw new InvalidOperationException("没有有效发送单元：" + string.Join("；", analysis.Select(a => a.Detail)));
            foreach (var (rows, plan) in prepared)
            {
                token.ThrowIfCancellationRequested();
                try
                {
                    var count = await plan.SubmitAsync(token).ConfigureAwait(false);
                    if (count <= 0) throw new InvalidOperationException("没有实际提交目标。");
                    result.Submitted++;
                    result.SentData.Add(plan.Name + ": " + (plan.Value is BinaryPayload sentBinary
                        ? sentBinary.ToString() : Convert.ToString(plan.Value, System.Globalization.CultureInfo.InvariantCulture) ?? ""));
                    if (plan.Value is BinaryPayload binary) result.Payloads.Add(binary);
                    if (p.DestinationKind == "Variable") submittedValues.Add(rows[0].FieldId, plan.Value);
                    analysis.Add(new(rows[0].FieldId, rows[0].Name, plan.Name, "提交完成", plan.Value is BinaryPayload b ? b.ToString() : Convert.ToString(plan.Value, System.Globalization.CultureInfo.InvariantCulture) ?? "", true));
                }
                catch (OperationCanceledException) { throw; }
                catch (Exception e)
                {
                    result.Failed++; analysis.Add(new(rows[0].FieldId, rows[0].Name, plan.Name, "提交失败，可能已产生副作用；不自动重试", e.Message, false));
                    if (!p.IgnoreErrors || atomic) throw;
                }
            }
            if (result.Submitted == 0) throw new InvalidOperationException("所有发送单元均未成功。");
            if (p.DestinationKind == "Variable") result.PublishOutputs(submittedValues);
            result.SetSuccess(watch.Elapsed.TotalMilliseconds);
            if (result.Skipped > 0 || result.Failed > 0) { result.Status = ExecutionStatus.PartialSuccess; result.ErrorMessage = "部分单元未提交成功。"; }
        }
        catch (OperationCanceledException)
        { result.ClearOutputs(); result.Status = ExecutionStatus.Cancelled; result.ErrorMessage = "发送取消；已提交内容不能撤回，未继续后续提交。"; }
        catch (Exception e)
        { result.ClearOutputs(); result.SetError(e.Message); analysis.Add(new(null, "发送失败", "", "", e.Message, false)); }
        result.ToolId = "SendData"; result.ToolName = "发送数据"; result.ExecutionTimeMs = watch.Elapsed.TotalMilliseconds;
        result.SetAnalysis(analysis); return result;
    }

    private static object ResolveInput(SendDataRow row, string kind, IDictionary<string, ToolResults>? results, IDictionary<string, object?>? globals)
    {
        var value = ProtocolBindingInput.Resolve(row.InputText, results, globals);
        var dot = row.InputText.IndexOf('.');
        var bound = row.InputText.StartsWith("__GlobalVars__.", StringComparison.Ordinal) || dot > 0 && Guid.TryParse(row.InputText[..dot], out _);
        if (bound) return CommunicationSendTypes.Validate(value, row.DataType);
        var culture = System.Globalization.CultureInfo.InvariantCulture;
        var parsed = row.DataType.ToLowerInvariant() switch
        {
            "string" => value,
            "integer" or "int" or "int16" or "int32" or "int64" or "uint16" or "uint32" when long.TryParse(row.InputText, System.Globalization.NumberStyles.Integer, culture, out var n) => n,
            "float" or "float32" or "double" when double.TryParse(row.InputText, System.Globalization.NumberStyles.Float, culture, out var n) && double.IsFinite(n) => n,
            "bool" or "boolean" when bool.TryParse(row.InputText, out var b) => b,
            "binary" => new BinaryPayload(ProtocolValueCodec.ParseHex(row.InputText)),
            _ => throw new FormatException("常量与目标声明类型不兼容；字节载荷请绑定上游Binary输出。")
        };
        return CommunicationSendTypes.Validate(parsed, row.DataType);
    }
}
