using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using SunEyeVision.Foundation.Logging;

namespace SunEyeVision.UI.Services.Performance
{
    /// <summary>
    /// 性能探针：分段计时 + 超阈值告警。
    /// </summary>
    /// <remarks>
    /// PROBE-KEEP：窗口打开、Tab 切换、关闭释放这三条关键路径长期保留。
    /// 设计原则是「平时完全静默」——只有总耗时超过阈值才写一条 Warning，
    /// 这样既能自动发现后续重构造成的性能退化，又不产生日志噪音。
    ///
    /// 阈值来源：人类感知「即时」约 100ms、「无明显抱怨」约 300ms，当前为暂定值。
    /// 取得 VisionMaster 同版本实测值后，按实测值调整，不沿用猜测值。
    ///
    /// 不使用 System.Diagnostics.Debug/Console 输出，统一走 VisionLogger 进入日志系统。
    /// </remarks>
    public static class PerfProbe
    {
        /// <summary>窗口首次打开总耗时阈值（毫秒）。</summary>
        public static int WindowOpenThresholdMs { get; set; } = 300;

        /// <summary>Tab 切换总耗时阈值（毫秒）。</summary>
        public static int TabSwitchThresholdMs { get; set; } = 80;

        /// <summary>窗口关闭总耗时阈值（毫秒）。</summary>
        public static int WindowCloseThresholdMs { get; set; } = 150;

        /// <summary>总开关。关闭后 Begin 返回的 Scope 不做计时、不产生输出。</summary>
        public static bool IsEnabled { get; set; } = true;

        /// <summary>
        /// 环境变量名：置为 1 时无视阈值强制输出全部测量结果，仅用于取证调试。
        /// 这样取证时不需要改代码、不需要二次构建，正常运行时仍保持零噪音。
        /// </summary>
        public const string ForceOutputVariable = "SUNEYE_PERF_PROBE";

        /// <summary>当环境变量 <see cref="ForceOutputVariable"/> 为 1 时强制输出。</summary>
        private static readonly bool ForceOutput =
            Environment.GetEnvironmentVariable(ForceOutputVariable) == "1";

        /// <summary>
        /// 开始一次测量。配合 <c>using</c> 使用，未显式调用 End 时由 Dispose 收尾。
        /// </summary>
        /// <param name="scene">场景标识：open / tab / close。</param>
        /// <param name="target">测量对象，如工具 ID 或节点名。</param>
        public static Scope Begin(string scene, string target)
            => new Scope(scene, target, ResolveThreshold(scene), IsEnabled);

        /// <summary>按场景取阈值；未知场景按窗口打开阈值处理。</summary>
        private static int ResolveThreshold(string scene) => scene switch
        {
            "tab" => TabSwitchThresholdMs,
            "close" => WindowCloseThresholdMs,
            _ => WindowOpenThresholdMs,
        };

        /// <summary>
        /// 一次测量的上下文。持有自己的计时状态，不共享，可安全并发使用。
        /// </summary>
        public sealed class Scope : IDisposable
        {
            private readonly string _scene;
            private readonly string _target;
            private readonly int _threshold;
            private readonly bool _active;
            private readonly Stopwatch _clock;
            private readonly List<(string Stage, long Ms)> _stages = new();
            private long _lastMarkMs;
            private bool _ended;

            internal Scope(string scene, string target, int threshold, bool active)
            {
                _scene = scene;
                _target = target;
                _threshold = threshold;
                _active = active;
                _clock = active ? Stopwatch.StartNew() : new Stopwatch();
            }

            /// <summary>
            /// 标记一个阶段结束，记录距上一个标记的耗时。
            /// 探针关闭时不产生任何开销。
            /// </summary>
            public void Mark(string stage)
            {
                if (!_active || _ended) return;
                var now = _clock.ElapsedMilliseconds;
                _stages.Add((stage, now - _lastMarkMs));
                _lastMarkMs = now;
            }

            /// <summary>
            /// 结束测量。仅当总耗时超过阈值时写一条告警。
            /// </summary>
            /// <param name="windowInstances">本次创建的窗口实例数。</param>
            /// <param name="viewInstances">本次创建的视图实例数。</param>
            /// <param name="modelInstances">本次创建的模型实例数。</param>
            public void End(int windowInstances = 0, int viewInstances = 0, int modelInstances = 0)
            {
                if (!_active || _ended) return;
                _ended = true;
                _clock.Stop();

                var total = _clock.ElapsedMilliseconds;
                var exceeded = total > _threshold;
                if (!exceeded && !ForceOutput) return;

                var builder = new StringBuilder(160);
                builder.Append("[Perf] ").Append(_scene)
                       .Append(" target=").Append(_target)
                       .Append(" total=").Append(total).Append("ms");

                if (_stages.Count > 0)
                {
                    builder.Append(" stages=");
                    for (var i = 0; i < _stages.Count; i++)
                    {
                        if (i > 0) builder.Append('/');
                        builder.Append(_stages[i].Stage).Append(_stages[i].Ms);
                    }
                }

                if (windowInstances > 0 || viewInstances > 0 || modelInstances > 0)
                {
                    builder.Append(" instances=win").Append(windowInstances)
                           .Append("_view").Append(viewInstances)
                           .Append("_model").Append(modelInstances);
                }

                builder.Append(" threshold=").Append(_threshold)
                       .Append(exceeded ? " EXCEED" : " FORCED");

                // 超阈值记 Warning（退化要能被发现）；强制取证模式记 Info（不污染告警）。
                var level = exceeded ? LogLevel.Warning : LogLevel.Info;
                VisionLogger.Instance.Log(level, builder.ToString(), nameof(PerfProbe));
            }

            /// <inheritdoc />
            public void Dispose() => End();
        }
    }
}
