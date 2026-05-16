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

            // ★ 扩大图形栈容量，避免大量 DispObj 调用时前序对象被丢弃（默认 50）
            hWindow.SetWindowParam("graphics_stack_max_element_num", 5000);

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
            hWindow.SetWindowParam("graphics_stack_max_element_num", 5000);
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

            // 合并同色对象到单个 HObject，避免逐个 DispObj 撑爆图形栈（默认上限 50）
            HOperatorSet.GenEmptyObj(out HObject okObj);
            HOperatorSet.GenEmptyObj(out HObject ngObj);

            for (int i = 0; i < item.Geometries.Length; i++)
            {
                // Arrow 特殊处理：DispArrow 直接操作窗口，无法合并
                if (item.Type == GraphicsType.Arrow && item.Geometries[i] is ArrowGeometry arrow)
                {
                    var arrowColor = item.RenderMode == DisplayRenderMode.Individual
                        ? ColorToHalcon(style.GetEffectiveColor(item.IsPassArray[i]))
                        : ColorToHalcon(style.OkColor);
                    hWindow.SetColor(arrowColor);
                    HOperatorSet.DispArrow(hWindow,
                        arrow.Start.Y, arrow.Start.X, arrow.End.Y, arrow.End.X, arrow.HeadSize);
                    continue;
                }

                // 统一 GeometryToHObject
                using var hObj = DrawingObjectRenderer.GeometryToHObject(item.Type, item.Geometries[i], style);
                if (hObj == null || !hObj.IsInitialized()) continue;

                bool isOk = item.RenderMode != DisplayRenderMode.Individual || item.IsPassArray[i];
                if (isOk)
                {
                    HOperatorSet.ConcatObj(okObj, hObj, out HObject newObj);
                    okObj.Dispose();
                    okObj = newObj;
                }
                else
                {
                    HOperatorSet.ConcatObj(ngObj, hObj, out HObject newObj);
                    ngObj.Dispose();
                    ngObj = newObj;
                }
            }

            // 批量 DispObj — OK 对象（一次调用 = 1 个栈位，无论多少个子对象）
            if (okObj.CountObj() > 0)
            {
                HOperatorSet.SetColor(hWindow, ColorToHalcon(style.OkColor));
                HOperatorSet.DispObj(okObj, hWindow);
            }
            // 批量 DispObj — NG 对象
            if (ngObj.CountObj() > 0)
            {
                HOperatorSet.SetColor(hWindow, ColorToHalcon(style.NgColor));
                HOperatorSet.DispObj(ngObj, hWindow);
            }

            okObj.Dispose();
            ngObj.Dispose();
        }

        private static void RenderTextItem(HWindow hWindow, TextDisplayItem item)
        {
            var style = item.Style;
            if (style == null || item.Texts.Length == 0) return;

            // 设置字体
            string fontName = "Consolas";
            string fontWeight = style.IsBold ? "Bold" : "Normal";
            HOperatorSet.SetFont(hWindow, $"{fontName}-{fontWeight}-{style.FontSize}");

            // 按 OK/NG 分组批量 DispText（每组 1 个栈位）
            var okStrings = new HTuple();
            var okRows = new HTuple();
            var okCols = new HTuple();
            var ngStrings = new HTuple();
            var ngRows = new HTuple();
            var ngCols = new HTuple();

            for (int i = 0; i < item.Texts.Length; i++)
            {
                var text = item.Texts[i];
                bool isOk = item.IsPassArray.Length > 0 && i < item.IsPassArray.Length
                    ? item.IsPassArray[i] : true;
                if (isOk)
                {
                    okStrings = okStrings.TupleConcat(text.Content);
                    okRows = okRows.TupleConcat(text.Y);
                    okCols = okCols.TupleConcat(text.X);
                }
                else
                {
                    ngStrings = ngStrings.TupleConcat(text.Content);
                    ngRows = ngRows.TupleConcat(text.Y);
                    ngCols = ngCols.TupleConcat(text.X);
                }
            }

            // 批量显示 OK 文本
            if (okStrings.Length > 0)
            {
                HOperatorSet.DispText(hWindow, okStrings, "image", okRows, okCols,
                    ColorToHalcon(style.OkColor), "box", "false");
            }
            // 批量显示 NG 文本
            if (ngStrings.Length > 0)
            {
                HOperatorSet.DispText(hWindow, ngStrings, "image", ngRows, ngCols,
                    ColorToHalcon(style.NgColor), "box", "false");
            }
        }

        private static string ColorToHalcon(System.Windows.Media.Color c)
        {
            return $"#{c.R:X2}{c.G:X2}{c.B:X2}";
        }
    }
}
