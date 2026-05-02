using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Models
{
    /// <summary>
    /// 区域定义基类
    /// </summary>
    [JsonPolymorphic(TypeDiscriminatorPropertyName = "$type")]
    [JsonDerivedType(typeof(ShapeParameters), "Shape")]
    [JsonDerivedType(typeof(FixedRegion), "Fixed")]
    [JsonDerivedType(typeof(ComputedRegion), "Computed")]
    public abstract class RegionDefinition : ObservableObject
    {
        private Guid _id = Guid.NewGuid();
        private RegionSourceMode _mode = RegionSourceMode.Drawing;

        /// <summary>
        /// 区域定义ID
        /// </summary>
        public Guid Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 定义模式
        /// </summary>
        public RegionSourceMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        /// <summary>
        /// 获取形状类型（如果适用）
        /// </summary>
        public abstract ShapeType? GetShapeType();

        /// <summary>
        /// 克隆
        /// </summary>
        public abstract RegionDefinition Clone();
    }

    /// <summary>
    /// 形状参数 - 用户绘制的形状参数
    /// </summary>
    public class ShapeParameters : RegionDefinition
    {
        #region 私有字段

        private ShapeType _shapeType = ShapeType.Rectangle;
        private double _centerX = 0;
        private double _centerY = 0;
        private double _width = 100;
        private double _height = 100;
        private double _angle = 0;
        private double _radius = 50;
        private double _outerRadius = 0;
        private double _startX = 0;
        private double _startY = 0;
        private double _endX = 100;
        private double _endY = 100;
        private double _startAngle = 0;
        private double _endAngle = 0;
        private uint _fillColorArgb = 0x00000000;  // 完全透明
        private uint _strokeColorArgb = 0xFF0000FF; // 蓝色 RGB(0,0,255)
        private double _strokeThickness = RegionEditorSettings.Default.DefaultStrokeThickness;
        private double _opacity = 0.3;

        #endregion

        #region 通用参数

        /// <summary>
        /// 形状类型
        /// </summary>
        public ShapeType ShapeType
        {
            get => _shapeType;
            set => SetProperty(ref _shapeType, value, "形状类型");
        }

        /// <summary>
        /// 中心点X坐标
        /// </summary>
        public double CenterX
        {
            get => _centerX;
            set => SetProperty(ref _centerX, value, "中心点X坐标");
        }

        /// <summary>
        /// 中心点Y坐标
        /// </summary>
        public double CenterY
        {
            get => _centerY;
            set => SetProperty(ref _centerY, value, "中心点Y坐标");
        }

        /// <summary>
        /// 宽度（矩形、旋转矩形）
        /// </summary>
        public double Width
        {
            get => _width;
            set => SetProperty(ref _width, value, "宽度");
        }

        /// <summary>
        /// 高度（矩形、旋转矩形）
        /// </summary>
        public double Height
        {
            get => _height;
            set => SetProperty(ref _height, value, "高度");
        }

        /// <summary>
        /// 旋转角度（度数，数学角度系统：[-180°, 180°]，逆时针为正）
        /// </summary>
        public double Angle
        {
            get => _angle;
            set => SetProperty(ref _angle, value, "角度");
        }

        /// <summary>
        /// 半径（圆形、圆环）
        /// </summary>
        public double Radius
        {
            get => _radius;
            set
            {
                SetProperty(ref _radius, value, "半径");
                if (ShapeType == ShapeType.Circle)
                {
                    // 同步 Width/Height，保证 GetShapePosition 定位正确
                    _width = value * 2;
                    _height = value * 2;
                    OnPropertyChanged(nameof(Width));
                    OnPropertyChanged(nameof(Height));
                }
            }
        }

        /// <summary>
        /// 外半径（圆环）
        /// </summary>
        public double OuterRadius
        {
            get => _outerRadius;
            set => SetProperty(ref _outerRadius, value, "外半径");
        }

        #endregion

        #region 直线参数

        /// <summary>
        /// 起点X坐标（直线）
        /// </summary>
        public double StartX
        {
            get => _startX;
            set => SetProperty(ref _startX, value, "起点X坐标");
        }

        /// <summary>
        /// 起点Y坐标（直线）
        /// </summary>
        public double StartY
        {
            get => _startY;
            set => SetProperty(ref _startY, value, "起点Y坐标");
        }

        /// <summary>
        /// 终点X坐标（直线）
        /// </summary>
        public double EndX
        {
            get => _endX;
            set => SetProperty(ref _endX, value, "终点X坐标");
        }

        /// <summary>
        /// 终点Y坐标（直线）
        /// </summary>
        public double EndY
        {
            get => _endY;
            set => SetProperty(ref _endY, value, "终点Y坐标");
        }

        #endregion

        #region 多边形参数

        /// <summary>
        /// 多边形顶点（使用 ObservableCollection 支持 ItemsControl 自动刷新）
        /// </summary>
        public ObservableCollection<Point2D> Points { get; set; } = new();

        #endregion

        #region 弧形参数

        /// <summary>
        /// 起始角度（弧形）
        /// </summary>
        public double StartAngle
        {
            get => _startAngle;
            set => SetProperty(ref _startAngle, value, "起始角度");
        }

        /// <summary>
        /// 结束角度（弧形）
        /// </summary>
        public double EndAngle
        {
            get => _endAngle;
            set => SetProperty(ref _endAngle, value, "结束角度");
        }

        #endregion

        #region 样式属性（参考ROI编辑器）

        /// <summary>
        /// 填充颜色（ARGB）
        /// </summary>
        public uint FillColorArgb
        {
            get => _fillColorArgb;
            set => SetProperty(ref _fillColorArgb, value, "填充颜色");
        }

        /// <summary>
        /// 边框颜色（ARGB）
        /// </summary>
        public uint StrokeColorArgb
        {
            get => _strokeColorArgb;
            set => SetProperty(ref _strokeColorArgb, value, "边框颜色");
        }

        /// <summary>
        /// 边框厚度
        /// </summary>
        public double StrokeThickness
        {
            get => _strokeThickness;
            set => SetProperty(ref _strokeThickness, value, "边框厚度");
        }

        /// <summary>
        /// 透明度 (0-1)
        /// </summary>
        public double Opacity
        {
            get => _opacity;
            set => SetProperty(ref _opacity, value, "透明度");
        }

        #endregion

        public ShapeParameters()
        {
            Mode = RegionSourceMode.Drawing;
        }

        public override ShapeType? GetShapeType() => ShapeType;

        /// <summary>
        /// 获取中心点
        /// </summary>
        public Point2D GetCenter() => new Point2D(CenterX, CenterY);

        /// <summary>
        /// 设置中心点
        /// </summary>
        public void SetCenter(Point2D center)
        {
            CenterX = center.X;
            CenterY = center.Y;
        }

        /// <summary>
        /// 获取起点（直线）
        /// </summary>
        public Point2D GetStartPoint() => new Point2D(StartX, StartY);

        /// <summary>
        /// 设置起点（直线）
        /// </summary>
        public void SetStartPoint(Point2D point)
        {
            StartX = point.X;
            StartY = point.Y;
        }

        /// <summary>
        /// 获取终点（直线）
        /// </summary>
        public Point2D GetEndPoint() => new Point2D(EndX, EndY);

        /// <summary>
        /// 设置终点（直线）
        /// </summary>
        public void SetEndPoint(Point2D point)
        {
            EndX = point.X;
            EndY = point.Y;
        }

        /// <summary>
        /// 获取直线长度
        /// </summary>
        public double GetLineLength()
        {
            if (ShapeType != ShapeType.Line) return 0;
            var dx = EndX - StartX;
            var dy = EndY - StartY;
            return Math.Sqrt(dx * dx + dy * dy);
        }

        /// <summary>
        /// 获取直线角度（数学角度系统）
        /// </summary>
        public double GetLineAngle()
        {
            if (ShapeType != ShapeType.Line) return 0;
            var dx = EndX - StartX;
            var dy = EndY - StartY;
            // 数学角度：逆时针为正，屏幕坐标系Y轴向下，需要取负
            return -Math.Atan2(dy, dx) * 180 / Math.PI;
        }

        public override RegionDefinition Clone()
        {
            var clone = new ShapeParameters
            {
                Id = Id,
                Mode = Mode,
                ShapeType = ShapeType,
                CenterX = CenterX,
                CenterY = CenterY,
                Width = Width,
                Height = Height,
                Angle = Angle,
                Radius = Radius,
                OuterRadius = OuterRadius,
                StartX = StartX,
                StartY = StartY,
                EndX = EndX,
                EndY = EndY,
                StartAngle = StartAngle,
                EndAngle = EndAngle,
                FillColorArgb = FillColorArgb,
                StrokeColorArgb = StrokeColorArgb,
                StrokeThickness = StrokeThickness,
                Opacity = Opacity
            };
            foreach (var point in Points)
            {
                clone.Points.Add(point.Clone());
            }
            return clone;
        }
    }

    /// <summary>
    /// 固定区域 - 订阅其他节点的区域输出
    /// </summary>
    public class FixedRegion : RegionDefinition
    {
        /// <summary>
        /// 源节点ID
        /// </summary>
        public string SourceNodeId { get; set; } = string.Empty;

        /// <summary>
        /// 输出端口名称
        /// </summary>
        public string OutputName { get; set; } = string.Empty;

        /// <summary>
        /// 区域索引（用于多区域输出）
        /// </summary>
        public int? RegionIndex { get; set; }

        public FixedRegion()
        {
            Mode = RegionSourceMode.Subscribe;
        }

        public FixedRegion(string nodeId, string outputName, int? index = null)
        {
            Mode = RegionSourceMode.Subscribe;
            SourceNodeId = nodeId;
            OutputName = outputName;
            RegionIndex = index;
        }

        public override ShapeType? GetShapeType() => null;

        public override RegionDefinition Clone()
        {
            return new FixedRegion(SourceNodeId, OutputName, RegionIndex)
            {
                Id = Id
            };
        }
    }

    /// <summary>
    /// 计算区域 - 每个参数可单独绑定
    /// </summary>
    public class ComputedRegion : RegionDefinition
    {
        /// <summary>
        /// 目标形状类型
        /// </summary>
        public ShapeType TargetShapeType { get; set; } = ShapeType.Rectangle;

        /// <summary>
        /// 参数绑定字典（参数名 -> 参数源）
        /// </summary>
        public Dictionary<string, ParameterSource> ParameterBindings { get; set; } = new();

        public ComputedRegion()
        {
            Mode = RegionSourceMode.Subscribe;
        }

        public override ShapeType? GetShapeType() => TargetShapeType;

        /// <summary>
        /// 设置参数绑定
        /// </summary>
        public void SetParameterBinding(string parameterName, ParameterSource source)
        {
            ParameterBindings[parameterName] = source;
        }

        /// <summary>
        /// 获取参数绑定
        /// </summary>
        public ParameterSource? GetParameterBinding(string parameterName)
        {
            return ParameterBindings.TryGetValue(parameterName, out var source) ? source : null;
        }

        /// <summary>
        /// 移除参数绑定
        /// </summary>
        public void RemoveParameterBinding(string parameterName)
        {
            ParameterBindings.Remove(parameterName);
        }

        /// <summary>
        /// 解析参数值
        /// </summary>
        public object? ResolveParameter(string parameterName, IParameterContext? context)
        {
            var source = GetParameterBinding(parameterName);
            return source?.GetValue(context);
        }

        public override RegionDefinition Clone()
        {
            var clone = new ComputedRegion
            {
                Id = Id,
                TargetShapeType = TargetShapeType
            };
            foreach (var kvp in ParameterBindings)
            {
                clone.ParameterBindings[kvp.Key] = kvp.Value.Clone();
            }
            return clone;
        }
    }
}
