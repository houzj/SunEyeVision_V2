using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Presenters.Parameters;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 结果显示页的控件组合声明：图形/文本/可见性分组与文本坐标引用。
/// 与原件 9 个显示项对应（BoundingBox 与 MatchResults 合并）；缺失这些声明时，
/// 不限小数位的文本坐标字段会被参数视图拒绝。
/// </summary>
internal static class ContourMatchDisplayUi
{
    internal static ToolSettingsViewOptions Configure(ToolSettingsViewOptions options)
    {
        string Id(string group, string role) => ContourMatchResultDisplay.Field(group, role);
        var expressions = options.TextExpressions?.ToDictionary(item => item.Key, item => item.Value, StringComparer.Ordinal) ?? new();
        var contentId = Id(ContourMatchResultDisplay.Text, "content");
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
                return ContourMatchResultDisplay.ParseContent(proposal.Text, slots);
            });
        return options with
        {
            TextExpressions = expressions,
            GraphicDisplays = new[]
                {
                    // 原厂 result-tab 顺序：检测区域 → 匹配结果 → 匹配点 → 匹配轮廓点。
                    // 轮廓点在原厂 Display.xml 里是 outlinepoint（带 okcolor/ngcolor/opacity/linewidth），
                    // 因此属于图形组，不是纯可见性项。
                    ContourMatchResultDisplay.Sensor, ContourMatchResultDisplay.Match,
                    ContourMatchResultDisplay.Location, ContourMatchResultDisplay.Outline,
                    ContourMatchResultDisplay.ReferencePoint,
                    ContourMatchResultDisplay.RunPoint, ContourMatchResultDisplay.Mask
                }
                .Select(group => new SunEyeVision.Plugin.Wpf.Fields.NativeGraphicDisplayOptions(
                    group, Id(group, "visible"), Id(group, "ok"), Id(group, "ng"), Id(group, "opacity"), Id(group, "width")))
                .ToArray(),
            VisibilityDisplays = new[]
            {
                new SunEyeVision.Plugin.Wpf.Fields.NativeVisibilityDisplayOptions(Id(ContourMatchResultDisplay.Text, "visible"))
            },
            TextDisplays = new[]
            {
                new SunEyeVision.Plugin.Wpf.Fields.NativeTextDisplayOptions(
                    Id(ContourMatchResultDisplay.Text, "content"), Id(ContourMatchResultDisplay.Text, "ok"),
                    Id(ContourMatchResultDisplay.Text, "ng"), Id(ContourMatchResultDisplay.Text, "size"),
                    Id(ContourMatchResultDisplay.Text, "opacity"), Id(ContourMatchResultDisplay.Text, "x"),
                    Id(ContourMatchResultDisplay.Text, "y"))
            }
        };
    }
}
