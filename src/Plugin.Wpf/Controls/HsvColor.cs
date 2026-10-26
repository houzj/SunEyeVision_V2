using System;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Controls
{
    /// <summary>
    /// HSV颜色模型
    /// </summary>
    /// <remarks>
    /// HSV模型包含色相(H)、饱和度(S)、明度(V)三个分量。
    /// - H (Hue): 色相，范围 0-360 度
    /// - S (Saturation): 饱和度，范围 0-1
    /// - V (Value): 明度，范围 0-1
    /// </remarks>
    public class HsvColor
    {
        /// <summary>
        /// 色相（0-360度）
        /// </summary>
        public double H { get; set; }

        /// <summary>
        /// 饱和度（0-1）
        /// </summary>
        public double S { get; set; }

        /// <summary>
        /// 明度（0-1）
        /// </summary>
        public double V { get; set; }

        /// <summary>
        /// 创建HSV颜色
        /// </summary>
        public HsvColor(double h, double s, double v)
        {
            H = h;
            S = s;
            V = v;
        }

        /// <summary>
        /// 从WPF Color转换为HSV
        /// </summary>
        public static HsvColor FromColor(Color color)
        {
            double r = color.R / 255.0;
            double g = color.G / 255.0;
            double b = color.B / 255.0;

            double max = Math.Max(Math.Max(r, g), b);
            double min = Math.Min(Math.Min(r, g), b);
            double delta = max - min;

            double h = 0;
            double s = max == 0 ? 0 : delta / max;
            double v = max;

            if (delta != 0)
            {
                if (max == r)
                {
                    h = 60 * (((g - b) / delta) % 6);
                }
                else if (max == g)
                {
                    h = 60 * (((b - r) / delta) + 2);
                }
                else if (max == b)
                {
                    h = 60 * (((r - g) / delta) + 4);
                }
            }

            if (h < 0)
                h += 360;

            return new HsvColor(h, s, v);
        }

        /// <summary>
        /// 转换为WPF Color
        /// </summary>
        public Color ToColor()
        {
            double c = V * S;
            double x = c * (1 - Math.Abs((H / 60.0) % 2 - 1));
            double m = V - c;

            double r1, g1, b1;

            if (H >= 0 && H < 60)
            {
                r1 = c;
                g1 = x;
                b1 = 0;
            }
            else if (H >= 60 && H < 120)
            {
                r1 = x;
                g1 = c;
                b1 = 0;
            }
            else if (H >= 120 && H < 180)
            {
                r1 = 0;
                g1 = c;
                b1 = x;
            }
            else if (H >= 180 && H < 240)
            {
                r1 = 0;
                g1 = x;
                b1 = c;
            }
            else if (H >= 240 && H < 300)
            {
                r1 = x;
                g1 = 0;
                b1 = c;
            }
            else
            {
                r1 = c;
                g1 = 0;
                b1 = x;
            }

            byte r = (byte)Math.Round((r1 + m) * 255);
            byte g = (byte)Math.Round((g1 + m) * 255);
            byte b = (byte)Math.Round((b1 + m) * 255);

            return Color.FromRgb(r, g, b);
        }

        /// <summary>
        /// 克隆当前对象
        /// </summary>
        public HsvColor Clone()
        {
            return new HsvColor(H, S, V);
        }

        /// <summary>
        /// 限制值在有效范围内
        /// </summary>
        public void Clamp()
        {
            H = H < 0 ? 0 : (H > 360 ? 360 : H);
            S = S < 0 ? 0 : (S > 1 ? 1 : S);
            V = V < 0 ? 0 : (V > 1 ? 1 : V);
        }
    }
}
