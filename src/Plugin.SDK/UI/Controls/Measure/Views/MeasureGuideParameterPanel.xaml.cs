using System;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls.Measure.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Measure.Views
{
    /// <summary>
    /// 测量引导几何参数面板 - 选中引导几何时显示其几何参数（直线/圆/圆弧）
    /// </summary>
    /// <remarks>
    /// 对齐 Roi 编辑器 DrawingParameterPanel 范式：
    /// - 显示引导几何参数（起点/终点/圆心/半径/大弧/方向）
    /// - 显示卡尺通用参数（数量/高/宽）
    /// - 双向同步：画布编辑 → 面板刷新；面板编辑 → 画布实时更新
    /// 不含测量带运行参数（间距/Sigma/阈值等留在 Tab2）。
    /// </remarks>
    public partial class MeasureGuideParameterPanel : UserControl
    {
        /// <summary>关联的测量引导编辑器</summary>
        public static readonly DependencyProperty EditorProperty =
            DependencyProperty.Register(nameof(Editor), typeof(MeasureEditorControl),
                typeof(MeasureGuideParameterPanel),
                new PropertyMetadata(null, OnEditorChanged));

        public MeasureEditorControl? Editor
        {
            get => (MeasureEditorControl?)GetValue(EditorProperty);
            set => SetValue(EditorProperty, value);
        }

        public MeasureGuideParameterPanel()
        {
            InitializeComponent();
            SubscribeNumericControls();
        }

        /// <summary>
        /// 直接订阅每个 NumericUpDown 的 ValueChanged（不依赖路由事件 e.Source，绕开 Source 解析异常）
        /// </summary>
        private void SubscribeNumericControls()
        {
            // 引导几何控件 → 无条件应用几何
            LineStartX.ValueChanged += OnGeometryNumericChanged;
            LineStartY.ValueChanged += OnGeometryNumericChanged;
            LineEndX.ValueChanged += OnGeometryNumericChanged;
            LineEndY.ValueChanged += OnGeometryNumericChanged;
            CircleCenterX.ValueChanged += OnGeometryNumericChanged;
            CircleCenterY.ValueChanged += OnGeometryNumericChanged;
            CircleRadius.ValueChanged += OnGeometryNumericChanged;
            ArcStartX.ValueChanged += OnGeometryNumericChanged;
            ArcStartY.ValueChanged += OnGeometryNumericChanged;
            ArcEndX.ValueChanged += OnGeometryNumericChanged;
            ArcEndY.ValueChanged += OnGeometryNumericChanged;
            ArcRadius.ValueChanged += OnGeometryNumericChanged;
            // 卡尺控件 → 无条件应用卡尺
            CaliberCount.ValueChanged += OnCaliberNumericChanged;
            CaliberHeight.ValueChanged += OnCaliberNumericChanged;
            CaliberWidth.ValueChanged += OnCaliberNumericChanged;
        }

        private void OnGeometryNumericChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingFromEditor || Editor == null) return;
            ApplyGeometryFromPanel();
        }

        private void OnCaliberNumericChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (_syncingFromEditor || Editor == null) return;
            ApplyCaliberValues();
        }

        private static void OnEditorChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            var panel = (MeasureGuideParameterPanel)d;
            if (e.OldValue is MeasureEditorControl oldEditor)
            {
                oldEditor.GuideGeometryChanged -= panel.OnGuideGeometryChanged;
                oldEditor.CaliberParamsChanged -= panel.OnCaliberParamsChanged;
                oldEditor.SelectionChanged -= panel.OnSelectionChanged;
            }
            if (e.NewValue is MeasureEditorControl newEditor)
            {
                newEditor.GuideGeometryChanged += panel.OnGuideGeometryChanged;
                newEditor.CaliberParamsChanged += panel.OnCaliberParamsChanged;
                newEditor.SelectionChanged += panel.OnSelectionChanged;
                panel.RefreshFromEditor();
            }
        }

        /// <summary>
        /// 选中状态变化：选中时立即刷新参数（对齐 Roi 端"选中即显示参数"，
        /// 无需拖拽手柄才刷新）；取消选中时由管理器隐藏折叠框，无需处理。
        /// </summary>
        private void OnSelectionChanged(object? sender, bool selected)
        {
            if (selected)
                RefreshFromEditor();
        }

        /// <summary>
        /// 卡尺参数变更（主卡尺手柄拖拽）→ 刷新卡尺数值控件
        /// </summary>
        private void OnCaliberParamsChanged(object? sender, CaliberParamsChangedEventArgs e)
        {
            if (_syncingFromEditor) return;
            _syncingFromEditor = true;
            try
            {
                RefreshCaliberControls();
            }
            finally
            {
                _syncingFromEditor = false;
            }
        }

        #region 画布 → 面板刷新

        private void OnGuideGeometryChanged(object? sender, GuideGeometryChangedEventArgs e)
        {
            // 完成/编辑/移动均刷新（绘制预览只调 Render 不触发本事件，无需区分 IsDrawing）
            RefreshFromEditor();
        }

        /// <summary>
        /// 从编辑器当前状态刷新面板数值与可见性
        /// </summary>
        public void RefreshFromEditor()
        {
            var editor = Editor;
            if (editor == null || !editor.HasGuide)
            {
                LinePanel.Visibility = Visibility.Collapsed;
                CirclePanel.Visibility = Visibility.Collapsed;
                ArcPanel.Visibility = Visibility.Collapsed;
                CaliberPanel.Visibility = Visibility.Collapsed;
                return;
            }

            var type = editor.GuideGeometryType;
            LinePanel.Visibility = type == MeasureGeometryType.Line ? Visibility.Visible : Visibility.Collapsed;
            CirclePanel.Visibility = type == MeasureGeometryType.Circle ? Visibility.Visible : Visibility.Collapsed;
            ArcPanel.Visibility = type == MeasureGeometryType.Arc ? Visibility.Visible : Visibility.Collapsed;
            CaliberPanel.Visibility = Visibility.Visible;

            _syncingFromEditor = true;
            try
            {
                RefreshCaliberControls();
                switch (type)
                {
                    case MeasureGeometryType.Line:
                        RefreshLineValues();
                        break;
                    case MeasureGeometryType.Circle:
                        RefreshCircleValues();
                        break;
                    case MeasureGeometryType.Arc:
                        RefreshArcValues();
                        break;
                }
            }
            finally
            {
                _syncingFromEditor = false;
            }
        }

        /// <summary>是否正在从编辑器同步（避免面板写回触发循环刷新）</summary>
        private bool _syncingFromEditor;

        private void RefreshCaliberControls()
        {
            var editor = Editor;
            if (editor == null) return;
            CaliberCount.Value = editor.CaliberCount;
            CaliberHeight.Value = editor.CaliberHeight;
            CaliberWidth.Value = editor.CaliberWidth;
        }

        private void RefreshLineValues()
        {
            var editor = Editor;
            if (editor?.EditingLine == null) return;
            LineStartX.Value = editor.InteractionModel.LineStartPoint.X;
            LineStartY.Value = editor.InteractionModel.LineStartPoint.Y;
            LineEndX.Value = editor.InteractionModel.LineEndPoint.X;
            LineEndY.Value = editor.InteractionModel.LineEndPoint.Y;
        }

        private void RefreshCircleValues()
        {
            var editor = Editor;
            if (editor?.EditingCircle == null) return;
            CircleCenterX.Value = editor.EditingCircle.Value.Center.X;
            CircleCenterY.Value = editor.EditingCircle.Value.Center.Y;
            CircleRadius.Value = editor.EditingCircle.Value.Radius;
        }

        private void RefreshArcValues()
        {
            var editor = Editor;
            if (editor?.EditingArc == null || editor.EditingArc.Value.IsEmpty) return;
            var arc = editor.EditingArc.Value;
            ArcStartX.Value = editor.InteractionModel.ArcPoint1.X;
            ArcStartY.Value = editor.InteractionModel.ArcPoint1.Y;
            ArcEndX.Value = editor.InteractionModel.ArcPoint2.X;
            ArcEndY.Value = editor.InteractionModel.ArcPoint2.Y;
            ArcRadius.Value = arc.Radius;
            ArcDirection.SelectedIndex = arc.IsClockwise ? 1 : 0;
            ArcMajor.SelectedIndex = arc.IsMajorArc ? 1 : 0;
        }

        #endregion

        #region 面板 → 画布写回



        /// <summary>
        /// 应用面板当前数值到编辑器（起点/终点/圆心/半径 → 重建几何）
        /// </summary>
        private void ApplyGeometryFromPanel()
        {
            if (Editor == null) return;

            switch (Editor.GuideGeometryType)
            {
                case MeasureGeometryType.Line:
                    ApplyLine();
                    break;
                case MeasureGeometryType.Circle:
                    ApplyCircle();
                    break;
                case MeasureGeometryType.Arc:
                    ApplyArc();
                    break;
            }
        }

        private void ApplyLine()
        {
            var editor = Editor!;
            var start = new Point(LineStartX.Value, LineStartY.Value);
            var end = new Point(LineEndX.Value, LineEndY.Value);
            if ((end - start).Length < 1e-6) return;
            // 面板输入的是精确端点，不能调用仅接收 Line2d 的兜底重载：
            // 该重载会把线段扩展为默认 200px，再由事件把错误终点写回工具参数。
            // 此处同时通知宿主，使 LineDetect 等工具参数与画布使用同一组端点。
            editor.SetGuide(Line2d.FromTwoPoints(start, end), start, end, notifyGeometryChanged: true);
        }

        private void ApplyCircle()
        {
            var editor = Editor!;
            var center = new Point(CircleCenterX.Value, CircleCenterY.Value);
            var radius = Math.Max(1.0, CircleRadius.Value);
            editor.SetGuide(new Circle2d(center, radius), notifyGeometryChanged: true);
        }

        private void ApplyArc()
        {
            var editor = Editor!;
            var p1 = new Point(ArcStartX.Value, ArcStartY.Value);
            var p2 = new Point(ArcEndX.Value, ArcEndY.Value);
            var radius = Math.Max(1.0, ArcRadius.Value);
            bool clockwise = ArcDirection.SelectedIndex == 1;
            bool majorArc = ArcMajor.SelectedIndex == 1;
            var arc = Arc2d.FromEndpointsAndRadius(p1, p2, radius, clockwise, majorArc);
            if (arc.IsEmpty) return;
            editor.SetGuide(arc, notifyGeometryChanged: true);
        }

        private void ApplyCaliberValues()
        {
            var editor = Editor;
            if (editor == null) return;
            editor.EditCaliberParams(
                CaliberWidth.Value,
                CaliberHeight.Value,
                Math.Max(1, (int)Math.Round(CaliberCount.Value)));
        }

        #endregion

        #region 枚举下拉事件

        private void ArcDirection_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingFromEditor || Editor == null) return;
            ApplyArc();
        }

        private void ArcMajor_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_syncingFromEditor || Editor == null) return;
            ApplyArc();
        }

        #endregion
    }
}
