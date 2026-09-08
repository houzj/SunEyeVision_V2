using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32.SafeHandles;

namespace SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Rendering
{
    /// <summary>
    /// 旋转缩放光标工厂：自绘双向箭头并按屏幕方向角旋转，生成 WPF Cursor。
    /// </summary>
    /// <remarks>
    /// WPF 内置 Cursors（SizeNS/SizeWE/SizeNWSE/SizeNESW）均为固定屏幕方向，无法跟随几何旋转。
    /// 本类采用业界标准做法：
    ///   ① 自绘双向箭头（System.Drawing）
    ///   ② GDI+ RotateTransform 绕中心旋转
    ///   ③ Bitmap.GetHicon → GetIconInfo → CreateIconIndirect → CursorInteropHelper.Create
    /// 样式对齐系统 resize 光标：黑色箭头主体 + 白色描边（深浅背景均清晰可见）。
    /// 尺寸复用 HandleRenderer.GetScaledHandleSize 的幂律公式（屏幕 10~12px），随视口缩放自适应。
    /// 结果按（尺寸档, 角度档）缓存，避免拖拽时每帧重建与句柄泄漏。
    /// </remarks>
    public static class RotatedCursor
    {
        private const int CanvasSize = 32;
        private const int CacheStepDeg = 5;

        private static readonly Dictionary<(int Size, int Angle), Cursor> Cache = new();

        /// <summary>旋转光标独立缓存（键仅为尺寸档；不得并入主 Cache，否则与 0°/180° 双向箭头键冲突）。</summary>
        private static readonly Dictionary<int, Cursor> RotateCache = new();

        /// <summary>
        /// 获取指向指定屏幕方向的双向缩放箭头光标。
        /// </summary>
        /// <param name="angleDegrees">屏幕方向角（度，0=水平向右，顺时针为正，y 向下）。双向箭头 180° 周期，自动归一化。</param>
        /// <param name="scale">视口缩放（用于光标尺寸的幂律自适应，与手柄一致）。</param>
        public static Cursor GetBidirectional(double angleDegrees, double scale = 1.0)
        {
            int size = QuantizeSize(scale);
            int angle = QuantizeAngle(angleDegrees);
            var key = (size, angle);
            if (Cache.TryGetValue(key, out var cached))
                return cached;

            using var bmp = DrawArrow(angle, size);
            var cursor = BitmapToCursor(bmp);
            Cache[key] = cursor;
            return cursor;
        }

        /// <summary>
        /// 获取旋转手柄光标：双箭头环形（缺一小口 + 双向箭头），任意角度旋转操作均适用。
        /// 黑色主体 + 白色描边（深浅背景均清晰），尺寸随视口缩放幂律自适应（与手柄一致）。
        /// </summary>
        public static Cursor GetRotate(double scale = 1.0)
        {
            int size = QuantizeSize(scale);
            if (RotateCache.TryGetValue(size, out var cached))
                return cached;

            using var bmp = DrawRotate(size);
            var cursor = BitmapToCursor(bmp);
            RotateCache[size] = cursor;
            return cursor;
        }

        /// <summary>光标尺寸幂律自适应：与 HandleRenderer.GetScaledHandleSize 同公式，屏幕像素 10~12px。</summary>
        private static int QuantizeSize(double scale)
        {
            double s = Math.Max(scale, 0.01);
            double screenPx = 10.0 / Math.Pow(s, 0.15);
            screenPx = Math.Clamp(screenPx, 10.0, 12.0);
            return (int)Math.Round(screenPx);
        }

        /// <summary>双向箭头 180° 周期，归一化到 [0, 180) 后按 CacheStepDeg 量化。</summary>
        private static int QuantizeAngle(double angleDegrees)
        {
            double a = angleDegrees % 180.0;
            if (a < 0) a += 180.0;
            int bucket = (int)Math.Round(a / CacheStepDeg) * CacheStepDeg;
            return bucket % 180;
        }

        /// <summary>绘制指向 0° 的黑色双向箭头（白色描边），再绕中心旋转 angleDegrees。</summary>
        private static Bitmap DrawArrow(int angleDegrees, int sizePx)
        {
            var bmp = new Bitmap(CanvasSize, CanvasSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                float c = CanvasSize / 2f;
                g.TranslateTransform(c, c);
                g.RotateTransform(angleDegrees);
                g.TranslateTransform(-c, -c);

                // 箭头半长 = 幂律屏幕尺寸（10~12px），三角头/半宽按比例，随缩放自适应
                float half = sizePx;                        // 尖端到中心
                float head = Math.Max(4f, sizePx * 0.45f);  // 三角头长
                float halfW = Math.Max(3f, sizePx * 0.4f);  // 三角半宽

                // 黑色箭头主体 + 白色描边（对齐系统 resize 光标，深浅背景均清晰）
                using var pen = new Pen(Color.White, 2.0f);
                using var fill = new SolidBrush(Color.Black);

                // 中心锚点（小圆）
                float r = 1.5f;
                g.FillEllipse(fill, c - r, c - r, r * 2, r * 2);
                g.DrawEllipse(pen, c - r, c - r, r * 2, r * 2);

                // 左箭头
                var leftTip = new PointF(c - half, c);
                var leftTop = new PointF(c - half + head, c - halfW);
                var leftBottom = new PointF(c - half + head, c + halfW);
                g.FillPolygon(fill, new[] { leftTip, leftTop, leftBottom });
                g.DrawPolygon(pen, new[] { leftTip, leftTop, leftBottom });

                // 右箭头
                var rightTip = new PointF(c + half, c);
                var rightTop = new PointF(c + half - head, c - halfW);
                var rightBottom = new PointF(c + half - head, c + halfW);
                g.FillPolygon(fill, new[] { rightTip, rightTop, rightBottom });
                g.DrawPolygon(pen, new[] { rightTip, rightTop, rightBottom });

                // 中间连杆
                g.DrawLine(pen, c - half + head, c, c + half - head, c);
            }
            return bmp;
        }

