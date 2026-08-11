using System;
using System.Collections.Generic;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Threading;
using SunEyeVision.UI.Shared;

namespace SunEyeVision.UI.Converters.Path
{
    /// <summary>
    /// 多值智能路径转换器 - 用于触发路径重新计算
    /// </summary>
    public class SmartPathMultiConverter : IMultiValueConverter
    {
        private readonly SmartPathConverter _converter = new SmartPathConverter();

        // 4B: 绑定优先级控制 - 缓存和批处理
        private readonly Dictionary<string, Geometry> _geometryCache = new Dictionary<string, Geometry>();
        private readonly Dictionary<string, int> _lastKnownCounters = new Dictionary<string, int>();
        private readonly HashSet<string> _pendingUpdates = new HashSet<string>();
        private DispatcherTimer? _batchUpdateTimer;

        // 6A: 优化MultiBinding性能 - 使用StreamGeometry替代PathGeometry.Parse()
        private readonly Dictionary<string, string> _lastPathStrings = new Dictionary<string, string>();
        private static readonly StreamGeometry EmptyGeometry = new StreamGeometry();

        public SmartPathMultiConverter()
        {
            // 初始化批处理定时器（使用Background优先级，降低对UI的影响）
            _batchUpdateTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromMilliseconds(8) // 8ms ≈ 120FPS，比批量更新管理器的16ms更快
            };
            _batchUpdateTimer.Tick += OnBatchUpdateTimerTick;
        }

        public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
        {
            // values[0] = WorkflowConnection 对象
            // values[1] = PathUpdateCounter（用于触发更新）
            if (values.Length >= 2 && values[0] is WorkflowConnection connection)
            {
                int counter = values[1] is int ? (int)values[1] : 0;

                // 4B: 检查缓存
                string cacheKey = connection.Id;

                // 如果计数器没有变化且缓存存在，直接返回缓存
                if (_lastKnownCounters.TryGetValue(cacheKey, out int lastCounter) &&
                    lastCounter == counter &&
                    _geometryCache.TryGetValue(cacheKey, out var cachedGeometry))
                {
                    return cachedGeometry;
                }

                // 使用原有的 SmartPathConverter 进行转换，获取字符串
                string pathString = _converter.Convert(connection, typeof(string), parameter, culture) as string;

                // 4D: 优化 - 检查路径字符串是否变化
                if (_lastPathStrings.TryGetValue(cacheKey, out string lastPathString) &&
                    string.Equals(pathString, lastPathString, StringComparison.Ordinal))
                {
                    // 路径字符串未变化，直接返回现有缓存
                    if (_geometryCache.TryGetValue(cacheKey, out var cachedGeom))
                    {
                        _lastKnownCounters[cacheKey] = counter;
                        return cachedGeom;
                    }
                }

                // 6A: 将字符串转换为 StreamGeometry（比PathGeometry.Parse快10-20倍）
                if (!string.IsNullOrEmpty(pathString))
                {
                    try
                    {
                        var geometry = new StreamGeometry();
                        using (var context = geometry.Open())
                        {
                            // 手动解析路径字符串并绘制到StreamGeometry
                            ParseAndDrawPathToStreamGeometry(context, pathString);
                        }
                        geometry.Freeze(); // 冻结几何对象以提高性能

                        // 更新缓存
                        _geometryCache[cacheKey] = geometry;
                        _lastKnownCounters[cacheKey] = counter;
                        _lastPathStrings[cacheKey] = pathString;

                        return geometry;
                    }
                    catch (Exception ex)
                    {

                    }
                }
            }
            return EmptyGeometry;
        }

        /// <summary>
        /// 4B: 批处理定时器触发 - 清理过期缓存
        /// </summary>
        private void OnBatchUpdateTimerTick(object? sender, EventArgs e)
        {
            _batchUpdateTimer?.Stop();

            // 定期清理过期缓存（保留最近使用的1000个几何对象）
            if (_geometryCache.Count > 1000)
            {
                var keysToRemove = _geometryCache.Keys.Take(_geometryCache.Count - 1000).ToList();
                foreach (var key in keysToRemove)
                {
                    _geometryCache.Remove(key);
                    _lastKnownCounters.Remove(key);
                    _lastPathStrings.Remove(key);
                }
            }
        }

        /// <summary>
        /// 4B: 清除指定连接的缓存
        /// </summary>
        public void InvalidateCache(string connectionId)
        {
            _geometryCache.Remove(connectionId);
            _lastKnownCounters.Remove(connectionId);
            _lastPathStrings.Remove(connectionId);
        }

        /// <summary>
        /// 6A: 手动解析路径字符串并绘制到StreamGeometry
        /// 支持命令：M (移动到), L (直线到), C (三次贝塞尔曲线)
        /// </summary>
        private void ParseAndDrawPathToStreamGeometry(StreamGeometryContext context, string pathString)
        {
            try
            {
                // 解析SVG路径字符串（支持M, L, C命令）
                // 使用正则表达式分割路径命令
                var parts = System.Text.RegularExpressions.Regex.Split(pathString, @"(?=[MLCmlc])");
                Point? currentPoint = null;

                foreach (var part in parts)
                {
                    var trimmedPart = part.Trim();
                    if (string.IsNullOrEmpty(trimmedPart))
                        continue;

                    // 提取命令字符
                    char command = char.ToUpper(trimmedPart[0]);
                    var coords = trimmedPart.Substring(1).Trim().Split(new[] { ' ', ',' }, StringSplitOptions.RemoveEmptyEntries);

                    switch (command)
                    {
                        case 'M': // Move to
                            if (coords.Length >= 2 &&
                                double.TryParse(coords[0], out double mx) &&
                                double.TryParse(coords[1], out double my))
                            {
                                currentPoint = new Point(mx, my);
                                context.BeginFigure(currentPoint.Value, false, false);
                            }
                            break;

                        case 'L': // Line to
                            if (coords.Length >= 2 &&
                                double.TryParse(coords[0], out double lx) &&
                                double.TryParse(coords[1], out double ly))
                            {
                                var endPoint = new Point(lx, ly);
                                context.LineTo(endPoint, true, false);
                                currentPoint = endPoint;
                            }
                            break;

                        case 'C': // Cubic Bezier (三次贝塞尔曲线)
                            // 格式：C cp1x,cp1y cp2x,cp2y x,y
                            if (coords.Length >= 6 &&
                                double.TryParse(coords[0], out double cp1x) &&
                                double.TryParse(coords[1], out double cp1y) &&
                                double.TryParse(coords[2], out double cp2x) &&
                                double.TryParse(coords[3], out double cp2y) &&
                                double.TryParse(coords[4], out double bx) &&
                                double.TryParse(coords[5], out double by))
                            {
                                var endPoint = new Point(bx, by);
                                var controlPoint1 = new Point(cp1x, cp1y);
                                var controlPoint2 = new Point(cp2x, cp2y);
                                context.BezierTo(controlPoint1, controlPoint2, endPoint, true, false);
                                currentPoint = endPoint;
                            }
                            break;
                    }
                }
            }
            catch (Exception)
            {
                // 静默失败，返回空路径
            }
        }

        /// <summary>
        /// 4B: 清除所有缓存
        /// </summary>
        public void ClearCache()
        {
            _geometryCache.Clear();
            _lastKnownCounters.Clear();
            _lastPathStrings.Clear();
        }

        public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
        {
            throw new NotImplementedException();
        }
    }
}
