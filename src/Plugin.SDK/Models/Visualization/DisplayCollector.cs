using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Models.Geometry;

namespace SunEyeVision.Plugin.SDK.Models.Visualization
{
    /// <summary>
    /// 显示收集器 - Style 引用驱动的按需 Push 收集器
    /// </summary>
    /// <remarks>
    /// 使用方式：
    ///   1. AddRect/AddCross/AddText — 算法过程中按需 Push（第一个参数为 Style 引用）
    ///   2. Clear() — 清空收集器（算法开始前调用）
    ///
    /// 设计要点：Style 对象由 Parameters 持有，全链路共享同一实例。
    /// DisplayItem 通过 Style 引用读取渲染属性，无需属性拷贝。
    /// Add 时即时构建 DisplayItem 并加入 Items（O(1) 追加，Freeze 时一次数组构建）。
    /// </remarks>
    public sealed class DisplayCollector
    {
        private readonly List<DisplayItemBase> _items = new();

        // 按 Style 引用分组（同 Style 的几何数据合并到同一个 GraphicsDisplayItem）
        private readonly Dictionary<GraphicsStyle, GraphicsDisplayItem> _graphicsAccum = new();
        private readonly Dictionary<TextStyle, TextDisplayItem> _textAccum = new();

        private bool _isFinalized;

        // ===== 图形按需收集 =====

        /// <summary>添加矩形</summary>
        public void AddRect(GraphicsStyle style, Rect2d rect, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Rectangle);
            AppendGeometry(item, rect, isPass);
        }

        /// <summary>批量添加矩形</summary>
        public void AddRects(GraphicsStyle style, IReadOnlyList<Rect2d> rects, IReadOnlyList<bool>? isPassList = null)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Rectangle);
            for (int i = 0; i < rects.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                AppendGeometry(item, rects[i], isPass);
            }
        }

        /// <summary>添加十字标记</summary>
        public void AddCross(GraphicsStyle style, Point2d center, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Cross);
            AppendGeometry(item, center, isPass);
        }

        /// <summary>批量添加十字标记</summary>
        public void AddCrosses(GraphicsStyle style, IReadOnlyList<Point2d> centers, IReadOnlyList<bool>? isPassList = null)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Cross);
            for (int i = 0; i < centers.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                AppendGeometry(item, centers[i], isPass);
            }
        }

        /// <summary>添加旋转矩形</summary>
        public void AddRotatedRect(GraphicsStyle style, RotatedRect rect, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.RotatedRectangle);
            AppendGeometry(item, rect, isPass);
        }

        /// <summary>批量添加旋转矩形</summary>
        public void AddRotatedRects(GraphicsStyle style, IReadOnlyList<RotatedRect> rects, IReadOnlyList<bool>? isPassList = null)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.RotatedRectangle);
            for (int i = 0; i < rects.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                AppendGeometry(item, rects[i], isPass);
            }
        }

        /// <summary>添加椭圆</summary>
        public void AddEllipse(GraphicsStyle style, Ellipse2d ellipse, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Ellipse);
            AppendGeometry(item, ellipse, isPass);
        }

        /// <summary>批量添加椭圆</summary>
        public void AddEllipses(GraphicsStyle style, IReadOnlyList<Ellipse2d> ellipses, IReadOnlyList<bool>? isPassList = null)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Ellipse);
            for (int i = 0; i < ellipses.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                AppendGeometry(item, ellipses[i], isPass);
            }
        }

        /// <summary>添加线段</summary>
        public void AddLine(GraphicsStyle style, LineSegment line, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Line);
            AppendGeometry(item, line, isPass);
        }

        /// <summary>批量添加线段</summary>
        public void AddLines(GraphicsStyle style, IReadOnlyList<LineSegment> lines, IReadOnlyList<bool>? isPassList = null)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Line);
            for (int i = 0; i < lines.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                AppendGeometry(item, lines[i], isPass);
            }
        }

        /// <summary>添加箭头</summary>
        public void AddArrow(GraphicsStyle style, ArrowGeometry arrow, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Arrow);
            AppendGeometry(item, arrow, isPass);
        }

        /// <summary>批量添加箭头</summary>
        public void AddArrows(GraphicsStyle style, IReadOnlyList<ArrowGeometry> arrows, IReadOnlyList<bool>? isPassList = null)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Arrow);
            for (int i = 0; i < arrows.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                AppendGeometry(item, arrows[i], isPass);
            }
        }

        /// <summary>添加圆弧</summary>
        public void AddArc(GraphicsStyle style, ArcGeometry arc, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Arc);
            AppendGeometry(item, arc, isPass);
        }

        /// <summary>批量添加圆弧</summary>
        public void AddArcs(GraphicsStyle style, IReadOnlyList<ArcGeometry> arcs, IReadOnlyList<bool>? isPassList = null)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Arc);
            for (int i = 0; i < arcs.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                AppendGeometry(item, arcs[i], isPass);
            }
        }

        /// <summary>添加轮廓</summary>
        public void AddContour(GraphicsStyle style, Point2d[] points, bool isPass = true)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Contour);
            var contourGeom = new ContourGeometry(points, true);
            AppendGeometry(item, contourGeom, isPass);
        }

        /// <summary>批量添加轮廓</summary>
        public void AddContours(GraphicsStyle style, IReadOnlyList<Point2d[]> contoursList, IReadOnlyList<bool>? isPassList = null)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Contour);
            for (int i = 0; i < contoursList.Count; i++)
            {
                var isPass = isPassList != null && i < isPassList.Count ? isPassList[i] : true;
                var contourGeom = new ContourGeometry(contoursList[i], true);
                AppendGeometry(item, contourGeom, isPass);
            }
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

        // ===== 生命周期 =====

        /// <summary>清空收集器（算法开始前调用）</summary>
        public void Clear()
        {
            _items.Clear();
            _graphicsAccum.Clear();
            _textAccum.Clear();
            _isFinalized = false;
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
                    Type = type
                };
                _graphicsAccum[style] = item;
                _items.Add(item);
            }
            return item;
        }

        private void AppendGeometry(GraphicsDisplayItem item, object geom, bool isPass)
        {
            // O(1) 追加到内部列表，Freeze 时一次数组构建
            item.GeometryList.Add(geom);
            item.PassList.Add(isPass);
        }

        private TextDisplayItem GetOrCreateTextItem(TextStyle style)
        {
            if (!_textAccum.TryGetValue(style, out var item))
            {
                item = new TextDisplayItem
                {
                    Style = style
                };
                _textAccum[style] = item;
                _items.Add(item);
            }
            return item;
        }

        private void AppendText(TextDisplayItem item, double x, double y, string content, bool isPass = true)
        {
            // O(1) 追加到内部列表，Freeze 时一次数组构建
            item.TextList.Add(new DisplayTextData { Content = content, X = x, Y = y });
            item.PassList.Add(isPass);

            // 更新锚点：首个文本的坐标即为锚点
            if (item.TextList.Count == 1)
            {
                item.AnchorX = x;
                item.AnchorY = y;
            }
        }
    }
}
