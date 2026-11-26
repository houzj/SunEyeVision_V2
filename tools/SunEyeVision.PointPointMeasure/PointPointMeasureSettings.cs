using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.PointPointMeasure.Runtime;

/// <summary>点点测量的持久化设置：14 个原厂 ParamItem 的默认投影。</summary>
public sealed record PointPointMeasureSettings : IToolSettings<PointPointMeasureSettings>
{
    public IReadOnlyDictionary<string, object?> Values { get; init; } = PointPointMeasureFields.Defaults;

    public PointPointMeasureSettings() { }

    public PointPointMeasureSettings(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    public PointPointMeasureSettings Freeze() => this;

    private T Get<T>(string id) => Values.TryGetValue(id, out var value) && value is T typed
        ? typed
        : throw new ArgumentException("点点测量设置字段缺失或类型不正确：" + id);

    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Range(string enabled, string low, string high, string label, double minimum, double maximum)
        {
            if (!Get<bool>(enabled)) return;
            var lower = Convert.ToDouble(Get<object>(low));
            var upper = Convert.ToDouble(Get<object>(high));
            if (!double.IsFinite(lower) || !double.IsFinite(upper) || lower < minimum || upper > maximum || lower > upper)
                errors.Add(new("POINTMEASURE_LIMIT_RANGE", $"{label}范围非法：须在 {minimum}~{maximum} 内且下限不大于上限。", low));
        }
        Range("settings.angle-limit-enabled", "settings.angle-limit-low", "settings.angle-limit-high", "角度", -180d, 180d);
        Range("settings.dist-limit-enabled", "settings.dist-limit-low", "settings.dist-limit-high", "距离", 0d, 99999d);
        Range("settings.mid-x-limit-enabled", "settings.mid-x-limit-low", "settings.mid-x-limit-high", "中点X", 0d, 99999d);
        Range("settings.mid-y-limit-enabled", "settings.mid-y-limit-low", "settings.mid-y-limit-high", "中点Y", 0d, 99999d);
        return errors.AsReadOnly();
    }
}
