using System;
using System.ComponentModel;
using System.Windows;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Metadata;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views;
using SunEyeVision.Plugin.SDK.VisionAdapters;

namespace SunEyeVision.Tool.CircleDetect.Views
{
    /// <summary>
    /// 圆查找工具调试控件
    /// </summary>
    /// <remarks>
    /// 3 Tab 布局（对齐 EdgeDetect）：
    /// - Tab1 基本参数：引导圆（GuideEditor，锁定 Circle 类型）
    /// - Tab2 运行参数：卡尺参数 + 圆弧参数 + 拟合筛选
    /// - Tab3 结果显示：拟合结果 + RMSE + 边缘点数
    ///
    /// 引导几何绘制/编辑交互由 SDK 通用控件 MeasurementEditorManagerControl 承载，
    /// 本工具只负责组装 + 参数同步。
    /// </remarks>
    public partial class CircleDetectToolDebugControl : ToolDebugControlBase
    {
        #region Properties

        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(
                nameof(Parameters),
                typeof(CircleDetectParameters),
                typeof(CircleDetectToolDebugControl),
                new PropertyMetadata(null));

        public CircleDetectParameters Parameters
        {
            get => (CircleDetectParameters)GetValue(ParametersProperty);
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

        #endregion

        #region Constructor

        public CircleDetectToolDebugControl()
        {
            InitializeComponent();
            Parameters = new CircleDetectParameters();
            HookCaliberEditor();
        }

        #endregion

        #region GuideEditor 集成

        /// <summary>
        /// 测量卡尺引导编辑器（锁定圆类型，屏蔽区域由 Manager 编排）
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
        private void OnParameterChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (Parameters == null || GuideEditor == null) return;
            if (e.PropertyName == nameof(CircleDetectParameters.MeasureLength1) ||
                e.PropertyName == nameof(CircleDetectParameters.MeasureLength2) ||
                e.PropertyName == nameof(CircleDetectParameters.NumMeasures))
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
            if (e.GeometryType == MeasureGeometryType.Circle && model.EditingCircle.HasValue)
            {
                Parameters.GuideCircle = model.EditingCircle.Value;
                Parameters.LogInfo(
                    $"引导圆已绘制: 圆心({model.EditingCircle.Value.Center.X:F1}, {model.EditingCircle.Value.Center.Y:F1})");
            }

            // 绘制流程完成 → 清除工具栏选中，回到选择模式
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

            if (Parameters.GuideCircle.HasValue)
                GuideEditor.SetGuide(Parameters.GuideCircle.Value);
        }

        #endregion

        #region Overrides

        /// <summary>数据提供者（取图像 / 查询位置修正矩阵）</summary>
        private DataSourceQueryService? _dataProvider;

        public override void SetParameters(ToolParameters parameters)
        {
            base.SetParameters(parameters);
            // 零兼容（rule-008/rule-015）：参数类型不符直接报错
            if (parameters is not CircleDetectParameters circleParams)
            {
                throw new InvalidOperationException(
                    $"圆查找节点参数类型异常（期望 CircleDetectParameters，实际 {parameters?.GetType().Name ?? "null"}）。");
            }
            Parameters = circleParams;

            // 屏蔽区域：绑定到 MeasurementEditorManager
            if (MeasurementManager != null)
            {
                MeasurementManager.MaskRegionsSource = Parameters.MaskRegions.Regions;
            }
        }

        public override void SetDataProvider(object dataProvider)
        {
            base.SetDataProvider(dataProvider);
            if (dataProvider is DataSourceQueryService queryService)
            {
                _dataProvider = queryService;
                Parameters?.RestoreImageSource(AvailableDataSources);
                Parameters?.RestorePositionCorrectionSource(AvailableDataSources);
                UpdatePositionCorrectionMatrix();
            }
        }

        protected override object ExecuteTool()
        {
            if (Parameters == null)
                throw new InvalidOperationException("Parameters not initialized.");

            var imageSource = Parameters?.ImageSource;
            if (imageSource == null)
                throw new InvalidOperationException("请选择输入图像源");
            if (_dataProvider == null)
                throw new InvalidOperationException("数据提供者未初始化");
            if (!_dataProvider.HasNodeExecuted(imageSource.SourceNodeId))
                throw new InvalidOperationException(
                    $"当前工具缺少输入图像：图像源 '{imageSource.SourceNodeId}.{imageSource.PropertyName}' 没有值");

            var image = _dataProvider.GetPropertyValue(imageSource.SourceNodeId, imageSource.PropertyName) as IVisionImage;
            if (image == null || image.IsEmpty)
                throw new InvalidOperationException("无法获取图像数据或图像为空");

            Parameters!.ResolvedInputImage = image;
            var tool = new CircleDetectTool();
            var result = tool.Run(Parameters);

            // 执行完成后刷新位置修正矩阵到编辑器
            UpdatePositionCorrectionMatrix();
            UpdateResultDisplay(result);
            return result;
        }

        protected override bool CanExecuteTool()
        {
            return Parameters != null && Parameters.ImageSource != null && _dataProvider != null;
        }

        /// <summary>
        /// 从 DataSourceQueryService 实时查询位置修正矩阵并设置到编辑器
        /// （交互层与结果层同一数据源，保证显示与算法一致）
        /// </summary>
        private void UpdatePositionCorrectionMatrix()
        {
            if (Parameters?.IsPositionCorrectionEnabled == true && _dataProvider != null
                && Parameters.PositionCorrectionBindingSource != null)
            {
                var source = Parameters.PositionCorrectionBindingSource;
                var matrix = _dataProvider.GetPropertyValue(source.SourceNodeId, source.PropertyName) as AffineMatrix;
                if (matrix != null)
                {
                    MeasurementManager?.SetPositionCorrectionMatrix(matrix);
                    return;
                }
            }
            if (_dataProvider != null)
            {
                MeasurementManager?.SetPositionCorrectionMatrix(null);
            }
        }

        /// <summary>
        /// 外部结果到达 - 工作流执行完成后刷新位置修正矩阵
        /// </summary>
        public override void OnExternalResultArrived()
        {
            UpdatePositionCorrectionMatrix();
        }

        /// <summary>
        /// 设置主界面图像控件（委托给 MeasurementEditorManager 承载交互）
        /// </summary>
        public override void SetMainImageControl(ImageControl? imageControl)
        {
            base.SetMainImageControl(imageControl);
            MeasurementManager?.SetMainImageControl(imageControl);
            // 锁定引导类型为圆（工具固定为圆查找）
            MeasurementManager?.GuideEditor.StartDraw(MeasureGeometryType.Circle);
            RestoreGuideFromParameters();
        }

        #endregion

        #region 枚举下拉处理

        /// <summary>
        /// 边缘极性选择变更
        /// </summary>
        private void TransitionCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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
        private void SelectCombo_SelectionChanged(object sender, System.Windows.Controls.SelectionChangedEventArgs e)
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
        /// 更新结果显示
        /// </summary>
        private void UpdateResultDisplay(CircleDetectResults result)
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
