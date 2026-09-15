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
        => Graphics.Single(item => string.Equals(item.Key, key, StringComparison.Ordinal)).Style;

    public TextStyle GetTextStyle(string key)
        => Texts.Single(item => string.Equals(item.Key, key, StringComparison.Ordinal)).Style;

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
