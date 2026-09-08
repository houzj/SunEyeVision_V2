using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters
{
    /// <summary>
    /// 点结构（可观察，UI 交互/绘制用）。
    /// 注：与 SDK 纯值点 <see cref="SunEyeVision.Plugin.SDK.Models.Geometry.Point2d"/>（readonly struct，算法/契约用）并存，
    /// 本类用于可交互场景（多边形顶点、几何扁平编辑桥、命中坐标）。
    /// </summary>
    public class Point2D : INotifyPropertyChanged, IMonitorable
    {
        private double _x;
        private double _y;

        public double X
        {
            get => _x;
            set
            {
                if (Math.Abs(_x - value) < 0.001) return;
                _x = value;
                OnPropertyChanged();
            }
        }

        public double Y
        {
            get => _y;
            set
            {
                if (Math.Abs(_y - value) < 0.001) return;
                _y = value;
                OnPropertyChanged();
            }
        }

        public Point2D() { }

        public Point2D(double x, double y)
        {
            _x = x;
            _y = y;
        }

        public Point2D Clone() => new Point2D(_x, _y);

        public static Point2D Empty => new Point2D(0, 0);

        public bool IsEmpty => Math.Abs(_x) < 0.001 && Math.Abs(_y) < 0.001;

        public double DistanceTo(Point2D other)
        {
            if (other == null) return double.MaxValue;
            var dx = _x - other._x;
            var dy = _y - other._y;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        public override bool Equals(object? obj)
        {
            if (obj is not Point2D other) return false;
            return Math.Abs(_x - other._x) < 0.001 && Math.Abs(_y - other._y) < 0.001;
        }

        public override int GetHashCode()
        {
            return HashCode.Combine(_x, _y);
        }

        public override string ToString()
        {
            return $"({_x:F2}, {_y:F2})";
        }

        public static bool operator ==(Point2D? left, Point2D? right)
        {
            if (left is null && right is null) return true;
            if (left is null || right is null) return false;
            return left.Equals(right);
        }

        public static bool operator !=(Point2D? left, Point2D? right)
        {
            return !(left == right);
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        protected void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public IReadOnlyList<(string FieldName, object? FieldValue)> GetMonitorFields()
        {
            return new (string, object?)[]
            {
                ("X", _x),
                ("Y", _y),
            };
        }
    }
}
