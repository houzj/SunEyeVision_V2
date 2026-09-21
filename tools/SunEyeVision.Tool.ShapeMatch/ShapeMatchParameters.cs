using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Tool.ShapeMatch.Models;

namespace SunEyeVision.Tool.ShapeMatch;

/// <summary>
/// 轮廓匹配工具参数 — 对齐 Halcon 形状匹配
/// </summary>
[JsonDerivedType(typeof(ShapeMatchParameters), "ShapeMatch")]
public class ShapeMatchParameters : RoiParameters
{
    #region 匹配参数

    /// <summary>最小匹配分数（0~1）</summary>
    [DisplayName("最小匹配分数")]
    public double MinMatchScore { get; set; } = 0.5;

    /// <summary>最大匹配数量</summary>
    [DisplayName("最大匹配数量")]
    public int MaxMatchCount { get; set; } = 1;

    /// <summary>匹配超时（毫秒）</summary>
    [DisplayName("匹配超时(毫秒)")]
    public int MatchTimeoutMs { get; set; } = 30000;

    /// <summary>角度范围起始值（度）</summary>
    [DisplayName("角度起始值")]
    public double AngleStart { get; set; } = -45.0;

    /// <summary>角度范围终止值（度）</summary>
    [DisplayName("角度终止值")]
    public double AngleEnd { get; set; } = 45.0;

    /// <summary>缩放范围最小值</summary>
    [DisplayName("缩放最小值")]
    public double ScaleMin { get; set; } = 0.8;

    /// <summary>缩放范围最大值</summary>
    [DisplayName("缩放最大值")]
    public double ScaleMax { get; set; } = 1.2;

    /// <summary>最大返回匹配数（Halcon NumMatches）</summary>
    [DisplayName("最大返回数")]
    public int NumMatches { get; set; } = 1;

    /// <summary>搜索起始层（0=使用模型全层级，>0 表示从该层开始粗定位）</summary>
    [DisplayName("搜索起始层")]
    public int SearchLevelStart { get; set; } = 0;

    /// <summary>搜索结束层（0=全分辨率，>=1 表示搜索到该层结束细定位）</summary>
    [DisplayName("搜索结束层")]
    public int SearchLevelEnd { get; set; } = 1;

    /// <summary>NMS 最大重叠率 0~1（Halcon MaxOverlap）</summary>
    [DisplayName("最大重叠率")]
    public double MaxOverlap { get; set; } = 0.5;

    /// <summary>贪婪度 0~1（Halcon Greediness）</summary>
    [DisplayName("贪婪度")]
    public double Greediness { get; set; } = 0.9;

    #endregion

    #region 引擎参数（新增 — 对齐 Halcon）

    /// <summary>亚像素模式</summary>
    [DisplayName("亚像素模式")]
    public SubPixelMode SubPixelMode { get; set; } = Models.SubPixelMode.LeastSquares;

    #endregion

    #region 结果排序

    /// <summary>排序方式（默认按分数降序：BestMatch=[0] 必须是最高分，见 M8）</summary>
    [DisplayName("排序方式")]
    public SortMode SortMode { get; set; } = Models.SortMode.ScoreDescending;

    #endregion

    #region 模板管理

    /// <summary>模板列表</summary>
    public ObservableCollection<TemplateModel> Templates { get; set; } = new();

    /// <summary>每模型参数覆盖（多模板时覆盖全局参数）</summary>
    public List<PerModelParamOverride> PerModelOverrides { get; set; } = new();

    #endregion

    #region 结果判断配置

    /// <summary>结果判断配置与显示样式</summary>
    public ResultPresentationConfig ResultPresentation { get; set; } = CreateResultPresentation();

