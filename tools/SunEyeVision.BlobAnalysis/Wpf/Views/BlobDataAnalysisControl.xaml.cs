using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.BlobAnalysis.Views
{
    /// <summary>
    /// Blob 数据分析控件 - 显示连通域特征数据的 DataGrid + 统计摘要 + 选中高亮
    /// </summary>
    public partial class BlobDataAnalysisControl : UserControl, IToolControlDataAnalysis, System.ComponentModel.INotifyPropertyChanged
    {
        /// <summary>Blob 特征数据列表</summary>
        public ObservableCollection<BlobResult> FilteredBlobs { get; } = new();

        /// <summary>筛选后数量</summary>
        public int FilteredBlobCount => FilteredBlobs.Count;

        /// <summary>总面积（px²）</summary>
        public double TotalArea { get; private set; }

        /// <summary>筛选后总面积（px²）</summary>
        public double FilteredTotalArea => FilteredBlobs.Sum(b => b.Area);

        /// <summary>平均面积（px²）</summary>
        public double AverageArea => FilteredBlobs.Count > 0 ? FilteredBlobs.Sum(b => b.Area) / FilteredBlobs.Count : 0;

        /// <summary>最大面积（px²）</summary>
        public double MaxArea => FilteredBlobs.Count > 0 ? FilteredBlobs.Max(b => b.Area) : 0;

        /// <summary>最小面积（px²）</summary>
        public double MinArea => FilteredBlobs.Count > 0 ? FilteredBlobs.Min(b => b.Area) : 0;

        /// <summary>阈值分割实际使用的范围</summary>
        public string ThresholdRangeText { get; private set; } = "-";

        /// <summary>阈值类型</summary>
        public string ThresholdTypeText { get; private set; } = "-";

        #region 选中 Blob

        private BlobResult? _selectedBlob;

        /// <summary>当前选中的 Blob（由 DataGrid SelectionChanged 驱动）</summary>
        public BlobResult? SelectedBlob
        {
            get => _selectedBlob;
            private set
            {
                if (_selectedBlob != value)
                {
                    _selectedBlob = value;
                    NotifyPropertyChanged(nameof(SelectedBlob));

                    // 触发接口事件：构建高亮元素供图像联动
                    SelectionHighlightChanged?.Invoke(this, BuildHighlightItems(_selectedBlob));
                }
            }
        }

        /// <inheritdoc/>
        public event EventHandler<IEnumerable<DisplayItemBase>?>? SelectionHighlightChanged;

        public BlobDataAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        /// <summary>DataGrid 行选择变化 → 更新 SelectedBlob 属性并触发事件</summary>
        private void BlobDataGrid_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (BlobDataGrid.SelectedItem is BlobResult blob)
                SelectedBlob = blob;
            else
                SelectedBlob = null;
        }

        /// <summary>构建选中 Blob 的高亮 DisplayItem（黄色外接矩形）</summary>
        private static IEnumerable<DisplayItemBase>? BuildHighlightItems(BlobResult? blob)
            {
                if (blob == null) return null;

                var highlightColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00); // Yellow
                var bb = blob.BoundingBox;

                return new DisplayItemBase[]
                {
                    new GraphicsDisplayItem
                    {
                        Name = "HighlightRect", Type = GraphicsType.Rectangle,
                        Geometries = new object[] { bb },
                        Style = new GraphicsStyle { OkColor = highlightColor, LineWidth = 3 }
                    }
                };
            }

        #endregion

        #region IToolControlDataAnalysis

        /// <summary>
        /// 工具执行完成后，更新 Blob 特征数据和统计摘要
        /// </summary>
        public void OnResultUpdated(ToolResults result)
        {
            PluginLogger.Info($"[OnResultUpdated] 收到结果 | Type={result?.GetType().Name ?? "null"}", "BlobDataAnalysis");

            if (result is BlobAnalysisResults blobResult)
            {
                PluginLogger.Info($"[OnResultUpdated] BlobAnalysisResults | Blob数量={blobResult.FilteredBlobs?.Count ?? 0} | 总面积={blobResult.FilteredTotalArea:F0}", "BlobDataAnalysis");

                // Step 1: 先清空并填充 Blob 列表数据
                FilteredBlobs.Clear();
                if (blobResult.FilteredBlobs != null)
                {
                    foreach (var blob in blobResult.FilteredBlobs)
                    {
                        FilteredBlobs.Add(blob);
                    }
                }

                // 更新统计摘要（字段）
                TotalArea = blobResult.FilteredTotalArea;
                ThresholdRangeText = $"{blobResult.ThresholdMinUsed}–{blobResult.ThresholdMaxUsed}";
                ThresholdTypeText = blobResult.TypeUsed.ToString();

                // Step 2: 数据就绪后，再通知所有计算属性重新计算
                NotifyPropertyChanged(nameof(FilteredBlobCount));
                NotifyPropertyChanged(nameof(TotalArea));
                NotifyPropertyChanged(nameof(FilteredTotalArea));
                NotifyPropertyChanged(nameof(AverageArea));
                NotifyPropertyChanged(nameof(MaxArea));
                NotifyPropertyChanged(nameof(MinArea));
                NotifyPropertyChanged(nameof(ThresholdRangeText));
                NotifyPropertyChanged(nameof(ThresholdTypeText));

                PluginLogger.Success($"[OnResultUpdated] UI通知完成 | 实际Blob数={FilteredBlobs.Count} | 总面积={FilteredTotalArea:F0}", "BlobDataAnalysis");
            }
            else
            {
                PluginLogger.Warning($"[OnResultUpdated] 类型不匹配: 期望 BlobAnalysisResults, 实际 {result?.GetType().Name ?? "null"}", "BlobDataAnalysis");
            }
        }

        #endregion

        /// <summary>
        /// 属性变更通知
        /// </summary>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void NotifyPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
