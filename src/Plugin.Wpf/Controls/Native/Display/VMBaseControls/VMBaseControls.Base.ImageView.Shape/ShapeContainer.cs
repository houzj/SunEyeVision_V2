using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.Shape;

public class ShapeContainer : UserControl, INotifyPropertyChanged
{
	public static readonly DependencyProperty BaseStrokeThicknessProperty = DependencyProperty.Register("BaseStrokeThickness", typeof(double), typeof(ShapeContainer), new PropertyMetadata(1.0, OnThicknessScaleChanged));

	public static readonly DependencyProperty ContainerWidthProperty = DependencyProperty.Register("ContainerWidth", typeof(double), typeof(ShapeContainer), new PropertyMetadata(0.0));

	public static readonly DependencyProperty ContainerHeightProperty = DependencyProperty.Register("ContainerHeight", typeof(double), typeof(ShapeContainer), new PropertyMetadata(0.0));

	public static readonly DependencyProperty ContainerPositionProperty = DependencyProperty.Register("ContainerPosition", typeof(Point), typeof(ShapeContainer), new PropertyMetadata(default(Point)));

	public static readonly DependencyProperty ThicknessScaleProperty = DependencyProperty.Register("ThicknessScale", typeof(double), typeof(ShapeContainer), new PropertyMetadata(1.0, OnThicknessScaleChanged));

	public static readonly DependencyProperty ControlToImageRadioProperty = DependencyProperty.Register("ControlToImageRadio", typeof(double), typeof(ShapeContainer), new PropertyMetadata(1.0));

	public static readonly DependencyProperty ImageSourceWidthProperty = DependencyProperty.Register("ImageSourceWidth", typeof(int), typeof(ShapeContainer), new PropertyMetadata(0));

	public static readonly DependencyProperty ImageSourceHeightProperty = DependencyProperty.Register("ImageSourceHeight", typeof(int), typeof(ShapeContainer), new PropertyMetadata(0));

	protected Canvas container;

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

	public double ContainerWidth
	{
		get
		{
			return (double)GetValue(ContainerWidthProperty);
		}
		set
		{
			SetValue(ContainerWidthProperty, value);
		}
	}

	public double ContainerHeight
	{
		get
		{
			return (double)GetValue(ContainerHeightProperty);
		}
		set
		{
			SetValue(ContainerHeightProperty, value);
		}
	}

	public Point ContainerPosition
	{
		get
		{
			return (Point)GetValue(ContainerPositionProperty);
		}
		set
		{
			SetValue(ContainerPositionProperty, value);
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
		ShapeContainer shapeContainer = d as ShapeContainer;
		shapeContainer.ChangeThickness();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		container = Template.FindName("container", this) as Canvas;
	}

	protected virtual void ChangeThickness()
	{
	}
}
