using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using OpenCvSharp;
using SunEyeVision.CvEngine.Models;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.BlobAnalysis.Views
{
    /// <summary>
    /// Blob 数据分析控件 - 显示连通域特征数据的 DataGrid + 统计摘要 + 选中高亮
    /// </summary>
    public partial class BlobDataAnalysisControl : UserControl, IToolControlDataAnalysis
    {
        /// <summary>Blob 特征数据列表</summary>
        public ObservableCollection<BlobInfo> FilteredBlobs { get; } = new();

        /// <summary>Blob总数</summary>
        public int TotalBlobCount { get; private set; }

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

        /// <summary>实际阈值</summary>
        public int ThresholdUsed { get; private set; }

        /// <summary>阈值类型</summary>
        public string ThresholdTypeText { get; private set; } = "-";

        /// <summary>连通性</summary>
        public string ConnectivityText { get; private set; } = "-";

        #region 选中 Blob 相关

        private BlobInfo? _selectedBlob;

        /// <summary>当前选中的 Blob（由 DataGrid SelectionChanged 驱动）</summary>
        public BlobInfo? SelectedBlob
        {
            get => _selectedBlob;
            private set
            {
                if (_selectedBlob != value)
                {
                    _selectedBlob = value;
                    NotifyPropertyChanged(nameof(SelectedBlob));
                    NotifyPropertyChanged(nameof(HasSelectedBlob));
                    NotifyPropertyChanged(nameof(SelectedBlobDetailText));

                    // 触发接口事件：构建高亮元素供图像联动
                    SelectionHighlightChanged?.Invoke(this, BuildHighlightItems(_selectedBlob));
                }
            }
        }

        /// <summary>是否有选中的 Blob</summary>
        public bool HasSelectedBlob => _selectedBlob != null;

        /// <summary>选中 Blob 的详情文本（绑定到底部面板）</summary>
        public string SelectedBlobDetailText
        {
            get
            {
                if (_selectedBlob == null) return string.Empty;
                var bb = _selectedBlob.BoundingBox;
                return $"面积: {_selectedBlob.Area:F0}px²  周长: {_selectedBlob.Perimeter:F1}  圆度: {_selectedBlob.Circularity:F3}  矩形度: {_selectedBlob.Rectangularity:F3}  质心: ({_selectedBlob.CentroidX:F1}, {_selectedBlob.CentroidY:F1})  边框: {bb.Width}×{bb.Height}@({bb.X},{bb.Y})";
            }
        }

        #endregion

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
            if (BlobDataGrid.SelectedItem is BlobInfo blob)
                SelectedBlob = blob;
            else
                SelectedBlob = null;
        }

        /// <summary>构建选中 Blob 的高亮 DisplayItem 列表（黄色外接矩形+标注+质心十字）</summary>
        private static IEnumerable<DisplayItemBase>? BuildHighlightItems(BlobInfo? blob)
            {
                if (blob == null) return null;

                var highlightColor = Color.FromArgb(0xFF, 0xFF, 0xFF, 0x00); // Yellow
                var bb = blob.BoundingBox;

                return new DisplayItemBase[]
                {
                    new GraphicsDisplayItem
                    {
                        Name = "HighlightRect", Type = GraphicsType.Rectangle,
                        Geometries = new object[] { new Rect2d(bb.X, bb.Y, bb.Width, bb.Height) },
                        Style = new GraphicsStyle { OkColor = highlightColor, LineWidth = 3 }
                    },
                    new TextDisplayItem
                    {
                        Name = "HighlightLabel",
                        Texts = new[] { new DisplayTextData { Content =$"#{blob.Label}: {blob.Area:F0}px²", X = bb.X, Y = bb.Y - 5 } },
                        AnchorX = bb.X, AnchorY = bb.Y - 5,
                        Style = new TextStyle { OkColor = highlightColor, FontSize = 14.0 }
                    },
                    new GraphicsDisplayItem
                    {
                        Name = "HighlightPoint", Type = GraphicsType.Point,
                        Geometries = new object[] { new Point2d(blob.CentroidX, blob.CentroidY) },
                        Style = new GraphicsStyle { OkColor = highlightColor, LineWidth = 4.0 }
                    }
                };
            }

        #region IToolControlDataAnalysis

        /// <summary>
        /// 工具执行完成后，更新 Blob 特征数据和统计摘要
        /// </summary>
        public void OnResultUpdated(ToolResults result)
        {
            if (result is BlobAnalysisResults blobResult)
            {
                // 更新 Blob 列表
                FilteredBlobs.Clear();
                if (blobResult.FilteredBlobs != null)
                {
                    foreach (var blob in blobResult.FilteredBlobs)
                    {
                        FilteredBlobs.Add(blob);
                    }
                }

                // 更新统计摘要
                TotalBlobCount = blobResult.TotalBlobCount;
                TotalArea = blobResult.TotalArea;
                ThresholdUsed = blobResult.ThresholdUsed;
                ThresholdTypeText = blobResult.TypeUsed.ToString();
                ConnectivityText = blobResult.ConnectivityUsed.ToString();

                // 通知 UI 更新所有统计属性
                NotifyPropertyChanged(nameof(TotalBlobCount));
                NotifyPropertyChanged(nameof(FilteredBlobCount));
                NotifyPropertyChanged(nameof(TotalArea));
                NotifyPropertyChanged(nameof(FilteredTotalArea));
                NotifyPropertyChanged(nameof(AverageArea));
                NotifyPropertyChanged(nameof(MaxArea));
                NotifyPropertyChanged(nameof(MinArea));
                NotifyPropertyChanged(nameof(ThresholdUsed));
                NotifyPropertyChanged(nameof(ThresholdTypeText));
                NotifyPropertyChanged(nameof(ConnectivityText));
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
