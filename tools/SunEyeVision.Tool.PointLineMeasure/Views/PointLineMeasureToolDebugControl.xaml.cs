using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Tool.PointLineMeasure.Views
{
    /// <summary>
    /// 点-线测量调试控件（三 Tab）：
    /// Tab1 = 元素编辑（点1 = 仅参数订阅 X/Y / 线2 = 引导绘制 + 卡尺），
    /// Tab2 = 运行参数（线2 边缘检测 + 拟合筛选），
    /// Tab3 = 判定结果 + 图形/文本样式。
    ///
    /// 点元素承载方案（与 SDK 加 SetElementSlotVisible 对齐）：
    /// 元素1（点）= MeasureElementsEditorControl 槽位1 → 由 XAML Profile 隐藏工具栏，
    /// 点 X/Y 通过独立的 ParamBinding 控件订阅（支持常量/上游绑定）。
    /// 元素2（线）= 槽位2 → 由 XAML Profile 声明为 Line。
    /// </summary>
    public partial class PointLineMeasureToolDebugControl : ToolDebugControlBase
    {
        private bool _syncingElement2Caliber;
        private bool _elementsHooked;
        private object? _dataProvider;

        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(nameof(Parameters), typeof(PointLineMeasureParameters),
                typeof(PointLineMeasureToolDebugControl), new PropertyMetadata(null));

        public PointLineMeasureParameters Parameters
        {
            get => (PointLineMeasureParameters)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        private string _resultText = "尚未执行";
        public string ResultText { get => _resultText; set { _resultText = value; OnPropertyChanged(nameof(ResultText)); } }

        private MeasureEditorControl Element2Editor => ElementsEditor.Element2;

        public PointLineMeasureToolDebugControl()
        {
            Parameters = new PointLineMeasureParameters();
            InitializeComponent();

            InitializeEnumCombos();
            HookElementsEditor();
            Loaded += (_, _) =>
            {
                HookElementsEditor();
                if (Window.GetWindow(this) is { } window)
                    window.Closing += (_, _) => Cleanup();
            };
        }

        public void Cleanup()
        {
            ElementsEditor?.ClearMainOverlay();
        }

        private void InitializeEnumCombos()
        {
            E2TransitionCombo.ItemsSource = new[] { "全部", "黑到白", "白到黑" };
            E2SelectCombo.ItemsSource = new[] { "全部", "第一个", "最后一个" };
            SyncEnumsFromParameters();
        }

        private void SyncEnumsFromParameters()
        {
            if (Parameters == null) return;
            E2TransitionCombo.SelectedIndex = (int)Parameters.E2Transition;
            E2SelectCombo.SelectedIndex = (int)Parameters.E2Select;
        }

        private void OnE2TransitionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Parameters != null && E2TransitionCombo.SelectedIndex >= 0)
                Parameters.E2Transition = (EdgeTransition)E2TransitionCombo.SelectedIndex;
        }

        private void OnE2SelectChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Parameters != null && E2SelectCombo.SelectedIndex >= 0)
                Parameters.E2Select = (EdgeSelect)E2SelectCombo.SelectedIndex;
        }

        private void HookElementsEditor()
        {
            if (_elementsHooked) return;
            _elementsHooked = true;

            // 元素1 = 点（无引导入口）→ 不订阅事件、不配置类型、工具栏隐藏（在 SetMainImageControl 完成）
            // 元素2 = 线（引导绘制 + 卡尺事件）
            Element2Editor.GuideGeometryChanged += (_, e) =>
            {
                if (Parameters == null) return;
                if (e.GeometryType == MeasureGeometryType.Line && e.Model.EditingLine.HasValue)
                {
                    var start = e.Model.LineStartPoint;
                    var end = e.Model.LineEndPoint;
                    Parameters.E2StartX = start.X;
                    Parameters.E2StartY = start.Y;
                    Parameters.E2EndX = end.X;
                    Parameters.E2EndY = end.Y;
                }
                if (e.IsDrawing) ElementsEditor?.ClearElementToolSelection(1);
            };
            Element2Editor.CaliberParamsChanged += (_, e) =>
            {
                if (Parameters == null || _syncingElement2Caliber) return;
                Parameters.E2MeasureLength1 = e.Width / 2;
                Parameters.E2MeasureLength2 = e.Length / 2;
                Parameters.E2NumMeasures = e.Count;
            };
        }

        private void RestoreEditorsFromParameters()
        {
            if (ElementsEditor == null || Parameters == null) return;

            var g2Start = new Point(Parameters.E2StartX, Parameters.E2StartY);
            var g2End = new Point(Parameters.E2EndX, Parameters.E2EndY);
            if ((g2End - g2Start).Length > 1e-6)
                Element2Editor.SetGuide(
                        Line2d.FromTwoPoints(g2Start, g2End),
                        g2Start, g2End);

            PushCaliberToEditor();
            ElementsEditor.RefreshOverlay();
        }

        private void PushCaliberToEditor()
        {
            if (ElementsEditor == null || Parameters == null) return;
            _syncingElement2Caliber = true;
            try
            {
                Element2Editor.SetCaliberParams(
                    Parameters.E2MeasureLength1 * 2,
                    Parameters.E2MeasureLength2 * 2,
                    Parameters.E2NumMeasures);
            }
            finally { _syncingElement2Caliber = false; }
        }

        #region ToolDebugControlBase 生命周期

        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);
            if (parameters is not PointLineMeasureParameters typed)
                throw new InvalidOperationException($"点-线测量节点参数类型异常（期望 {nameof(PointLineMeasureParameters)}，实际 {parameters?.GetType().Name ?? "null"}）。");
            Parameters = typed;
            ConfigureCaliperDrawing(Element2Editor, Parameters, 1, defaultWidth: 10.0, defaultHeight: 40.0);
            RestoreEditorsFromParameters();
            SyncEnumsFromParameters();
        }

        public override void SetDataProvider(object dataProvider)
        {
            base.SetDataProvider(dataProvider);
            _dataProvider = dataProvider;
            Parameters?.RestoreImageSource(AvailableDataSources);
            Parameters?.RestorePositionCorrectionSource(AvailableDataSources);
            UpdatePositionCorrectionMatrix();
        }

        public override void SetMainImageControl(ImageControl? imageControl)
        {
            base.SetMainImageControl(imageControl);
            ElementsEditor?.SetMainImageControl(imageControl);
            RestoreEditorsFromParameters();
            UpdatePositionCorrectionMatrix();
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

            var result = ExecuteWithLiveContext(new PointLineMeasureTool(), Parameters, image);

            UpdatePositionCorrectionMatrix();
            return result;
        }

        protected override bool CanExecuteTool()
            => Parameters != null && Parameters.ImageSource != null && _dataProvider != null;

        public override void OnExternalResultArrived() => UpdatePositionCorrectionMatrix();

        private void UpdatePositionCorrectionMatrix()
        {
            if (Parameters == null || ElementsEditor == null) return;
            ApplyPositionCorrectionPreview(Parameters, ElementsEditor.SetPositionCorrectionMatrix, ElementsEditor);
        }

        #endregion
    }
}
