using System;

namespace SunEyeVision.Plugin.SDK.Models.Geometry
{
    /// <summary>
    /// 双精度尺寸
    /// </summary>
    public readonly struct Size2d : IEquatable<Size2d>
    {
        public readonly double Width;
        public readonly double Height;

        public Size2d(double width, double height) { Width = width; Height = height; }

        public static Size2d Empty => new Size2d(0, 0);
        public bool IsEmpty => Width <= 0 || Height <= 0;
        public double Area => Width * Height;

        public bool Equals(Size2d other) => Width.Equals(other.Width) && Height.Equals(other.Height);
        public override bool Equals(object? obj) => obj is Size2d other && Equals(other);
        public override int GetHashCode() => HashCode.Combine(Width, Height);
        public override string ToString() => $"Size2d({Width:F1}x{Height:F1})";
    }
}
