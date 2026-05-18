using System;
using System.Collections.Generic;
using System.Linq;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// HDrawingObject 渲染器 - Interactive 模式下将 DisplayCollector 渲染为可交互的 HDrawingObject
    /// 管理 HDrawingObject 生命周期，支持文字拖拽回写
    /// </summary>
    public sealed class DrawingObjectRenderer
    {
        private readonly List<HDrawingObject> _drawObjs = new();

        // drawId(IntPtr) → (HDrawingObject, TextDisplayItem, int index)
        private readonly Dictionary<IntPtr, (HDrawingObject DrawObj, TextDisplayItem Item, int Index)> _textMap = new();

        // 持久化保存所有文本拖拽回调委托，防止 Halcon native 层调用时被 GC 回收
        private readonly List<HDrawingObject.HDrawingObjectCallback> _textCallbacks = new();

        /// <summary>
        /// 将 DisplayCollector 渲染为混合模式：
        /// - GraphicsDisplayItem → DispObj（静态计算结果图形）
        /// - TextDisplayItem → HDrawingObject（可拖拽文本）
        /// </summary>
        public void RenderInteractive(HWindow hWindow, DisplayCollector collector)
        {
            // 每次渲染前先清理旧的 DrawingObject（仅文本的 DrawingObj）
            DetachAll(hWindow);

            foreach (var item in collector.Items)
            {
                if (!item.IsVisible) continue;

                if (item is GraphicsDisplayItem gfx)
                    RenderGraphicsAsDispObj(hWindow, gfx);
                else if (item is TextDisplayItem txt)
                    CreateTextDrawingObjects(hWindow, txt);
            }
        }

        /// <summary>
        /// 将图形项渲染为 DispObj（计算结果图形，不可交互）
        /// </summary>
        private static void RenderGraphicsAsDispObj(HWindow hWindow, GraphicsDisplayItem item)
        {
            var style = item.Style;
            if (style == null || item.Geometries.Length == 0) return;

            hWindow.SetLineWidth(style.LineWidth);
            hWindow.SetDraw("margin");

            for (int i = 0; i < item.Geometries.Length; i++)
            {
                var color = item.RenderMode == DisplayRenderMode.Individual
                    ? ColorToHalcon(style.GetEffectiveColor(item.IsPassArray[i]))
                    : ColorToHalcon(style.OkColor);

                hWindow.SetColor(color);

                // Arrow 特殊处理：DispArrow 直接操作窗口
                if (item.Type == GraphicsType.Arrow && item.Geometries[i] is ArrowGeometry arrow)
                {
                    HOperatorSet.DispArrow(hWindow,
                        arrow.Start.Y, arrow.Start.X, arrow.End.Y, arrow.End.X, arrow.HeadSize);
                    continue;
                }

                using var hObj = GeometryToHObject(item.Type, item.Geometries[i], style);
                if (hObj != null && hObj.IsInitialized())
                    hWindow.DispObj(hObj);
            }
        }

        private void CreateTextDrawingObjects(HWindow hWindow, TextDisplayItem item)
        {
            var style = item.Style;
            if (style == null) return;

            for (int i = 0; i < item.Texts.Length; i++)
            {
                var td = item.Texts[i];
                var color = item.IsPassArray.Length > i
                    ? ColorToHalcon(style.GetEffectiveColor(item.IsPassArray[i]))
                    : ColorToHalcon(style.OkColor);

                string font = $"Consolas-{(style.IsBold ? "Bold" : "Normal")}-{style.FontSize}";

                var drawObj = new HDrawingObject(0.0, 0.0, 1.0);
                drawObj.CreateDrawingObjectText((int)td.Y, (int)td.X, td.Content);
                drawObj.SetDrawingObjectParams(
                    new HTuple(new[] { "color", "font" }),
                    new HTuple(new[] { color, font }));

                // 拖拽回调：将新位置写回 TextDisplayItem
                int capturedIndex = i;
                IntPtr drawId = (IntPtr)drawObj.ID;
                _textMap[drawId] = (drawObj, item, capturedIndex);

                var callback = new HDrawingObject.HDrawingObjectCallback(OnTextDrag);
                drawObj.OnDrag(callback);
                _textCallbacks.Add(callback);

                hWindow.AttachDrawingObjectToWindow(drawObj);
                _drawObjs.Add(drawObj);
            }
        }

        private void OnTextDrag(IntPtr drawId, IntPtr windowHandle, string type)
        {
            if (!_textMap.TryGetValue(drawId, out var entry)) return;

            var (drawObj, textItem, index) = entry;

            double newRow = drawObj.GetDrawingObjectParams("row");
            double newCol = drawObj.GetDrawingObjectParams("column");
            textItem.Texts[index].X = newCol;
            textItem.Texts[index].Y = newRow;
            textItem.NotifyChanged();
        }

        /// <summary>分离并释放所有 HDrawingObject</summary>
        public void DetachAll(HWindow hWindow)
        {
            if (hWindow == null || !hWindow.IsInitialized())
            {
                // 窗口已无效，直接释放
                foreach (var drawObj in _drawObjs)
                {
                    try { drawObj.Dispose(); }
                    catch { /* 忽略释放异常 */ }
                }
                _drawObjs.Clear();
                _textMap.Clear();
                _textCallbacks.Clear();
                return;
            }

            foreach (var drawObj in _drawObjs)
            {
                try
                {
                    hWindow.DetachDrawingObjectFromWindow(drawObj);
                    drawObj.Dispose();
                }
                catch
                {
                    // 分离失败时仍然尝试释放
                    try { drawObj.Dispose(); }
                    catch { }
                }
            }
            _drawObjs.Clear();
            _textMap.Clear();
            _textCallbacks.Clear();
        }

        // ===== Static 模式回退：将几何数据转为 HObject（与 WindowRenderer 共享逻辑） =====

        /// <summary>
        /// 将几何数据转换为 HObject（用于不支持 HDrawingObject 的类型回退 DispObj）
        /// 或由 WindowRenderer 在 Static 模式下调用
        /// </summary>
        internal static HObject? GeometryToHObject(GraphicsType type, object geometry, GraphicsStyle style)
        {
            switch (type)
            {
                case GraphicsType.Rectangle when geometry is Rect2d r:
                {
                    var region = new HRegion();
                    region.GenRectangle1(r.Y, r.X, r.Y + r.Height, r.X + r.Width);
                    return region;
                }

                case GraphicsType.RotatedRectangle when geometry is RotatedRect rr:
                {
                    var region = new HRegion();
                    region.GenRectangle2(rr.Center.Y, rr.Center.X,
                        rr.AngleRadians, rr.Width / 2, rr.Height / 2);
                    return region;
                }

                case GraphicsType.Circle when geometry is Circle2d c:
                {
                    var region = new HRegion();
                    region.GenCircle(c.Center.Y, c.Center.X, c.Radius);
                    return region;
                }

                case GraphicsType.Ellipse when geometry is Ellipse2d e:
                {
                    var region = new HRegion();
                    region.GenEllipse(e.Center.Y, e.Center.X, e.Phi, e.Radius1, e.Radius2);
                    return region;
                }

                case GraphicsType.Line when geometry is LineSegment l:
                {
                    var xld = new HXLDCont();
                    xld.GenContourPolygonXld(
                        new HTuple(new[] { l.Start.Y, l.End.Y }),
                        new HTuple(new[] { l.Start.X, l.End.X }));
                    return xld;
                }

                case GraphicsType.Cross when geometry is Point2d p:
                {
                    var xld = new HXLDCont();
                    xld.GenCrossContourXld(p.Y, p.X, style.CrossSize, 0.785398);
                    return xld;
                }

                case GraphicsType.Arrow when geometry is ArrowGeometry a:
                {
                    // DispArrow 不返回 HObject，由调用方特殊处理（直接 HOperatorSet.DispArrow）
                    return null;
                }

                case GraphicsType.Contour when geometry is ContourGeometry ct:
                {
                    var rows = ct.Points.Select(p => p.Y).ToArray();
                    var cols = ct.Points.Select(p => p.X).ToArray();
                    var xld = new HXLDCont();
                    xld.GenContourPolygonXld(new HTuple(rows), new HTuple(cols));
                    return xld;
                }

                case GraphicsType.Arc when geometry is ArcGeometry arc:
                {
                    int n = 64;
                    var rows = new double[n + 1];
                    var cols = new double[n + 1];
                    double angleStep = (arc.EndAngle - arc.StartAngle) / n;
                    for (int j = 0; j <= n; j++)
                    {
                        double theta = arc.StartAngle + j * angleStep;
                        rows[j] = arc.Center.Y + arc.Radius * Math.Sin(theta);
                        cols[j] = arc.Center.X + arc.Radius * Math.Cos(theta);
                    }
                    var xld = new HXLDCont();
                    xld.GenContourPolygonXld(new HTuple(rows), new HTuple(cols));
                    return xld;
                }

                default:
                    return null;
            }
        }

        internal static string ColorToHalcon(System.Windows.Media.Color c)
        {
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }
}
