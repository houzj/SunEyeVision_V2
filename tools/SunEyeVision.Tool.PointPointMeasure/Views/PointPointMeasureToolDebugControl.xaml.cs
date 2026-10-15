using System;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Tool.PointPointMeasure.Views
{
    /// <summary>
    /// 点-点测量调试控件（两 Tab）：
    /// Tab1 = 基本参数（输入图像 + 点1/点2 各自的「按图形 / 按参数」输入），
    /// Tab2 = 判定结果 + 图形/文本样式。
    ///
    /// 与其他点类工具的关键差异：
    /// - 两个元素均为点、均无引导绘制入口，故不装配 MeasureElementsEditorControl（无引导几何、无卡尺带承载）；
    /// - 两个元素均为纯订阅，坐标已是最终位置，故不提供位置修正（再修正即重复修正）。
    /// </summary>
    public partial class PointPointMeasureToolDebugControl : ToolDebugControlBase
    {
        private object? _dataProvider;

        public static readonly System.Windows.DependencyProperty ParametersProperty =
            System.Windows.DependencyProperty.Register(nameof(Parameters), typeof(PointPointMeasureParameters),
                typeof(PointPointMeasureToolDebugControl), new System.Windows.PropertyMetadata(null));

        public PointPointMeasureParameters Parameters
        {
            get => (PointPointMeasureParameters)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        private string _resultText = "尚未执行";
        public string ResultText { get => _resultText; set { _resultText = value; OnPropertyChanged(nameof(ResultText)); } }

        public PointPointMeasureToolDebugControl()
        {
            InitializeComponent();
            Parameters = new PointPointMeasureParameters();
        }

        #region ToolDebugControlBase 生命周期

        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);
            if (parameters is not PointPointMeasureParameters typed)
                throw new InvalidOperationException($"点-点测量节点参数类型异常（期望 {nameof(PointPointMeasureParameters)}，实际 {parameters?.GetType().Name ?? "null"}）。");
            Parameters = typed;
        }

        public override void SetDataProvider(object dataProvider)
        {
            base.SetDataProvider(dataProvider);
            _dataProvider = dataProvider;
            Parameters?.RestoreImageSource(AvailableDataSources);
        }

        public override void SetMainImageControl(ImageControl? imageControl)
        {
            base.SetMainImageControl(imageControl);
            // 两个元素均为点（无引导绘制 / 无卡尺），无元素编辑器需要装配与矩阵同步。
        }

        protected override object ExecuteTool()
        {
            if (Parameters == null) throw new InvalidOperationException("Parameters not initialized.");
            var imageSource = Parameters.ImageSource;
            if (imageSource == null) throw new InvalidOperationException("请选择输入图像源");
            if (_dataProvider is not DataSourceQueryService query)
                throw new InvalidOperationException("数据提供者未初始化");

            if (!query.HasNodeExecuted(imageSource.SourceNodeId))
                throw new InvalidOperationException($"当前工具缺少输入图像：图像源 '{imageSource.SourceNodeId}.{imageSource.PropertyName}' 没有值");

            var image = query.GetPropertyValue(imageSource.SourceNodeId, imageSource.PropertyName) as IVisionImage;
            if (image == null || image.IsEmpty) throw new InvalidOperationException("无法获取图像数据或图像为空");

            return ExecuteWithLiveContext(new PointPointMeasureTool(), Parameters, image);
        }

        protected override bool CanExecuteTool()
            => Parameters != null && Parameters.ImageSource != null && _dataProvider != null;

        #endregion
    }
}
