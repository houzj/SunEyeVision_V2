using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>Model-only adapters. The same descriptors feed validation, candidates and dependency compilation.</summary>
public static class BindingInputCatalog
{
    public static IReadOnlyList<BindingInputReference> Describe(ToolParameters parameters)
    {
        var inputs = new List<BindingInputReference>();
        if (parameters is IBindingInputProvider provider) inputs.AddRange(provider.GetBindingInputs());
        foreach (var property in parameters.GetType().GetProperties().Where(p => p.CanRead && p.GetIndexParameters().Length == 0))
        {
            // Persisted image references must also work before any editor has restored runtime source objects.
            if (property.Name is "ImageSource" or "PositionCorrectionBindingSource")
            {
                var correction = property.Name != "ImageSource";
                var reference = parameters.GetType().GetProperty(correction ? "PositionCorrectionSource" : "ImageSourceRef");
                var originalSource = property.GetValue(parameters);
                var originalReference = reference?.GetValue(parameters);
                string Read() => property.GetValue(parameters) is AvailableDataSource ds ? ds.GetBindingPath()
                    : reference?.GetValue(parameters) is ImageSourceReference r ? r.SourceNodeId + "." + r.PropertyName : "";
                void Write(string text)
                {
                    if (text.Length != 0)
                    {
                        property.SetValue(parameters, RewriteSource(originalSource as AvailableDataSource, text));
                        if (reference != null)
                        {
                            var dot = text.IndexOf('.'); var prior = originalReference as ImageSourceReference;
                            reference.SetValue(parameters, new ImageSourceReference(text[..dot], text[(dot + 1)..], prior?.SourceNodeName ?? "", prior?.DisplayName ?? "")
                                { SourceWorkflowId = prior?.SourceWorkflowId, SourceFieldId = prior?.SourceFieldId, AutomaticConnectionId = prior?.AutomaticConnectionId });
                        }
                        return;
                    }
                    property.SetValue(parameters, null); reference?.SetValue(parameters, null);
                }
                inputs.Add(new(property.Name, correction ? "位置修正" : "图像来源", Read, Write,
                    // PositionCorrectionResolver accepts one matrix or the first item of a matrix array.
                    correction ? new[] { typeof(AffineMatrix), typeof(AffineMatrix[]) } : new[] { typeof(IVisionImage) }, ReadWorkflowId: () =>
                        (property.GetValue(parameters) as AvailableDataSource)?.SourceWorkflowId ?? (reference?.GetValue(parameters) as ImageSourceReference)?.SourceWorkflowId,
                    ReferenceOnly: true, Active: (!correction || parameters.GetType().GetProperty("IsPositionCorrectionEnabled")?.GetValue(parameters) is true)
                        && (parameters is not IBindingInputActivityProvider activity || activity.IsBindingInputActive(property.Name)),
                    ReadFieldId: () => (property.GetValue(parameters) as AvailableDataSource)?.SourceFieldId ?? (reference?.GetValue(parameters) as ImageSourceReference)?.SourceFieldId));
            }
            else if (typeof(ParamValueBase).IsAssignableFrom(property.PropertyType) && property.GetValue(parameters) is ParamValueBase value)
            {
                var type = property.PropertyType.IsGenericType ? property.PropertyType.GetGenericArguments()[0] : typeof(RoiShape[]);
                AddSetting(property.Name, property.Name, () => value.BindingConfig, s => value.BindingConfig = s, type);
                if (value is RoiCollection rois)
                    foreach (var roi in rois.Rois)
                    {
                        var prefix = property.Name + ":" + roi.Id;
                        AddSetting(prefix, roi.Name, () => roi.Binding, s => roi.Binding = s, typeof(RoiShape[]), roi.IsEnabled && roi.IsSubscribed);
                        foreach (var key in roi.ParameterBindings?.Keys.ToArray() ?? Array.Empty<string>())
                            AddSetting(prefix + ":" + key, roi.Name + "." + key, () => roi.ParameterBindings!.GetValueOrDefault(key),
                                s => { if (s == null) roi.ParameterBindings!.Remove(key); else roi.ParameterBindings![key] = s; }, typeof(double[]), roi.IsEnabled && roi.IsComposed);
                    }
            }
            // Legacy script/subroutine input models share this explicit schema, not arbitrary recursive reflection.
            else if (property.Name == "Inputs" && property.GetValue(parameters) is IEnumerable rows)
                foreach (var row in rows)
                {
                    if (row == null) continue;
                    var rt = row.GetType(); var binding = rt.GetProperty("BindingSource");
                    if (binding?.PropertyType != typeof(string) || !binding.CanWrite) continue;
                    var name = rt.GetProperty("Name")?.GetValue(row)?.ToString() ?? "输入";
                    var id = rt.GetProperty("FieldId")?.GetValue(row)?.ToString() ?? rt.GetProperty("BindingKey")?.GetValue(row)?.ToString() ?? name;
                    var type = rt.GetProperty("ParamType")?.GetValue(row) as Type;
                    var sourceProperty = rt.GetProperty("Source");
                    var originalSource = sourceProperty?.GetValue(row) as AvailableDataSource;
                    inputs.Add(new("Input:" + id, name, () => (sourceProperty?.GetValue(row) as AvailableDataSource)?.GetBindingPath() ?? binding.GetValue(row) as string ?? "", text =>
                    {
                        binding.SetValue(row, text);
                        sourceProperty?.SetValue(row, text.Length == 0 ? null : RewriteSource(originalSource, text));
                        if (text.Length == 0) rt.GetProperty("BindingDisplayText")?.SetValue(row, "");
                    // Subroutine Transform ports carry the resolved value unchanged, including matrix arrays.
                    // Do not apply this transport rule to scalar script parameters or ordinary tool inputs.
                    }, parameters is SubroutineParameters && type == typeof(AffineMatrix)
                        ? new[] { typeof(AffineMatrix), typeof(AffineMatrix[]) }
                        : type == null ? Array.Empty<Type>() : new[] { type }, ReadWorkflowId: () => originalSource?.SourceWorkflowId, ReferenceOnly: true) { Owner = row, MemberPath = "BindingSource" });
                }
        }
        if (parameters is SubroutineParameters subroutine)
        {
            foreach (var row in subroutine.Outputs)
            {
                var original = row.Source;
                inputs.Add(new("Output:" + row.FieldId, row.Name, () => row.Source?.GetBindingPath() ?? row.BindingSource,
                    text => { row.BindingSource = text; row.Source = RewriteSource(original, text); if (text.Length == 0) row.BindingDisplayText = ""; },
                    row.ParamType == null ? Array.Empty<Type>() : new[] { row.ParamType }, ReadWorkflowId: () => subroutine.SubroutineId, ReferenceOnly: true, InternalProjection: true) { Owner = row, MemberPath = nameof(row.BindingSource) });
            }
            foreach (var row in subroutine.DisplayConfigs)
            {
                var original = row.BoundDataSource;
                inputs.Add(new("Display:" + row.FieldId, row.DisplayName, () => row.BoundDataSource?.GetBindingPath() ?? row.BindingSource,
                    text => { row.BindingSource = text; row.BoundDataSource = RewriteSource(original, text); if (text.Length == 0) row.BindingDisplayText = ""; },
                    row.ParamType == null ? Array.Empty<Type>() : new[] { row.ParamType }, ReadWorkflowId: () => subroutine.SubroutineId, ReferenceOnly: true, InternalProjection: true) { Owner = row, MemberPath = nameof(row.BindingSource) });
            }
            var loop = subroutine.LoopConfig;
            foreach (var key in new[] { "StartValueBinding", "EndValueBinding", "SourceCompareBinding", "TargetCompareBinding" })
            {
                var property = loop.GetType().GetProperty(key)!; var inner = key.Contains("Compare");
                var type = inner ? loop.DataType switch { "double" => typeof(double), "string" => typeof(string), _ => typeof(long) } : typeof(long);
                inputs.Add(new("Loop:" + key, key, () => (string)property.GetValue(loop)!, text => { property.SetValue(loop, text); if (text.Length == 0) loop.GetType().GetProperty(key + "DisplayText")?.SetValue(loop, ""); },
                    new[] { type }, ReadWorkflowId: inner ? () => subroutine.SubroutineId : null, ReferenceOnly: true,
                    Active: loop.EnableLoop && (!inner || loop.EnableBreak), InternalProjection: inner));
            }
        }
        return inputs.GroupBy(i => i.Key, StringComparer.Ordinal).Select(g => g.First()).ToArray();

        void AddSetting(string key, string name, Func<ParamSetting?> get, Action<ParamSetting?> set, Type type, bool active = true)
        {
            var original = get();
            inputs.Add(new(key, name, () => get() is { BindingType: BindingType.Binding } s ? s.SourceNodeId + "." + s.SourceProperty : "",
                text => { if (text.Length == 0) set(null); else { var s = get() ?? original!; var dot = text.IndexOf('.'); s.SourceNodeId = text[..dot]; s.SourceProperty = text[(dot + 1)..]; set(s); } },
                type == typeof(double[]) ? new[] { type, typeof(long[]) } : new[] { type }, ReadWorkflowId: () => get()?.SourceWorkflowId, ReferenceOnly: true, Active: active));
        }
    }

