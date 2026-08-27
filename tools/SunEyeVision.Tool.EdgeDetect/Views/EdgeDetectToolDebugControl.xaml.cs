using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure;

namespace SunEyeVision.Tool.EdgeDetect.Views
{
    /// <summary>
    /// 边缘检测工具调试控件
    /// </summary>
    /// <remarks>
    /// 3 Tab 布局（对齐 Threshold/ImageFilter）：
    /// - Tab1 基本参数：卡尺类型（MeasureToolBar）+ 引导几何（MeasureEditorControl）
    /// - Tab2 运行参数：卡尺参数 + 拟合筛选（Expander 分组）
    /// - Tab3 结果显示：拟合结果 + RMSE + 边缘点数
    ///
    /// 引导几何绘制/编辑交互由 SDK 通用控件 MeasureEditorControl 承载，
    /// 本工具只负责组装 + 参数同步（对齐 RoiEditorManagerControl 复用模式）。
    /// </remarks>
    public partial class EdgeDetectToolDebugControl : ToolDebugControlBase
    {
        #region Properties

        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(
                nameof(Parameters),
                typeof(EdgeDetectParameters),
                typeof(EdgeDetectToolDebugControl),
                new PropertyMetadata(null));

        public EdgeDetectParameters Parameters
        {
            get => (EdgeDetectParameters)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        /// <summary>卡尺工具项集合（驱动 MeasureToolBar）</summary>
        public ObservableCollection<MeasureToolItem> MeasureTools { get; } = new();

        /// <summary>结果显示文本</summary>
        public string ResultText
        {
            get => _resultText;
            set { _resultText = value; OnPropertyChanged(nameof(ResultText)); }
        }
        private string _resultText = "尚未执行";

        /// <summary>拟合残差显示</summary>
        public string ResultRMSE
        {
            get => _resultRMSE;
            set { _resultRMSE = value; OnPropertyChanged(nameof(ResultRMSE)); }
        }
        private string _resultRMSE = "--";

        /// <summary>边缘点数显示</summary>
        public string ResultEdgePointCount
        {
            get => _resultEdgePointCount;
            set { _resultEdgePointCount = value; OnPropertyChanged(nameof(ResultEdgePointCount)); }
        }
        private string _resultEdgePointCount = "--";

        public override Size PreferredDebugWindowSize => new Size(900, 620);

        #endregion

        #region Constructor

        public EdgeDetectToolDebugControl()
        {
            InitializeComponent();
            Parameters = new EdgeDetectParameters();
            InitializeMeasureTools();
            HookCaliberEditor();
        }

        #endregion

        #region MeasureToolBar 初始化

        /// <summary>
        /// 初始化卡尺工具栏（带卡尺特征图标，与区域编辑区分）
        /// </summary>
        private void InitializeMeasureTools()
        {
            MeasureTools.Add(new MeasureToolItem(
                MeasureGeometryType.Line, "直线卡尺", MeasureToolIconFactory.LineIcon, Key.L));
            MeasureTools.Add(new MeasureToolItem(
                MeasureGeometryType.Circle, "圆卡尺", MeasureToolIconFactory.CircleIcon, Key.C));
            MeasureTools.Add(new MeasureToolItem(
                MeasureGeometryType.Arc, "圆弧卡尺", MeasureToolIconFactory.ArcIcon, Key.A));

            // 默认选中直线
            MeasureToolBarControl.SetSelectedType(MeasureGeometryType.Line);
            Parameters.GeometryType = GeometryType.Line;
        }

        #endregion

        #region MeasureEditorControl 集成

        /// <summary>
        /// 订阅引导几何交互宿主的事件 + 参数联动
        /// </summary>
        private void HookCaliberEditor()
        {
            if (MeasureEditorControl == null) return;
            MeasureEditorControl.GuideGeometryChanged += OnGuideGeometryChanged;
            Parameters.PropertyChanged += OnParameterChanged;
        }

        /// <summary>
        /// 卡尺参数变更 → 实时刷新测量带渲染
        /// </summary>
        private void OnParameterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (Parameters == null || MeasureEditorControl == null) return;
            if (e.PropertyName == nameof(EdgeDetectParameters.MeasureLength1) ||
                e.PropertyName == nameof(EdgeDetectParameters.MeasureLength2) ||
                e.PropertyName == nameof(EdgeDetectParameters.NumMeasures))
            {
                MeasureEditorControl.SetCaliberParams(
                    Parameters.MeasureLength1 * 2,
                    Parameters.MeasureLength2 * 2,
                    Parameters.NumMeasures);
            }
        }

