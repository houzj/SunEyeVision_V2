using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Visualization;

namespace SunEyeVision.Tool.ShapeMatch.Models
{
    /// <summary>
    /// 轮廓匹配工具结果判断配置与显示样式
    /// </summary>
    public class ShapeMatchResultConfig : ObservableObject
    {
        #region 匹配数量判断

        private bool _isMatchCountCheckEnabled;
        public bool IsMatchCountCheckEnabled
        {
            get => _isMatchCountCheckEnabled;
            set => SetProperty(ref _isMatchCountCheckEnabled, value, "匹配数量判断");
        }

        private int _matchCountMin = 1;
        public int MatchCountMin
        {
            get => _matchCountMin;
            set => SetProperty(ref _matchCountMin, value, "匹配数量最小值");
        }

        private int _matchCountMax = 10;
        public int MatchCountMax
        {
            get => _matchCountMax;
            set => SetProperty(ref _matchCountMax, value, "匹配数量最大值");
        }

        #endregion

        #region 匹配分数判断

        private bool _isMatchScoreCheckEnabled = true;
        public bool IsMatchScoreCheckEnabled
        {
            get => _isMatchScoreCheckEnabled;
            set => SetProperty(ref _isMatchScoreCheckEnabled, value, "匹配分数判断");
        }

        private double _matchScoreMin;
        public double MatchScoreMin
        {
            get => _matchScoreMin;
            set => SetProperty(ref _matchScoreMin, value, "匹配分数最小值");
        }

        private double _matchScoreMax = 1.0;
        public double MatchScoreMax
        {
            get => _matchScoreMax;
            set => SetProperty(ref _matchScoreMax, value, "匹配分数最大值");
        }

        #endregion

        #region 显示样式

        private GraphicsStyle _matchBoxStyle = new()
        {
            Name = "MatchBox",
            LineWidth = 2,
        };
        public GraphicsStyle MatchBoxStyle
        {
            get => _matchBoxStyle;
            set => SetProperty(ref _matchBoxStyle, value);
        }

        private GraphicsStyle _matchCenterStyle = new()
        {
            Name = "MatchCenter",
            LineWidth = 2,
            CrossSize = 4,
        };
        public GraphicsStyle MatchCenterStyle
        {
            get => _matchCenterStyle;
            set => SetProperty(ref _matchCenterStyle, value);
        }

        private GraphicsStyle _angleLineStyle = new()
        {
            Name = "AngleLine",
            LineWidth = 1,
        };
        public GraphicsStyle AngleLineStyle
        {
            get => _angleLineStyle;
            set => SetProperty(ref _angleLineStyle, value);
        }

        private GraphicsStyle _templateContourStyle = new()
        {
            Name = "TemplateContour",
            LineWidth = 1,
        };
        public GraphicsStyle TemplateContourStyle
        {
            get => _templateContourStyle;
            set => SetProperty(ref _templateContourStyle, value);
        }

        private TextStyle _scoreStyle = new() { Name = "Score", FontSize = 12.0, PositionX = 0, PositionY = 0 };
        public TextStyle ScoreStyle
        {
            get => _scoreStyle;
            set => SetProperty(ref _scoreStyle, value);
        }



        #endregion

        public ShapeMatchResultConfig Clone()
        {
            return new ShapeMatchResultConfig
            {
                IsMatchCountCheckEnabled = _isMatchCountCheckEnabled,
                MatchCountMin = _matchCountMin,
                MatchCountMax = _matchCountMax,
                IsMatchScoreCheckEnabled = _isMatchScoreCheckEnabled,
                MatchScoreMin = _matchScoreMin,
                MatchScoreMax = _matchScoreMax,
                MatchBoxStyle = new GraphicsStyle { Name = _matchBoxStyle.Name, IsVisible = _matchBoxStyle.IsVisible, OkColor = _matchBoxStyle.OkColor, NgColor = _matchBoxStyle.NgColor, Opacity = _matchBoxStyle.Opacity, LineWidth = _matchBoxStyle.LineWidth, CrossSize = _matchBoxStyle.CrossSize },
                MatchCenterStyle = new GraphicsStyle { Name = _matchCenterStyle.Name, IsVisible = _matchCenterStyle.IsVisible, OkColor = _matchCenterStyle.OkColor, NgColor = _matchCenterStyle.NgColor, Opacity = _matchCenterStyle.Opacity, LineWidth = _matchCenterStyle.LineWidth, CrossSize = _matchCenterStyle.CrossSize },
                AngleLineStyle = new GraphicsStyle { Name = _angleLineStyle.Name, IsVisible = _angleLineStyle.IsVisible, OkColor = _angleLineStyle.OkColor, NgColor = _angleLineStyle.NgColor, Opacity = _angleLineStyle.Opacity, LineWidth = _angleLineStyle.LineWidth, CrossSize = _angleLineStyle.CrossSize },
                TemplateContourStyle = new GraphicsStyle { Name = _templateContourStyle.Name, IsVisible = _templateContourStyle.IsVisible, OkColor = _templateContourStyle.OkColor, NgColor = _templateContourStyle.NgColor, Opacity = _templateContourStyle.Opacity, LineWidth = _templateContourStyle.LineWidth, CrossSize = _templateContourStyle.CrossSize },
                ScoreStyle = new TextStyle { Name = _scoreStyle.Name, FontSize = _scoreStyle.FontSize, PositionX = _scoreStyle.PositionX, PositionY = _scoreStyle.PositionY }
            };
        }
    }
}
