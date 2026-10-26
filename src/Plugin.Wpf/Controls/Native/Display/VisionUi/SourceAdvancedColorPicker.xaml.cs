using SunEyeVision.Plugin.Wpf.Native.Display.VisionUi;
using SunEyeVision.Plugin.Wpf.Native.Input.VisionUi;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.WriteableBitmapEx;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VisionUi;

public partial class SourceAdvancedColorPicker : UserControl
{
	private byte[]? _pixels;

	public Color CurrentColor = Colors.Gray;
 public event Action<SolidColorBrush>? ColorChanged;

	public static DependencyProperty SelectedCurrentColorProperty = DependencyProperty.Register("SelectedCurrentColor", typeof(SolidColorBrush), typeof(SourceAdvancedColorPicker), new PropertyMetadata(Brushes.White,(d,e)=>((SourceAdvancedColorPicker)d).ColorChanged?.Invoke((SolidColorBrush)e.NewValue)));

	private readonly TranslateTransform _markerTransform = new TranslateTransform();

	public SolidColorBrush SelectedCurrentColor
	{
		get
		{
			return (SolidColorBrush)GetValue(SelectedCurrentColorProperty);
		}
		set
		{
			SetValue(SelectedCurrentColorProperty, value);
		}
	}


	public event DragDeltaEventHandler? Drag;

	private void OnDragDelta(DragDeltaEventArgs e)
	{
		Drag?.Invoke(this, e);
	}

	public SourceAdvancedColorPicker()
	{
		InitializeComponent();
		brightnessSlider.ValueChanged += BrightnessSliderValueChanged;
		colorMarker.RenderTransform = _markerTransform;
		colorMarker.RenderTransformOrigin = new Point(1.0, 1.0);
		borderColorChart.Cursor = Cursors.Cross;
		brightnessSlider.Value = 0.5;
	}

    // 宿主组合适配：重开色板时同步原算法的基色，避免亮度仍沿用上次取色。
    public void SynchronizeSelection(Color color)
    {
        _pixels = new[] { color.B, color.G, color.R, color.A };
        CurrentColor = color;
        brightnessSlider.Value = 0.5;
        SetCurrentValue(SelectedCurrentColorProperty, new SolidColorBrush(color));
        UpdateSlider();
    }

	private void Image_MouseDown(object sender, MouseButtonEventArgs e)
	{
		if(e.Source is not Image sourceImage)return;
		try
		{
			CroppedBitmap croppedBitmap = new CroppedBitmap((BitmapSource)((Image)e.Source).Source, new Int32Rect((int)Mouse.GetPosition(sourceImage).X, (int)Mouse.GetPosition(sourceImage).Y, 1, 1));
			_pixels = new byte[4];
			try
			{
				croppedBitmap.CopyPixels(_pixels, 4, 0);
				UpdateCurrentColor();
				UpdateMarkerPosition();
			}
			catch
			{
			}
			UpdateSlider();
		}
		catch (Exception)
		{
		}
	}

	private void UpdateCurrentColor()
	{
		if(_pixels is null)return;
		CurrentColor = Color.FromRgb(_pixels[2], _pixels[1], _pixels[0]);
		brightnessSlider.Value = 0.5;
		SelectedCurrentColor = new SolidColorBrush(CurrentColor);
	}

	private void UpdateMarkerPosition()
	{
		_markerTransform.X = Mouse.GetPosition(borderColorChart).X - borderColorChart.ActualWidth / 2.0;
		_markerTransform.Y = Mouse.GetPosition(borderColorChart).Y - borderColorChart.ActualHeight / 2.0;
	}

	private void BrightnessSliderValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		if (_pixels == null)
		{
			_pixels = new byte[3];
			_pixels[2] = CurrentColor.R;
			_pixels[1] = CurrentColor.G;
			_pixels[0] = CurrentColor.B;
		}
		Color color = Color.FromRgb(_pixels[2], _pixels[1], _pixels[0]);
		float num = (float)e.NewValue;
		float r;
		float g;
		float b;
		if (num >= 0.5f)
		{
			r = color.ScR + (1f - color.ScR) * (num - 0.5f) * 2f;
			g = color.ScG + (1f - color.ScG) * (num - 0.5f) * 2f;
			b = color.ScB + (1f - color.ScB) * (num - 0.5f) * 2f;
		}
		else
		{
			r = color.ScR * num * 2f;
			g = color.ScG * num * 2f;
			b = color.ScB * num * 2f;
		}
		CurrentColor = Color.FromScRgb(1f, r, g, b);
		SelectedCurrentColor = new SolidColorBrush(CurrentColor);
	}

	private void UpdateSlider()
	{
		if(_pixels is null)return;
		Color color = Color.FromRgb(_pixels[2], _pixels[1], _pixels[0]);
		float r = 1f * color.ScR;
		float g = 1f * color.ScG;
		float b = 1f * color.ScB;
		Border border = brightnessSliderBorder;
		if(border.Background is not LinearGradientBrush linearGradientBrush)return;
        if (linearGradientBrush.IsFrozen)
        {
            linearGradientBrush = linearGradientBrush.Clone();
            border.Background = linearGradientBrush;
        }
		linearGradientBrush.StartPoint = new Point(0.0, 1.0);
		linearGradientBrush.EndPoint = new Point(0.0, 0.0);
		linearGradientBrush.GradientStops[0].Color = Color.FromScRgb(1f, 0f, 0f, 0f);
		linearGradientBrush.GradientStops[1].Color = Color.FromScRgb(1f, r, g, b);
		linearGradientBrush.GradientStops[2].Color = Color.FromScRgb(1f, 1f, 1f, 1f);
	}

	private void Image_MouseMove(object sender, MouseEventArgs e)
	{
		if (Mouse.LeftButton == MouseButtonState.Pressed && e.Source is Image sourceImage)
		{
			try
			{
				Int32Rect sourceRect = new Int32Rect((int)Mouse.GetPosition(sourceImage).X, (int)Mouse.GetPosition(sourceImage).Y, 1, 1);
				if ((double)sourceRect.Y > sourceImage.ActualHeight - 2.0)
				{
					return;
				}
				CroppedBitmap croppedBitmap = new CroppedBitmap((BitmapSource)((Image)e.Source).Source, sourceRect);
				_pixels = new byte[4];
				croppedBitmap.CopyPixels(_pixels, 4, 0);
				UpdateMarkerPosition();
				UpdateCurrentColor();
				Mouse.Synchronize();
			}
			catch
			{
			}
			UpdateSlider();
		}
		e.Handled = true;
	}
}
