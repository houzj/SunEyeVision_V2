using System;
using System.Collections.ObjectModel;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

public enum JudgmentRuleKind
{
    Range,
    Empty,
    Boolean,
}

public enum JudgmentScope
{
    Overall,
    PerItem,
}

/// <summary>算法工具结果页的完整数据源。工具声明模块并组织项目，公共 UI 只负责渲染。</summary>
public sealed class ResultPresentationConfig : ObservableObject
{
    public ObservableCollection<JudgmentConfigItem> Judgments { get; set; } = new();
    public ObservableCollection<GraphicsPresentationItem> Graphics { get; set; } = new();
    public ObservableCollection<TextPresentationItem> Texts { get; set; } = new();

    public string[] ValidateContract()
    {
        var errors = new System.Collections.Generic.List<string>();
        ValidateKeys(Judgments, item => item.Key, "结果判断", errors);
        ValidateKeys(Graphics, item => item.Key, "图形显示", errors);
        ValidateKeys(Texts, item => item.Key, "文本显示", errors);
        return errors.ToArray();
    }

    public JudgmentConfigItem GetJudgment(string key)
        => Judgments.Single(item => string.Equals(item.Key, key, StringComparison.Ordinal));

    public GraphicsStyle GetGraphicsStyle(string key)
    {
        var item = Graphics.Single(item => string.Equals(item.Key, key, StringComparison.Ordinal));
        item.Style.PresentationKey = item.Key;
        return item.Style;
    }

    public TextStyle GetTextStyle(string key)
    {
        var item = Texts.Single(item => string.Equals(item.Key, key, StringComparison.Ordinal));
        item.Style.PresentationKey = item.Key;
        return item.Style;
    }

    /// <summary>
    /// 将当前编辑中的显示样式重新关联到已生成的结果几何。
    /// 执行器为保证本次计算稳定，会使用参数快照生成结果；调试 UI 则编辑原始参数。
    /// 此方法只替换结果项的样式引用，不改动已计算的几何数据，因此可在不重新执行算法的
    /// 情况下即时预览可见性、颜色、线宽及文本样式。
    /// </summary>
    public void ApplyLiveStyles(DisplayCollector? collector)
    {
        if (collector == null) return;

        foreach (var item in Graphics) item.Style.PresentationKey = item.Key;
        foreach (var item in Texts) item.Style.PresentationKey = item.Key;

        var graphicsStyles = Graphics.ToDictionary(item => item.Key, item => item.Style, StringComparer.Ordinal);
        var textStyles = Texts.ToDictionary(item => item.Key, item => item.Style, StringComparer.Ordinal);

        foreach (var item in collector.Items)
        {
            switch (item)
            {
                case GraphicsDisplayItem graphics when graphics.Style != null
                    && TryGetLiveStyle(graphics.PresentationKey, graphics.Style.PresentationKey,
                        graphics.Style.Name, graphicsStyles, out var graphicsStyle):
                    graphics.Style = graphicsStyle;
                    break;

                case TextDisplayItem text when text.Style != null
                    && TryGetLiveStyle(text.PresentationKey, text.Style.PresentationKey,
                        text.Style.Name, textStyles, out var textStyle):
                    text.Style = textStyle;
                    break;
            }
        }
    }

    private static bool TryGetLiveStyle<TStyle>(string itemKey, string styleKey, string legacyName,
        IReadOnlyDictionary<string, TStyle> styles, out TStyle style)
        where TStyle : class
    {
        if (styles.TryGetValue(itemKey, out style!) || styles.TryGetValue(styleKey, out style!))
            return true;

        // 兼容旧执行结果：历史图元不带 PresentationKey 时按唯一显示名称回退。
        var matches = styles.Values.Where(candidate => candidate switch
        {
            GraphicsStyle graphics => graphics.Name == legacyName,
            TextStyle text => text.Name == legacyName,
            _ => false,
        }).Take(2).ToArray();
        style = matches.Length == 1 ? matches[0] : null!;
        return matches.Length == 1;
    }

    private static void ValidateKeys<T>(System.Collections.Generic.IEnumerable<T> items,
        Func<T, string> selector, string moduleName, System.Collections.Generic.List<string> errors)
    {
        var keys = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
        foreach (var item in items)
        {
            var key = selector(item);
            if (string.IsNullOrWhiteSpace(key)) errors.Add($"{moduleName}存在空 Key");
            else if (!keys.Add(key)) errors.Add($"{moduleName}存在重复 Key：{key}");
        }
    }
}

public sealed class JudgmentConfigItem : ObservableObject
{
    private bool _enabled = true;
    private double _min;
    private double _max;

    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public int Order { get; set; }
    public JudgmentRuleKind RuleKind { get; set; } = JudgmentRuleKind.Range;
    public JudgmentScope Scope { get; set; } = JudgmentScope.Overall;
    public bool Enabled { get => _enabled; set => SetProperty(ref _enabled, value); }
    public double Min { get => _min; set => SetProperty(ref _min, value); }
    public double Max { get => _max; set => SetProperty(ref _max, value); }
    public string Unit { get; set; } = string.Empty;
    public int DecimalPlaces { get; set; } = 2;
    public double AllowedMinimum { get; set; } = double.MinValue;
    public double AllowedMaximum { get; set; } = double.MaxValue;
    public bool AffectsItemDisplayState { get; set; }
}

public sealed class GraphicsPresentationItem : ObservableObject
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public int Order { get; set; }
    public GraphicsStyle Style { get; set; } = new();
}

public sealed class TextPresentationItem : ObservableObject
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Group { get; set; } = string.Empty;
    public int Order { get; set; }
    public TextStyle Style { get; set; } = new();
}

public sealed record JudgmentOutcome(string Key, double Value, bool IsPass, string Message);

public static class ResultJudgmentEvaluator
{
    public static IReadOnlyList<JudgmentOutcome> EvaluateRanges(
        IEnumerable<JudgmentConfigItem> judgments,
        Func<string, double?> valueResolver)
    {
        ArgumentNullException.ThrowIfNull(judgments);
        ArgumentNullException.ThrowIfNull(valueResolver);

        return judgments.Select(item =>
        {
            if (!item.Enabled)
                return new JudgmentOutcome(item.Key, double.NaN, true, $"「{item.Name}」未启用");

            var value = valueResolver(item.Key);
            if (!value.HasValue)
                return new JudgmentOutcome(item.Key, double.NaN, false, $"「{item.Name}」无实测值");

            var pass = item.RuleKind switch
            {
                JudgmentRuleKind.Range => value.Value >= item.Min && value.Value <= item.Max,
                JudgmentRuleKind.Empty => false,
                JudgmentRuleKind.Boolean => value.Value != 0,
                _ => false,
            };
            var message = item.RuleKind == JudgmentRuleKind.Range
                ? (pass
                    ? $"「{item.Name}」= {value.Value:G}{item.Unit} 在 [{item.Min:G}, {item.Max:G}]{item.Unit} 内"
                    : $"「{item.Name}」= {value.Value:G}{item.Unit} 越界 [{item.Min:G}, {item.Max:G}]{item.Unit}")
                : $"「{item.Name}」= {value.Value:G}{item.Unit} {(pass ? "通过" : "未通过")}";
            return new JudgmentOutcome(item.Key, value.Value, pass, message);
        }).ToArray();
    }

    public static bool IsAllPass(IEnumerable<JudgmentOutcome> outcomes)
        => outcomes.All(item => item.IsPass);
}
