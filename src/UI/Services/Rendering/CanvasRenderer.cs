using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.UI.Shared;
using SunEyeVision.UI.Services.Canvas;
using SunEyeVision.UI.Services.Rendering;
using static SunEyeVision.UI.Services.Canvas.CanvasConfig;

namespace SunEyeVision.UI.Services.Rendering
{
    /// <summary>
    /// 画布渲染器 - 使用 DrawingContext 直接绘制，减少视觉树开销
    /// </summary>
    public class CanvasRenderer
    {
        private readonly WriteableBitmap _bitmap;
        private readonly DrawingVisual _drawingVisual;
        private readonly RenderTargetBitmap _renderTarget;

        private double _scale = 1.0;
        private Point _offset = new Point(0, 0);
        private Rect _viewport;
        private bool _isDirty = true;

        /// <summary>
        /// 缩放比例
        /// </summary>
        public double Scale
        {
            get => _scale;
            set
            {
                if (Math.Abs(_scale - value) > 0.001)
                {
                    _scale = value;
                    _isDirty = true;
                }
            }
        }

        /// <summary>
        /// 平移偏移
        /// </summary>
        public Point Offset
        {
            get => _offset;
            set
            {
                if (Math.Abs(_offset.X - value.X) > 0.001 || Math.Abs(_offset.Y - value.Y) > 0.001)
                {
                    _offset = value;
                    _isDirty = true;
                }
            }
        }

        /// <summary>
        /// 视口边界
        /// </summary>
        public Rect Viewport => _viewport;

        public CanvasRenderer(double width, double height)
        {
            _bitmap = new WriteableBitmap(
                (int)width,
                (int)height,
                96,
                96,
                PixelFormats.Pbgra32,
                null
            );

            _drawingVisual = new DrawingVisual();
            _renderTarget = new RenderTargetBitmap(
                (int)width,
                (int)height,
                96,
                96,
                PixelFormats.Pbgra32
            );

            _viewport = new Rect(0, 0, width, height);
        }

        /// <summary>
        /// 渲染节点和连接线
        /// </summary>
        public ImageSource Render(
            IEnumerable<WorkflowNode> nodes,
            IEnumerable<WorkflowConnection> connections)
        {
            if (!_isDirty)
                return _renderTarget;

            using (var dc = _drawingVisual.RenderOpen())
            {
                dc.PushTransform(new ScaleTransform(_scale, _scale));
                dc.PushTransform(new TranslateTransform(_offset.X, _offset.Y));

                RenderGrid(dc);

                RenderConnections(dc, connections);

                RenderNodes(dc, nodes);

                dc.Pop();
                dc.Pop();
            }

            _renderTarget.Render(_drawingVisual);
            _isDirty = false;

            return _renderTarget;
        }

        /// <summary>
        /// 标记为需要重新渲染
        /// </summary>
        public void Invalidate()
        {
            _isDirty = true;
        }

        /// <summary>
        /// 更新视口大小
        /// </summary>
        public void UpdateViewport(double width, double height)
        {
            _viewport = new Rect(0, 0, width, height);
            _isDirty = true;
        }

        private void RenderGrid(DrawingContext dc)
        {
            var gridColor = (Color)ColorConverter.ConvertFromString("#E0E0E0");
            var pen = new Pen(new SolidColorBrush(gridColor), 0.5);

            var startX = (int)(_viewport.X / Drag.SnapGridSize) * Drag.SnapGridSize;
            var startY = (int)(_viewport.Y / Drag.SnapGridSize) * Drag.SnapGridSize;
            var endX = _viewport.Right;
            var endY = _viewport.Bottom;

            for (double x = startX; x <= endX; x += Drag.SnapGridSize)
            {
                dc.DrawLine(pen, new Point(x, _viewport.Top), new Point(x, _viewport.Bottom));
            }

            for (double y = startY; y <= endY; y += Drag.SnapGridSize)
            {
                dc.DrawLine(pen, new Point(_viewport.Left, y), new Point(_viewport.Right, y));
            }
        }

        private void RenderConnections(DrawingContext dc, IEnumerable<WorkflowConnection> connections)
        {
            var connectionBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(CanvasConfig.Connection.DefaultColor));
            var pen = new Pen(connectionBrush, CanvasConfig.Connection.DefaultThickness);

            foreach (var connection in connections)
            {
                var pathGeometry = CreateConnectionPath(connection);
                dc.DrawGeometry(null, pen, pathGeometry);

                RenderArrow(dc, connection);
            }
        }

        private void RenderNodes(DrawingContext dc, IEnumerable<WorkflowNode> nodes)
        {
            foreach (var node in nodes)
            {
                RenderNode(dc, node);
            }
        }

