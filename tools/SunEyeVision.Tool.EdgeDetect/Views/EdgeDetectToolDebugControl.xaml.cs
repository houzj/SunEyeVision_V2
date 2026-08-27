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
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Rendering;

namespace SunEyeVision.Tool.EdgeDetect.Views
{
    /// <summary>
    /// 边缘检测工具调试控件
    /// </summary>
    /// <remarks>
    /// 3 Tab 布局（对齐 Threshold/ImageFilter）：
    /// - Tab1 基本参数：卡尺类型（MeasureToolBar）+ 引导几何绘制
    /// - Tab2 运行参数：卡尺参数 + 拟合筛选（Expander 分组）
    /// - Tab3 结果显示：拟合结果 + RMSE + 边缘点数
    /// 
    /// 引导几何绘制：用户在主界面图像上鼠标绘制引导线/圆，绘制后自动叠加测量带。
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

        /// <summary>测量带叠加渲染器（渲染到主界面图像）</summary>
        private MeasureBandOverlay? _bandOverlay;

        /// <summary>当前主界面图像控件</summary>
        private ImageControl? _mainImageControl;

        /// <summary>引导几何绘制状态</summary>
        private bool _isDrawingGuide;
        private Point _guideStart;

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
        /// 设置主界面图像控件（用于绘制引导几何 + 渲染测量带）
        /// </summary>
        public override void SetMainImageControl(ImageControl? imageControl)
        {
            // 解除旧订阅
            if (_mainImageControl != null)
            {
                _mainImageControl.CanvasMouseLeftButtonDown -= OnGuideMouseDown;
                _mainImageControl.CanvasMouseLeftButtonUp -= OnGuideMouseUp;
                _mainImageControl.ImageMouseMove -= OnGuideMouseMove;
            }

            base.SetMainImageControl(imageControl);
            _mainImageControl = imageControl;

            if (imageControl != null)
            {
                _bandOverlay = new MeasureBandOverlay(imageControl.InteractionOverlayCanvasControl);

                // 订阅主界面鼠标事件（绘制引导几何）
                imageControl.CanvasMouseLeftButtonDown += OnGuideMouseDown;
                imageControl.CanvasMouseLeftButtonUp += OnGuideMouseUp;
                imageControl.ImageMouseMove += OnGuideMouseMove;
            }
            else
            {
                _bandOverlay = null;
            }

            RenderGuideOverlay();
        }

        #endregion

        #region 引导几何绘制

        /// <summary>
        /// 鼠标按下：开始绘制引导几何
        /// </summary>
        private void OnGuideMouseDown(object sender, ImageMouseEventArgs e)
        {
            var pos = e.ImagePosition;

            if (Parameters.GeometryType == GeometryType.Line)
            {
                // 直线：按下定起点，移动更新，松开定终点
                _guideStart = pos;
                _isDrawingGuide = true;
                e.Handled = true;
            }
            else if (Parameters.GeometryType == GeometryType.Circle)
            {
                // 圆：按下定圆心，移动定半径
                _guideStart = pos;
                _isDrawingGuide = true;
                e.Handled = true;
            }
        }

        /// <summary>
        /// 鼠标移动：实时更新引导几何预览
        /// </summary>
        private void OnGuideMouseMove(object sender, ImageMouseEventArgs e)
        {
            if (!_isDrawingGuide || _bandOverlay == null) return;

            var pos = e.ImagePosition;

            if (Parameters.GeometryType == GeometryType.Line)
            {
                var line = Line2d.FromTwoPoints(_guideStart, pos);
                _bandOverlay.RenderLine(line, Parameters.MeasureLength1 * 2, Parameters.MeasureLength2 * 2, 10);
            }
            else if (Parameters.GeometryType == GeometryType.Circle)
            {
                var radius = (_guideStart - pos).Length;
                var circle = new Circle2d(_guideStart, radius);
                _bandOverlay.RenderCircle(circle, Parameters.MeasureLength1 * 2, Parameters.MeasureLength2 * 2, 10);
            }
        }

        /// <summary>
        /// 鼠标松开：完成引导几何绘制
        /// </summary>
        private void OnGuideMouseUp(object sender, ImageMouseEventArgs e)
        {
            if (!_isDrawingGuide) return;

            _isDrawingGuide = false;
            var pos = e.ImagePosition;

            if (Parameters.GeometryType == GeometryType.Line)
            {
                // 防退化：起点终点过近则取消
                if ((_guideStart - pos).Length > 1e-3)
                {
                    Parameters.GuideLine = Line2d.FromTwoPoints(_guideStart, pos);
                    Parameters.LogInfo($"引导直线已绘制: ({_guideStart.X:F1}, {_guideStart.Y:F1}) → ({pos.X:F1}, {pos.Y:F1})");
                }
            }
            else if (Parameters.GeometryType == GeometryType.Circle)
            {
                var radius = (_guideStart - pos).Length;
                if (radius > 1e-3)
                {
                    Parameters.GuideCircle = new Circle2d(_guideStart, radius);
                    Parameters.LogInfo($"引导圆已绘制: 圆心({_guideStart.X:F1}, {_guideStart.Y:F1}) 半径 {radius:F1}");
                }
            }

            // 最终渲染
            RenderGuideOverlay();
            e.Handled = true;
        }

        #endregion

        #region 工具栏 ↔ 参数同步

        /// <summary>
        /// 卡尺类型选择 → 同步到工具参数
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
        /// 更新引导几何显示（按卡尺类型切换提示 + 渲染测量带）
        /// </summary>
        private void UpdateGuidePanel()
        {
            if (GuideInfoText == null) return;
            GuideInfoText.Text = Parameters.GeometryType switch
            {
                GeometryType.Line   => "在主界面图像上按住鼠标左键拖动绘制引导直线",
                GeometryType.Circle => "在主界面图像上按住鼠标左键拖动绘制引导圆",
                GeometryType.Arc    => "圆弧引导（绘制交互待完善）",
                _ => "",
            };
            RenderGuideOverlay();
        }

        /// <summary>
        /// 在主界面图像上渲染引导几何 + 测量带
        /// </summary>
        private void RenderGuideOverlay()
        {
            if (_bandOverlay == null || Parameters == null) return;

            _bandOverlay.Clear();

            if (Parameters.GuideLine.HasValue)
            {
                _bandOverlay.RenderLine(Parameters.GuideLine.Value,
                    Parameters.MeasureLength1 * 2, Parameters.MeasureLength2 * 2, 10);
            }
            else if (Parameters.GuideCircle.HasValue)
            {
                _bandOverlay.RenderCircle(Parameters.GuideCircle.Value,
                    Parameters.MeasureLength1 * 2, Parameters.MeasureLength2 * 2, 10);
            }
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
