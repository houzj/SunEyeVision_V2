using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Roi.Models
{
    /// <summary>
    /// 形状类型枚举
    /// </summary>
    public enum ShapeType
    {
        /// <summary>
        /// 无工具（空闲状态）
        /// </summary>
        None = 0,

        /// <summary>
        /// 点
        /// </summary>
        Point,

        /// <summary>
        /// 直线
        /// </summary>
        Line,

        /// <summary>
        /// 圆形
        /// </summary>
        Circle,

        /// <summary>
        /// 矩形
        /// </summary>
        Rectangle,

        /// <summary>
        /// 旋转矩形
        /// </summary>
        RotatedRectangle,

        /// <summary>
        /// 多边形
        /// </summary>
        Polygon,

        /// <summary>
        /// 圆环
        /// </summary>
        Annulus,

        /// <summary>
        /// 弧形
        /// </summary>
        Arc
    }

    /// <summary>
    /// 区域来源模式
    /// </summary>
    public enum RoiSourceMode
    {
        /// <summary>
        /// 绘制模式 - 区域来源于用户手动绘制
        /// </summary>
        Drawing,

        /// <summary>
        /// 订阅模式 - 区域来源于其他节点的输出
        /// </summary>
        Subscribe
    }

    /// <summary>
    /// 点结构
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
