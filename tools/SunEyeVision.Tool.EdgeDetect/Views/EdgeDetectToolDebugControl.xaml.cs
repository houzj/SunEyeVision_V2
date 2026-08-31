using System;
using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views;

namespace SunEyeVision.Tool.EdgeDetect.Views
{
    /// <summary>
    /// 边缘检测工具调试控件
    /// </summary>
    /// <remarks>
    /// 3 Tab 布局（对齐 Threshold/ImageFilter）：
    /// - Tab1 基本参数：卡尺类型（MeasureToolBar）+ 引导几何（GuideEditor）
    /// - Tab2 运行参数：卡尺参数 + 拟合筛选（Expander 分组）
    /// - Tab3 结果显示：拟合结果 + RMSE + 边缘点数
    ///
    /// 引导几何绘制/编辑交互由 SDK 通用控件 GuideEditor 承载，
    /// 本工具只负责组装 + 参数同步（对齐 RoiEditorManagerControl 复用模式）。
    /// </remarks>
    public partial class EdgeDetectToolDebugControl : ToolDebugControlBase
    {
        /// <summary>正在从编辑器同步卡尺参数（防止 参数→编辑器→参数 回写死循环）</summary>
        private bool _syncingCaliberFromEditor;

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
            HookCaliberEditor();

            // 窗口关闭时清理主图像测量叠加层（对齐 Threshold/Blob 范式）
            Loaded += (s, e) =>
            {
                var window = System.Windows.Window.GetWindow(this);
                if (window != null)
                {
                    window.Closing += OnWindowClosing;
                }
            };
        }

        #endregion

        #region 资源清理

        /// <summary>
        /// 清理主图像测量引导叠加层（关闭调试窗口时防止残留）
        /// </summary>
        public void Cleanup()
        {
            MeasurementManager?.ClearMainOverlay();
        }

        private void OnWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
        {
            Cleanup();
        }

        #endregion

        #region GuideEditor 集成

        /// <summary>
        /// 测量卡尺引导编辑器（通过 MeasurementEditorManager 访问，屏蔽区域由 Manager 编排）
        /// </summary>
        private MeasureEditorControl GuideEditor => MeasurementManager.GuideEditor;

        /// <summary>
        /// 订阅引导几何交互宿主的事件 + 参数联动
        /// </summary>
        private void HookCaliberEditor()
        {
            if (GuideEditor == null) return;
            GuideEditor.GuideGeometryChanged += OnGuideGeometryChanged;
            Parameters.PropertyChanged += OnParameterChanged;
        }

        /// <summary>
        /// 卡尺参数变更 → 实时刷新测量带渲染
        /// </summary>
        private void OnParameterChanged(object? sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (Parameters == null || GuideEditor == null) return;
            if (e.PropertyName == nameof(EdgeDetectParameters.MeasureLength1) ||
                e.PropertyName == nameof(EdgeDetectParameters.MeasureLength2) ||
                e.PropertyName == nameof(EdgeDetectParameters.NumMeasures))
            {
                GuideEditor.SetCaliberParams(
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
                        Parameters.LineStartPoint = model.LineStartPoint;
                        Parameters.LineEndPoint = model.LineEndPoint;
                        Parameters.LogInfo(
                            $"引导直线已绘制: 起点({model.LineStartPoint.X:F1}, {model.LineStartPoint.Y:F1})");
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
                        Parameters.IsMajorArc = model.EditingArc.Value.IsMajorArc;
                        Parameters.IsClockwise = model.EditingArc.Value.IsClockwise;
                        Parameters.LogInfo($"引导圆弧已绘制: 圆心({model.EditingArc.Value.Center.X:F1}, {model.EditingArc.Value.Center.Y:F1})");
                    }
                    break;
            }

            // 绘制流程完成 → 清除工具栏选中，回到选择模式（对齐 RoiEditorControl 绘制完成 ClearSelection）
            if (e.IsDrawing)
            {
                GuideEditor.ClearCaliberSelection();
            }
        }

        /// <summary>
        /// 渲染初始引导几何（从参数回填到编辑器）
        /// </summary>
        private void RestoreGuideFromParameters()
        {
            if (GuideEditor == null || Parameters == null) return;

            switch (Parameters.GeometryType)
            {
                case GeometryType.Line:
                    var lineLen = (Parameters.LineEndPoint - Parameters.LineStartPoint).Length;
                    if (lineLen > 1e-6)
                        GuideEditor.SetGuide(
                            Line2d.FromTwoPoints(Parameters.LineStartPoint, Parameters.LineEndPoint));
                    break;
                case GeometryType.Circle:
                    if (Parameters.GuideCircle.HasValue)
                        GuideEditor.SetGuide(Parameters.GuideCircle.Value);
                    break;
                case GeometryType.Arc:
                    if (Parameters.GuideCircle.HasValue)
                        GuideEditor.SetGuide(new Arc2d(
                            Parameters.GuideCircle.Value.Center,
                            Parameters.GuideCircle.Value.Radius,
                            Parameters.StartAngle,
                            Parameters.EndAngle));
                    break;
            }

            // ★ 修复持久化：卡尺参数从 Parameters 回填到编辑器（宽度/长度/数量）
            //   SetCaliberParams 会触发 CaliberParamsChanged，用同步标志避免回写循环
            _syncingCaliberFromEditor = true;
            try
            {
                GuideEditor.SetCaliberParams(
                    Parameters.MeasureLength1 * 2,
                    Parameters.MeasureLength2 * 2,
                    Parameters.NumMeasures);
            }
            finally
            {
                _syncingCaliberFromEditor = false;
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

            // 屏蔽区域：绑定到 MeasurementEditorManager（MaskRegionsSource → maskEditor）
            if (MeasurementManager != null)
            {
                MeasurementManager.MaskRegionsSource = Parameters.MaskRegions.Regions;
            }

            // ★ 修复持久化：打开调试窗口即回填引导几何 + 卡尺参数到编辑器
            RestoreGuideFromParameters();

            // ★ 同步 ComboBox 选中（反序列化时按已保存值显示）
            SyncEnumsFromParameters();
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
        /// 设置主界面图像控件（委托给 GuideEditor 承载交互）
        /// </summary>
        public override void SetMainImageControl(ImageControl? imageControl)
        {
            base.SetMainImageControl(imageControl);
            MeasurementManager?.SetMainImageControl(imageControl);
            RestoreGuideFromParameters();
        }

        #endregion

        #region 枚举下拉处理

        /// <summary>
        /// <summary>
        /// 从 Parameters 同步 ComboBox 选中（反序列化时按已保存值显示）
        /// </summary>
        private void SyncEnumsFromParameters()
        {
            if (TransitionCombo != null)
            {
                TransitionCombo.SelectedIndex = Parameters.Transition switch
                {
                    EdgeTransition.Positive => 1,
                    EdgeTransition.Negative => 2,
                    _ => 0,
                };
            }
            if (SelectCombo != null)
            {
                SelectCombo.SelectedIndex = Parameters.Select switch
                {
                    EdgeSelect.First => 1,
                    EdgeSelect.Last => 2,
                    _ => 0,
                };
            }
        }

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
