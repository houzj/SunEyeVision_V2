using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.UI.Controls.Common.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Roi.Rendering;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Interactions
{
    /// <summary>
    /// 圆弧引导交互策略
    /// </summary>
    /// <remarks>
    /// 圆弧参数化：起点(P1) + 终点(P2) + 半径 + 方向 + 大小弧。
    /// 三个手柄：P1 起点手柄 / P2 终点手柄 / P3 弧中点手柄（拖 P3 三点定圆提取半径/方向/大小弧）。
    /// </remarks>
    public class ArcGuideInteraction : IGuideGeometryInteraction
    {
        /// <inheritdoc/>
        public MeasureGeometryType GeometryType => MeasureGeometryType.Arc;

        /// <inheritdoc/>
        public bool UpdatePreview(MeasureInteractionModel model, Point start, Point current)
        {
            if ((current - start).Length <= 1e-6) return false;
            return model.SetArcFromEndpoints(start, current);
        }

        /// <inheritdoc/>
        public bool Complete(MeasureInteractionModel model, Point start, Point current)
        {
            if ((current - start).Length <= 1e-3) return false;
            return model.SetArcFromEndpoints(start, current);
        }

        /// <inheritdoc/>
        public bool EditHandle(MeasureInteractionModel model, HandleType handleType, Point current)
        {
            if (!model.EditingArc.HasValue || model.EditingArc.Value.IsEmpty) return false;
            switch (handleType)
            {
                case HandleType.LineStart:
                    return model.UpdateArcEndpoint(true, current);      // 拖 P1（半径/方向/大小弧不变）
                case HandleType.LineEnd:
                    return model.UpdateArcEndpoint(false, current);     // 拖 P2（半径/方向/大小弧不变）
                case HandleType.Right:
                    return model.UpdateArcRadiusFromPoint(current);     // 拖 P3：三点定圆提取半径/方向/大小弧
                default:
                    return false;
            }
        }

        /// <inheritdoc/>
        public EditHandle[] CreateHandles(MeasureInteractionModel model, double zoom)
        {
            if (!model.EditingArc.HasValue || model.EditingArc.Value.IsEmpty)
                return Array.Empty<EditHandle>();

            var arc = model.EditingArc.Value;
            var size = HandleRenderer.GetScaledHandleSize(zoom);
            var p1 = model.ArcPoint1;
            var p2 = model.ArcPoint2;
            var p3 = arc.GetPointOnArc(arc.StartAngle + arc.AngularSpan / 2);

            var handles = new List<EditHandle>
            {
                new() { Type = HandleType.LineStart, Position = p1, HandleSize = size, Cursor = Cursors.SizeAll },
                new() { Type = HandleType.LineEnd, Position = p2, HandleSize = size, Cursor = Cursors.SizeAll },
                new() { Type = HandleType.Right, Position = p3, HandleSize = size, Cursor = Cursors.SizeAll },
            };

            foreach (var h in handles)
            {
                h.Bounds = new Rect(h.Position.X - size / 2, h.Position.Y - size / 2, size, size);
            }
            return handles.ToArray();
        }

        /// <inheritdoc/>
        public bool HitTestGuide(MeasureInteractionModel model, Point point, double tolerance)
        {
            if (!model.EditingArc.HasValue || model.EditingArc.Value.IsEmpty) return false;
            var a = model.EditingArc.Value;
            var dist = Math.Abs((point - a.Center).Length - a.Radius);
            if (dist > tolerance) return false;
            // 角度范围判断（含跨 0）
            double angle = Math.Atan2(point.Y - a.Center.Y, point.X - a.Center.X);
            return GeometryToolkit.AngleInRange(angle, a.StartAngle, a.EndAngle, tolerance / a.Radius);
        }
    }
}
