using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.ImageFilter.Views
{
    /// <summary>
    /// 图像滤波数据分析控件 - 显示执行结果统计信息的摘要 + 详细列表
    /// </summary>
    public partial class ImageFilterDataAnalysisControl : UserControl, IToolControlDataAnalysis, System.ComponentModel.INotifyPropertyChanged
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

        /// <summary>滤波类型名称</summary>
        public string FilterTypeName { get; private set; } = "-";

        /// <summary>内核文本</summary>
        public string KernelText { get; private set; } = "-";

        /// <summary>输出均值</summary>
        public double MeanValue { get; private set; }

        /// <summary>标准差</summary>
        public double StdValue { get; private set; }

        /// <summary>最小值</summary>
        public double MinValue { get; private set; }

        /// <summary>最大值</summary>
        public double MaxValue { get; private set; }

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

        public ImageFilterDataAnalysisControl()
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
            if (result is ImageFilterResults filterResult)
            {
                // 更新摘要
                FilterTypeName = filterResult.FilterTypeUsed.ToString();
                KernelText = $"{filterResult.KernelWidthUsed}×{filterResult.KernelHeightUsed}";
                MeanValue = filterResult.OutputMean;
                StdValue = filterResult.OutputStd;
                MinValue = filterResult.OutputMin;
                MaxValue = filterResult.OutputMax;
                IsPass = filterResult.IsPass;
                FailReasonText = filterResult.FailReasons.Count > 0
                    ? string.Join("; ", filterResult.FailReasons)
                    : "全部通过";

                // 更新详细列表
                DataItems.Clear();
                AddItem("滤波类型", filterResult.FilterTypeUsed.ToString());
                AddItem("内核宽度", filterResult.KernelWidthUsed.ToString());
                AddItem("内核高度", filterResult.KernelHeightUsed.ToString());
                AddItem("输入尺寸", $"{filterResult.InputWidth}×{filterResult.InputHeight}");
                AddItem("输入通道数", filterResult.InputChannels.ToString());
                AddItem("输出均值", filterResult.OutputMean.ToString("F1"));
                AddItem("输出标准差", filterResult.OutputStd.ToString("F1"));
                AddItem("输出最小值", filterResult.OutputMin.ToString("F1"));
                AddItem("输出最大值", filterResult.OutputMax.ToString("F1"));
                if (filterResult.ChannelMeans.Length > 0)
                    AddItem("通道均值", string.Join(", ", filterResult.ChannelMeans));
                AddItem("执行耗时", $"{filterResult.ExecutionTimeMs} ms");
                AddItem("判断结果", filterResult.IsPass ? "OK" : "NG");
                if (filterResult.FailReasons.Count > 0)
                    AddItem("失败原因", string.Join("; ", filterResult.FailReasons));

                HasResultData = true;

                // 通知 UI 更新摘要属性
                NotifyPropertyChanged(nameof(FilterTypeName));
                NotifyPropertyChanged(nameof(KernelText));
                NotifyPropertyChanged(nameof(MeanValue));
                NotifyPropertyChanged(nameof(StdValue));
                NotifyPropertyChanged(nameof(MinValue));
                NotifyPropertyChanged(nameof(MaxValue));
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
