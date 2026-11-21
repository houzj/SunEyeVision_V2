namespace SunEyeVision.Plugin.Contracts;

/// <summary>
/// UI 无关的参数展示树。工具只声明页、分组和字段身份，视觉模板由宿主注册表决定。
/// </summary>
public sealed class ParameterPresentationTree
{
    public IReadOnlyList<ParameterPresentationPage> Pages { get; }

    private ParameterPresentationTree(IReadOnlyList<ParameterPresentationPage> pages)
    {
        Pages = pages;
    }

    public static ParameterPresentationTree Create(IEnumerable<FieldPresentation> fields, IEnumerable<string>? pageOnlyPages = null)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var orderedFields = fields.OrderBy(field => field.Order).ToArray();
        if (orderedFields.Any(field => field is null))
            throw new ArgumentException("参数展示树不能包含空字段。", nameof(fields));

        var pages = orderedFields
            .GroupBy(PageKey, StringComparer.Ordinal)
            .Select(page => new ParameterPresentationPage(
                page.Key,
                page
                    .GroupBy(field => (Key: SectionKey(field), IsAdvanced: field.Layout?.Advanced == true))
                    .Select(section => new ParameterPresentationSection(
                        section.Key.Key,
                        section.Key.IsAdvanced,
                        section.ToArray()))
                    .ToArray(),
                page.ToArray()))
            .ToArray();

        var byKey = pages.ToDictionary(page => page.Key, StringComparer.Ordinal);
        var requestedOrder = (pageOnlyPages ?? Array.Empty<string>())
            .Where(page => !string.IsNullOrWhiteSpace(page))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
        var orderedPages = requestedOrder
            .Select(page => byKey.TryGetValue(page, out var declared)
                ? declared
                : new ParameterPresentationPage(page, Array.Empty<ParameterPresentationSection>(), Array.Empty<FieldPresentation>()))
            .Concat(pages.Where(page => !requestedOrder.Contains(page.Key, StringComparer.Ordinal)))
            .ToArray();
        return new ParameterPresentationTree(orderedPages);

        static string PageKey(FieldPresentation field) => field.Layout?.Page ?? field.Group;
        static string SectionKey(FieldPresentation field) => field.Layout?.Section ?? field.Group;
    }
}

public sealed record ParameterPresentationPage(
    string Key,
    IReadOnlyList<ParameterPresentationSection> Sections,
    IReadOnlyList<FieldPresentation> Fields);

public sealed record ParameterPresentationSection(
    string Key,
    bool IsAdvanced,
    IReadOnlyList<FieldPresentation> Fields);
