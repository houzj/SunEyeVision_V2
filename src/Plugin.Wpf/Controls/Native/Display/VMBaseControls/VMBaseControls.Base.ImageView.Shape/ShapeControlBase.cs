using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class ShapeControlBase : System.Windows.Shapes.Shape, INotifyPropertyChanged
{
	public static readonly DependencyProperty BaseStrokeThicknessProperty = DependencyProperty.Register("BaseStrokeThickness", typeof(double), typeof(ShapeControlBase), new PropertyMetadata(1.0, OnThicknessScaleChanged));

	public static readonly DependencyProperty ThicknessScaleProperty = DependencyProperty.Register("ThicknessScale", typeof(double), typeof(ShapeControlBase), new PropertyMetadata(1.0, OnThicknessScaleChanged));

	public static readonly DependencyProperty ControlToImageRadioProperty = DependencyProperty.Register("ControlToImageRadio", typeof(double), typeof(ShapeControlBase), new PropertyMetadata(1.0));

	public static readonly DependencyProperty ImageSourceWidthProperty = DependencyProperty.Register("ImageSourceWidth", typeof(int), typeof(ShapeControlBase), new PropertyMetadata(0));

	public static readonly DependencyProperty ImageSourceHeightProperty = DependencyProperty.Register("ImageSourceHeight", typeof(int), typeof(ShapeControlBase), new PropertyMetadata(0));

	protected PathGeometry geometry = new PathGeometry();

	public double BaseStrokeThickness
	{
		get
		{
			return (double)GetValue(BaseStrokeThicknessProperty);
		}
		set
		{
			SetValue(BaseStrokeThicknessProperty, value);
		}
	}

	public double ThicknessScale
	{
		get
		{
			return (double)GetValue(ThicknessScaleProperty);
		}
		set
		{
			SetValue(ThicknessScaleProperty, value);
		}
	}

	public double ControlToImageRadio
	{
		get
		{
			return (double)GetValue(ControlToImageRadioProperty);
		}
		set
		{
			SetValue(ControlToImageRadioProperty, value);
		}
	}

	public int ImageSourceWidth
	{
		get
		{
			return (int)GetValue(ImageSourceWidthProperty);
		}
		set
		{
			SetValue(ImageSourceWidthProperty, value);
		}
	}

	public int ImageSourceHeight
	{
		get
		{
			return (int)GetValue(ImageSourceHeightProperty);
		}
		set
		{
			SetValue(ImageSourceHeightProperty, value);
		}
	}

	protected override Geometry DefiningGeometry => geometry;

	public event PropertyChangedEventHandler PropertyChanged;

	public void OnPropertyChanged(string strPropertyInfo)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(strPropertyInfo));
		}
	}

	private static void OnThicknessScaleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		ShapeControlBase shapeControlBase = d as ShapeControlBase;
		shapeControlBase.ChangeThickness();
	}

	protected virtual void ChangeThickness()
	{
		StrokeThickness = BaseStrokeThickness / ThicknessScale;
	}
}