    private static AvailableDataSource? RewriteSource(AvailableDataSource? original, string expression)
    {
        if (original == null || expression.Length == 0) return null;
        var dot = expression.IndexOf('.');
        var path = expression[(dot + 1)..]; var root = PropertyPathParser.ExtractPropertyName(path.Split('.')[0]);
        return new AvailableDataSource { SourceNodeId = expression[..dot], PropertyName = root,
            SourceWorkflowId = original.SourceWorkflowId, SourceFieldId = original.SourceFieldId, SourceNodeName = original.SourceNodeName,
            DisplayName = original.DisplayName, PropertyType = original.PropertyType, VariableType = original.VariableType,
            AutomaticConnectionId = original.AutomaticConnectionId, BindingSuffix = path[root.Length..] };
    }

    public static bool TryReference(string expression, IEnumerable<string> localNodeIds, bool referenceOnly, out string sourceId, out string path)
    {
        var dot = expression.IndexOf('.'); sourceId = dot > 0 ? expression[..dot] : ""; path = dot > 0 ? expression[(dot + 1)..] : "";
        return dot > 0 && (referenceOnly || sourceId.StartsWith("__", StringComparison.Ordinal) || Guid.TryParse(sourceId, out _) || localNodeIds.Contains(sourceId, StringComparer.Ordinal));
    }