        /// <summary>
        /// 绘制旋转光标：完整圆环 + 右下 45° 处一个粗壮箭头沿切向伸出（顺时针旋转语义）。
        /// 与系统加载/旋转图标同构，黑色主体 + 白色描边，深浅背景均清晰。
        /// </summary>
        private static Bitmap DrawRotate(int sizePx)
        {
            var bmp = new Bitmap(CanvasSize, CanvasSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                float c = CanvasSize / 2f;
                float ringR = Math.Max(5f, sizePx * 0.62f);   // 环半径（足尺寸，清晰可辨）
                float penW = Math.Max(3f, sizePx * 0.34f);    // 环粗细（加粗，缩略图也不失真）

                // 完整圆环（白色外描边 + 黑色主体）
                using (var white = new Pen(Color.White, penW + 3f))
                using (var black = new Pen(Color.Black, penW))
                {
                    g.DrawEllipse(white, c - ringR, c - ringR, ringR * 2, ringR * 2);
                    g.DrawEllipse(black, c - ringR, c - ringR, ringR * 2, ringR * 2);
                }

                // 右下 45° 处切向箭头：绕中心顺时针旋转的语义
                const float arrowAngle = 45f;
                double rad = arrowAngle * Math.PI / 180;
                // 顺时针切向（角度增大方向）：dir = (-sin, cos)（屏幕 y 向下）
                float tx = (float)(-Math.Sin(rad));
                float ty = (float)(Math.Cos(rad));
                float px = (float)(c + ringR * Math.Cos(rad));
                float py = (float)(c + ringR * Math.Sin(rad));

                float arrowLen = Math.Max(5f, sizePx * 0.7f);  // 箭头伸出长度（明显）
                float halfW = Math.Max(2.5f, sizePx * 0.30f);  // 箭头底半宽
                float headBack = 1.5f;                          // 底边微缩回，让箭头更立体

                // 箭头底边两侧端点（垂直于切向）
                float perpX = ty;
                float perpY = -tx;
                var tip = new PointF(px + tx * arrowLen, py + ty * arrowLen);
                var b1 = new PointF(px + perpX * halfW - tx * headBack, py + perpY * halfW - ty * headBack);
                var b2 = new PointF(px - perpX * halfW - tx * headBack, py - perpY * halfW - ty * headBack);

                using var fill = new SolidBrush(Color.Black);
                using var stroke = new Pen(Color.White, 1.8f);
                g.FillPolygon(fill, new[] { tip, b1, b2 });
                g.DrawPolygon(stroke, new[] { tip, b1, b2 });
            }
            return bmp;
        }

        /// <summary>屏幕角度系（0=右，顺时针为正）圆上一点。</summary>
        private static PointF RingPoint(float c, float r, float deg)
        {
            double rad = deg * Math.PI / 180;
            return new PointF(c + r * (float)Math.Cos(rad), c + r * (float)Math.Sin(rad));
        }

        /// <summary>Bitmap → WPF Cursor（HICON → CreateIconIndirect → CursorInteropHelper.Create）。</summary>
        private static Cursor BitmapToCursor(Bitmap bmp)
        {
            IntPtr hIcon = bmp.GetHicon();
            try
            {
                var info = new IconInfo();
                GetIconInfo(hIcon, ref info);
                info.fIcon = false;                       // false = 光标（cursor），true = 图标（icon）
                info.xHotspot = CanvasSize / 2;           // 热点 = 中心
                info.yHotspot = CanvasSize / 2;

                IntPtr hCursor = CreateIconIndirect(ref info);

                // 释放 GetIconInfo 产生的掩码/颜色位图（已复制进 cursor）
                if (info.hbmColor != IntPtr.Zero) DeleteObject(info.hbmColor);
                if (info.hbmMask != IntPtr.Zero) DeleteObject(info.hbmMask);

                return CursorInteropHelper.Create(new SafeIconHandle(hCursor));
            }
            finally
            {
                DestroyIcon(hIcon);   // 释放 GetHicon 的临时句柄
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct IconInfo
        {
            public bool fIcon;
            public int xHotspot;
            public int yHotspot;
            public IntPtr hbmMask;
            public IntPtr hbmColor;
        }

        [DllImport("user32.dll")]
        private static extern IntPtr CreateIconIndirect(ref IconInfo icon);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DestroyIcon(IntPtr hIcon);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool GetIconInfo(IntPtr hIcon, ref IconInfo pIconInfo);

        [DllImport("gdi32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool DeleteObject(IntPtr hObject);

        /// <summary>持有 cursor 句柄的 SafeHandle，句柄由 Cursor 持有期间免于被回收，最终释放时 DestroyIcon。</summary>
        private sealed class SafeIconHandle : SafeHandleZeroOrMinusOneIsInvalid
        {
            public SafeIconHandle(IntPtr handle) : base(true) { SetHandle(handle); }
            protected override bool ReleaseHandle() => DestroyIcon(handle);
        }
    }
}
