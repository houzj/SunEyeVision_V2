using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.UI.Services.Editing;

/// <summary>Explicit CLR slots on a private node draft, using the existing semantic value rules.</summary>
public sealed class EditorDraftAdapter
{
    private readonly ToolParameters _parameters;
    private Action? _validateConfiguration;
    private readonly (Port Port, PropertyInfo Property)[] _slots;
    public EditorDraftAdapter(ToolParameters parameters, IReadOnlyDictionary<string, Type> slots)
    {
        _parameters = parameters ?? throw new ArgumentNullException(nameof(parameters));
        _slots = slots.Select(slot =>
        {
            var property = parameters.GetType().GetProperty(slot.Key, BindingFlags.Instance | BindingFlags.Public);
            if (property is null || property.PropertyType != slot.Value || !property.CanRead || !property.CanWrite)
                throw new ArgumentException($"参数槽 {slot.Key} 与明确的 CLR 声明不一致。");
            return (BindingClrAdapter.DescribePort(slot.Key, slot.Value), property);
        }).ToArray();
    }
    public void Validate()
    {
        var values = _slots.ToDictionary(slot => slot.Port.Id,
            slot => (SunEyeVision.Plugin.Contracts.ParameterSetting)new ConstantSetting(BindingClrAdapter.NormalizeValue(slot.Port, slot.Property.GetValue(_parameters))), StringComparer.Ordinal);
        var session = new ParameterEditSession(_slots.Select(slot => slot.Port), values);
        if (!session.TryCommit()) throw new InvalidOperationException("参数草稿不符合声明。");
        _validateConfiguration?.Invoke();
    }
    // InputText is intentionally absent: its existing binding descriptor accepts String/Binary.
    // Outputs are validated by the existing output contract and binding graph, never a fabricated union.
    public static EditorDraftAdapter ForProtocolParse(ToolParameters parameters)
    {
        if (parameters.GetType().FullName != "SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters")
            throw new ArgumentException("此适配器仅接受 ProtocolParse 的明确参数声明。");
        return new(parameters, new Dictionary<string, Type>(StringComparer.Ordinal)
        { ["Mode"] = typeof(string), ["Separator"] = typeof(string), ["InputFormat"] = typeof(string),
          ["StringEncoding"] = typeof(string), ["AsciiData"] = typeof(bool) });
    }
    internal static bool Supports(ToolParameters? parameters) => parameters?.GetType().FullName is
        "SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters" or "SunEyeVision.Tool.ImageThreshold.ImageThresholdParameters" or
        "SunEyeVision.Tool.BlobAnalysis.BlobAnalysisParameters";

    public static EditorDraftAdapter ForProduction(ToolParameters parameters) => parameters.GetType().FullName switch
    {
        "SunEyeVision.Tool.BlobAnalysis.BlobAnalysisParameters" => ForBlob(parameters),
        "SunEyeVision.Tool.ProtocolParse.ProtocolParseParameters" => ForProtocolParse(parameters),
        "SunEyeVision.Tool.ImageThreshold.ImageThresholdParameters" => ForImageThreshold(parameters),
        _ => throw new ArgumentException("此工具尚未接入生产草稿适配器。", nameof(parameters))
    };

    private static EditorDraftAdapter ForBlob(ToolParameters parameters)
    {
        // The plugin owns its input declarations and configuration validation. Do not duplicate
        // the 117-field contract in the UI or add a UI-to-plugin assembly dependency.
        var adapter = new EditorDraftAdapter(parameters, new Dictionary<string, Type>());
        adapter._validateConfiguration = () =>
        {
            var result = parameters.Validate();
            if (!result.IsValid)
                throw new InvalidOperationException(result.Errors.FirstOrDefault() ?? "Blob配置无效。");
        };
        return adapter;
    }

    private static EditorDraftAdapter ForImageThreshold(ToolParameters parameters)
    {
        // Only this known enum member is read; no fabricated integer port or general object traversal.
        var method = parameters.GetType().GetProperty("Method", BindingFlags.Public | BindingFlags.Instance);
        if (method is null || !method.PropertyType.IsEnum ||
            method.PropertyType.FullName != "SunEyeVision.Tool.ImageThreshold.ThresholdMethod")
            throw new ArgumentException("ImageThreshold.Method 与明确枚举声明不一致。");
        var adapter = new EditorDraftAdapter(parameters, new Dictionary<string, Type>(StringComparer.Ordinal)
        {
            ["Threshold"] = typeof(int), ["MaxValue"] = typeof(int), ["Invert"] = typeof(bool),
            ["BlockSize"] = typeof(int), ["C"] = typeof(double),
            ["MinThreshold"] = typeof(int), ["MaxThreshold"] = typeof(int)
        });
        adapter._validateConfiguration = () =>
        {
            var value = method.GetValue(parameters);
            if (value is null || !Enum.IsDefined(method.PropertyType, value))
                throw new InvalidOperationException("二值化方法不是已声明的枚举值。");
            // RoiParameters/MeasureGuideParameters add no image-readiness validation.
            // This existing override checks configuration, including cross-field/result rules.
            var presentation = parameters.GetType().GetProperty("ResultPresentation", BindingFlags.Public | BindingFlags.Instance)?.GetValue(parameters) as ResultPresentationConfig;
            if (presentation is not null && presentation.ValidateContract() is { Length: > 0 } issues)
                throw new InvalidOperationException(issues[0]);
            var result = parameters.Validate();
            if (!result.IsValid) throw new InvalidOperationException(result.Errors.FirstOrDefault() ?? "二值化配置无效。");
        };
        return adapter;
    }
}
