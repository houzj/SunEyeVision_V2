using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Plugin.SDK.Models
{
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
            set => SetProperty(ref _displayType, value);
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

        public Type? ParamType => null;

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
            [SubroutineDisplayType.Text] = "鏂囨湰",
            [SubroutineDisplayType.Image] = "鍥惧儚",
        };

        public static string GetChineseName(this SubroutineDisplayType type)
            => ChineseNames.TryGetValue(type, out var name) ? name : type.ToString();

        public static List<DisplayTypeOption> GetAllOptions()
            => ChineseNames.Select(kvp => new DisplayTypeOption(kvp.Value, kvp.Key)).ToList();
    }
}