        /// <summary>
        /// 引导几何变更 → 同步到工具参数
        /// </summary>
        private void OnGuideGeometryChanged(object? sender, GuideGeometryChangedEventArgs e)
        {
            if (Parameters == null) return;

            var model = e.Model;
            switch (e.GeometryType)
            {
                case MeasureGeometryType.Line:
                    if (model.EditingLine.HasValue)
                    {
                        Parameters.GuideLine = model.EditingLine.Value;
                        Parameters.LogInfo(
                            $"引导直线已绘制: ({model.EditingLine.Value.Point.X:F1}, {model.EditingLine.Value.Point.Y:F1})");
                    }
                    break;

                case MeasureGeometryType.Circle:
                    if (model.EditingCircle.HasValue)
                    {
                        Parameters.GuideCircle = model.EditingCircle.Value;
                        Parameters.LogInfo(
                            $"引导圆已绘制: 圆心({model.EditingCircle.Value.Center.X:F1}, {model.EditingCircle.Value.Center.Y:F1})");
                    }
                    break;

                case MeasureGeometryType.Arc:
                    if (model.EditingArc.HasValue)
                    {
                        Parameters.GuideCircle = new Circle2d(
                            model.EditingArc.Value.Center, model.EditingArc.Value.Radius);
                        Parameters.StartAngle = model.EditingArc.Value.StartAngle;
                        Parameters.EndAngle = model.EditingArc.Value.EndAngle;
                        Parameters.LogInfo($"引导圆弧已绘制: 圆心({model.EditingArc.Value.Center.X:F1}, {model.EditingArc.Value.Center.Y:F1})");
                    }
                    break;
            }
        }

        /// <summary>
        /// 渲染初始引导几何（从参数回填到编辑器）
        /// </summary>
        private void RestoreGuideFromParameters()
        {
            if (MeasureEditorControl == null || Parameters == null) return;

            switch (Parameters.GeometryType)
            {
                case GeometryType.Line:
                    if (Parameters.GuideLine.HasValue)
                        MeasureEditorControl.SetGuide(Parameters.GuideLine.Value);
                    break;
                case GeometryType.Circle:
                    if (Parameters.GuideCircle.HasValue)
                        MeasureEditorControl.SetGuide(Parameters.GuideCircle.Value);
                    break;
                case GeometryType.Arc:
                    if (Parameters.GuideCircle.HasValue)
                        MeasureEditorControl.SetGuide(new Arc2d(
                            Parameters.GuideCircle.Value.Center,
                            Parameters.GuideCircle.Value.Radius,
                            Parameters.StartAngle,
                            Parameters.EndAngle));
                    break;
            }
        }

        #endregion