    private static ResultPresentationConfig CreateResultPresentation()
    {
        var config = new ResultPresentationConfig();
        config.Judgments.Add(new() { Key = "match-count", Name = "匹配数量", Enabled = false, Min = 1, Max = 10, AllowedMinimum = 0, AllowedMaximum = 10000, DecimalPlaces = 0, Order = 10 });
        config.Judgments.Add(new() { Key = "best-score", Name = "最佳匹配得分", Enabled = true, Min = 0, Max = 1, AllowedMinimum = 0, AllowedMaximum = 1, DecimalPlaces = 3, AffectsItemDisplayState = true, Order = 20 });
        config.Graphics.Add(new() { Key = "match-box", Name = "匹配框", Order = 10, Style = new() { Name = "MatchBox", LineWidth = 2 } });
        config.Graphics.Add(new() { Key = "match-center", Name = "匹配中心", Order = 20, Style = new() { Name = "MatchCenter", LineWidth = 2, CrossSize = 4 } });
        config.Graphics.Add(new() { Key = "angle-line", Name = "角度指示线", Order = 30, Style = new() { Name = "AngleLine", LineWidth = 1 } });
        config.Graphics.Add(new() { Key = "template-contour", Name = "模板轮廓", Order = 40, Style = new() { Name = "TemplateContour", LineWidth = 1 } });
        config.Texts.Add(new() { Key = "score", Name = "匹配信息", Order = 10, Style = new() { Name = "Score", FontSize = 12.0, PositionX = 0, PositionY = 0 } });
        return config;
    }

    #endregion

    #region 位置修正

    /// <summary>是否启用位置修正</summary>
    [DisplayName("启用位置修正")]
    public bool IsPositionCorrectionEnabled { get; set; } = false;

    /// <summary>位置修正绑定源（运行时，不序列化）</summary>
    [JsonIgnore]
    public AvailableDataSource? PositionCorrectionBindingSource
    {
        get => _positionCorrectionBindingSource;
        set
        {
            if (SetProperty(ref _positionCorrectionBindingSource, value, "位置修正绑定源"))
            {
                _positionCorrectionSource = ImageSourceReference.FromDataSource(value);
            }
        }
    }

    /// <summary>位置修正绑定源（序列化引用）</summary>
    [JsonPropertyOrder(16)]
    [JsonPropertyName("positionCorrectionSource")]
    public ImageSourceReference? PositionCorrectionSource
    {
        get => _positionCorrectionSource;
        set => _positionCorrectionSource = value;
    }

    /// <summary>位置修正绑定源显示文本</summary>
    [JsonPropertyOrder(17)]
    public string? PositionCorrectionBindingDisplayText { get; set; }

    private AvailableDataSource? _positionCorrectionBindingSource;
    private ImageSourceReference? _positionCorrectionSource;

    /// <summary>从 AvailableDataSources 恢复 PositionCorrectionBindingSource</summary>
    public bool RestorePositionCorrectionSource(IEnumerable<AvailableDataSource>? dataSources)
    {
        if (_positionCorrectionSource == null || dataSources == null)
            return false;

        var matched = _positionCorrectionSource.MatchDataSource(dataSources);
        if (matched != null)
        {
            _positionCorrectionBindingSource = matched;
            OnPropertyChanged(nameof(PositionCorrectionBindingSource));
            return true;
        }
        return false;
    }

    #endregion

    #region 参数验证

    public override ValidationResult Validate()
    {
        var result = base.Validate();

        if (MinMatchScore < 0 || MinMatchScore > 1.0)
            result.AddError($"最小匹配分数必须在 0~1 范围内，当前值: {MinMatchScore}");

        if (MaxMatchCount < 1)
            result.AddError($"最大匹配数量必须大于0，当前值: {MaxMatchCount}");

        if (MatchTimeoutMs <= 0)
            result.AddError($"匹配超时必须大于0，当前值: {MatchTimeoutMs}ms");

        if (AngleStart > AngleEnd)
            result.AddError($"角度范围无效: 起始值({AngleStart})不能大于终止值({AngleEnd})");

        if (ScaleMin > ScaleMax)
            result.AddError($"缩放范围无效: 最小值({ScaleMin})不能大于最大值({ScaleMax})");

        if (NumMatches < 1)
            result.AddError($"NumMatches({NumMatches}) 必须 >= 1");

        if (MaxOverlap < 0 || MaxOverlap > 1)
            result.AddError($"MaxOverlap({MaxOverlap}) 必须在 0~1 范围内");

        if (Greediness < 0 || Greediness > 1)
            result.AddError($"Greediness({Greediness}) 必须在 0~1 范围内");

        return result;
    }

    #endregion
}
