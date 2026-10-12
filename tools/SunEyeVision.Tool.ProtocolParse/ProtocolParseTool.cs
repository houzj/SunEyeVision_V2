using System.Diagnostics;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.UI.Controls.Communication;

namespace SunEyeVision.Tool.ProtocolParse;

[Tool("ProtocolParse", "协议解析", Icon = "parse", Order = 20, Category = ToolCategories.Communication,
    Description = "按配置字段解析文本或字节，不读取设备", HasDebugWindow = true)]
public sealed class ProtocolParseTool : IToolPlugin<ProtocolParseParameters, CommunicationToolResults>, IToolEditorFactory
{
    public System.Windows.FrameworkElement CreateDebugControl(IToolPlugin tool) => new Views.ProtocolParseEditor();
    public System.Windows.FrameworkElement CreateDataAnalysisControl() => new CommunicationDataAnalysisControl();
    public CommunicationToolResults Run(ProtocolParseParameters parameters)
    {
        var result = new CommunicationToolResults();
        var rows = new List<CommunicationAnalysisRow>();
        var watch = Stopwatch.StartNew();
        try
        {
            var fields = parameters.Outputs.Select(f => new Field(f.FieldId, f.Name, f.DataType,
                f.Start, f.End, f.Order, f.NumericInterpretation)).ToArray();
            result = new CommunicationToolResults(fields.Select(f => new CommunicationOutputField(f.Id,
                f.Name, ProtocolParseField.Vocabulary(f.Type))));
            if (fields.Length == 0) throw new FormatException("请至少配置一个输出字段。");
            var mode = parameters.Mode; var format = parameters.InputFormat; var ascii = parameters.AsciiData;
            var separator = parameters.Separator; var encoding = parameters.StringEncoding;
            var source = ProtocolBindingInput.Resolve(parameters.InputText, parameters.NodeResults, parameters.Context?.GlobalVariables);
            var values = new Dictionary<Guid, object?>();
            if (mode == "Text")
            {
                var text = source switch
                {
                    string s => s,
                    BinaryPayload b => ProtocolValueCodec.EncodingFor("UTF8").GetString(b.ToArray()),
                    _ => throw new FormatException("文本解析输入必须为字符串或字节序列。")
                };
                if (separator == null) throw new FormatException("分隔符不能为null。");
                var pieces = separator.Length == 0 ? new[] { text } : text.Split(separator, StringSplitOptions.None);
                if (pieces.Length < fields.Length) throw new FormatException($"片段不足：需要{fields.Length}项，实际{pieces.Length}项。");
                for (var i = 0; i < fields.Length; i++)
                {
                    parameters.Context?.CancellationToken.ThrowIfCancellationRequested();
                    values[fields[i].Id] = WorkflowValue(ProtocolValueCodec.ConvertText(pieces[i], fields[i].Type));
                    rows.Add(new(fields[i].Id, fields[i].Name, pieces[i], fields[i].Type, $"文本第{i + 1}段", true));
                }
                if (pieces.Length > fields.Length) rows.Add(new(null, "未配置片段", "", "不生成输出", $"剩余{pieces.Length - fields.Length}段", true));
            }
            else if (mode == "Bytes")
            {
                var bytes = source switch
                {
                    BinaryPayload b => b.ToArray(),
                    string s when format == "Hex" => ProtocolValueCodec.ParseHex(s),
                    string s when format == "Text" => ProtocolValueCodec.EncodingFor(ascii ? "ASCII" : encoding).GetBytes(s),
                    _ => throw new FormatException("字节输入需要字节序列，或明确格式的字符串。")
                };
                foreach (var field in fields)
                {
                    parameters.Context?.CancellationToken.ThrowIfCancellationRequested();
                    var slice = ProtocolValueCodec.Slice(bytes, field.Start, field.End);
                    values[field.Id] = WorkflowValue(ProtocolValueCodec.Decode(bytes, field.Start, field.End,
                        field.Type, field.Order, field.NumericInterpretation, encoding, ascii));
                    rows.Add(new(field.Id, field.Name, Convert.ToHexString(slice), $"[{field.Start},{field.End}] {field.Type}",
                        ascii ? "ASCII字符转换（原顺序）" : field.Type == "string" ? "字符解码（原顺序）"
                        : field.Order + " " + field.NumericInterpretation, true));
                }
            }
            else throw new FormatException("仅支持文本解析和字节解析。");
            parameters.Context?.CancellationToken.ThrowIfCancellationRequested();
            result.PublishOutputs(values); result.SetSuccess(watch.Elapsed.TotalMilliseconds);
        }
        catch (OperationCanceledException)
        { result.ClearOutputs(); result.Status = ExecutionStatus.Cancelled; result.ErrorMessage = "解析已取消。"; }
        catch (Exception ex)
        { result.ClearOutputs(); result.SetError(ex.Message); rows.Add(new(null, "解析失败", "", "", ex.Message, false)); }
        result.ToolId = "ProtocolParse"; result.ToolName = "协议解析";
        result.ExecutionTimeMs = watch.Elapsed.TotalMilliseconds; result.SetAnalysis(rows);
        return result;
    }
    private static object WorkflowValue(object value) => value switch { int n => (long)n, float f => (double)f, _ => value };
    private sealed record Field(Guid Id, string Name, string Type, int Start, int End, string Order, string NumericInterpretation);
}
