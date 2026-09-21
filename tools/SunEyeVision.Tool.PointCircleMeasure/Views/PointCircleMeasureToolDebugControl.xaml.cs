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

namespace SunEyeVision.Tool.PointCircleMeasure.Views
{
    /// <summary>
    /// 点-圆测量调试控件（三 Tab）：
    /// Tab1 = 元素编辑（点1 = 仅参数订阅 X/Y / 圆2 = 引导绘制 + 卡尺），
    /// Tab2 = 运行参数（圆2 边缘检测 + 拟合筛选），
    /// Tab3 = 判定结果 + 图形/文本样式。
    ///
    /// 点元素承载方案（与 SDK 加 SetElementSlotVisible 对齐）：
    /// 元素1（点）= MeasureElementsEditorControl 槽位1 → 由 XAML Profile 隐藏工具栏，
    /// 点 X/Y 通过独立的 ConfigSetting 控件订阅（支持常量/上游绑定）。
    /// 元素2（圆）= 槽位2 → 由 XAML Profile 声明为 Circle。
    /// </summary>
    public partial class PointCircleMeasureToolDebugControl : ToolDebugControlBase
    {
        private bool _syncingElement2Caliber;
        private bool _elementsHooked;
        private object? _dataProvider;

        public static readonly DependencyProperty ParametersProperty =
            DependencyProperty.Register(nameof(Parameters), typeof(PointCircleMeasureParameters),
                typeof(PointCircleMeasureToolDebugControl), new PropertyMetadata(null));

        public PointCircleMeasureParameters Parameters
        {
            get => (PointCircleMeasureParameters)GetValue(ParametersProperty);
            set => SetValue(ParametersProperty, value);
        }

        private string _resultText = "尚未执行";
        public string ResultText { get => _resultText; set { _resultText = value; OnPropertyChanged(nameof(ResultText)); } }

        private MeasureEditorControl Element2Editor => ElementsEditor.Element2;

        public PointCircleMeasureToolDebugControl()
        {
            InitializeComponent();
            Parameters = new PointCircleMeasureParameters();

            // 点1「按图形」绑定目标为契约点数组（Point2d[]，词目 Point）：数组类型无法用
            // XAML x:Type 表达，故在 code-behind 设置（对齐 RoiSubscribePanel 的 RoiShape[] 做法）。
            P1PointBinding.DataType = typeof(Point2d[]);

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
            // 元素2 = 圆（引导绘制 + 卡尺事件）
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

            if (Parameters.E2Radius > 1e-6)
                Element2Editor.SetGuide(new Circle2d(
                    new Point(Parameters.E2CenterX, Parameters.E2CenterY),
                    Parameters.E2Radius));

            PushCaliberToEditor();
            // 数据恢复完成后立即刷新画布（测量带 + 角标首屏可见，无需等鼠标进入图像区）
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
            if (parameters is not PointCircleMeasureParameters typed)
                throw new InvalidOperationException($"点-圆测量节点参数类型异常（期望 {nameof(PointCircleMeasureParameters)}，实际 {parameters?.GetType().Name ?? "null"}）。");
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
            var result = ExecuteWithLiveContext(new PointCircleMeasureTool(), Parameters);

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
