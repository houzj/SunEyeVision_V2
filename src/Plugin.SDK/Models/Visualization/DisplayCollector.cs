using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 显示收集器 - Style 引用驱动的按需 Push 收集器
    /// </summary>
    public sealed class DisplayCollector
    {
        private readonly List<DisplayItemBase> _items = new();

        // 按 Style 引用分组（同 Style 的几何数据合并到同一个 GraphicsDisplayItem）
        private readonly Dictionary<GraphicsStyle, GraphicsDisplayItem> _graphicsAccum = new();
        private readonly Dictionary<TextStyle, TextDisplayItem> _textAccum = new();

        private bool _isFinalized;

        // ===== 图形按需收集 =====

        /// <summary>添加矩形</summary>
        public void AddRect(GraphicsStyle style, Rect rect, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Rectangle);
            AppendGeometry(item, rect, isPass);
        }

        /// <summary>批量添加矩形</summary>
        public void AddRects(GraphicsStyle style, IReadOnlyList<Rect> rects, IReadOnlyList<bool>? isPassList = null)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Rectangle);
            for (int i = 0; i < rects.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                AppendGeometry(item, rects[i], isPass);
            }
        }

        /// <summary>添加十字标记</summary>
        public void AddCross(GraphicsStyle style, Point center, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Cross);
            AppendGeometry(item, center, isPass);
        }

        /// <summary>批量添加十字标记</summary>
        public void AddCrosses(GraphicsStyle style, IReadOnlyList<Point> centers, IReadOnlyList<bool>? isPassList = null)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Cross);
            for (int i = 0; i < centers.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                AppendGeometry(item, centers[i], isPass);
            }
        }

        /// <summary>添加点（实心圆）</summary>
        public void AddPoint(GraphicsStyle style, Point point, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Point);
            AppendGeometry(item, point, isPass);
        }

        /// <summary>批量添加点（实心圆）</summary>
        public void AddPoints(GraphicsStyle style, IReadOnlyList<Point> points, IReadOnlyList<bool>? isPassList = null)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Point);
            for (int i = 0; i < points.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                AppendGeometry(item, points[i], isPass);
            }
        }

        /// <summary>添加轮廓</summary>
        public void AddContour(GraphicsStyle style, Point[] points, bool isPass = true, bool isClosed = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Contour);
            var contourGeom = new ContourGeometry(points, isClosed);
            AppendGeometry(item, contourGeom, isPass);
        }

        /// <summary>添加轮廓（带仿射变换矩阵）</summary>
        public void AddContour(GraphicsStyle style, Point[] points, Matrix transform, bool isPass = true, bool isClosed = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Contour);
            var contourGeom = new ContourGeometry(points, isClosed, transform);
            AppendGeometry(item, contourGeom, isPass);
        }

        /// <summary>批量添加轮廓</summary>
        public void AddContours(GraphicsStyle style, IReadOnlyList<Point[]> contoursList, IReadOnlyList<bool>? isPassList = null, bool isClosed = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Contour);
            for (int i = 0; i < contoursList.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                var contourGeom = new ContourGeometry(contoursList[i], isClosed);
                AppendGeometry(item, contourGeom, isPass);
            }
        }

        /// <summary>
        /// 添加一个带统一判定状态的批量轮廓。用于大量区域显示，避免每个区域创建
        /// 一个 ContourGeometry 对象；OK/NG 应分别调用此方法。
        /// </summary>
        public void AddContourBatch(GraphicsStyle style, IReadOnlyList<Point[]> contours,
            bool isPass = true, bool isClosed = true)
        {
            if (contours == null || contours.Count == 0) return;
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Contour);
            AppendGeometry(item, new BatchedContourGeometry(contours, isClosed), isPass);
        }

        /// <summary>添加圆（真圆，由 Circle2d 定义圆心与半径，渲染层按 GraphicsType.Circle 绘制椭圆）</summary>
        public void AddCircle(GraphicsStyle style, Circle2d circle, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Circle);
            AppendGeometry(item, circle, isPass);
        }

        /// <summary>添加线段（由起点/终点定义，渲染层按 GraphicsType.Line 绘制直线）</summary>
        public void AddLine(GraphicsStyle style, Point start, Point end, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Line);
            AppendGeometry(item, new LineSegment(start, end), isPass);
        }

        /// <summary>
        /// 添加圆弧（由 Arc2d 定义圆心/半径/起止角/大小弧，渲染层按 GraphicsType.Arc 绘制）。
        /// 用于角度角标、倒角弧等。
        /// </summary>
        public void AddArc(GraphicsStyle style, Arc2d arc, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Arc);
            AppendGeometry(item, arc, isPass);
        }

        // ===== 文本按需收集 =====

        /// <summary>添加文本数组（每个元素独立定位）</summary>
        public void AddText(TextStyle style, double[] x, double[] y, string[] content, bool isPass = true)
        {
            var item = GetOrCreateTextItem(style);
            for (int i = 0; i < content.Length; i++)
                AppendText(item, x[i], y[i], content[i], isPass);
        }

        /// <summary>添加文本数组（每个元素独立定位、独立isPass控制）</summary>
        public void AddText(TextStyle style, double[] x, double[] y, string[] content, IReadOnlyList<bool> isPassList)
        {
            var item = GetOrCreateTextItem(style);
            for (int i = 0; i < content.Length; i++)
            {
                var isPass = i < isPassList.Count ? isPassList[i] : true;
                AppendText(item, x[i], y[i], content[i], isPass);
            }
        }

        /// <summary>在指定锚点位置自动换行显示多行文本</summary>
        public void AddText(TextStyle style, double x, double y, string[] content, bool isPass = true)
        {
            var item = GetOrCreateTextItem(style);
            foreach (var c in content)
                AppendText(item, x, y, c, isPass);
        }

        /// <summary>在指定位置显示单条文本</summary>
        public void AddText(TextStyle style, double x, double y, string content, bool isPass = true)
        {
            var item = GetOrCreateTextItem(style);
            AppendText(item, x, y, content, isPass);
        }

        /// <summary>
        /// 在指定**锚点**显示单条文本，附加**屏幕像素**偏移（锚点=图像坐标跟随几何；偏移与字号同缩放模型）。
        /// 同一锚点的多条文本由渲染层自动换行排版（行高随字号自适应）。
        /// </summary>
        public void AddText(TextStyle style, double anchorX, double anchorY, string content,
            double screenOffsetX, double screenOffsetY, bool isPass = true)
        {
            var item = GetOrCreateTextItem(style);
            AppendText(item, anchorX, anchorY, content, isPass, screenOffsetX, screenOffsetY);
        }

        // ===== 生命周期 =====

        /// <summary>清空收集器（算法开始前调用）</summary>
        public void Clear()
        {
            _items.Clear();
            _graphicsAccum.Clear();
            _textAccum.Clear();
            _isFinalized = false;
        }

        /// <summary>
        /// 将另一个收集器的所有显示项合并到当前收集器（追加语义）。
        /// 用于循环累积显示：每轮子收集器构建的单轮图形追加到主收集器叠加。
        /// 相同 Style 的图形/文本自动并入同一条目（追加几何数据）。
        /// </summary>
        public void Merge(DisplayCollector other)
        {
            if (other == null) return;

            // 合并图形：按 Style + Type 匹配并入，否则新建条目
            foreach (var (style, srcItem) in other._graphicsAccum)
            {
                if (_graphicsAccum.TryGetValue(style, out var destItem))
                {
                    // 相同 Style 已存在：追加几何数据（保持原条目类型）
                    for (int i = 0; i < srcItem.GeometryList.Count; i++)
                    {
                        destItem.GeometryList.Add(srcItem.GeometryList[i]);
                        destItem.PassList.Add(srcItem.PassList[i]);
                    }
                }
                else
                {
                    _graphicsAccum[style] = srcItem;
                    _items.Add(srcItem);
                }
            }

            // 合并文本：相同 TextStyle 并入，否则新建条目
            foreach (var (style, srcItem) in other._textAccum)
            {
                if (_textAccum.TryGetValue(style, out var destItem))
                {
                    for (int i = 0; i < srcItem.TextList.Count; i++)
                        destItem.TextList.Add(srcItem.TextList[i]);
                    for (int i = 0; i < srcItem.PassList.Count; i++)
                        destItem.PassList.Add(srcItem.PassList[i]);
                }
                else
                {
                    _textAccum[style] = srcItem;
                    _items.Add(srcItem);
                }
            }
        }

        // ===== 查询 =====

        /// <summary>获取所有显示项（自动冻结后返回）</summary>
        public IReadOnlyList<DisplayItemBase> Items
        {
            get
            {
                EnsureFinalized();
                return _items;
            }
        }

        public bool HasItems => _items.Count > 0;

        // ===== 内部辅助 =====

        private void EnsureFinalized()
        {
            if (_isFinalized) return;
            foreach (var item in _graphicsAccum.Values)
                item.Freeze();
            foreach (var item in _textAccum.Values)
                item.Freeze();
            _isFinalized = true;
        }

        private GraphicsDisplayItem GetOrCreateGraphicsItem(GraphicsStyle style, GraphicsType type)
        {
            if (!_graphicsAccum.TryGetValue(style, out var item))
            {
                item = new GraphicsDisplayItem
                {
                    Style = style,
                    PresentationKey = style.PresentationKey,
                    Type = type
                };
                _graphicsAccum[style] = item;
                _items.Add(item);
            }
            return item;
        }

        private void AppendGeometry(GraphicsDisplayItem item, object geom, bool isPass)
        {
            item.GeometryList.Add(geom);
            item.PassList.Add(isPass);
        }

        private TextDisplayItem GetOrCreateTextItem(TextStyle style)
        {
            if (!_textAccum.TryGetValue(style, out var item))
            {
                item = new TextDisplayItem
                {
                    Style = style,
                    PresentationKey = style.PresentationKey,
                };
                _textAccum[style] = item;
                _items.Add(item);
            }
            return item;
        }

        private void AppendText(TextDisplayItem item, double x, double y, string content, bool isPass = true,
            double screenOffsetX = 0.0, double screenOffsetY = 0.0)
        {
            item.TextList.Add(new DisplayTextData
            {
                Content = content, X = x, Y = y,
                ScreenOffsetX = screenOffsetX, ScreenOffsetY = screenOffsetY,
            });
            item.PassList.Add(isPass);

            if (item.TextList.Count == 1)
            {
                item.AnchorX = x;
                item.AnchorY = y;

                // 检测：如果文本位置与样式位置一致，认定为样式驱动
                // （工具显式使用 style.PositionX/Y 作为锚点，如 Blob 面积列表）
                if (item.Style != null &&
                    Math.Abs(x - item.Style.PositionX) < 0.001 &&
                    Math.Abs(y - item.Style.PositionY) < 0.001)
                {
                    item.IsPositionStyleDriven = true;
                }
            }
        }
    }
}
