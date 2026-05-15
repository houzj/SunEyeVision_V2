using System;
using System.Linq;
using HalconDotNet;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.UI.Services.Workflow
{
    /// <summary>
    /// 窗口渲染器 - 将 DisplayCollector 渲染到 HWindow 句柄
    /// 支持 Static（DispObj）/ Interactive（HDrawingObject）双模式
    /// 对齐 Mage3D 的 AlgorithmModle.DispGraphics 模式：直接操作 Halcon 句柄，不经过中间层
    /// </summary>
    public static class WindowRenderer
    {
        /// <summary>
        /// 渲染 DisplayCollector 到 HWindow
        /// </summary>
        /// <param name="hWindow">目标窗口句柄</param>
        /// <param name="baseImage">背景图像</param>
        /// <param name="collector">显示收集器</param>
        /// <param name="mode">渲染模式（Static: DispObj, Interactive: HDrawingObject）</param>
        public static void Render(HWindow? hWindow, HImage? baseImage, DisplayCollector? collector,
            RenderMode mode = RenderMode.Static)
        {
            if (hWindow == null || !hWindow.IsInitialized())
                return;

            // ★ 使用 AttachBackgroundToWindow：图像在背景层，覆盖图形在前景层
            //   ClearWindow 只清前景层（旧覆盖），背景图像保留
            if (baseImage != null && baseImage.IsInitialized())
            {
                hWindow.AttachBackgroundToWindow(baseImage);
                hWindow.ClearWindow();  // 清前景层（旧覆盖图形/文字）
            }
            else
            {
                hWindow.ClearWindow();
            }

            if (collector?.Items == null || collector.Items.Count == 0)
                return;

            // ★ Interactive 模式委托给 DrawingObjectRenderer
            if (mode == RenderMode.Interactive)
            {
                // 注意：调用者（NodeResultManager）需要在切换 Interactive 前创建 DrawingObjectRenderer
                // 此方法仅用于委托，由 NodeResultManager 管理 DrawingObjectRenderer 生命周期
                throw new InvalidOperationException(
                    "Interactive 模式必须通过 NodeResultManager 托管 DrawingObjectRenderer，不能直接调用 WindowRenderer.Render");
            }

            // ===== Static 模式：统一 GeometryToHObject + DispObj =====
            hWindow.SetDraw("margin");

            foreach (var item in collector.Items)
            {
                if (!item.IsVisible) continue;

                if (item is GraphicsDisplayItem gfx)
                    RenderGraphicsItemStatic(hWindow, gfx);
                else if (item is TextDisplayItem txt)
                    RenderTextItem(hWindow, txt);
            }
        }

        /// <summary>
        /// 内部渲染方法（供 NodeResultManager 在 Interactive 模式使用 DrawingObjectRenderer 时调用）
        /// </summary>
        internal static void RenderStaticGraphics(HWindow hWindow, DisplayCollector collector)
        {
            hWindow.SetDraw("margin");
            foreach (var item in collector.Items)
            {
                if (!item.IsVisible) continue;
                if (item is GraphicsDisplayItem gfx)
                    RenderGraphicsItemStatic(hWindow, gfx);
                else if (item is TextDisplayItem txt)
                    RenderTextItem(hWindow, txt);
            }
        }

        private static void RenderGraphicsItemStatic(HWindow hWindow, GraphicsDisplayItem item)
        {
            var style = item.Style;
            if (style == null || item.Geometries.Length == 0) return;

            hWindow.SetLineWidth(style.LineWidth);
            hWindow.SetDraw(item.IsFilled ? "fill" : "margin");

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

                // 统一 GeometryToHObject + DispObj
                using var hObj = DrawingObjectRenderer.GeometryToHObject(item.Type, item.Geometries[i], style);
                if (hObj != null && hObj.IsInitialized())
                    hWindow.DispObj(hObj);
            }
        }

        private static void RenderTextItem(HWindow hWindow, TextDisplayItem item)
        {
            var style = item.Style;
            if (style == null || item.Texts.Length == 0) return;

            // 设置字体
            string fontName = "Consolas";
            string fontWeight = style.IsBold ? "Bold" : "Normal";
            HOperatorSet.SetFont(hWindow, $"{fontName}-{fontWeight}-{style.FontSize}");

            for (int i = 0; i < item.Texts.Length; i++)
            {
                var text = item.Texts[i];
                var color = item.IsPassArray.Length > 0 && i < item.IsPassArray.Length
                    ? ColorToHalcon(style.GetEffectiveColor(item.IsPassArray[i]))
                    : ColorToHalcon(style.OkColor);

                // 对齐 Halcon disp_message 本地方法：通过 genParam 控制 box/shadow
                DispMessage(hWindow, text.Content, "image", text.Y, text.X, color, "false");
            }
        }

        /// <summary>对齐 Halcon disp_message 本地方法的 disp_text 封装</summary>
        private static void DispMessage(HWindow hWindow, string message, string coordSystem,
            double row, double col, string color, string box)
        {
            var genParamName = new HTuple();
            var genParamValue = new HTuple();

            if (box == "false")
            {
                genParamName = genParamName.TupleConcat("box");
                genParamValue = genParamValue.TupleConcat("false");
            }
            else if (box != "true")
            {
                genParamName = genParamName.TupleConcat("box_color");
                genParamValue = genParamValue.TupleConcat(box);
            }

            HOperatorSet.DispText(hWindow, message, coordSystem, row, col, color,
                genParamName, genParamValue);
        }

        private static string ColorToHalcon(System.Windows.Media.Color c)
        {
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }
}
