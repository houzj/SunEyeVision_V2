using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.Threshold.Views
{
    /// <summary>
    /// 阈值化数据分析控件 - 显示执行结果统计信息的摘要 + 详细列表
    /// </summary>
    public partial class ThresholdDataAnalysisControl : UserControl, IToolControlDataAnalysis, System.ComponentModel.INotifyPropertyChanged
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

        /// <summary>实际阈值</summary>
        public double ThresholdValue { get; private set; }

        /// <summary>最大值</summary>
        public double MaxValue { get; private set; }

        /// <summary>阈值类型名称</summary>
        public string ThresholdTypeName { get; private set; } = "-";

        /// <summary>白色像素比例</summary>
        public double WhiteRatio { get; private set; }

        /// <summary>输出面积</summary>
        public long OutputArea { get; private set; }

        /// <summary>自适应文本</summary>
        public string AdaptiveText { get; private set; } = string.Empty;

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

        public ThresholdDataAnalysisControl()
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
            if (result is ThresholdResults thresholdResult)
            {
                // 更新摘要
                ThresholdValue = thresholdResult.ThresholdUsed;
                MaxValue = thresholdResult.MaxValueUsed;
                ThresholdTypeName = thresholdResult.TypeUsed.ToString();
                WhiteRatio = thresholdResult.WhitePixelRatio;
                OutputArea = thresholdResult.OutputArea;

                var adaptive = thresholdResult.AdaptiveMethodUsed;
                AdaptiveText = adaptive != "Mean" && adaptive != "Gaussian"
                    ? "固定阈值"
                    : $"{adaptive}(块大小:{thresholdResult.BlockSizeUsed})";

                IsPass = thresholdResult.IsPass;
                FailReasonText = thresholdResult.FailReasons.Count > 0
                    ? string.Join("; ", thresholdResult.FailReasons)
                    : "全部通过";

                // 更新详细列表
                DataItems.Clear();
                AddItem("实际阈值", thresholdResult.ThresholdUsed.ToString("F0"));
                AddItem("最大值", thresholdResult.MaxValueUsed.ToString("F0"));
                AddItem("阈值类型", thresholdResult.TypeUsed.ToString());
                AddItem("自适应方法", adaptive != "Mean" && adaptive != "Gaussian"
                    ? "无" : adaptive);
                AddItem("块大小", adaptive != "Mean" && adaptive != "Gaussian"
                    ? "N/A" : thresholdResult.BlockSizeUsed.ToString());
                AddItem("输入尺寸", $"{thresholdResult.InputWidth}×{thresholdResult.InputHeight}");
                AddItem("输入通道数", thresholdResult.InputChannels.ToString());
                AddItem("白色像素数", thresholdResult.WhitePixelCount.ToString());
                AddItem("白色像素比例", $"{thresholdResult.WhitePixelRatio:F1}%");
                AddItem("输出均值", thresholdResult.OutputMean.ToString("F1"));
                AddItem("输出面积", thresholdResult.OutputArea.ToString());
                AddItem("质心X", thresholdResult.CentroidX.ToString("F1"));
                AddItem("质心Y", thresholdResult.CentroidY.ToString("F1"));
                AddItem("执行耗时", $"{thresholdResult.ExecutionTimeMs} ms");
                AddItem("判断结果", thresholdResult.IsPass ? "OK" : "NG");
                if (thresholdResult.FailReasons.Count > 0)
                    AddItem("失败原因", string.Join("; ", thresholdResult.FailReasons));

                HasResultData = true;

                // 通知 UI 更新摘要属性
                NotifyPropertyChanged(nameof(ThresholdValue));
                NotifyPropertyChanged(nameof(MaxValue));
                NotifyPropertyChanged(nameof(ThresholdTypeName));
                NotifyPropertyChanged(nameof(WhiteRatio));
                NotifyPropertyChanged(nameof(OutputArea));
                NotifyPropertyChanged(nameof(AdaptiveText));
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
