using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Controls;
using SunEyeVision.CvEngine.Models;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.BlobAnalysis.Views
{
    /// <summary>
    /// Blob 数据分析控件 - 显示连通域特征数据的 DataGrid + 统计摘要
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

        public BlobDataAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
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
