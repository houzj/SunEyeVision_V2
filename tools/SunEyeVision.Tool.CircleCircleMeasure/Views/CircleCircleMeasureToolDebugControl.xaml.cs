using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
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
using SunEyeVision.Tool.CircleCircleMeasure;

namespace SunEyeVision.Tool.CircleCircleMeasure.Views
{
    /// <summary>
    /// 圆-圆测量调试控件（三 Tab）：双元素编辑（卡尺1 圆 / 卡尺2 圆，MeasureElementsEditorControl）
    /// + 全 纯 CLR 运行参数 + 判定结果。
    /// 生命周期：Loaded→Closing 链 Cleanup → ElementsEditor.ClearMainOverlay()。
    /// 绑定范式（C3）：引导/卡尺数值纯 CLR；XAML 一律绑 ".Value"；枚举走 ComboBox 索引翻译。
    /// </summary>
    public partial class CircleCircleMeasureToolDebugControl : ToolDebugControlBase
    {
        private readonly CircleCircleMeasureParameters _parameters = new();
        private object? _dataProvider;
        private CircleCircleMeasureParameters? _observedParameters;
        private bool _cleanedUp;
        public PositionCorrectionResolution? CorrectionPreview { get; private set; }
        private void ObserveParameters()
        {
            _cleanedUp = false;
            if (ReferenceEquals(_observedParameters, Parameters)) return;
            if (_observedParameters != null) PropertyChangedEventManager.RemoveHandler(_observedParameters, OnCorrectionParameterChanged, "");
            _observedParameters = Parameters;
            if (_observedParameters != null) PropertyChangedEventManager.AddHandler(_observedParameters, OnCorrectionParameterChanged, "");
        }
        private void OnCorrectionParameterChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.BeginInvoke(new Action(() => OnCorrectionParameterChanged(sender, e)));
                return;
            }
            if (_cleanedUp || !ReferenceEquals(sender, _observedParameters)) return;
            if (e.PropertyName is nameof(MeasureGuideParameters.IsPositionCorrectionEnabled) or
                nameof(MeasureGuideParameters.PositionCorrectionBindingSource) or nameof(MeasureGuideParameters.ImageSource))
            {
                ElementsEditor?.CancelActiveEdits();
                UpdatePositionCorrectionMatrix();
            }
        }

        // 标注全部删除（对齐线-线测量）：不向主图叠加任何结果，图元观察在"模块结果"面板。
        private bool _syncingElement1Caliber;
        private bool _syncingElement2Caliber;
        private bool _elementsHooked;

        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(nameof(Parameters), typeof(CircleCircleMeasureParameters),
                typeof(CircleCircleMeasureToolDebugControl), new PropertyMetadata(null));

        public CircleCircleMeasureParameters Parameters
        {
            get => (CircleCircleMeasureParameters)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        private string _resultText = "尚未执行";
        public string ResultText { get => _resultText; set { _resultText = value; OnPropertyChanged(nameof(ResultText)); } }

        private MeasureEditorControl Element1Editor => ElementsEditor.Element1;
        private MeasureEditorControl Element2Editor => ElementsEditor.Element2;

        public CircleCircleMeasureToolDebugControl()
        {
            InitializeComponent();
            Parameters = _parameters;

            InitializeEnumCombos();
            HookElementsEditor();
            RunElementSwitchCombo.SelectedIndex = 0;   // 默认编辑卡尺1 运行参数
            Loaded += (_, _) =>
            {
                ObserveParameters();
                UpdatePositionCorrectionMatrix();
                HookElementsEditor();
                if (Window.GetWindow(this) is { } window)
                    window.Closing += (_, _) => Cleanup();
            };
            Unloaded += (_, _) => Cleanup();
        }

        public void Cleanup()
        {
            _cleanedUp = true;
            if (_observedParameters != null) PropertyChangedEventManager.RemoveHandler(_observedParameters, OnCorrectionParameterChanged, "");
            _observedParameters = null;
            ElementsEditor?.ClearMainOverlay();
            RemoveLastResultOverlay();
        }

        /// <summary>标注全部删除：不向主图叠加结果；保留方法以防未来扩展。</summary>
        private void UpdateResultDisplay(CircleCircleMeasureResults result)
        {
            // no-op
        }

        private void RemoveLastResultOverlay()
        {
            // no-op
        }

        #region 双元素编辑器事件 ↔ 参数（参数值 回写）

        private void HookElementsEditor()
        {
            if (_elementsHooked) return;
            _elementsHooked = true;
            ElementsEditor.DisplayedImageChanged += (_, _) => UpdatePositionCorrectionMatrix();

            // 元素1 = 圆（只有"圆心 + 半径"三个自由度）
            Element1Editor.GuideGeometryChanged += (_, e) =>
            {
                if (Parameters == null) return;
                if (e.GeometryType == MeasureGeometryType.Circle && e.Model.EditingCircle.HasValue)
                {
                    var circle = e.Model.EditingCircle.Value;
                    Parameters.E1CenterX = circle.Center.X;
                    Parameters.E1CenterY = circle.Center.Y;
                    Parameters.E1Radius = circle.Radius;
                }
                if (e.IsDrawing) ElementsEditor?.ClearElementToolSelection(0);
            };
            Element1Editor.CaliberParamsChanged += (_, e) =>
            {
                if (Parameters == null || _syncingElement1Caliber) return;
                Parameters.E1MeasureLength1 = e.Width / 2;
                Parameters.E1MeasureLength2 = e.Length / 2;
                Parameters.E1NumMeasures = e.Count;
            };

            // 元素2 = 圆（同构）
            Element2Editor.GuideGeometryChanged += (_, e) =>
            {
                if (Parameters == null) return;
                if (e.GeometryType == MeasureGeometryType.Circle && e.Model.EditingCircle.HasValue)
                {
                    var circle = e.Model.EditingCircle.Value;
                    Parameters.E2CenterX = circle.Center.X;
                    Parameters.E2CenterY = circle.Center.Y;
                    Parameters.E2Radius = circle.Radius;
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

            if (Parameters.E1Radius > 1e-6)
                Element1Editor.SetGuide(new Circle2d(
                    new Point(Parameters.E1CenterX, Parameters.E1CenterY),
                    Parameters.E1Radius));

            if (Parameters.E2Radius > 1e-6)
                Element2Editor.SetGuide(new Circle2d(
                    new Point(Parameters.E2CenterX, Parameters.E2CenterY),
                    Parameters.E2Radius));

            PushCaliberToEditor(1);
            PushCaliberToEditor(2);
            // 数据恢复完成后立即刷新画布（测量带 + ①②角标首屏可见，无需等鼠标进入图像区）
            ElementsEditor.RefreshOverlay();
        }

        private void PushCaliberToEditor(int index)
        {
            if (ElementsEditor == null || Parameters == null) return;
            var e = index == 1 ? Element1Editor : Element2Editor;
            var l1 = index == 1 ? Parameters.E1MeasureLength1 : Parameters.E2MeasureLength1;
            var l2 = index == 1 ? Parameters.E1MeasureLength2 : Parameters.E2MeasureLength2;
            var n = index == 1 ? Parameters.E1NumMeasures : Parameters.E2NumMeasures;

            if (index == 1) _syncingElement1Caliber = true; else _syncingElement2Caliber = true;
            try { e.SetCaliberParams(l1 * 2, l2 * 2, n); }
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
            E1TransitionCombo.SelectedIndex = (int)Parameters.E1Transition;
            E1SelectCombo.SelectedIndex = (int)Parameters.E1Select;
            E2TransitionCombo.SelectedIndex = (int)Parameters.E2Transition;
            E2SelectCombo.SelectedIndex = (int)Parameters.E2Select;
        }

        private void OnE1TransitionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Parameters != null && E1TransitionCombo.SelectedIndex >= 0)
                Parameters.E1Transition = (EdgeTransition)E1TransitionCombo.SelectedIndex;
        }
        private void OnE1SelectChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
        {
            if (Parameters != null && E1SelectCombo.SelectedIndex >= 0)
                Parameters.E1Select = (EdgeSelect)E1SelectCombo.SelectedIndex;
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
            if (parameters is not CircleCircleMeasureParameters typed)
                throw new InvalidOperationException($"圆-圆测量节点参数类型异常（期望 {nameof(CircleCircleMeasureParameters)}，实际 {parameters?.GetType().Name ?? "null"}）。");
            Parameters = typed;
            ConfigureCaliperDrawing(Element1Editor, Parameters, 0);
            ConfigureCaliperDrawing(Element2Editor, Parameters, 1);
            ObserveParameters();
            RestoreEditorsFromParameters();
            SyncEnumsFromParameters();
            UpdatePositionCorrectionMatrix();
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

            var requiredSources = new List<string?> { imageSource.SourceNodeId };
            if (Parameters.IsPositionCorrectionEnabled)
                requiredSources.Add(Parameters.PositionCorrectionBindingSource?.SourceNodeId ?? Parameters.PositionCorrectionSource?.SourceNodeId);
            if (CurrentNode?.GetType().GetProperty("ParamSettings")?.GetValue(CurrentNode) is ParamSettingContainer settings)
                requiredSources.AddRange(settings.Settings.Where(setting => setting.BindingType == BindingType.Binding).Select(setting => setting.SourceNodeId));
            var nodeResults = ToolDebugResultSnapshot.Capture(query, requiredSources);

            if (!nodeResults.TryGetValue(imageSource.SourceNodeId, out var imageResult) || !imageResult.IsSuccess)
                throw new InvalidOperationException($"当前工具缺少有效输入图像：上游 '{imageSource.SourceNodeId}.{imageSource.PropertyName}' 本轮没有成功结果。");

            var image = OutputVariableResolver.Resolve(imageResult, imageSource.PropertyName, imageSource.SourceNodeId) as IVisionImage;
            if (image == null || image.IsEmpty) throw new InvalidOperationException("无法获取图像数据或图像为空");

            // Capture image, correction and regular bindings from one stable set of upstream result objects.
            var execution = CreateCorrectionExecutionParameters(nodeResults, image);
            var request = CreateLiveExecutionRequest(execution, image, nodeResults);
            var result = new ToolExecutor(new ParameterResolver(_dataProvider as IDataSourceQueryService, VisionLogger.Instance), VisionLogger.Instance)
                .Run(new CircleCircleMeasureTool(), request);

            UpdatePositionCorrectionMatrix();
            return result;
        }

        private CircleCircleMeasureParameters CreateCorrectionExecutionParameters(
            IReadOnlyDictionary<string, ToolResults> nodeResults,
            IVisionImage image)
        {
            var execution = new CircleCircleMeasureParameters();
            execution.CopyFrom(Parameters);
            execution.NodeResults = nodeResults.ToDictionary(pair => pair.Key, pair => pair.Value, StringComparer.Ordinal);
            execution.ResolvedInputImage = image;
            var correction = PositionCorrectionResolution.Capture(execution);
            if (!correction.CanUse) throw new InvalidOperationException(correction.Message);
            // Binding objects compare by path; clear the copy first so equal-path assignment cannot retain stale CurrentValue.
            execution.PositionCorrectionBindingSource = null;
            if (correction.State == PositionCorrectionState.Valid)
            {
                execution.PositionCorrectionBindingSource = new AvailableDataSource
                {
                    SourceNodeId = Parameters.PositionCorrectionBindingSource?.SourceNodeId ?? Parameters.PositionCorrectionSource!.SourceNodeId,
                    PropertyName = Parameters.PositionCorrectionBindingSource?.PropertyName ?? Parameters.PositionCorrectionSource!.PropertyName,
                    CurrentValue = correction.Matrix
                };
            }
            return execution;
        }

        protected override bool CanExecuteTool()
            => Parameters != null && Parameters.ImageSource != null && _dataProvider != null;

        public override void OnExternalResultArrived() => UpdatePositionCorrectionMatrix();

        private void UpdatePositionCorrectionMatrix()
        {
            if (!Dispatcher.CheckAccess()) { Dispatcher.BeginInvoke(new Action(UpdatePositionCorrectionMatrix)); return; }
            if (_cleanedUp) return;
            if (ElementsEditor == null || Parameters == null) return;
            CorrectionPreview = PositionCorrectionResolution.Capture(Parameters, _dataProvider as IDataSourceQueryService);
            if (ElementsEditor.HasImageControl)
                CorrectionPreview = CorrectionPreview.ForPreview(PreviewFrameProvenance.Read(ElementsEditor.DisplayedImage), ElementsEditor.DisplayedImage != null);
            ElementsEditor.Element1.IsCorrectionAvailable = CorrectionPreview.CanUse;
            ElementsEditor.Element2.IsCorrectionAvailable = CorrectionPreview.CanUse;
            ElementsEditor.IsEnabled = CorrectionPreview.CanUse;
            ElementsEditor.ToolTip = CorrectionPreview.Message;
            ElementsEditor.SetPositionCorrectionMatrix(CorrectionPreview.Matrix);
            CorrectionStatusText.Text = CorrectionPreview.Message;
            CorrectionStatusText.ToolTip = CorrectionPreview.Diagnostic;
            CorrectionStatusText.Visibility = Parameters.IsPositionCorrectionEnabled ? Visibility.Visible : Visibility.Collapsed;
        }

        #endregion
    }
}
