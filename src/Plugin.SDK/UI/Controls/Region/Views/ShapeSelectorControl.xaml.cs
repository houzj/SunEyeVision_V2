using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using SunEyeVision.Plugin.SDK.UI.Controls.Region.Models;

namespace SunEyeVision.Plugin.SDK.UI.Controls.Region.Views
{
    public partial class ShapeSelectorControl : UserControl
    {
        public static readonly DependencyProperty SelectedShapeTypeProperty =
            DependencyProperty.Register(nameof(SelectedShapeType), typeof(ShapeType),
                typeof(ShapeSelectorControl), new PropertyMetadata(ShapeType.None));

        public ShapeType SelectedShapeType
        {
            get => (ShapeType)GetValue(SelectedShapeTypeProperty);
            set => SetValue(SelectedShapeTypeProperty, value);
        }

        public static readonly DependencyProperty IsRectangleVisibleProperty =
            DependencyProperty.Register(nameof(IsRectangleVisible), typeof(bool),
                typeof(ShapeSelectorControl), new PropertyMetadata(true, OnVisibilityChanged));
        public bool IsRectangleVisible
        {
            get => (bool)GetValue(IsRectangleVisibleProperty);
            set => SetValue(IsRectangleVisibleProperty, value);
        }

        public static readonly DependencyProperty IsCircleVisibleProperty =
            DependencyProperty.Register(nameof(IsCircleVisible), typeof(bool),
                typeof(ShapeSelectorControl), new PropertyMetadata(true, OnVisibilityChanged));
        public bool IsCircleVisible
        {
            get => (bool)GetValue(IsCircleVisibleProperty);
            set => SetValue(IsCircleVisibleProperty, value);
        }

        public static readonly DependencyProperty IsRotatedRectangleVisibleProperty =
            DependencyProperty.Register(nameof(IsRotatedRectangleVisible), typeof(bool),
                typeof(ShapeSelectorControl), new PropertyMetadata(true, OnVisibilityChanged));
        public bool IsRotatedRectangleVisible
        {
            get => (bool)GetValue(IsRotatedRectangleVisibleProperty);
            set => SetValue(IsRotatedRectangleVisibleProperty, value);
        }

        public static readonly DependencyProperty IsLineVisibleProperty =
            DependencyProperty.Register(nameof(IsLineVisible), typeof(bool),
                typeof(ShapeSelectorControl), new PropertyMetadata(true, OnVisibilityChanged));
        public bool IsLineVisible
        {
            get => (bool)GetValue(IsLineVisibleProperty);
            set => SetValue(IsLineVisibleProperty, value);
        }

        public static readonly DependencyProperty IsPolygonVisibleProperty =
            DependencyProperty.Register(nameof(IsPolygonVisible), typeof(bool),
                typeof(ShapeSelectorControl), new PropertyMetadata(true, OnVisibilityChanged));
        public bool IsPolygonVisible
        {
            get => (bool)GetValue(IsPolygonVisibleProperty);
            set => SetValue(IsPolygonVisibleProperty, value);
        }

        public event EventHandler<ShapeType>? ShapeTypeChanged;

        public ShapeSelectorControl()
        {
            InitializeComponent();
            Loaded += OnLoaded;
        }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            ApplyVisibilities();
        }

        public void DeselectAll()
        {
            SelectedShapeType = ShapeType.None;
            ResetAllButtons();
        }

        private void OnRectangleClick(object sender, RoutedEventArgs e) => SelectShape(ShapeType.Rectangle, RectangleButton);
        private void OnCircleClick(object sender, RoutedEventArgs e) => SelectShape(ShapeType.Circle, CircleButton);
        private void OnRotatedRectangleClick(object sender, RoutedEventArgs e) => SelectShape(ShapeType.RotatedRectangle, RotatedRectangleButton);
        private void OnLineClick(object sender, RoutedEventArgs e) => SelectShape(ShapeType.Line, LineButton);
        private void OnPolygonClick(object sender, RoutedEventArgs e) => SelectShape(ShapeType.Polygon, PolygonButton);

        private void SelectShape(ShapeType type, Button button)
        {
            if (SelectedShapeType == type)
            {
                DeselectAll();
                return;
            }

            ResetAllButtons();
            SetButtonActive(button);
            SelectedShapeType = type;
            ShapeTypeChanged?.Invoke(this, type);
        }

        private static readonly SolidColorBrush ActiveBg = new(Color.FromRgb(230, 242, 255));
        private static readonly SolidColorBrush ActiveBorder = new(Color.FromRgb(0, 120, 215));
        private static readonly SolidColorBrush InactiveBg = Brushes.Transparent;
        private static readonly SolidColorBrush InactiveBorder = new(Color.FromRgb(224, 224, 224));
        private static readonly Color ActiveIconColor = Color.FromRgb(0, 120, 215);
        private static readonly Color InactiveIconColor = Color.FromRgb(85, 85, 85);

        private static void SetButtonActive(Button button)
        {
            button.Background = ActiveBg;
            button.BorderBrush = ActiveBorder;
            button.BorderThickness = new Thickness(2);
            SetIconColor(button, ActiveIconColor);
        }

        private static void ResetButtonStyle(Button button)
        {
            button.Background = InactiveBg;
            button.BorderBrush = InactiveBorder;
            button.BorderThickness = new Thickness(1);
            SetIconColor(button, InactiveIconColor);
        }

        private void ResetAllButtons()
        {
            foreach (var child in ButtonPanel.Children)
            {
                if (child is Button btn)
                    ResetButtonStyle(btn);
            }
        }

        private static void SetIconColor(Button button, Color color)
        {
            var brush = new SolidColorBrush(color);
            if (button.Content is TextBlock tb)
                tb.Foreground = brush;
            else if (button.Content is Shape shape)
                shape.Stroke = brush;
        }

        private void ApplyVisibilities()
        {
            RectangleButton.Visibility = IsRectangleVisible ? Visibility.Visible : Visibility.Collapsed;
            CircleButton.Visibility = IsCircleVisible ? Visibility.Visible : Visibility.Collapsed;
            RotatedRectangleButton.Visibility = IsRotatedRectangleVisible ? Visibility.Visible : Visibility.Collapsed;
            LineButton.Visibility = IsLineVisible ? Visibility.Visible : Visibility.Collapsed;
            PolygonButton.Visibility = IsPolygonVisible ? Visibility.Visible : Visibility.Collapsed;
        }

        private static void OnVisibilityChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
        {
            if (d is ShapeSelectorControl ctrl)
                ctrl.ApplyVisibilities();
        }
    }
}
