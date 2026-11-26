using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Tool.CircleCircleMeasure.Runtime;

/// <summary>
/// 圆圆测量的唯一持久化设置：46 个原厂 ParamItem 的默认投影。值按字段 id 保存，
/// 缺省值逐项来自 docs/07-工具逆向/tools/imvsc2cmeasuremodu/parameters.json。
/// </summary>
public sealed record CircleCircleMeasureSettings : IToolSettings<CircleCircleMeasureSettings>
{
    public IReadOnlyDictionary<string, object?> Values { get; init; } = CircleCircleMeasureFields.Defaults;

    public CircleCircleMeasureSettings() { }

    public CircleCircleMeasureSettings(IReadOnlyDictionary<string, object?> values)
    {
        ArgumentNullException.ThrowIfNull(values);
        Values = new Dictionary<string, object?>(values, StringComparer.Ordinal);
    }

    public CircleCircleMeasureSettings Freeze() => this;

    private T Get<T>(string id) => Values.TryGetValue(id, out var value) && value is T typed
        ? typed
        : throw new ArgumentException("圆圆测量设置字段缺失或类型不正确：" + id);

    public IReadOnlyList<Diagnostic> Validate()
    {
        var errors = new List<Diagnostic>();
        void Range(string enabled, string low, string high, string label, double minimum, double maximum)
        {
            if (!Get<bool>(enabled)) return;
            var lower = Convert.ToDouble(Get<object>(low));
            var upper = Convert.ToDouble(Get<object>(high));
            if (!double.IsFinite(lower) || !double.IsFinite(upper) || lower < minimum || upper > maximum || lower > upper)
                errors.Add(new("CIRCLECIRCLEMEASURE_LIMIT_RANGE", $"{label}范围非法：须在 {minimum}~{maximum} 内且下限不大于上限。", low));
        }
        Range("settings.angle-limit-enabled", "settings.angle-limit-low", "settings.angle-limit-high", "角度", -180d, 180d);
        Range("settings.dist-limit-enabled", "settings.dist-limit-low", "settings.dist-limit-high", "距离", 0d, 99999d);
        Range("settings.inter1-x-limit-enabled", "settings.inter1-x-limit-low", "settings.inter1-x-limit-high", "交点1 X", 0d, 99999d);
        Range("settings.inter1-y-limit-enabled", "settings.inter1-y-limit-low", "settings.inter1-y-limit-high", "交点1 Y", 0d, 99999d);
        Range("settings.inter2-x-limit-enabled", "settings.inter2-x-limit-low", "settings.inter2-x-limit-high", "交点2 X", 0d, 99999d);
        Range("settings.inter2-y-limit-enabled", "settings.inter2-y-limit-low", "settings.inter2-y-limit-high", "交点2 Y", 0d, 99999d);
        return errors.AsReadOnly();
    }
}
