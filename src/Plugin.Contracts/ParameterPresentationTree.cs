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

    public static ParameterPresentationTree Create(IEnumerable<FieldPresentation> fields)
    {
        ArgumentNullException.ThrowIfNull(fields);
        var ordered = fields.OrderBy(field => field.Order).ToArray();
        if (ordered.Any(field => field is null))
            throw new ArgumentException("参数展示树不能包含空字段。", nameof(fields));

        var pages = ordered
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

        return new ParameterPresentationTree(pages);

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
