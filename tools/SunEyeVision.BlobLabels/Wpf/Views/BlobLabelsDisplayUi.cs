using System.Windows;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.BlobLabels.Runtime;

namespace SunEyeVision.Tool.BlobLabels.Wpf.Views;

/// <summary>
/// 结果显示页的控件组合声明：图形/文本/可见性分组与文本坐标引用。
/// 与原件 8 个显示项一一对应；缺失这些声明时，不限小数位的文本坐标字段会被参数视图拒绝。
/// </summary>
internal static class BlobLabelsDisplayUi
{
    internal static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options)
    {
        string Id(string group, string role) => BlobLabelsResultDisplay.Field(group, role);
        var expressions = options.TextExpressions?.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal) ?? new();
        var contentId = Id(BlobLabelsResultDisplay.Text, "content");
        var sourceOptions = expressions.GetValueOrDefault(contentId);
        expressions[contentId] = new(sourceOptions?.ReadCandidates ?? (() => new(
                Array.Empty<SunEyeVision.Plugin.Wpf.Adapters.References.DeclaredReferenceCandidate>(),
                _ => throw new InvalidOperationException("文本来源尚未就绪。"), () => true)),
            CreateSetting: proposal =>
            {
                var slots = proposal.Accepted is DisplayExpressionSetting existing ? existing.Slots.ToList() : new List<DisplayExpressionSlot>();
                if (proposal.Selected is { } selected)
                {
                    if (proposal.Token is not { Length: > 0 } token) throw new ArgumentException("文本来源标记无效。");
                    var reference = selected.Reference(BindingSelection.All);
                    if (proposal.Text.Split("{" + token + "}", StringSplitOptions.None).Length > 2 &&
                        slots.Any(slot => slot.Token == token && slot.Reference is { } old &&
                            SunEyeVision.Plugin.Contracts.Results.Presentation.ResultDisplayFields.SameReferenceIdentity(old, reference) == false))
                        throw new ArgumentException("文本来源名称不唯一，请使用不同的来源名称。");
                    slots.RemoveAll(slot => slot.Token == token);
                    slots.Add(new(token, reference));
                }
                return BlobLabelsResultDisplay.ParseContent(proposal.Text, slots);
            });
        return options with
        {
            TextExpressions = expressions,
            GraphicDisplays = new[]
                {
                    BlobLabelsResultDisplay.Graphic, BlobLabelsResultDisplay.Inspection, BlobLabelsResultDisplay.Outline,
                    BlobLabelsResultDisplay.ReferencePoint, BlobLabelsResultDisplay.RunPoint, BlobLabelsResultDisplay.Mask,
                    BlobLabelsResultDisplay.Annulus
                }
                .Select(group => new SunEyeVision.Plugin.Wpf.Fields.NativeGraphicDisplayOptions(
                    group, Id(group, "visible"), Id(group, "ok"), Id(group, "ng"), Id(group, "opacity"), Id(group, "width")))
                .ToArray(),
            VisibilityDisplays = new[]
            {
                new SunEyeVision.Plugin.Wpf.Fields.NativeVisibilityDisplayOptions(Id(BlobLabelsResultDisplay.Text, "visible")),
                new SunEyeVision.Plugin.Wpf.Fields.NativeVisibilityDisplayOptions(Id(BlobLabelsResultDisplay.Outline, "visible"))
            },
            TextDisplays = new[]
            {
                new SunEyeVision.Plugin.Wpf.Fields.NativeTextDisplayOptions(
                    Id(BlobLabelsResultDisplay.Text, "content"), Id(BlobLabelsResultDisplay.Text, "ok"),
                    Id(BlobLabelsResultDisplay.Text, "ng"), Id(BlobLabelsResultDisplay.Text, "size"),
                    Id(BlobLabelsResultDisplay.Text, "opacity"), Id(BlobLabelsResultDisplay.Text, "x"),
                    Id(BlobLabelsResultDisplay.Text, "y"))
            }
        };
    }
}
