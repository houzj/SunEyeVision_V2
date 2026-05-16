using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Tool.BlobAnalysis.Models;

namespace SunEyeVision.Tool.BlobAnalysis.Views
{
    /// <summary>
    /// Blob 数据分析控件 - 显示连通域特征数据的 DataGrid + 统计摘要 + 选中高亮
    /// </summary>
    /// <remarks>
    /// 直接实现 INotifyPropertyChanged（需继承 UserControl 无法同时继承 ObservableObject）
    /// 使用社区工具包 ObservableObject 的兼容模式绑定，参见 rule-001 例外说明。
    /// </remarks>
    public partial class BlobDataAnalysisControl : UserControl, IToolControlDataAnalysis
    {
        /// <summary>Blob 特征数据列表</summary>
        public ObservableCollection<BlobData> FilteredBlobs { get; } = new();

        private int _totalBlobCount;

        /// <summary>Blob总数</summary>
        public int TotalBlobCount
        {
            get => _totalBlobCount;
            private set
            {
                if (_totalBlobCount != value)
                {
                    _totalBlobCount = value;
                    NotifyPropertyChanged(nameof(TotalBlobCount));
                }
            }
        }

        /// <summary>筛选后数量</summary>
        public int FilteredBlobCount => FilteredBlobs.Count;

        private double _totalArea;

        /// <summary>总面积（px²）</summary>
        public double TotalArea
        {
            get => _totalArea;
            private set
            {
                if (Math.Abs(_totalArea - value) > double.Epsilon)
                {
                    _totalArea = value;
                    NotifyPropertyChanged(nameof(TotalArea));
                }
            }
        }

        private double _filteredTotalArea;

        /// <summary>筛选后总面积（px²）</summary>
        public double FilteredTotalArea
        {
            get => _filteredTotalArea;
            private set
            {
                if (Math.Abs(_filteredTotalArea - value) > double.Epsilon)
                {
                    _filteredTotalArea = value;
                    NotifyPropertyChanged(nameof(FilteredTotalArea));
                }
            }
        }

        private double _averageArea;

        /// <summary>平均面积（px²）</summary>
        public double AverageArea
        {
            get => _averageArea;
            private set
            {
                if (Math.Abs(_averageArea - value) > double.Epsilon)
                {
                    _averageArea = value;
                    NotifyPropertyChanged(nameof(AverageArea));
                }
            }
        }

        private double _maxArea;

        /// <summary>最大面积（px²）</summary>
        public double MaxArea
        {
            get => _maxArea;
            private set
            {
                if (Math.Abs(_maxArea - value) > double.Epsilon)
                {
                    _maxArea = value;
                    NotifyPropertyChanged(nameof(MaxArea));
                }
            }
        }

        private double _minArea;

        /// <summary>最小面积（px²）</summary>
        public double MinArea
        {
            get => _minArea;
            private set
            {
                if (Math.Abs(_minArea - value) > double.Epsilon)
                {
                    _minArea = value;
                    NotifyPropertyChanged(nameof(MinArea));
                }
            }
        }

        private double _thresholdUsed;

        /// <summary>实际阈值</summary>
        public double ThresholdUsed
        {
            get => _thresholdUsed;
            private set
            {
                if (Math.Abs(_thresholdUsed - value) > double.Epsilon)
                {
                    _thresholdUsed = value;
                    NotifyPropertyChanged(nameof(ThresholdUsed));
                }
            }
        }

        /// <summary>阈值类型</summary>
        public string ThresholdTypeText { get; private set; } = "-";

        #region 选中 Blob 相关

        private BlobData? _selectedBlob;

        /// <summary>当前选中的 Blob（由 DataGrid SelectionChanged 驱动）</summary>
        public BlobData? SelectedBlob
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
            if (BlobDataGrid.SelectedItem is BlobData blob)
                SelectedBlob = blob;
            else
                SelectedBlob = null;
        }

        /// <summary>构建选中 Blob 的高亮 DisplayItem（黄色外接矩形）</summary>
        private static IEnumerable<DisplayItemBase>? BuildHighlightItems(BlobData? blob)
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

                // 更新统计摘要（从 BlobAnalysisResults 直读，避免重复计算）
                TotalBlobCount = blobResult.TotalBlobCount;
                TotalArea = blobResult.TotalArea;
                FilteredTotalArea = blobResult.FilteredTotalArea;
                AverageArea = blobResult.AverageArea;
                MaxArea = blobResult.LargestBlobArea;
                MinArea = blobResult.SmallestBlobArea;
                ThresholdUsed = blobResult.ThresholdUsed;
                ThresholdTypeText = blobResult.TypeUsed.ToString();

                // 通知 UI 更新派生属性
                NotifyPropertyChanged(nameof(FilteredBlobCount));
                NotifyPropertyChanged(nameof(ThresholdTypeText));
            }
        }

        #endregion

        /// <summary>
        /// 属性变更通知
        /// 必须直接实现 INotifyPropertyChanged：UserControl 无法同时继承 ObservableObject
        /// </summary>
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void NotifyPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
    }
}
