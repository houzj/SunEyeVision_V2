using System;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Helpers;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views;
using SunEyeVision.Plugin.SDK.Vision.Contracts;
using SunEyeVision.Tool.LineLineMeasure;

namespace SunEyeVision.Tool.LineLineMeasure.Views
{
    /// <summary>
    /// 线-线测量调试控件（三 Tab）：双卡尺编辑（MeasureElementsEditorControl）+ 全 ParamValue 运行参数 + 判定结果。
    /// 生命周期：Loaded→Closing 链 Cleanup → ElementsEditor.ClearMainOverlay()。
    /// 绑定范式（C3）：引导/卡尺数值全 ParamValue；XAML 一律绑 ".Value"；枚举走 ComboBox 索引翻译。
    /// </summary>
    public partial class LineLineMeasureToolDebugControl : ToolDebugControlBase
    {
        private readonly LineLineMeasureParameters _parameters = new();
        private object? _dataProvider;

        // 标注全部删除（用户要求）：不向主图叠加任何结果，图元观察在"模块结果"面板。
        // 因此不再保存主图 ImageControl 引用与上次叠加。
        private bool _syncingElement1Caliber;
        private bool _syncingElement2Caliber;
        private bool _elementsHooked;

        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(nameof(Parameters), typeof(LineLineMeasureParameters),
                typeof(LineLineMeasureToolDebugControl), new PropertyMetadata(null));

