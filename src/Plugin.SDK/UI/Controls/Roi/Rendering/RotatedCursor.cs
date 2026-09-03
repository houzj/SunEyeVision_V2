using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Input;
using System.Windows.Interop;
using Microsoft.Win32.SafeHandles;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Rendering
{
    /// <summary>
    /// 旋转缩放光标工厂：自绘双向箭头并按屏幕方向角旋转，生成 WPF Cursor。
    /// </summary>
    /// <remarks>
    /// WPF 内置 Cursors（SizeNS/SizeWE/SizeNWSE/SizeNESW）均为固定屏幕方向，无法跟随几何旋转。
    /// 本类采用业界标准做法（三步链路）：
    ///   ① 自绘双向箭头（System.Drawing）
    ///   ② GDI+ RotateTransform 绕中心旋转
    ///   ③ Bitmap.GetHicon → GetIconInfo → CreateIconIndirect → CursorInteropHelper.Create
    /// 结果按 5° 量化缓存（双向箭头 180° 周期 → 最多 36 个缓存项），避免拖拽时每帧重建与句柄泄漏。
    /// </remarks>
    public static class RotatedCursor
    {
        private const int CanvasSize = 32;
        private const int CacheStepDeg = 5;

        private static readonly Dictionary<int, Cursor> Cache = new();

        /// <summary>
        /// 获取指向指定屏幕方向的双向缩放箭头光标。
        /// </summary>
        /// <param name="angleDegrees">屏幕方向角（度，0=水平向右，顺时针为正，y 向下）。双向箭头 180° 周期，自动归一化。</param>
        public static Cursor GetBidirectional(double angleDegrees)
        {
            int bucket = Quantize(angleDegrees);
            if (Cache.TryGetValue(bucket, out var cached))
                return cached;

            using var bmp = DrawArrow(bucket);
            var cursor = BitmapToCursor(bmp);
            Cache[bucket] = cursor;
            return cursor;
        }

        /// <summary>双向箭头 180° 周期，归一化到 [0, 180) 后按 CacheStepDeg 量化。</summary>
        private static int Quantize(double angleDegrees)
        {
            double a = angleDegrees % 180.0;
            if (a < 0) a += 180.0;
            int bucket = (int)Math.Round(a / CacheStepDeg) * CacheStepDeg;
            return bucket % 180;
        }

        /// <summary>绘制指向 0°（水平）的双向箭头，再绕中心旋转 angleDegrees（GDI+ 顺时针为正）。</summary>
        private static Bitmap DrawArrow(int angleDegrees)
        {
            var bmp = new Bitmap(CanvasSize, CanvasSize, System.Drawing.Imaging.PixelFormat.Format32bppArgb);
            using (var g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.Clear(Color.Transparent);

                // 绕画布中心旋转
                float c = CanvasSize / 2f;
                g.TranslateTransform(c, c);
                g.RotateTransform(angleDegrees);
                g.TranslateTransform(-c, -c);

                using var pen = new Pen(Color.Black, 1.5f);
                using var fill = new SolidBrush(Color.White);

                const float half = 11f;    // 箭头尖端到中心的距离
                const float head = 5.5f;   // 三角形箭头长度
                const float halfW = 3.8f;  // 三角形箭头半宽

                // 中心锚点
                g.FillEllipse(fill, c - 1.5f, c - 1.5f, 3f, 3f);
                g.DrawEllipse(pen, c - 1.5f, c - 1.5f, 3f, 3f);

                // 左右两个三角箭头 + 中间连杆（水平，0° 方向）
                var leftTip = new PointF(c - half, c);
                var leftBaseTop = new PointF(c - half + head, c - halfW);
                var leftBaseBottom = new PointF(c - half + head, c + halfW);
                g.FillPolygon(fill, new[] { leftTip, leftBaseTop, leftBaseBottom });
                g.DrawPolygon(pen, new[] { leftTip, leftBaseTop, leftBaseBottom });

                var rightTip = new PointF(c + half, c);
                var rightBaseTop = new PointF(c + half - head, c - halfW);
                var rightBaseBottom = new PointF(c + half - head, c + halfW);
                g.FillPolygon(fill, new[] { rightTip, rightBaseTop, rightBaseBottom });
                g.DrawPolygon(pen, new[] { rightTip, rightBaseTop, rightBaseBottom });

                g.DrawLine(pen, c - half + head, c, c + half - head, c);
            }
            return bmp;
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
