using System.Collections.ObjectModel;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Tool.Threshold.Models;

namespace SunEyeVision.Tool.Threshold.Views
{
    /// <summary>
    /// Blob 数据分析控件 - 显示连通域特征数据的 DataGrid
    /// </summary>
    public partial class BlobDataAnalysisControl : UserControl, IToolControlDataAnalysis
    {
        /// <summary>Blob 特征数据列表</summary>
        public ObservableCollection<BlobInfo> Blobs { get; } = new();

        public BlobDataAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        #region IToolControlDataAnalysis

        /// <summary>
        /// 工具执行完成后，更新 Blob 特征数据
        /// </summary>
        public void OnResultUpdated(ToolResults result)
        {
            if (result is ThresholdResults thresholdResult)
            {
                Blobs.Clear();
                if (thresholdResult.Blobs != null)
                {
                    foreach (var blob in thresholdResult.Blobs)
                    {
                        Blobs.Add(blob);
                    }
                }
            }
        }

        #endregion
    }
}