        private void RenderNode(DrawingContext dc, WorkflowNode node)
        {
            var bounds = new Rect(
                node.Position.X,
                node.Position.Y,
                CanvasConfig.Node.DefaultWidth,
                CanvasConfig.Node.DefaultHeight
            );

            var backgroundBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(CanvasConfig.Node.DefaultBackgroundColor));
            var borderBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(CanvasConfig.Node.DefaultBackgroundColor));
            var borderPen = new Pen(borderBrush, CanvasConfig.Node.SelectionBorderThickness);

            var roundedRect = new RectangleGeometry(bounds);
            roundedRect.RadiusX = CanvasConfig.Node.CornerRadius;
            roundedRect.RadiusY = CanvasConfig.Node.CornerRadius;
            dc.DrawGeometry(backgroundBrush, borderPen, roundedRect);

            RenderNodeHeader(dc, node);
            RenderNodeBody(dc, node);
            RenderNodeFooter(dc, node);
        }

        private void RenderNodeHeader(DrawingContext dc, WorkflowNode node)
        {
            var headerBounds = new Rect(
                node.Position.X,
                node.Position.Y,
                CanvasConfig.Node.DefaultWidth,
                30
            );

            var headerBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0066CC"));
            var headerRect = new RectangleGeometry(headerBounds);
            headerRect.RadiusX = 3;
            headerRect.RadiusY = 3;
            dc.DrawGeometry(headerBrush, null, headerRect);

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
            var text = new FormattedText(
                node.Name,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                12,
                Brushes.White,
                96.0
            );

            dc.DrawText(text, new Point(node.Position.X + 30, node.Position.Y + 8));
        }

        private void RenderNodeBody(DrawingContext dc, WorkflowNode node)
        {
            var bodyBounds = new Rect(
                node.Position.X,
                node.Position.Y + 30,
                CanvasConfig.Node.DefaultWidth,
                CanvasConfig.Node.DefaultHeight - 60
            );

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var text = new FormattedText(
                node.ToolType,
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#0066CC")),
                96.0
            );

            var textSize = new Size(text.Width, text.Height);
            var textPosition = new Point(
                bodyBounds.X + (bodyBounds.Width - textSize.Width) / 2,
                bodyBounds.Y + (bodyBounds.Height - textSize.Height) / 2
            );

            dc.DrawText(text, textPosition);
        }

        private void RenderNodeFooter(DrawingContext dc, WorkflowNode node)
        {
            var footerBounds = new Rect(
                node.Position.X,
                node.Position.Y + CanvasConfig.Node.DefaultHeight - 30,
                CanvasConfig.Node.DefaultWidth,
                30
            );

            var footerBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#F0F8FF"));
            var footerRect = new RectangleGeometry(footerBounds);
            footerRect.RadiusX = 0;
            footerRect.RadiusY = 3;
            dc.DrawGeometry(footerBrush, null, footerRect);

            var typeface = new Typeface(new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);
            var text = new FormattedText(
                "⏱ 0ms",
                System.Globalization.CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
                10,
                new SolidColorBrush((Color)ColorConverter.ConvertFromString("#666666")),
                96.0
            );

            var textSize = new Size(text.Width, text.Height);
            var textPosition = new Point(
                footerBounds.X + (footerBounds.Width - textSize.Width) / 2,
                footerBounds.Y + (footerBounds.Height - textSize.Height) / 2
            );

            dc.DrawText(text, textPosition);
        }

        private void RenderArrow(DrawingContext dc, WorkflowConnection connection)
        {
            var targetNode = GetTargetNode(connection);
            if (targetNode == null)
                return;

            var arrowBrush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(CanvasConfig.Connection.ArrowColor));
            var arrowGeometry = CreateArrowGeometry(connection, targetNode);
            dc.DrawGeometry(arrowBrush, null, arrowGeometry);
        }

        private PathGeometry CreateConnectionPath(WorkflowConnection connection)
        {
            var sourceNode = GetSourceNode(connection);
            var targetNode = GetTargetNode(connection);

            if (sourceNode == null || targetNode == null)
                return new PathGeometry();

            var sourcePos = new Point(
                sourceNode.Position.X + CanvasConfig.Node.DefaultWidth / 2,
                sourceNode.Position.Y + CanvasConfig.Node.DefaultHeight / 2
            );
            var targetPos = new Point(
                targetNode.Position.X + CanvasConfig.Node.DefaultWidth / 2,
                targetNode.Position.Y + CanvasConfig.Node.DefaultHeight / 2
            );

            var pathGeometry = new PathGeometry();
            var pathFigure = new PathFigure
            {
                StartPoint = sourcePos,
                IsClosed = false
            };

            var midPoint1 = new Point(sourcePos.X, sourcePos.Y + (targetPos.Y - sourcePos.Y) / 2);
            var midPoint2 = new Point(targetPos.X, sourcePos.Y + (targetPos.Y - sourcePos.Y) / 2);

            pathFigure.Segments.Add(new BezierSegment(midPoint1, midPoint2, targetPos, true));
            pathGeometry.Figures.Add(pathFigure);

            return pathGeometry;
        }

        private PathGeometry CreateArrowGeometry(WorkflowConnection connection, WorkflowNode targetNode)
        {
            var targetPos = new Point(
                targetNode.Position.X + CanvasConfig.Node.DefaultWidth / 2,
                targetNode.Position.Y + CanvasConfig.Node.DefaultHeight / 2
            );

            var arrowSize = CanvasConfig.Connection.ArrowSize;
            var pathGeometry = new PathGeometry();
            var pathFigure = new PathFigure
            {
                StartPoint = targetPos,
                IsClosed = true
            };

            pathFigure.Segments.Add(new LineSegment(new Point(targetPos.X - arrowSize, targetPos.Y - arrowSize / 2), true));
            pathFigure.Segments.Add(new LineSegment(new Point(targetPos.X - arrowSize, targetPos.Y + arrowSize / 2), true));
            pathFigure.Segments.Add(new LineSegment(targetPos, true));

            pathGeometry.Figures.Add(pathFigure);

            return pathGeometry;
        }

        private WorkflowNode? GetSourceNode(WorkflowConnection connection)
        {
            return null;
        }

        private WorkflowNode? GetTargetNode(WorkflowConnection connection)
        {
            return null;
        }
    }
}