    public static bool IsInputActive(ToolParameters parameters, string key)
    {
        if (parameters is IBindingInputActivityProvider activity && !activity.IsBindingInputActive(key)) return false;
        var descriptor = Describe(parameters).FirstOrDefault(i => i.Key == key);
        if (descriptor == null && key == "位置修正") descriptor = Describe(parameters).FirstOrDefault(i => i.Key == "PositionCorrectionBindingSource");
        if (descriptor != null) return descriptor.Active;
        foreach (var property in parameters.GetType().GetProperties().Where(p => typeof(RoiCollection).IsAssignableFrom(p.PropertyType)))
            if (key.StartsWith(property.Name + ":", StringComparison.Ordinal) && property.GetValue(parameters) is RoiCollection rois)
            {
                var roi = rois.Rois.FirstOrDefault(r => key == property.Name + ":" + r.Id || key.StartsWith(property.Name + ":" + r.Id + ":", StringComparison.Ordinal));
                return roi != null && roi.IsEnabled && (key == property.Name + ":" + roi.Id ? roi.IsSubscribed : roi.IsComposed);
            }
        return !new[] { "Input:", "Row:", "Part:", "Variable:", "Output:", "Display:", "Loop:" }.Any(prefix => key.StartsWith(prefix, StringComparison.Ordinal));
    }
}
