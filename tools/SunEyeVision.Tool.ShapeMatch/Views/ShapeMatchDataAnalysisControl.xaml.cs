using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls;
using WpfPoint = System.Windows.Point;
using WpfRect = System.Windows.Rect;

namespace SunEyeVision.Tool.ShapeMatch.Views;

/// <summary>
/// 轮廓匹配数据分析控件 - 显示匹配结果的 DataGrid + 统计摘要 + 选中高亮
/// </summary>
public partial class ShapeMatchDataAnalysisControl : UserControl, IToolControlDataAnalysis, INotifyPropertyChanged
{
    /// <summary>匹配项列表（DataGrid 绑定）</summary>
    public ObservableCollection<MatchResultItemViewModel> MatchedItems { get; } = new();

    /// <summary>匹配数量</summary>
    public int MatchCount => MatchedItems.Count;

    /// <summary>最佳分数</summary>
    public double BestScore => MatchedItems.Count > 0 ? MatchedItems.Max(m => m.Score) : 0;

    /// <summary>判断状态文本</summary>
    public string JudgmentStatus { get; private set; } = "-";

    /// <summary>判断颜色</summary>
    public string JudgmentColor { get; private set; } = "#666";

    /// <summary>执行耗时(ms)</summary>
    public long ExecutionTime { get; private set; }

    #region 选中匹配项

    private MatchResultItemViewModel? _selectedItem;

    /// <summary>当前选中的匹配项（由 DataGrid SelectionChanged 驱动）</summary>
    public MatchResultItemViewModel? SelectedItem
    {
        get => _selectedItem;
        private set
        {
            if (_selectedItem != value)
            {
                _selectedItem = value;
                NotifyPropertyChanged(nameof(SelectedItem));

                // 触发接口事件：构建高亮元素供图像联动
                SelectionHighlightChanged?.Invoke(this, BuildHighlightItems(_selectedItem));
            }
        }
    }

    /// <inheritdoc/>
    public event EventHandler<IEnumerable<DisplayItemBase>?>? SelectionHighlightChanged;

    public ShapeMatchDataAnalysisControl()
    {
        InitializeComponent();
        DataContext = this;
    }

    /// <summary>DataGrid 行选择变化 → 更新 SelectedItem 属性并触发事件</summary>
    private void MatchDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (MatchDataGrid.SelectedItem is MatchResultItemViewModel item)
            SelectedItem = item;
        else
            SelectedItem = null;
    }

    /// <summary>构建选中匹配项的高亮 DisplayItem（黄色匹配框）</summary>
    private static IEnumerable<DisplayItemBase>? BuildHighlightItems(MatchResultItemViewModel? item)
    {
        if (item == null || item.SourceItem == null) return null;

        var match = item.SourceItem;
        if (!match.BoundingBox.HasValue || match.BoundingBox.Value.Width <= 0 || match.BoundingBox.Value.Height <= 0)
            return null;

        var highlightColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00); // Yellow

        return new DisplayItemBase[]
        {
            new GraphicsDisplayItem
            {
                Name = "HighlightRect",
                Type = GraphicsType.Rectangle,
                Geometries = new object[] { match.BoundingBox.Value },
                Style = new GraphicsStyle { OkColor = highlightColor, LineWidth = 3 }
            }
        };
    }

    #endregion

    #region IToolControlDataAnalysis

    /// <summary>
    /// 工具执行完成后，更新匹配结果数据和统计摘要
    /// </summary>
    public void OnResultUpdated(ToolResults result)
    {
        PluginLogger.Info($"[OnResultUpdated] 收到结果 | Type={result?.GetType().Name ?? "null"}", "ShapeMatchDataAnalysis");

        if (result is ShapeMatchResults matchResult)
        {
            PluginLogger.Info($"[OnResultUpdated] ShapeMatchResults | 匹配数量={matchResult.MatchedItems?.Count ?? 0}", "ShapeMatchDataAnalysis");

            // Step 1: 清空并填充匹配列表数据（保持工具设定的排序顺序）
            MatchedItems.Clear();
            if (matchResult.MatchedItems != null)
            {
                int index = 1;
                foreach (var match in matchResult.MatchedItems)
                {
                    MatchedItems.Add(new MatchResultItemViewModel
                    {
                        Label = index++,
                        TemplateName = match.TemplateName,
                        Score = match.Score,
                        CenterX = match.CenterX,
                        CenterY = match.CenterY,
                        Angle = match.Angle,
                        Scale = match.Scale,
                        JudgmentText = match.JudgmentPassed ? "✓" : "✗",
                        SourceItem = match
                    });
                }
            }

            // 更新统计摘要
            JudgmentStatus = matchResult.IsPass ? "OK ✓" : "NG ✗";
            JudgmentColor = matchResult.IsPass ? "#4CAF50" : "#E91E63";
            ExecutionTime = (long)matchResult.ExecutionTimeMs;

            // Step 2: 通知所有计算属性重新计算
            NotifyPropertyChanged(nameof(MatchCount));
            NotifyPropertyChanged(nameof(BestScore));
            NotifyPropertyChanged(nameof(JudgmentStatus));
            NotifyPropertyChanged(nameof(JudgmentColor));
            NotifyPropertyChanged(nameof(ExecutionTime));

            // 清除选中状态
            SelectedItem = null;
            if (MatchDataGrid.SelectedItem != null)
                MatchDataGrid.SelectedItem = null;

            PluginLogger.Success($"[OnResultUpdated] UI更新完成 | 匹配数={MatchCount} | 最佳分={BestScore:F3}", "ShapeMatchDataAnalysis");
        }
        else
        {
            PluginLogger.Warning($"[OnResultUpdated] 类型不匹配: 期望 ShapeMatchResults, 实际 {result?.GetType().Name ?? "null"}", "ShapeMatchDataAnalysis");
        }
    }

    #endregion

    /// <summary>
    /// 属性变更通知
    /// </summary>
    public event PropertyChangedEventHandler? PropertyChanged;

    private void NotifyPropertyChanged(string propertyName) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
}

/// <summary>
/// 匹配结果项 ViewModel — 用于 DataGrid 绑定显示
/// </summary>
public class MatchResultItemViewModel
{
    /// <summary>序号</summary>
    public int Label { get; set; }

    /// <summary>模板名称</summary>
    public string TemplateName { get; set; } = string.Empty;

    /// <summary>匹配分数</summary>
    public double Score { get; set; }

    /// <summary>中心X</summary>
    public double CenterX { get; set; }

    /// <summary>中心Y</summary>
    public double CenterY { get; set; }

    /// <summary>角度(°)</summary>
    public double Angle { get; set; }

    /// <summary>缩放</summary>
    public double Scale { get; set; }

    /// <summary>判断文本(✓/✗)</summary>
    public string JudgmentText { get; set; } = string.Empty;

    /// <summary>原始匹配结果项（用于选中高亮时提取轮廓和变换矩阵）</summary>
    public ShapeMatchResultItem? SourceItem { get; set; }
}