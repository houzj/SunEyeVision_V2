using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Serialization;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Models
{
    public class SubroutineDisplayItem : ObservableObject
    {
        public Guid FieldId { get; set; } = Guid.NewGuid();
        private string _displayName = string.Empty;
        private SubroutineDisplayType _displayType = SubroutineDisplayType.Circle;
        private string _mappedSource = string.Empty;
        private AvailableDataSource? _boundDataSource;
        private bool _isAccumulated;
        private string _bindingSource = string.Empty;
        private string _bindingDisplayText = string.Empty;
        private GraphicsStyle _graphicsStyle = new()
        {
            Name = "\u5b50\u7a0b\u5e8f\u663e\u793a",
            LineWidth = 2,
            OkColor = Color.FromRgb(0, 255, 0)
        };
        private TextStyle? _textStyle;

        public string DisplayName
        {
            get => _displayName;
            set => SetProperty(ref _displayName, value);
        }

        public SubroutineDisplayType DisplayType
        {
            get => _displayType;
            set
            {
                if (SetProperty(ref _displayType, value))
                    OnPropertyChanged(nameof(ParamType));
            }
        }

        public string MappedSource
        {
            get => _mappedSource;
            set => SetProperty(ref _mappedSource, value);
        }

        public AvailableDataSource? BoundDataSource
        {
            get => _boundDataSource;
            set
            {
                if (SetProperty(ref _boundDataSource, value))
                    MappedSource = value?.GetDisplayText() ?? string.Empty;
            }
        }

        public bool IsAccumulated
        {
            get => _isAccumulated;
            set => SetProperty(ref _isAccumulated, value);
        }

        /// <summary>
        /// 显示项可绑定的数据源类型。绑定控件 ParamBindingBase.FilterDataSourcesByType 依据此类型
        /// 过滤数据源列表（标量 T 与数组 T[] 经 SdkTypeVocabulary 归并到同一词汇）。
        /// 箭头/坐标系当前渲染不支持，不过滤（返回 null = 显示全部，保持遗留行为）。
        /// </summary>
        [JsonIgnore]
        public Type? ParamType => DisplayType switch
        {
            SubroutineDisplayType.Circle => typeof(Circle2d[]),
            SubroutineDisplayType.Arc => typeof(Arc2d[]),
            SubroutineDisplayType.Point => typeof(Geometry.Point2d[]),
            SubroutineDisplayType.Cross => typeof(Geometry.Point2d[]),
            SubroutineDisplayType.Line => typeof(Line2d[]),
            SubroutineDisplayType.Polygon => typeof(Polygon2d[]),
            SubroutineDisplayType.Contour => typeof(Polygon2d[]),
            SubroutineDisplayType.Rectangle => typeof(RotatedRect2d[]),
            SubroutineDisplayType.Image => typeof(IVisionImage),
            SubroutineDisplayType.Text => typeof(double[]),
            _ => null,
        };

        public string BindingSource
        {
            get => _bindingSource;
            set => SetProperty(ref _bindingSource, value);
        }

        public string BindingDisplayText
        {
            get => _bindingDisplayText;
            set => SetProperty(ref _bindingDisplayText, value);
        }

        public GraphicsStyle GraphicsStyle
        {
            get => _graphicsStyle;
            set => SetProperty(ref _graphicsStyle, value);
        }

        public TextStyle? TextStyle
        {
            get => _textStyle;
            set => SetProperty(ref _textStyle, value);
        }

        public bool IsGraphicType => DisplayType != SubroutineDisplayType.Text && DisplayType != SubroutineDisplayType.Image;
        public bool IsTextType => DisplayType == SubroutineDisplayType.Text;
    }

    public enum SubroutineDisplayType
    {
        Point, Line, Rectangle, Circle, Arc, Polygon, Contour, Arrow, Cross, CoordinateSystem, Text, Image
    }

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

    public static class SubroutineDisplayTypeExtensions
    {
        private static readonly Dictionary<SubroutineDisplayType, string> ChineseNames = new()
        {
            [SubroutineDisplayType.Point] = "\u70b9",
            [SubroutineDisplayType.Line] = "\u76f4\u7ebf",
            [SubroutineDisplayType.Rectangle] = "\u77e9\u5f62",
            [SubroutineDisplayType.Circle] = "\u5706",
            [SubroutineDisplayType.Arc] = "\u5706\u5f27",
            [SubroutineDisplayType.Polygon] = "\u591a\u8fb9\u5f62",
            [SubroutineDisplayType.Contour] = "\u8f6e\u5ed3",
            [SubroutineDisplayType.Arrow] = "\u7bad\u5934",
            [SubroutineDisplayType.Cross] = "\u5341\u5b57",
            [SubroutineDisplayType.CoordinateSystem] = "\u5750\u6807\u7cfb",
            [SubroutineDisplayType.Text] = "\u6587\u672c",
            [SubroutineDisplayType.Image] = "\u56fe\u50cf",
        };

        public static string GetChineseName(this SubroutineDisplayType type)
            => ChineseNames.TryGetValue(type, out var name) ? name : type.ToString();

        public static List<DisplayTypeOption> GetAllOptions()
            => ChineseNames.Select(kvp => new DisplayTypeOption(kvp.Value, kvp.Key)).ToList();
    }
}
