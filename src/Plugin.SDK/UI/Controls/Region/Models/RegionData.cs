using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Models
{
    /// <summary>
    /// 区域数据 - 顶层容器
    /// </summary>
    public class RegionData : ObservableObject, ICloneable
    {
        private Guid _id = Guid.NewGuid();
        private string _name = string.Empty;
        private RegionDefinition? _definition;
        private bool _isEnabled = true;
        private bool _isVisible = true;
        private bool _isEditable = true;
        private bool _isPreview;
        private string _tag = string.Empty;
        private int _zIndex;
        private uint _displayColor = 0xFFFF0000;
        private double _displayOpacity = 0.3;
        private RegionType _regionType = RegionType.Inspection;
        private DateTime _createdTime = DateTime.Now;
        private DateTime _modifiedTime = DateTime.Now;
        private Dictionary<string, object> _extendedProperties = new();

        /// <summary>
        /// 唯一标识符
        /// </summary>
        public Guid Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        /// <summary>
        /// 区域名称
        /// </summary>
        public string Name
        {
            get => _name;
            set => SetProperty(ref _name, value, "区域名称");
        }

        /// <summary>
        /// 区域参数
        /// </summary>
        public RegionDefinition? Parameters
        {
            get => _definition;
            set => SetProperty(ref _definition, value);
        }

        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled
        {
            get => _isEnabled;
            set => SetProperty(ref _isEnabled, value, "启用状态");
        }

        /// <summary>
        /// 是否可见
        /// </summary>
        public bool IsVisible
        {
            get => _isVisible;
            set => SetProperty(ref _isVisible, value, "可见性");
        }

        /// <summary>
        /// 是否可编辑
        /// </summary>
        public bool IsEditable
        {
            get => _isEditable;
            set => SetProperty(ref _isEditable, value, "可编辑性");
        }

        /// <summary>
        /// 是否为预览状态
        /// </summary>
        public bool IsPreview
        {
            get => _isPreview;
            set => SetProperty(ref _isPreview, value);
        }

        /// <summary>
        /// 标签
        /// </summary>
        public string Tag
        {
            get => _tag;
            set => SetProperty(ref _tag, value);
        }

        /// <summary>
        /// 显示顺序
        /// </summary>
        public int ZIndex
        {
            get => _zIndex;
            set => SetProperty(ref _zIndex, value);
        }

        /// <summary>
        /// 显示颜色（ARGB）
        /// </summary>
        public uint DisplayColor
        {
            get => _displayColor;
            set => SetProperty(ref _displayColor, value, "显示颜色");
        }

        /// <summary>
        /// 显示透明度 (0-1)
        /// </summary>
        public double DisplayOpacity
        {
            get => _displayOpacity;
            set => SetProperty(ref _displayOpacity, value, "显示透明度");
        }

        /// <summary>
        /// 区域类型（检测/屏蔽）
        /// 修改时自动同步 ShapeParameters 的颜色
        /// </summary>
        public RegionType RegionType
        {
            get => _regionType;
            set
            {
                if (SetProperty(ref _regionType, value, "区域类型"))
                {
                    ApplyTypeColors();
                    MarkModified();
                }
            }
        }

        /// <summary>
        /// 根据区域类型自动设置 ShapeParameters 的填充色和边框色
        /// </summary>
        private void ApplyTypeColors()
        {
            if (Parameters is ShapeParameters shape)
            {
                switch (_regionType)
                {
                    case RegionType.Mask:
                        shape.FillColorArgb = 0x55FFA500;   // 半透明橙色 (Alpha=0x55≈33%)
                        shape.StrokeColorArgb = 0xFFFFA500; // 橙色边框
                        shape.Opacity = 1.0;
                        break;
                    case RegionType.Inspection:
                    default:
                        shape.FillColorArgb = 0x00000000;   // 透明填充
                        shape.StrokeColorArgb = 0xFF4488FF; // 蓝色边框
                        shape.Opacity = 1.0;
                        break;
                }
            }
        }

        /// <summary>
        /// 创建时间
        /// </summary>
        public DateTime CreatedTime
        {
            get => _createdTime;
            set => SetProperty(ref _createdTime, value);
        }

        /// <summary>
        /// 最后修改时间
        /// </summary>
        public DateTime ModifiedTime
        {
            get => _modifiedTime;
            set => SetProperty(ref _modifiedTime, value);
        }

        /// <summary>
        /// 扩展属性
        /// </summary>
        public Dictionary<string, object> ExtendedProperties
        {
            get => _extendedProperties;
            set => SetProperty(ref _extendedProperties, value);
        }

        /// <summary>
        /// 获取形状类型
        /// </summary>
        public ShapeType? GetShapeType()
        {
            return Parameters?.GetShapeType();
        }

        /// <summary>
        /// 获取定义模式
        /// </summary>
        public RegionSourceMode GetMode()
        {
            return Parameters?.Mode ?? RegionSourceMode.Drawing;
        }

        /// <summary>
        /// 创建绘制模式的区域数据
        /// </summary>
        public static RegionData CreateDrawingRegion(string name, ShapeType shapeType)
        {
            return new RegionData
            {
                Name = name,
                Parameters = new ShapeParameters { ShapeType = shapeType },
                IsPreview = true
            };
        }

        /// <summary>
        /// 创建订阅模式的区域数据（按区域类型）
        /// </summary>
        public static RegionData CreateSubscribedRegion(string name, string nodeId, string outputName, int? index = null)
        {
            return new RegionData
            {
                Name = name,
                Parameters = new FixedRegion(nodeId, outputName, index)
            };
        }

        /// <summary>
        /// 创建计算模式的区域数据（按参数订阅）
        /// </summary>
        public static RegionData CreateComputedRegion(string name, ShapeType targetShapeType)
        {
            return new RegionData
            {
                Name = name,
                Parameters = new ComputedRegion { TargetShapeType = targetShapeType }
            };
        }

        /// <summary>
        /// 标记已修改
        /// </summary>
        public void MarkModified()
        {
            ModifiedTime = DateTime.Now;
        }

        /// <summary>
        /// 克隆
        /// </summary>
        public object Clone()
        {
            var clone = new RegionData
            {
                Id = Id,
                Name = Name,
                Parameters = (RegionDefinition?)Parameters?.Clone(),
                IsEnabled = IsEnabled,
                IsVisible = IsVisible,
                IsEditable = IsEditable,
                Tag = Tag,
                ZIndex = ZIndex,
                DisplayColor = DisplayColor,
                DisplayOpacity = DisplayOpacity,
                RegionType = RegionType,
                CreatedTime = CreatedTime,
                ModifiedTime = DateTime.Now
            };
            foreach (var kvp in ExtendedProperties)
            {
                clone.ExtendedProperties[kvp.Key] = kvp.Value;
            }
            return clone;
        }

        public override string ToString()
        {
            var shapeType = GetShapeType()?.ToString() ?? "Unknown";
            return $"{Name} ({GetMode()}: {shapeType})";
        }
    }
}