        #region Overrides

        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);
            // 零兼容（rule-008/rule-015）：参数类型不符直接报错
            if (parameters is not EdgeDetectParameters edgeParams)
            {
                throw new InvalidOperationException(
                    $"边缘检测节点参数类型异常（期望 EdgeDetectParameters，实际 {parameters?.GetType().Name ?? "null"}）。");
            }
            Parameters = edgeParams;
            SyncToolBarFromParameters();
        }

        protected override object ExecuteTool()
        {
            if (Parameters == null)
                throw new InvalidOperationException("Parameters not initialized.");

            var tool = new EdgeDetectTool();
            var result = tool.Run(Parameters);
            UpdateResultDisplay(result);
            return result;
        }

        protected override bool CanExecuteTool()
        {
            return Parameters != null;
        }

        /// <summary>
        /// 设置主界面图像控件（委托给 MeasureEditorControl 承载交互）
        /// </summary>
        public override void SetMainImageControl(ImageControl? imageControl)
        {
            base.SetMainImageControl(imageControl);
            MeasureEditorControl?.SetMainImageControl(imageControl);
            RestoreGuideFromParameters();
        }

        #endregion

        #region 工具栏 ↔ 参数同步

        /// <summary>
        /// 卡尺类型选择 → 同步工具参数 + 进入绘制模式
        /// </summary>
        private void OnMeasureSelectionChanged(object? sender, MeasureGeometryType? type)
        {
            if (type == null) return;
            Parameters.GeometryType = type switch
            {
                MeasureGeometryType.Line   => GeometryType.Line,
                MeasureGeometryType.Circle => GeometryType.Circle,
                MeasureGeometryType.Arc    => GeometryType.Arc,
                _ => GeometryType.Line,
            };
            MeasureEditorControl?.StartDraw(type.Value);
            UpdateGuidePanel();
        }

        /// <summary>
        /// 从工具参数同步工具栏选中状态
        /// </summary>
        private void SyncToolBarFromParameters()
        {
            var uiType = Parameters.GeometryType switch
            {
                GeometryType.Line   => MeasureGeometryType.Line,
                GeometryType.Circle => MeasureGeometryType.Circle,
                GeometryType.Arc    => MeasureGeometryType.Arc,
                _ => MeasureGeometryType.Line,
            };
            MeasureToolBarControl.SetSelectedType(uiType);
            UpdateGuidePanel();
        }

        /// <summary>
        /// 更新引导几何提示（按卡尺类型切换）
        /// </summary>
        private void UpdateGuidePanel()
        {
            if (GuideInfoText == null) return;
            GuideInfoText.Text = Parameters.GeometryType switch
            {
                GeometryType.Line   => "在主界面图像上按住鼠标左键拖动绘制引导直线",
                GeometryType.Circle => "在主界面图像上按住鼠标左键拖动绘制引导圆",
                GeometryType.Arc    => "在主界面图像上按住鼠标左键拖动绘制引导圆弧",
                _ => "",
            };
        }

        #endregion

        #region 枚举下拉处理

        /// <summary>
        /// 边缘极性选择变更
        /// </summary>
        private void TransitionCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Parameters == null || TransitionCombo == null) return;
            Parameters.Transition = TransitionCombo.SelectedIndex switch
            {
                1 => EdgeTransition.Positive,
                2 => EdgeTransition.Negative,
                _ => EdgeTransition.All,
            };
        }

        /// <summary>
        /// 选取模式选择变更
        /// </summary>
        private void SelectCombo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (Parameters == null || SelectCombo == null) return;
            Parameters.Select = SelectCombo.SelectedIndex switch
            {
                1 => EdgeSelect.First,
                2 => EdgeSelect.Last,
                _ => EdgeSelect.All,
            };
        }

        #endregion

        #region 结果更新

        /// <summary>
        /// 更新结果显示（对齐 BlobAnalysis 结果显示模式）
        /// </summary>
        private void UpdateResultDisplay(EdgeDetectResults result)
        {
            if (result == null) return;

            if (result.IsSuccess)
            {
                ResultText = result.GeometryDescription;
                ResultRMSE = $"{result.RMSE:F3} px";
                ResultEdgePointCount = $"{result.EdgePointCount}";
            }
            else
            {
                ResultText = result.ErrorMessage ?? "执行失败";
                ResultRMSE = "--";
                ResultEdgePointCount = "--";
            }
        }

        #endregion
    }
}
