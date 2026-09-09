using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 单个测量输出项的上下限判定规格（几何测量工具共用）。
    /// 收敛现状中逐工具复制的 <c>Is*Enabled + *Min + *Max + Unit</c> 样板
    /// （LineDetect/Blob/ShapeMatch/... 各一份），使 6 个测量工具的判定模型与算法侧判定器一致。
    /// UI 范式：Enabled 开关控制范围输入可见（ToggleSwitch + RangeInputControl 数据驱动）。
    /// </summary>
    public class MeasureThreshold : ObservableObject
    {
        private string _name = string.Empty;
        private bool _enabled = true;
        private double _min;
        private double _max;
        private string? _unit;

        /// <summary>测量项名（日志/明细用，如"线距"）</summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value);
        }

        /// <summary>是否启用该上下限判定；false 时不参与判定、范围输入隐藏。</summary>
        public bool Enabled
        {
            get => _enabled;
            set => SetProperty(ref _enabled, value);
        }

        /// <summary>下限（含）；值 &lt; Min 判 NG</summary>
        public double Min
        {
            get => _min;
            set => SetProperty(ref _min, value);
        }

        /// <summary>上限（含）；值 &gt; Max 判 NG</summary>
        public double Max
        {
            get => _max;
            set => SetProperty(ref _max, value);
        }

        /// <summary>单位（px/°/mm…，仅供展示）</summary>
        public string? Unit
        {
            get => _unit;
            set => SetProperty(ref _unit, value);
        }

        public MeasureThreshold Clone() => new()
        {
            Name = Name,
            Enabled = Enabled,
            Min = Min,
            Max = Max,
            Unit = Unit,
        };
    }

    /// <summary>单项判定结果（值 + 是否通过 + 明细），供结果树/日志/整体聚合。</summary>
    public sealed class MeasureJudgeResult
    {
        public MeasureJudgeResult(MeasureThreshold threshold, double value, bool isPass, string message)
        {
            Threshold = threshold ?? throw new ArgumentNullException(nameof(threshold));
            Value = value;
            IsPass = isPass;
            Message = message;
        }

        /// <summary>对应的判定规格</summary>
        public MeasureThreshold Threshold { get; }

        /// <summary>实测值（未启用/无值时为 double.NaN）</summary>
        public double Value { get; }

        /// <summary>单项是否通过（未启用恒 true）</summary>
        public bool IsPass { get; }

        /// <summary>判定明细（通过/越界文本）</summary>
        public string Message { get; }
    }

    /// <summary>
    /// 测量上下限判定聚合器：所有启用项都落在 [Min, Max] 内 → OK，任一越界/无值 → NG。
    /// rule-015 报错优先：有启用项但取值缺失时判 NG（不静默跳过）。
    /// </summary>
    public static class MeasureJudge
    {
        /// <summary>对一组规格逐项判定。valueResolver 由算法侧按 Threshold 名提供实测值（null=缺失）。</summary>
        public static IReadOnlyList<MeasureJudgeResult> EvaluateAll(
            IEnumerable<MeasureThreshold>? thresholds,
            Func<MeasureThreshold, double?> valueResolver)
        {
            if (valueResolver == null) throw new ArgumentNullException(nameof(valueResolver));

            var results = new List<MeasureJudgeResult>();
            foreach (var threshold in thresholds ?? Enumerable.Empty<MeasureThreshold>())
            {
                if (!threshold.Enabled)
                {
                    results.Add(new MeasureJudgeResult(threshold, double.NaN, true, $"「{threshold.Name}」未启用"));
                    continue;
                }

                var value = valueResolver(threshold);
                if (!value.HasValue)
                {
                    // 启用却无实测值：按 NG 处理（显式报错，禁止静默当 OK）
                    results.Add(new MeasureJudgeResult(threshold, double.NaN, false, $"「{threshold.Name}」无实测值"));
                    continue;
                }

                var isPass = value.Value >= threshold.Min && value.Value <= threshold.Max;
                results.Add(new MeasureJudgeResult(
                    threshold,
                    value.Value,
                    isPass,
                    isPass
                        ? $"「{threshold.Name}」= {value.Value:G}{threshold.Unit} 在 [{threshold.Min:G}, {threshold.Max:G}]{threshold.Unit} 内"
                        : $"「{threshold.Name}」= {value.Value:G}{threshold.Unit} 越界 [{threshold.Min:G}, {threshold.Max:G}]{threshold.Unit}"));
            }
            return results;
        }

        /// <summary>整组是否全部通过（无启用项时视为通过）。</summary>
        public static bool IsAllPass(IEnumerable<MeasureJudgeResult> results)
            => results != null && results.All(r => r.IsPass);
    }
}
