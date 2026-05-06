using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.Morphology.Views
{
    /// <summary>
    /// 形态学数据分析控件 - 显示执行结果统计信息的摘要 + 详细列表
    /// </summary>
    public partial class MorphologyDataAnalysisControl : UserControl, IToolControlDataAnalysis
    {
        #region 数据项模型

        /// <summary>
        /// 数据显示项
        /// </summary>
        public class DataItem
        {
            public string Label { get; set; } = string.Empty;
            public string Value { get; set; } = string.Empty;
        }

        #endregion

        #region 摘要属性

        /// <summary>操作文本</summary>
        public string OperationText { get; private set; } = "-";

        /// <summary>核文本</summary>
        public string KernelText { get; private set; } = "-";

        /// <summary>迭代文本</summary>
        public string IterationText { get; private set; } = "-";

        /// <summary>Blob数量</summary>
        public int BlobCount { get; private set; }

        /// <summary>总面积</summary>
        public long TotalArea { get; private set; }

        /// <summary>最大Blob面积</summary>
        public double MaxBlobArea { get; private set; }

        /// <summary>输出均值</summary>
        public double OutputMean { get; private set; }

        /// <summary>是否通过</summary>
        public bool IsPass { get; private set; }

        /// <summary>通过文本</summary>
        public string IsPassText => IsPass ? "OK ✓" : "NG ✗";

        /// <summary>通过颜色</summary>
        public Color IsPassColor => IsPass ? Colors.Green : Colors.Red;

        /// <summary>失败原因</summary>
        public string FailReasonText { get; private set; } = string.Empty;

        /// <summary>是否有结果数据</summary>
        public bool HasResultData { get; private set; }

        #endregion

        #region 详细数据

        /// <summary>详细数据显示项</summary>
        public ObservableCollection<DataItem> DataItems { get; } = new();

        #endregion

        /// <inheritdoc/>
        public event EventHandler<IEnumerable<DisplayItemBase>?>? SelectionHighlightChanged;

        public MorphologyDataAnalysisControl()
        {
            InitializeComponent();
            DataContext = this;
        }

        #region IToolControlDataAnalysis

        /// <summary>
        /// 工具执行完成后，更新分析数据
        /// </summary>
        public void OnResultUpdated(ToolResults result)
        {
            if (result is MorphologyResults morphResult)
            {
                // 更新摘要
                OperationText = morphResult.OperationUsed;
                KernelText = $"{morphResult.KernelShapeUsed} {morphResult.KernelWidthUsed}x{morphResult.KernelHeightUsed}";
                IterationText = $"{morphResult.IterationsUsed}次";
                BlobCount = morphResult.BlobCount;
                TotalArea = morphResult.TotalArea;
                MaxBlobArea = morphResult.MaxBlobArea;
                OutputMean = morphResult.OutputMean;

                IsPass = morphResult.IsPass;
                FailReasonText = morphResult.FailReasons.Count > 0
                    ? string.Join("; ", morphResult.FailReasons)
                    : "全部通过";

                // 更新详细列表
                DataItems.Clear();
                AddItem("形态学操作", morphResult.OperationUsed);
                AddItem("结构元素形状", morphResult.KernelShapeUsed);
                AddItem("核宽度", morphResult.KernelWidthUsed.ToString());
                AddItem("核高度", morphResult.KernelHeightUsed.ToString());
                AddItem("迭代次数", morphResult.IterationsUsed.ToString());
                AddItem("输入尺寸", $"{morphResult.InputSize.Width}×{morphResult.InputSize.Height}");
                AddItem("输入通道数", morphResult.InputChannels.ToString());
                AddItem("输出均值", morphResult.OutputMean.ToString("F1"));
                AddItem("Blob数量", morphResult.BlobCount.ToString());
                AddItem("总面积", morphResult.TotalArea.ToString());
                AddItem("最大Blob面积", morphResult.MaxBlobArea.ToString("F1"));
                AddItem("执行耗时", $"{morphResult.ExecutionTimeMs} ms");
                AddItem("判断结果", morphResult.IsPass ? "OK" : "NG");
                if (morphResult.FailReasons.Count > 0)
                    AddItem("失败原因", string.Join("; ", morphResult.FailReasons));

                HasResultData = true;

                // 通知 UI 更新摘要属性
                NotifyPropertyChanged(nameof(OperationText));
                NotifyPropertyChanged(nameof(KernelText));
                NotifyPropertyChanged(nameof(IterationText));
                NotifyPropertyChanged(nameof(BlobCount));
                NotifyPropertyChanged(nameof(TotalArea));
                NotifyPropertyChanged(nameof(MaxBlobArea));
                NotifyPropertyChanged(nameof(OutputMean));
                NotifyPropertyChanged(nameof(IsPass));
                NotifyPropertyChanged(nameof(IsPassText));
                NotifyPropertyChanged(nameof(IsPassColor));
                NotifyPropertyChanged(nameof(FailReasonText));
                NotifyPropertyChanged(nameof(HasResultData));
            }
        }

        #endregion

        #region 辅助方法

        private void AddItem(string label, string value)
        {
            DataItems.Add(new DataItem { Label = label, Value = value });
        }

        #endregion

        #region 属性变更通知

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void NotifyPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

        #endregion
    }
}
