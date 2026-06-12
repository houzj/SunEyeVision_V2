using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Tool.Subroutine.Models
{
    /// <summary>
    /// 子程序显示配置项（对齐 21001 图像显示模式）
    /// </summary>
    public class SubroutineDisplayItem : ObservableObject
    {
        private string _displayName = string.Empty;
        private SubroutineDisplayType _displayType = SubroutineDisplayType.Circle;
        private string _mappedSource = string.Empty;
        private AvailableDataSource? _boundDataSource;
        private bool _isAccumulated;
        private string _bindingSource = string.Empty;
        private string _bindingDisplayText = string.Empty;
        private GraphicsStyle _graphicsStyle = new()
        {
            Name = "子程序显示",
            LineWidth = 2,
            OkColor = Color.FromRgb(0, 255, 0)
        };
        private TextStyle? _textStyle;

        /// <summary>参数名称</summary>
        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        /// <summary>显示类型</summary>
        public SubroutineDisplayType DisplayType
        {
            get => _displayType;
            set => SetProperty(ref _displayType, value);
        }

        /// <summary>映射源显示文本（关联关系）</summary>
        public string MappedSource
        {
            get => _mappedSource;
            set => SetProperty(ref _mappedSource, value);
        }

        /// <summary>绑定的数据源</summary>
        public AvailableDataSource? BoundDataSource
        {
            get => _boundDataSource;
            set
            {
                if (SetProperty(ref _boundDataSource, value))
                    MappedSource = value?.GetDisplayText() ?? string.Empty;
            }
        }

        /// <summary>是否累积显示</summary>
        public bool IsAccumulated
        {
            get => _isAccumulated;
            set => SetProperty(ref _isAccumulated, value);
        }

        /// <summary>参数类型（null 表示不限制，显示所有图形和图像数据源）</summary>
        public Type? ParamType => null;

        /// <summary>绑定源路径（格式：节点ID.属性名），对接 ParamBinding.BindingSource</summary>
        public string BindingSource
        {
            get => _bindingSource;
            set => SetProperty(ref _bindingSource, value);
        }

        /// <summary>绑定显示文本（格式：节点名.属性名），对接 ParamBinding.BindingDisplayText</summary>
        public string BindingDisplayText
        {
            get => _bindingDisplayText;
            set => SetProperty(ref _bindingDisplayText, value);
        }

        /// <summary>图形样式（Point/Line/Rectangle/Circle/Arc/Polygon/Contour/Arrow/Cross/CoordinateSystem 使用）</summary>
        public GraphicsStyle GraphicsStyle
        {
            get => _graphicsStyle;
            set => SetProperty(ref _graphicsStyle, value);
        }

        /// <summary>文本样式（Text 类型使用）</summary>
        public TextStyle? TextStyle
        {
            get => _textStyle;
            set => SetProperty(ref _textStyle, value);
        }

        /// <summary>是否为图形类型（非 Text/Image 时显示 DispItemColorControl）</summary>
        public bool IsGraphicType => DisplayType != SubroutineDisplayType.Text
                                   && DisplayType != SubroutineDisplayType.Image;

        /// <summary>是否为文本类型（Text 时显示 DispTextColorControl）</summary>
        public bool IsTextType => DisplayType == SubroutineDisplayType.Text;
    }

    /// <summary>
    /// 子程序显示类型（对齐 GraphicsType 枚举 + Text + Image）
    /// </summary>
    public enum SubroutineDisplayType
    {
        Point,
        Line,
        Rectangle,
        Circle,
        Arc,
        Polygon,
        Contour,
        Arrow,
        Cross,
        CoordinateSystem,
        Text,
        Image
    }

    /// <summary>显示类型选项（带中文显示名）</summary>
    public class DisplayTypeOption
    {
        public string DisplayName { get; }
        public SubroutineDisplayType Value { get; }

        public DisplayTypeOption(string displayName, SubroutineDisplayType value)
        {
            DisplayName = displayName;
            Value = value;
        }
    }

    /// <summary>SubroutineDisplayType 扩展方法</summary>
    public static class SubroutineDisplayTypeExtensions
    {
        private static readonly Dictionary<SubroutineDisplayType, string> ChineseNames = new()
        {
            [SubroutineDisplayType.Point] = "点",
            [SubroutineDisplayType.Line] = "直线",
            [SubroutineDisplayType.Rectangle] = "矩形",
            [SubroutineDisplayType.Circle] = "圆",
            [SubroutineDisplayType.Arc] = "圆弧",
            [SubroutineDisplayType.Polygon] = "多边形",
            [SubroutineDisplayType.Contour] = "轮廓",
            [SubroutineDisplayType.Arrow] = "箭头",
            [SubroutineDisplayType.Cross] = "十字",
            [SubroutineDisplayType.CoordinateSystem] = "坐标系",
            [SubroutineDisplayType.Text] = "文本",
            [SubroutineDisplayType.Image] = "图像",
        };

        public static string GetChineseName(this SubroutineDisplayType type)
            => ChineseNames.TryGetValue(type, out var name) ? name : type.ToString();

        public static List<DisplayTypeOption> GetAllOptions()
            => ChineseNames.Select(kvp => new DisplayTypeOption(kvp.Value, kvp.Key)).ToList();
    }
}
