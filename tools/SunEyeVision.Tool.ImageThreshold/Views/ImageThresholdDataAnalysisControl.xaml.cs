using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Media;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Visualization;
using SunEyeVision.Plugin.SDK.UI.Controls;

namespace SunEyeVision.Tool.ImageThreshold.Views
{
    /// <summary>
    /// 图像二值化数据分析控件
    /// </summary>
    public partial class ImageThresholdDataAnalysisControl : UserControl, IToolControlDataAnalysis
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

        /// <summary>阈值方法名称</summary>
        public string MethodName { get; private set; } = "-";

        /// <summary>实际阈值</summary>
        public double ThresholdValue { get; private set; }

        /// <summary>白色像素比例</summary>
        public double WhiteRatio { get; private set; }

        /// <summary>输出面积</summary>
        public long OutputArea { get; private set; }

        /// <summary>方法详情</summary>
        public string MethodDetail { get; private set; } = string.Empty;

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

#pragma warning disable CS0067 // IToolControlDataAnalysis 接口要求，由框架订阅
        /// <inheritdoc/>
        public event EventHandler<IEnumerable<DisplayItemBase>?>? SelectionHighlightChanged;
#pragma warning restore CS0067

        public ImageThresholdDataAnalysisControl()
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
            if (result is ImageThresholdResults thresholdResult)
            {
                // 更新摘要
                MethodName = GetMethodDisplayName(thresholdResult.MethodUsed);
                ThresholdValue = thresholdResult.ThresholdUsed;
                WhiteRatio = thresholdResult.WhitePixelRatio;
                OutputArea = thresholdResult.OutputArea;

                var method = thresholdResult.MethodUsed;
                MethodDetail = method switch
                {
                    ThresholdMethod.AdaptiveMean or ThresholdMethod.AdaptiveGaussian
                        => $"{GetMethodDisplayName(method)}",
                    ThresholdMethod.Otsu or ThresholdMethod.Triangle
                        => $"{GetMethodDisplayName(method)}",
                    _ => string.Empty
                };

                IsPass = thresholdResult.IsPass;
                FailReasonText = thresholdResult.FailReasons.Count > 0
                    ? string.Join("; ", thresholdResult.FailReasons)
                    : "全部通过";

                // 更新详细列表
                DataItems.Clear();
                AddItem("阈值方法", GetMethodDisplayName(thresholdResult.MethodUsed));
                AddItem("实际阈值", thresholdResult.ThresholdUsed.ToString("F0"));
                AddItem("最大值", thresholdResult.MaxValueUsed.ToString());
                AddItem("输入尺寸", $"{thresholdResult.InputSize.Width}x{thresholdResult.InputSize.Height}");
                AddItem("输入通道数", thresholdResult.InputChannels.ToString());
                AddItem("白色像素数", thresholdResult.WhitePixelCount.ToString());
                AddItem("白色像素比例", $"{thresholdResult.WhitePixelRatio:F1}%");
                AddItem("输出均值", thresholdResult.OutputMean.ToString("F1"));
                AddItem("输出面积", thresholdResult.OutputArea.ToString());
                AddItem("执行耗时", $"{thresholdResult.ExecutionTimeMs} ms");
                AddItem("判断结果", thresholdResult.IsPass ? "OK" : "NG");
                if (thresholdResult.FailReasons.Count > 0)
                    AddItem("失败原因", string.Join("; ", thresholdResult.FailReasons));

                HasResultData = true;

                // 通知 UI 更新
                NotifyPropertyChanged(nameof(MethodName));
                NotifyPropertyChanged(nameof(ThresholdValue));
                NotifyPropertyChanged(nameof(WhiteRatio));
                NotifyPropertyChanged(nameof(OutputArea));
                NotifyPropertyChanged(nameof(MethodDetail));
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

        private static string GetMethodDisplayName(ThresholdMethod method)
        {
            return method switch
            {
                ThresholdMethod.Binary => "固定阈值",
                ThresholdMethod.BinaryInv => "反二值化",
                ThresholdMethod.Trunc => "截断",
                ThresholdMethod.ToZero => "取零",
                ThresholdMethod.ToZeroInv => "反取零",
                ThresholdMethod.Otsu => "大津法(Otsu)",
                ThresholdMethod.Triangle => "三角形法(Triangle)",
                ThresholdMethod.AdaptiveMean => "自适应均值",
                ThresholdMethod.AdaptiveGaussian => "自适应高斯",
                ThresholdMethod.InRange => "范围阈值",
                _ => method.ToString()
            };
        }

        #endregion

        #region 属性变更通知

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void NotifyPropertyChanged(string propertyName) =>
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));

        #endregion
    }
}
