using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

public partial class LightnessBar : Control, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	public delegate void StartValueChange(object sender, MouseButtonEventArgs e);

	protected static int _width = 150;

	protected static int _height = 32;

	protected int d = 5;

	private Slider SL_Bat1;

	private Slider SL_Bat2;

	private LineGeometry iHighPathLine;

	private LineGeometry iLowPathLine;

	private Path iLowPointLine;

	private Path iHighPointLine;

	private Canvas view;

	private Grid imageGrid;

	public static readonly DependencyProperty ImageProperty = DependencyProperty.Register("BgImage", typeof(WriteableBitmap), typeof(LightnessBar), new PropertyMetadata(null));

	public static readonly DependencyProperty ILowProperty = DependencyProperty.Register("ILow", typeof(int), typeof(LightnessBar), new PropertyMetadata(0, OnILowChange));

	public static readonly DependencyProperty IHighProperty = DependencyProperty.Register("IHigh", typeof(int), typeof(LightnessBar), new PropertyMetadata(0, OnIHighChange));

	private bool isIRangeChanging = false;

	public static readonly DependencyProperty IRangeProperty = DependencyProperty.Register("IRange", typeof(Point), typeof(LightnessBar), new PropertyMetadata(default(Point), OnIRangeChange));

	private Geometry _StartRectClip = new RectangleGeometry(new Rect(0.0, 0.0, _width, _height));

	private Geometry _EndRectClip = new RectangleGeometry(new Rect(0.0, 0.0, _width, _height));

	public SliderArrangeType SliderType = SliderArrangeType.between;

	public static readonly DependencyProperty DataPointsProperty = DependencyProperty.Register("DataPoints", typeof(PointCollection), typeof(LightnessBar), new PropertyMetadata(null, OnDataPointsChanged));

	public WriteableBitmap BgImage
	{
		get
		{
			return (WriteableBitmap)GetValue(ImageProperty);
		}
		set
		{
			SetValue(ImageProperty, value);
		}
	}

	public int ILow
	{
		get
		{
			return (int)GetValue(ILowProperty);
		}
		set
		{
			SetValue(ILowProperty, value);
		}
	}

	public int IHigh
	{
		get
		{
			return (int)GetValue(IHighProperty);
		}
		set
		{
			SetValue(IHighProperty, value);
		}
	}

	public Point IRange
	{
		get
		{
			return (Point)GetValue(IRangeProperty);
		}
		set
		{
			SetValue(IRangeProperty, value);
		}
	}

	public Geometry StartRectClip
	{
		get
		{
			return _StartRectClip;
		}
		set
		{
			if (value != _StartRectClip)
			{
				_StartRectClip = value;
				OnPropertyChanged("StartRectClip");
			}
		}
	}

	public Geometry EndRectClip
	{
		get
		{
			return _EndRectClip;
		}
		set
		{
			if (value != _EndRectClip)
			{
				_EndRectClip = value;
				OnPropertyChanged("EndRectClip");
			}
		}
	}

	public PointCollection DataPoints
	{
		get
		{
			return (PointCollection)GetValue(DataPointsProperty);
		}
		set
		{
			SetValue(DataPointsProperty, value);
		}
	}

	public event StartValueChange OnStartValueChange;

	public event PropertyChangedEventHandler PropertyChanged;

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		imageGrid = Template.FindName("imageGrid", this) as Grid;
		SL_Bat1 = Template.FindName("SL_Bat1", this) as Slider;
		SL_Bat2 = Template.FindName("SL_Bat2", this) as Slider;
		iHighPathLine = Template.FindName("iHighPathLine", this) as LineGeometry;
		iLowPathLine = Template.FindName("iLowPathLine", this) as LineGeometry;
		iLowPointLine = Template.FindName("iLowPointLine", this) as Path;
		iHighPointLine = Template.FindName("iHighPointLine", this) as Path;
		view = Template.FindName("view", this) as Canvas;
		SL_Bat1.SizeChanged += SliderSizeChangedHorizon;
		view.SizeChanged += SliderSizeChangedHorizon;
	}

	private void SliderSizeChangedHorizon(object sender, EventArgs e)
	{
		ClipSilder();
		DoDataPointsChanged();
	}

	public LightnessBar()
	{
		InitializeComponent();
	}

	protected virtual void OnPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	private static void OnILowChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d as LightnessBar).isIRangeChanging)
		{
			if ((int)e.NewValue > (d as LightnessBar).IHigh)
			{
				(d as LightnessBar).ILow = (d as LightnessBar).IHigh;
			}
			else
			{
				(d as LightnessBar).IRange = new Point((int)e.NewValue, (d as LightnessBar).IHigh);
			}
		}
	}

	private static void OnIHighChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d as LightnessBar).isIRangeChanging)
		{
			if ((int)e.NewValue < (d as LightnessBar).ILow)
			{
				(d as LightnessBar).IHigh = (d as LightnessBar).ILow;
			}
			else
			{
				(d as LightnessBar).IRange = new Point((d as LightnessBar).ILow, (int)e.NewValue);
			}
		}
	}

	private static void OnIRangeChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as LightnessBar).isIRangeChanging = true;
		(d as LightnessBar).ILow = (int)((Point)e.NewValue).X;
		(d as LightnessBar).IHigh = (int)((Point)e.NewValue).Y;
		(d as LightnessBar).isIRangeChanging = false;
	}

	protected void ClipSilder()
	{
		double width = SL_Bat1.Width;
		double height = SL_Bat1.Height;
		if (width != 0.0 && !double.IsNaN(width) && height != 0.0 && !double.IsNaN(height) && view.Height != 0.0 && !double.IsNaN(view.Height))
		{
			double num = IHigh / 2 + ILow / 2;
			double num2 = 255.0;
			double x = (width - (double)(2 * d)) * (num - 0.0) / num2 + (double)d;
			StartRectClip = new RectangleGeometry(new Rect(0.0, 0.0, width, height));
			EndRectClip = new RectangleGeometry(new Rect(x, 0.0, width, height));
			int num3 = (int)((double)ILow * (Width - 10.0) / 255.0);
			iLowPathLine.StartPoint = new Point(num3, 0.0);
			iLowPathLine.EndPoint = new Point(num3, view.Height);
			int num4 = (int)((double)IHigh * (Width - 10.0) / 255.0);
			iHighPathLine.StartPoint = new Point(num4, 0.0);
			iHighPathLine.EndPoint = new Point(num4, view.Height);
		}
	}

	protected void SL_Bat1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		ClipSilder();
	}

	protected void SL_Bat2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		ClipSilder();
	}

	protected void RepeatButton_Click(object sender, RoutedEventArgs e)
	{
	}

	protected void Thumb_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
	{
		if (OnStartValueChange != null)
		{
			OnStartValueChange(sender, e);
		}
	}

	protected void Thumb_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
	}

	private static void OnDataPointsChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as LightnessBar).DoDataPointsChanged();
	}

	private void DoDataPointsChanged()
	{
		if (DataPoints != null && view != null && DataPoints.Count > 0)
		{
			int num = (int)view.ActualWidth;
			int num2 = (int)view.ActualHeight;
			ClearPoint();
			for (int i = 0; i < DataPoints.Count; i++)
			{
				int num3 = (int)(DataPoints[i].X * (double)num / 255.0);
				int num4 = (int)((31.0 - DataPoints[i].Y) * (double)num2 / 31.0);
				Line line = new Line();
				line.X1 = num3;
				line.Y1 = num4;
				line.X2 = num3 + 1;
				line.Y2 = num4 + 1;
				line.Stroke = Brushes.Red;
				view.Children.Add(line);
			}
		}
		else
		{
			ClearPoint();
		}
	}

	private void ClearPoint()
	{
		if (iLowPointLine != null && iHighPointLine != null && view != null)
		{
			Path element = iLowPointLine;
			Path element2 = iHighPointLine;
			view.Children.Clear();
			view.Children.Add(element);
			view.Children.Add(element2);
		}
	}
}
