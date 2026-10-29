using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.Json;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>Shared runtime input preparation for workflow, debug, sync and async entry points.</summary>
public static class ToolExecutionPreparation
{
    private static readonly JsonSerializerOptions CopyOptions = new() { IncludeFields = true };

    public static ToolParameters CopyParameters(ToolParameters source, JsonSerializerOptions? options = null)
    {
        ArgumentNullException.ThrowIfNull(source);
        var type = source.GetType();
        var json = JsonSerializer.Serialize(source, type, options ?? CopyOptions);
        return (ToolParameters)(JsonSerializer.Deserialize(json, type, options ?? CopyOptions)
            ?? throw new InvalidOperationException($"无法创建工具参数快照：{type.Name}"));
    }

    public static ImageSourceReference? ReadReference(ToolParameters parameters, string sourceProperty)
    {
        var type = parameters.GetType();
        if (type.GetProperty(sourceProperty)?.GetValue(parameters) is AvailableDataSource source)
            return ImageSourceReference.FromDataSource(source);
        var names = sourceProperty.EndsWith("BindingSource", StringComparison.Ordinal)
            ? new[] { sourceProperty[..^"BindingSource".Length] + "Source", sourceProperty[..^"BindingSource".Length] + "Ref" }
            : new[] { sourceProperty + "Ref", sourceProperty + "Reference", sourceProperty + "Source" };
        return names.Select(name => type.GetProperty(name)?.GetValue(parameters)).OfType<ImageSourceReference>().FirstOrDefault();
    }

    public static IVisionImage? ResolveInputImage(ToolParameters parameters,
        IDictionary<string, ToolResults> results, IVisionImage? fallback)
    {
        var reference = ReadReference(parameters, "ImageSource");
        if (reference == null) return fallback;
        if (!results.TryGetValue(reference.SourceNodeId, out var result) || !result.IsSuccess)
            throw new InvalidOperationException($"输入图像来源 {reference.SourceNodeId} 尚无有效结果，请先执行上游节点。");
        var image = OutputVariableResolver.Resolve(result, reference.PropertyName, reference.SourceNodeId) as IVisionImage;
        if (image == null || image.IsEmpty) throw new InvalidOperationException("绑定的输入图像为空或无效。");
        return image;
    }

    public static bool RequiresConformalCorrection(ToolParameters parameters) =>
        parameters is MeasureGuideParameters guide && (guide.HasMeasureGuide || parameters is not RoiParameters);

    /// <summary>Only call on a runtime parameter copy. Never writes values into shared candidate objects.</summary>
    public static void Apply(ToolParameters parameters, IDictionary<string, ToolResults> results, bool attachNodeResults = true)
    {
        if (attachNodeResults) parameters.NodeResults = results;
        parameters.ResolvedInputImage = ResolveInputImage(parameters, results, parameters.ResolvedInputImage);
        parameters.ResolvedPositionCorrection = null;
        var correction = PositionCorrectionResolution.Capture(parameters,
            requireConformal: RequiresConformalCorrection(parameters));
        if (!correction.CanUse) throw new InvalidOperationException(correction.Message);
        parameters.ResolvedPositionCorrection = correction;

        foreach (var property in parameters.GetType().GetProperties(BindingFlags.Public | BindingFlags.Instance)
                     .Where(p => p.CanRead && p.CanWrite && p.PropertyType == typeof(AvailableDataSource)))
        {
            var reference = ReadReference(parameters, property.Name);
            if (reference == null) continue;
            object? value = null;
            if (property.Name == "PositionCorrectionBindingSource") value = correction.Matrix;
            else
            {
                if (!results.TryGetValue(reference.SourceNodeId, out var result) || !result.IsSuccess)
                    throw new InvalidOperationException($"绑定来源 {reference.SourceNodeId}.{reference.PropertyName} 尚无有效结果。");
                value = OutputVariableResolver.Resolve(result, reference.PropertyName, reference.SourceNodeId);
            }
            var source = (property.GetValue(parameters) as AvailableDataSource)?.WithRuntimeValue(value)
                ?? new AvailableDataSource
                {
                    SourceNodeId = reference.SourceNodeId, PropertyName = reference.PropertyName,
                    SourceWorkflowId = reference.SourceWorkflowId, SourceFieldId = reference.SourceFieldId,
                    AutomaticConnectionId = reference.AutomaticConnectionId,
                    SourceNodeName = reference.SourceNodeName, DisplayName = reference.DisplayName, CurrentValue = value
                };
            // Source equality compares paths; reset only this runtime copy so its setter accepts the new value.
            property.SetValue(parameters, null);
            property.SetValue(parameters, source);
        }

        foreach (var style in parameters.GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                     .Select(p => p.GetValue(parameters)).OfType<TextStyle>())
            style.ResolveBindings(results);
        foreach (var presentation in parameters.GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0)
                     .Select(p => p.GetValue(parameters)).OfType<ResultPresentationConfig>())
            foreach (var item in presentation.Texts) item.Style.ResolveBindings(results);
    }
}
