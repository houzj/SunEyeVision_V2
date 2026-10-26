using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;

public partial class AdvancedColorPickerDialog : UserControl, IComponentConnector
{
	private byte[] _pixels;

	public Color CurrentColor = Colors.Gray;

	public static DependencyProperty SelectedCurrentColorProperty = DependencyProperty.Register("SelectedCurrentColor", typeof(SolidColorBrush), typeof(AdvancedColorPickerDialog), new PropertyMetadata(Brushes.White));

	public static readonly DependencyProperty ColorChangedCommandProperty = DependencyProperty.Register("ColorChangedCommand", typeof(ICommand), typeof(AdvancedColorPickerDialog), new PropertyMetadata(null));

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

	public ICommand ColorChangedCommand
	{
		get
		{
			return (ICommand)GetValue(ColorChangedCommandProperty);
		}
		set
		{
			SetValue(ColorChangedCommandProperty, value);
		}
	}

	public event DragDeltaEventHandler Drag;

	private void OnDragDelta(DragDeltaEventArgs e)
	{
		Drag?.Invoke(this, e);
	}

	public AdvancedColorPickerDialog()
	{
		InitializeComponent();
		brightnessSlider.ValueChanged += BrightnessSliderValueChanged;
		colorMarker.RenderTransform = _markerTransform;
		colorMarker.RenderTransformOrigin = new Point(1.0, 1.0);
		borderColorChart.Cursor = Cursors.Cross;
		brightnessSlider.Value = 0.5;
	}

	private void Image_MouseDown(object sender, MouseButtonEventArgs e)
	{
		try
		{
			CroppedBitmap croppedBitmap = new CroppedBitmap((BitmapSource)((Image)e.Source).Source, new Int32Rect((int)Mouse.GetPosition(e.Source as Image).X, (int)Mouse.GetPosition(e.Source as Image).Y, 1, 1));
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
			ColorChangedCommand?.Execute(SelectedCurrentColor);
		}
		catch (Exception)
		{
		}
	}

	private void UpdateCurrentColor()
	{
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
		ColorChangedCommand?.Execute(SelectedCurrentColor);
	}

	private void UpdateSlider()
	{
		Color color = Color.FromRgb(_pixels[2], _pixels[1], _pixels[0]);
		float r = 1f * color.ScR;
		float g = 1f * color.ScG;
		float b = 1f * color.ScB;
		Border border = brightnessSliderBorder;
		LinearGradientBrush linearGradientBrush = border.Background as LinearGradientBrush;
		linearGradientBrush.StartPoint = new Point(0.0, 1.0);
		linearGradientBrush.EndPoint = new Point(0.0, 0.0);
		linearGradientBrush.GradientStops[0].Color = Color.FromScRgb(1f, 0f, 0f, 0f);
		linearGradientBrush.GradientStops[1].Color = Color.FromScRgb(1f, r, g, b);
		linearGradientBrush.GradientStops[2].Color = Color.FromScRgb(1f, 1f, 1f, 1f);
	}

	private void Image_MouseMove(object sender, MouseEventArgs e)
	{
		if (Mouse.LeftButton == MouseButtonState.Pressed && e.Source.GetType().Equals(typeof(Image)))
		{
			try
			{
				Int32Rect sourceRect = new Int32Rect((int)Mouse.GetPosition(e.Source as Image).X, (int)Mouse.GetPosition(e.Source as Image).Y, 1, 1);
				if ((double)sourceRect.Y > (e.Source as Image).ActualHeight - 2.0)
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
