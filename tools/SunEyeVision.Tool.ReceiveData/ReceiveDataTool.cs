using System.Diagnostics;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.UI.Controls.Communication;

namespace SunEyeVision.Tool.ReceiveData;

[Tool("ReceiveData", "接收数据", Icon = "receive", Order = 10, Category = ToolCategories.Communication,
    Description = "执行时读取设备当前值或全局变量", HasDebugWindow = true)]
public sealed class ReceiveDataTool : IToolPlugin<ReceiveDataParameters, CommunicationToolResults>, IAsyncToolPlugin, IToolEditorFactory
{
    public System.Windows.FrameworkElement CreateDebugControl(IToolPlugin tool) => new Views.ReceiveDataEditor();
    public System.Windows.FrameworkElement CreateDataAnalysisControl() => new CommunicationDataAnalysisControl();
    public CommunicationToolResults Run(ReceiveDataParameters p) => ReadAsync(p, p.Context?.CommunicationInputs,
        p.Context?.CancellationToken ?? default, p.NodeResults).GetAwaiter().GetResult();
    public async ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request) =>
        await ReadAsync((ReceiveDataParameters)request.Parameters, request.Context?.CommunicationInputs ?? request.CommunicationInputs, request.CancellationToken, request.NodeResults).ConfigureAwait(false);
    private static async Task<CommunicationToolResults> ReadAsync(ReceiveDataParameters p, ICommunicationReceiveService? service, CancellationToken token,
        IDictionary<string, ToolResults>? nodeResults)
    {
        var result = new CommunicationToolResults(); var watch = Stopwatch.StartNew(); var analysis = new List<CommunicationAnalysisRow>();
        try
        {
            var fields = p.Definitions(); result = new(fields);
            token.ThrowIfCancellationRequested();
            if (service == null) throw new InvalidOperationException("宿主未提供当前值读取服务。");
            var values = new Dictionary<Guid, object?>();
            if (p.SourceKind == "Device")
            {
                var source = service.GetSources().FirstOrDefault(s => s.DeviceId == p.DeviceId && s.TargetId == p.TargetId);
                var sample = await service.ReadAsync(p.DeviceId, p.TargetId, token).ConfigureAwait(false);
                var sourceName = !string.IsNullOrWhiteSpace(source?.DeviceName)
                    ? source.DeviceName + (string.IsNullOrWhiteSpace(source.TargetName) ? "" : " / " + source.TargetName)
                    : !string.IsNullOrWhiteSpace(source?.DisplayName) ? source.DisplayName
                    : string.IsNullOrWhiteSpace(p.TargetId) ? "通信设备" : "通信设备 / 数据点";
                Add(fields[0], sample.Value, sourceName, $"数据时间 {sample.Timestamp:yyyy-MM-dd HH:mm:ss.fff zzz}");
            }
            else if (p.SourceKind == "Variable")
            {
                if (p.Variables.Count == 0 || p.Variables.Any(v => string.IsNullOrWhiteSpace(v.VariableId))) throw new InvalidOperationException("请选择每行对应的变量。");
                var current = new RuntimeVariableReceiveService(service, null, nodeResults, (service as RuntimeVariableReceiveService)?.WorkflowId)
                    .ReadVariables(p.Variables.Select(v => v.VariableId).ToArray());
                foreach (var row in p.Variables)
                {
                    if (!current.TryGetValue(row.VariableId, out var value)) throw new InvalidOperationException("所选变量已失效。");
                    var sourceName = "全局 / " + (string.IsNullOrWhiteSpace(value.Source.Group) ? "" : value.Source.Group + " / ") + value.Source.Name;
                    Add(row.Definition(), value.Value, sourceName, "读取时间 " + DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss.fff zzz"));
                }
            }
            else throw new NotSupportedException("队列接收尚未实现。");
            token.ThrowIfCancellationRequested(); result.PublishOutputs(values); result.SetSuccess(watch.Elapsed.TotalMilliseconds);
            void Add(CommunicationOutputField field, object value, string sourceName, string time)
            {
                value = CommunicationReceiveService.Normalize(value);
                if (CommunicationReceiveService.ValueType(value) != field.VariableType) throw new InvalidOperationException("来源类型已变化，请重新确认绑定。");
                var displayValue = value is BinaryPayload binary ? "HEX " + binary.ToString()
                    : Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
                values.Add(field.FieldId, value);
                analysis.Add(new(field.FieldId, field.Name, sourceName, field.VariableType, $"当前值：{displayValue}；{time}", true));
            }
        }
        catch (OperationCanceledException) { result.ClearOutputs(); result.Status = ExecutionStatus.Cancelled; result.ErrorMessage = "接收已取消。"; }
        catch (Exception e) { result.ClearOutputs(); result.SetError(e.Message); analysis.Add(new(null, "接收失败", "", "", e.Message, false)); }
        result.ToolId = "ReceiveData"; result.ToolName = "接收数据"; result.ExecutionTimeMs = watch.Elapsed.TotalMilliseconds; result.SetAnalysis(analysis); return result;
    }
}
