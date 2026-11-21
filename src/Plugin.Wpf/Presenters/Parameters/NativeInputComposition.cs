using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Fields;

namespace SunEyeVision.Plugin.Wpf.Presenters.Parameters;

/// <summary>Controls how formal tool inputs enter the native parameter page.</summary>
public enum InputCompositionMode
{
    /// <summary>Keep the existing nested input list while tools are migrated.</summary>
    NestedLegacy,
    /// <summary>Place formal input rows into the owning parameter page.</summary>
    FlatNative
}

/// <summary>One formal input row that can be transferred to a flat parameter page.</summary>
public sealed record NativeInputField(
    string FieldId,
    string Section,
    int Order,
    Port Port,
    FieldPresentation Presentation,
    DeclaredFieldRow Editor);

/// <summary>Evidence-backed placement of one formal input in the owning settings page.</summary>
public sealed record NativeInputPlacement(string FieldId, string Page, string Section, int Order);

public static class NativeInputPlacementMap
{
    public static IReadOnlyDictionary<string, NativeInputPlacement> FromSchemas(ToolSchema inputSchema, ToolSchema settingsSchema)
    {
        ArgumentNullException.ThrowIfNull(inputSchema);
        ArgumentNullException.ThrowIfNull(settingsSchema);
        var settingsPages=(settingsSchema.Presentation?.Fields??Array.Empty<FieldPresentation>())
            .Select(field=>field.Layout?.Page??field.Group).ToHashSet(StringComparer.Ordinal);
        var placements=(inputSchema.Presentation?.Fields??Array.Empty<FieldPresentation>())
            .Select(field=>
            {
                var page=field.Layout?.Page??throw new InvalidOperationException($"正式输入缺少原厂 page 映射：{field.FieldId}");
                var section=field.Layout?.Section??field.Group;
                if(string.IsNullOrWhiteSpace(section))throw new InvalidOperationException($"正式输入缺少原厂 section 映射：{field.FieldId}");
                // 原厂存在仅由图像来源或 ROI 组成的输入页。编辑器通过 PageOnlyPages 声明该页，
                // 输入行不需要伪装成持久化设置字段。
                return new NativeInputPlacement(field.FieldId,page,section,field.Order);
            }).ToArray();
        if(placements.Select(item=>item.FieldId).Distinct(StringComparer.Ordinal).Count()!=placements.Length)
            throw new InvalidOperationException("正式输入 page/section 映射包含重复字段。");
        return new System.Collections.ObjectModel.ReadOnlyDictionary<string,NativeInputPlacement>(
            placements.ToDictionary(item=>item.FieldId,StringComparer.Ordinal));
    }
}

/// <summary>
/// Shared ownership and validation seam for formal input rows.
/// The legacy implementation may still own a nested visual while migration is in progress.
/// </summary>
public interface INativeInputComposition : IDisposable
{
    IReadOnlyList<NativeInputField> InputFields { get; }
    bool HasInvalidInput { get; }
    bool HasPendingEdits { get; }
    event Action? Changed;
    DeclaredFieldRow TakeField(string fieldId);
    void SetFieldRowsVisibility(IEnumerable<string> fieldIds, bool visible);
    IReadOnlyList<NumericModelReadResult> ReadNumericModels(NumericModelReadScope scope, string? fieldId,
        bool pageKnown, bool currentPage, Func<bool> current,
        IReadOnlyDictionary<string, ConstantSetting?>? currentValues = null);
    bool TryValidate(out string error);
}
