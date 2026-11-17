using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.GrayMatch.Runtime;

namespace SunEyeVision.Tool.GrayMatch.Wpf.Views;

/// <summary>
/// 结果显示页的控件组合声明：图形/文本/可见性分组与文本坐标引用。
/// 与原件 6 个显示项对应；缺失这些声明时，不限小数位的文本坐标字段会被参数视图拒绝。
/// </summary>
internal static class GrayMatchDisplayUi
{
    internal static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options)
    {
        string Id(string group, string role) => GrayMatchResultDisplay.Field(group, role);
        var expressions = options.TextExpressions?.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal) ?? new();
        var contentId = Id(GrayMatchResultDisplay.Text, "content");
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
                return GrayMatchResultDisplay.ParseContent(proposal.Text, slots);
            });
        return options with
        {
            TextExpressions = expressions,
            GraphicDisplays = new[]
                {
                    GrayMatchResultDisplay.Match, GrayMatchResultDisplay.Location,
                    GrayMatchResultDisplay.Inspection, GrayMatchResultDisplay.ReferencePoint,
                    GrayMatchResultDisplay.RunPoint
                }
                .Select(group => new SunEyeVision.Plugin.Wpf.Fields.NativeGraphicDisplayOptions(
                    group, Id(group, "visible"), Id(group, "ok"), Id(group, "ng"), Id(group, "opacity"), Id(group, "width")))
                .ToArray(),
            VisibilityDisplays = new[]
            {
                new SunEyeVision.Plugin.Wpf.Fields.NativeVisibilityDisplayOptions(Id(GrayMatchResultDisplay.Text, "visible"))
            },
            TextDisplays = new[]
            {
                new SunEyeVision.Plugin.Wpf.Fields.NativeTextDisplayOptions(
                    Id(GrayMatchResultDisplay.Text, "content"), Id(GrayMatchResultDisplay.Text, "ok"),
                    Id(GrayMatchResultDisplay.Text, "ng"), Id(GrayMatchResultDisplay.Text, "size"),
                    Id(GrayMatchResultDisplay.Text, "opacity"), Id(GrayMatchResultDisplay.Text, "x"),
                    Id(GrayMatchResultDisplay.Text, "y"))
            }
        };
    }
}
