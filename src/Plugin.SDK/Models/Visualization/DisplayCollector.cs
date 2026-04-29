using System;
using System.Collections.Generic;
using System.Linq;
using OpenCvSharp;
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
    /// Add 时即时构建 DisplayItem 并加入 Items，无需 Complete() 合并。
    /// </remarks>
    public sealed class DisplayCollector
    {
        private readonly List<DisplayItemBase> _items = new();

        // 按 Style 引用分组（同 Style 的几何数据合并到同一个 GraphicsDisplayItem）
        private readonly Dictionary<GraphicsStyle, GraphicsDisplayItem> _graphicsAccum = new();
        private readonly Dictionary<TextStyle, TextDisplayItem> _textAccum = new();

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
        public void AddCross(GraphicsStyle style, Point2d center)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Cross);
            AppendGeometry(item, center, true);
        }

        /// <summary>批量添加十字标记</summary>
        public void AddCrosses(GraphicsStyle style, IReadOnlyList<Point2d> centers)
        {
            var item = GetOrCreateGraphicsItem(style, GraphicsType.Cross);
            foreach (var center in centers)
                AppendGeometry(item, center, true);
        }

        // ===== 文本按需收集 =====

        /// <summary>添加文本数组（每个元素独立定位）</summary>
        public void AddText(TextStyle style, double[] x, double[] y, string[] content, bool isPass = true)
        {
            var item = GetOrCreateTextItem(style);
            for (int i = 0; i < content.Length; i++)
                AppendText(item, x[i], y[i], content[i]);
        }

        /// <summary>在指定锚点位置自动换行显示多行文本</summary>
        public void AddText(TextStyle style, double x, double y, string[] content, bool isPass = true)
        {
            var item = GetOrCreateTextItem(style);
            foreach (var c in content)
                AppendText(item, x, y, c);
        }

        /// <summary>在指定位置显示单条文本</summary>
        public void AddText(TextStyle style, double x, double y, string content, bool isPass = true)
        {
            var item = GetOrCreateTextItem(style);
            AppendText(item, x, y, content);
        }

        // ===== 生命周期 =====

        /// <summary>清空收集器（算法开始前调用）</summary>
        public void Clear()
        {
            _items.Clear();
            _graphicsAccum.Clear();
            _textAccum.Clear();
        }

        // ===== 查询 =====

        public IReadOnlyList<DisplayItemBase> Items => _items;
        public bool HasItems => _items.Count > 0;

        // ===== 内部辅助 =====

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
            var list = item.Geometries.ToList();
            list.Add(geom);
            item.Geometries = list.ToArray();

            // 始终维护 IsPassArray，与 Geometries 一一对应
            var passList = item.IsPassArray.ToList();
            passList.Add(isPass);
            item.IsPassArray = passList.ToArray();
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

        private void AppendText(TextDisplayItem item, double x, double y, string content)
        {
            var list = item.Texts.ToList();
            list.Add(new DisplayTextData { Content = content, X = x, Y = y });
            item.Texts = list.ToArray();

            // 更新锚点：首个文本的坐标即为锚点
            if (list.Count == 1)
            {
                item.AnchorX = x;
                item.AnchorY = y;
            }
        }
    }
}