        public LineLineMeasureParameters Parameters
        {
            get => (LineLineMeasureParameters)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        private string _resultText = "尚未执行";
        public string ResultText { get => _resultText; set { _resultText = value; OnPropertyChanged(nameof(ResultText)); } }

        private MeasureEditorControl Element1Editor => ElementsEditor.Element1;
        private MeasureEditorControl Element2Editor => ElementsEditor.Element2;

        public LineLineMeasureToolDebugControl()
        {
            InitializeComponent();
            Parameters = _parameters;

            InitializeEnumCombos();
            HookElementsEditor();
            RunElementSwitchCombo.SelectedIndex = 0;   // 默认编辑卡尺1 运行参数
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
            RemoveLastResultOverlay();
        }

        /// <summary>标注全部删除：不向主图叠加结果；保留方法以防未来扩展。</summary>
        private void UpdateResultDisplay(LineLineMeasureResults result)
        {
            // no-op
        }

        private void RemoveLastResultOverlay()
        {
            // no-op
        }

        #region 双元素编辑器事件 ↔ 参数（ParamValue.Value 回写）

        private void HookElementsEditor()
        {
            if (_elementsHooked) return;
            _elementsHooked = true;

            Element1Editor.GuideGeometryChanged += (_, e) =>
            {
                if (Parameters == null) return;
                if (e.GeometryType == MeasureGeometryType.Line && e.Model.EditingLine.HasValue)
                {
                    Parameters.E1StartX.Value = e.Model.LineStartPoint.X;
                    Parameters.E1StartY.Value = e.Model.LineStartPoint.Y;
                    Parameters.E1EndX.Value = e.Model.LineEndPoint.X;
                    Parameters.E1EndY.Value = e.Model.LineEndPoint.Y;
                }
                if (e.IsDrawing) ElementsEditor?.ClearElementToolSelection(0);
            };
            Element1Editor.CaliberParamsChanged += (_, e) =>
            {
                if (Parameters == null || _syncingElement1Caliber) return;
                Parameters.E1MeasureLength1.Value = e.Width / 2;
                Parameters.E1MeasureLength2.Value = e.Length / 2;
                Parameters.E1NumMeasures.Value = e.Count;
            };

            Element2Editor.GuideGeometryChanged += (_, e) =>
            {
                if (Parameters == null) return;
                if (e.GeometryType == MeasureGeometryType.Line && e.Model.EditingLine.HasValue)
                {
                    Parameters.E2StartX.Value = e.Model.LineStartPoint.X;
                    Parameters.E2StartY.Value = e.Model.LineStartPoint.Y;
                    Parameters.E2EndX.Value = e.Model.LineEndPoint.X;
                    Parameters.E2EndY.Value = e.Model.LineEndPoint.Y;
                }
                if (e.IsDrawing) ElementsEditor?.ClearElementToolSelection(1);
            };
            Element2Editor.CaliberParamsChanged += (_, e) =>
            {
                if (Parameters == null || _syncingElement2Caliber) return;
                Parameters.E2MeasureLength1.Value = e.Width / 2;
                Parameters.E2MeasureLength2.Value = e.Length / 2;
                Parameters.E2NumMeasures.Value = e.Count;
            };
        }

        private void RestoreEditorsFromParameters()
        {
            if (ElementsEditor == null || Parameters == null) return;

            var e1 = new Point(Parameters.E1StartX.Value, Parameters.E1StartY.Value);
            var e1End = new Point(Parameters.E1EndX.Value, Parameters.E1EndY.Value);
            if ((e1End - e1).Length > 1e-6)
                Element1Editor.SetGuide(Line2d.FromTwoPoints(e1, e1End), e1, e1End);

            var e2 = new Point(Parameters.E2StartX.Value, Parameters.E2StartY.Value);
            var e2End = new Point(Parameters.E2EndX.Value, Parameters.E2EndY.Value);
            if ((e2End - e2).Length > 1e-6)
                Element2Editor.SetGuide(Line2d.FromTwoPoints(e2, e2End), e2, e2End);

            PushCaliberToEditor(1);
            PushCaliberToEditor(2);
        }

        private void PushCaliberToEditor(int index)
        {
            if (ElementsEditor == null || Parameters == null) return;
            var e = index == 1 ? Element1Editor : Element2Editor;
            var l1 = index == 1 ? Parameters.E1MeasureLength1 : Parameters.E2MeasureLength1;
            var l2 = index == 1 ? Parameters.E1MeasureLength2 : Parameters.E2MeasureLength2;
            var n = index == 1 ? Parameters.E1NumMeasures : Parameters.E2NumMeasures;

            if (index == 1) _syncingElement1Caliber = true; else _syncingElement2Caliber = true;
            try { e.SetCaliberParams(l1.Value * 2, l2.Value * 2, n.Value); }
            finally { if (index == 1) _syncingElement1Caliber = false; else _syncingElement2Caliber = false; }
        }

        #endregion

        #region 枚举下拉（值↔索引翻译）

        private void InitializeEnumCombos()
        {
            E1TransitionCombo.ItemsSource = new[] { "全部", "黑到白", "白到黑" };
            E1SelectCombo.ItemsSource = new[] { "全部", "第一个", "最后一个" };
            E2TransitionCombo.ItemsSource = new[] { "全部", "黑到白", "白到黑" };
            E2SelectCombo.ItemsSource = new[] { "全部", "第一个", "最后一个" };
            SyncEnumsFromParameters();
        }

        private void SyncEnumsFromParameters()
        {
            if (Parameters == null) return;
            E1TransitionCombo.SelectedIndex = (int)Parameters.E1Transition.Value;
            E1SelectCombo.SelectedIndex = (int)Parameters.E1Select.Value;
            E2TransitionCombo.SelectedIndex = (int)Parameters.E2Transition.Value;
            E2SelectCombo.SelectedIndex = (int)Parameters.E2Select.Value;
        }

        private void OnE1TransitionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Parameters != null && E1TransitionCombo.SelectedIndex >= 0)
                Parameters.E1Transition.Value = (EdgeTransition)E1TransitionCombo.SelectedIndex;
        }
        private void OnE1SelectChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Parameters != null && E1SelectCombo.SelectedIndex >= 0)
                Parameters.E1Select.Value = (EdgeSelect)E1SelectCombo.SelectedIndex;
        }
        private void OnE2TransitionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Parameters != null && E2TransitionCombo.SelectedIndex >= 0)
                Parameters.E2Transition.Value = (EdgeTransition)E2TransitionCombo.SelectedIndex;
        }
        private void OnE2SelectChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Parameters != null && E2SelectCombo.SelectedIndex >= 0)
                Parameters.E2Select.Value = (EdgeSelect)E2SelectCombo.SelectedIndex;
        }

        /// <summary>Tab2 卡尺切换：显示对应卡尺的一份运行参数区</summary>
        private void OnRunElementSwitchChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (RunElement1Panel == null || RunElement2Panel == null) return;
            var element2 = RunElementSwitchCombo.SelectedIndex == 1;
            RunElement1Panel.Visibility = element2 ? System.Windows.Visibility.Collapsed : System.Windows.Visibility.Visible;
            RunElement2Panel.Visibility = element2 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }

        #endregion

        #region ToolDebugControlBase 生命周期

        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);
            if (parameters is not LineLineMeasureParameters typed)
                throw new InvalidOperationException($"线-线测量节点参数类型异常（期望 {nameof(LineLineMeasureParameters)}，实际 {parameters?.GetType().Name ?? "null"}）。");
            Parameters = typed;
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
            ElementsEditor?.ConfigureElement(0, MeasureGeometryType.Line);
            ElementsEditor?.ConfigureElement(1, MeasureGeometryType.Line);
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

            Parameters.ResolvedInputImage = image;
            var result = new LineLineMeasureTool().Run(Parameters);

            UpdatePositionCorrectionMatrix();
            return result;
        }

        protected override bool CanExecuteTool()
            => Parameters != null && Parameters.ImageSource != null && _dataProvider != null;

        public override void OnExternalResultArrived() => UpdatePositionCorrectionMatrix();

        private void UpdatePositionCorrectionMatrix()
        {
            if (ElementsEditor == null || Parameters == null) return;
            ElementsEditor.SetPositionCorrectionMatrix(
                Parameters.IsPositionCorrectionEnabled
                    ? PositionCorrectionResolver.ResolveFirstMatrix(Parameters.PositionCorrectionBindingSource)
                    : null);
        }

        #endregion
    }
}
