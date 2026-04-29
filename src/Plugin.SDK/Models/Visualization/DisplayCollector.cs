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
    ///   2. Complete() — 完成收集，合并所有分组项（算法结束后调用一次）
    ///   3. Clear() — 清空收集器（算法开始前调用）
    ///
    /// 设计要点：Style 对象由 Parameters 持有，全链路共享同一实例。
    /// DisplayItem 通过 Style 引用读取渲染属性，无需属性拷贝。
    /// </remarks>
    public sealed class DisplayCollector
    {
        private readonly List<DisplayItemBase> _items = new();

        // 按 Style 引用分组（取代按 Name 字符串分组，天然唯一无碰撞）
        private readonly Dictionary<GraphicsStyle, GraphicsDisplayItem> _graphicsAccum = new();
        private readonly Dictionary<GraphicsStyle, List<bool>> _isPassAccum = new();
        private readonly Dictionary<TextStyle, List<DisplayTextData>> _textAccum = new();

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
            var list = GetOrCreateTextList(style, isPass);
            for (int i = 0; i < content.Length; i++)
                list.Add(new DisplayTextData { Content = content[i], X = x[i], Y = y[i] });
        }

        /// <summary>在指定锚点位置自动换行显示多行文本</summary>
        public void AddText(TextStyle style, double x, double y, string[] content, bool isPass = true)
        {
            var list = GetOrCreateTextList(style, isPass);
            foreach (var c in content)
                list.Add(new DisplayTextData { Content = c, X = x, Y = y });
        }

        /// <summary>在指定位置显示单条文本</summary>
        public void AddText(TextStyle style, double x, double y, string content, bool isPass = true)
        {
            var list = GetOrCreateTextList(style, isPass);
            list.Add(new DisplayTextData { Content = content, X = x, Y = y });
        }

        // ===== 生命周期 =====

        /// <summary>清空收集器（算法开始前调用）</summary>
        public void Clear()
        {
            _items.Clear();
            _graphicsAccum.Clear();
            _isPassAccum.Clear();
            _textAccum.Clear();
        }

        /// <summary>完成收集，合并所有分组项（算法结束后调用）</summary>
        public void Complete()
        {
            // 合并图形项
            foreach (var kvp in _graphicsAccum)
            {
                var item = kvp.Value;
                if (_isPassAccum.TryGetValue(kvp.Key, out var passList) && passList.Any(p => !p))
                    item.IsPassArray = passList.ToArray();
                _items.Add(item);
            }

            // 合并文本项
            foreach (var kvp in _textAccum)
            {
                var style = kvp.Key;
                var texts = kvp.Value;

                bool autoLine = texts.Count > 1 &&
                    texts.All(t => Math.Abs(t.X - texts[0].X) < 0.01 &&
                                   Math.Abs(t.Y - texts[0].Y) < 0.01);
                double anchorX = autoLine ? texts[0].X : 0;
                double anchorY = autoLine ? texts[0].Y : 0;

                _items.Add(new TextDisplayItem
                {
                    Style = style,
                    IsVisible = style.IsVisible,
                    IsPass = true, // 文本整体通过状态，由外部通过 AddText(isPass) 控制
                    Texts = texts.ToArray(),
                    AnchorX = autoLine ? anchorX : 0,
                    AnchorY = autoLine ? anchorY : 0,
                    ZOrder = 20
                });
            }
        }

        // ===== 查询 =====

        public IReadOnlyList<DisplayItemBase> Items => _items;
        public bool HasItems => _items.Count > 0 || _graphicsAccum.Count > 0 || _textAccum.Count > 0;

        // ===== 内部辅助 =====

        private GraphicsDisplayItem GetOrCreateGraphicsItem(GraphicsStyle style, GraphicsType type)
        {
            if (!_graphicsAccum.TryGetValue(style, out var item))
            {
                item = new GraphicsDisplayItem
                {
                    Style = style,
                    Type = type,
                    IsVisible = style.IsVisible,
                    ZOrder = 10
                };
                _graphicsAccum[style] = item;
                _isPassAccum[style] = new List<bool>();
            }
            return item;
        }

        private void AppendGeometry(GraphicsDisplayItem item, object geom, bool isPass)
        {
            var list = item.Geometries.ToList();
            list.Add(geom);
            item.Geometries = list.ToArray();

            var style = item.Style;
            if (style != null && (!isPass || (_isPassAccum.TryGetValue(style, out var passList) && passList.Count > 0)))
            {
                _isPassAccum[style].Add(isPass);
            }
        }

        private List<DisplayTextData> GetOrCreateTextList(TextStyle style, bool isPass = true)
        {
            if (!_textAccum.TryGetValue(style, out var list))
            {
                list = new List<DisplayTextData>();
                _textAccum[style] = list;
            }
            return list;
        }
    }
}
