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

public partial class ChromaticDiagram : Control, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	public delegate void StartValueChange(object sender, MouseButtonEventArgs e);

	protected static int _width = 150;

	protected static int _height = 32;

	protected int d = 5;

	protected bool IsThrough = false;

	private Border border1 = new Border();

	private Border border2 = new Border();

	private Slider SL_Bat1;

	private Slider SL_Bat2;

	private Slider SL_Bat3;

	private Slider SL_Bat4;

	private LineGeometry hHighPathLine;

	private LineGeometry hLowPathLine;

	private LineGeometry sLowPathLine;

	private LineGeometry sHighPathLine;

	private Path hLowPointLine;

	private Path hHighPointLine;

	private Path sLowPointLine;

	private Path sHighPointLine;

	private Canvas view;

	private Grid imageGrid;

	public static readonly DependencyProperty ImageProperty = DependencyProperty.Register("BgImage", typeof(WriteableBitmap), typeof(ChromaticDiagram), new PropertyMetadata(null));

	public static readonly DependencyProperty HLowProperty = DependencyProperty.Register("HLow", typeof(int), typeof(ChromaticDiagram), new PropertyMetadata(0, OnHLowChange));

	public static readonly DependencyProperty HHighProperty = DependencyProperty.Register("HHigh", typeof(int), typeof(ChromaticDiagram), new PropertyMetadata(0, OnHHighChange));

	private bool isHRangeChanging = false;

	public static readonly DependencyProperty HPointProperty = DependencyProperty.Register("HRange", typeof(Point), typeof(ChromaticDiagram), new PropertyMetadata(default(Point), OnHRangChange));

	public static readonly DependencyProperty SLowProperty = DependencyProperty.Register("SLow", typeof(int), typeof(ChromaticDiagram), new PropertyMetadata(0, OnSLowChange));

	public static readonly DependencyProperty SHighProperty = DependencyProperty.Register("SHigh", typeof(int), typeof(ChromaticDiagram), new PropertyMetadata(0, OnSHighChange));

	private bool isSRangeChanging = false;

	public static readonly DependencyProperty SRangeProperty = DependencyProperty.Register("SRange", typeof(Point), typeof(ChromaticDiagram), new PropertyMetadata(default(Point), OnSRangeChange));

	private double sliderbarWidth;

	private double sliderbarHight;

	private Geometry _StartRectClipHorizon = new RectangleGeometry(new Rect(0.0, 0.0, _width, _height));

	private Geometry _EndRectClipHorizon = new RectangleGeometry(new Rect(0.0, 0.0, _width, _height));

	private Geometry _StartRectClipVertical = new RectangleGeometry(new Rect(0.0, 0.0, _width, _height));

	private Geometry _EndRectClipVertical = new RectangleGeometry(new Rect(0.0, 0.0, _width, _height));

	public static readonly DependencyProperty DataPointsProperty = DependencyProperty.Register("DataPoints", typeof(PointCollection), typeof(ChromaticDiagram), new PropertyMetadata(null, OnDataPointsChanged));

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

	public int HLow
	{
		get
		{
			return (int)GetValue(HLowProperty);
		}
		set
		{
			SetValue(HLowProperty, value);
		}
	}

	public int HHigh
	{
		get
		{
			return (int)GetValue(HHighProperty);
		}
		set
		{
			SetValue(HHighProperty, value);
		}
	}

	public Point HRange
	{
		get
		{
			return (Point)GetValue(HPointProperty);
		}
		set
		{
			SetValue(HPointProperty, value);
		}
	}

	public int SLow
	{
		get
		{
			return (int)GetValue(SLowProperty);
		}
		set
		{
			SetValue(SLowProperty, value);
		}
	}

	public int SHigh
	{
		get
		{
			return (int)GetValue(SHighProperty);
		}
		set
		{
			SetValue(SHighProperty, value);
		}
	}

	public Point SRange
	{
		get
		{
			return (Point)GetValue(SRangeProperty);
		}
		set
		{
			SetValue(SRangeProperty, value);
		}
	}

	public double SliderbarWidth
	{
		get
		{
			return sliderbarWidth;
		}
		set
		{
			if (value != sliderbarWidth)
			{
				sliderbarWidth = value;
				OnPropertyChanged("SliderbarWidth");
			}
		}
	}

	public double SliderbarHight
	{
		get
		{
			return sliderbarHight;
		}
		set
		{
			if (value != sliderbarHight)
			{
				sliderbarHight = value;
				OnPropertyChanged("SliderbarHight");
			}
		}
	}

	public Geometry StartRectClipHorizon
	{
		get
		{
			return _StartRectClipHorizon;
		}
		set
		{
			if (value != _StartRectClipHorizon)
			{
				_StartRectClipHorizon = value;
				OnPropertyChanged("StartRectClipHorizon");
			}
		}
	}

	public Geometry EndRectClipHorizon
	{
		get
		{
			return _EndRectClipHorizon;
		}
		set
		{
			if (value != _EndRectClipHorizon)
			{
				_EndRectClipHorizon = value;
				OnPropertyChanged("EndRectClipHorizon");
			}
		}
	}

	public Geometry StartRectClipVertical
	{
		get
		{
			return _StartRectClipVertical;
		}
		set
		{
			if (value != _StartRectClipVertical)
			{
				_StartRectClipVertical = value;
				OnPropertyChanged("StartRectClipVertical");
			}
		}
	}

	public Geometry EndRectClipVertical
	{
		get
		{
			return _EndRectClipVertical;
		}
		set
		{
			if (value != _EndRectClipVertical)
			{
				_EndRectClipVertical = value;
				OnPropertyChanged("EndRectClipVertical");
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

	public ChromaticDiagram()
	{
		InitializeComponent();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		imageGrid = Template.FindName("imageGrid", this) as Grid;
		SL_Bat1 = Template.FindName("SL_Bat1", this) as Slider;
		SL_Bat2 = Template.FindName("SL_Bat2", this) as Slider;
		SL_Bat3 = Template.FindName("SL_Bat3", this) as Slider;
		SL_Bat4 = Template.FindName("SL_Bat4", this) as Slider;
		hHighPathLine = Template.FindName("hHighPathLine", this) as LineGeometry;
		hLowPathLine = Template.FindName("hLowPathLine", this) as LineGeometry;
		sLowPathLine = Template.FindName("sLowPathLine", this) as LineGeometry;
		sHighPathLine = Template.FindName("sHighPathLine", this) as LineGeometry;
		hLowPointLine = Template.FindName("hLowPointLine", this) as Path;
		hHighPointLine = Template.FindName("hHighPointLine", this) as Path;
		sLowPointLine = Template.FindName("sLowPointLine", this) as Path;
		sHighPointLine = Template.FindName("sHighPointLine", this) as Path;
		view = Template.FindName("view", this) as Canvas;
		border1.BorderThickness = new Thickness(1.0);
		border1.BorderBrush = new SolidColorBrush(Colors.Gray);
		border2.BorderThickness = new Thickness(1.0);
		border2.BorderBrush = new SolidColorBrush(Colors.Gray);
		view.SizeChanged += ViewSizeChange;
		SL_Bat1.SizeChanged += Slider1Change;
		SL_Bat3.SizeChanged += Slider3change;
	}

	private void Slider3change(object sender, SizeChangedEventArgs e)
	{
		if (e.HeightChanged)
		{
			SliderSizeChangedVertical(sender, e);
		}
	}

	private void Slider1Change(object sender, SizeChangedEventArgs e)
	{
		if (e.WidthChanged)
		{
			SliderSizeChangedHorizon(sender, e);
		}
	}

	private void ViewSizeChange(object sender, SizeChangedEventArgs e)
	{
		if (e.WidthChanged)
		{
			ViewSizeChanged(sender, e);
			SliderSizeChangedVertical(sender, e);
		}
		else if (e.HeightChanged)
		{
			SliderSizeChangedHorizon(sender, e);
			ViewSizeChanged(sender, e);
		}
	}

	private void ViewSizeChanged(object sender, EventArgs e)
	{
		if (view.Width != 0.0 && !double.IsNaN(view.Width) && view.Height != 0.0 && !double.IsNaN(view.Height))
		{
			SliderbarWidth = view.Width + 10.0;
			SliderbarHight = view.Height + 10.0;
			DoDataPointsChanged();
		}
	}

	private void SliderSizeChangedVertical(object sender, EventArgs e)
	{
		ClipSilderVertical();
	}

	private void SliderSizeChangedHorizon(object sender, EventArgs e)
	{
		ClipSilderHorizon();
	}

	private static void OnHLowChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d as ChromaticDiagram).isHRangeChanging)
		{
			if ((int)e.NewValue > (d as ChromaticDiagram).HHigh)
			{
				(d as ChromaticDiagram).IsThrough = true;
			}
			(d as ChromaticDiagram).HRange = new Point((int)e.NewValue, (d as ChromaticDiagram).HHigh);
		}
	}

	private static void OnHHighChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d as ChromaticDiagram).isHRangeChanging)
		{
			if ((int)e.NewValue < (d as ChromaticDiagram).HLow)
			{
				(d as ChromaticDiagram).IsThrough = true;
			}
			(d as ChromaticDiagram).HRange = new Point((d as ChromaticDiagram).HLow, (int)e.NewValue);
		}
	}

	private static void OnHRangChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as ChromaticDiagram).isHRangeChanging = true;
		(d as ChromaticDiagram).HLow = (int)((Point)e.NewValue).X;
		(d as ChromaticDiagram).HHigh = (int)((Point)e.NewValue).Y;
		(d as ChromaticDiagram).isHRangeChanging = false;
		(d as ChromaticDiagram).DrawBorder();
	}

	private static void OnSLowChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d as ChromaticDiagram).isSRangeChanging)
		{
			if ((int)e.NewValue > (d as ChromaticDiagram).SHigh)
			{
				(d as ChromaticDiagram).SLow = (d as ChromaticDiagram).SHigh;
			}
			else
			{
				(d as ChromaticDiagram).SRange = new Point((int)e.NewValue, (d as ChromaticDiagram).SHigh);
			}
		}
	}

	private static void OnSHighChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (!(d as ChromaticDiagram).isSRangeChanging)
		{
			if ((int)e.NewValue < (d as ChromaticDiagram).SLow)
			{
				(d as ChromaticDiagram).SHigh = (d as ChromaticDiagram).SLow;
			}
			else
			{
				(d as ChromaticDiagram).SRange = new Point((d as ChromaticDiagram).SLow, (int)e.NewValue);
			}
		}
	}

	private static void OnSRangeChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as ChromaticDiagram).isSRangeChanging = true;
		(d as ChromaticDiagram).SLow = (int)((Point)e.NewValue).X;
		(d as ChromaticDiagram).SHigh = (int)((Point)e.NewValue).Y;
		(d as ChromaticDiagram).isSRangeChanging = false;
		(d as ChromaticDiagram).DrawBorder();
	}

	protected virtual void OnPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	protected void ClipSilderHorizon()
	{
		double width = SL_Bat1.Width;
		double height = SL_Bat1.Height;
		if (width != 0.0 && !double.IsNaN(width) && height != 0.0 && !double.IsNaN(height) && view.Height != 0.0 && !double.IsNaN(view.Height))
		{
			if (HHigh < HLow)
			{
				IsThrough = true;
			}
			else
			{
				IsThrough = false;
			}
			double num = HHigh / 2 + HLow / 2;
			double num2 = 255.0;
			double num3 = (width - (double)(2 * d)) * (num - 0.0) / num2 + (double)d;
			if (!IsThrough)
			{
				StartRectClipHorizon = new RectangleGeometry(new Rect(0.0, 0.0, num3, height));
				EndRectClipHorizon = new RectangleGeometry(new Rect(num3, 0.0, width, height));
			}
			else
			{
				StartRectClipHorizon = new RectangleGeometry(new Rect(num3, 0.0, width, height));
				EndRectClipHorizon = new RectangleGeometry(new Rect(0.0, 0.0, num3, height));
			}
			int num4 = (int)((double)HLow * (SL_Bat1.Width - 10.0) / 255.0);
			hLowPathLine.StartPoint = new Point(num4, 0.0);
			hLowPathLine.EndPoint = new Point(num4, view.Height);
			int num5 = (int)((double)HHigh * (SL_Bat1.Width - 10.0) / 255.0);
			hHighPathLine.StartPoint = new Point(num5, 0.0);
			hHighPathLine.EndPoint = new Point(num5, view.Height);
			DrawBorder();
		}
	}

	private void DrawBorder()
	{
		if (SL_Bat1 == null || SL_Bat3 == null || SL_Bat1.Width == 0.0 || SL_Bat3.Height == 0.0 || isHRangeChanging || isSRangeChanging || SLow > SHigh)
		{
			return;
		}
		if (HHigh < HLow)
		{
			IsThrough = true;
		}
		else
		{
			IsThrough = false;
		}
		int num = (int)((double)HLow * (SL_Bat1.Width - 10.0) / 255.0);
		int num2 = (int)((double)HHigh * (SL_Bat1.Width - 10.0) / 255.0);
		int num3 = (int)((double)SLow * (SL_Bat3.Height - 10.0) / 255.0);
		int num4 = (int)((double)SHigh * (SL_Bat3.Height - 10.0) / 255.0);
		if (!IsThrough)
		{
			if (!view.Children.Contains(border1))
			{
				view.Children.Add(border1);
			}
			if (view.Children.Contains(border2))
			{
				view.Children.Remove(border2);
			}
			border1.BorderThickness = new Thickness(1.0);
			border1.Width = num2 - num;
			border1.Height = num4 - num3;
			Canvas.SetLeft(border1, num);
			Canvas.SetBottom(border1, (double)(255 - SHigh) * (SL_Bat3.Height - 10.0) / 255.0);
			return;
		}
		if (!view.Children.Contains(border1))
		{
			view.Children.Add(border1);
		}
		if (!view.Children.Contains(border2))
		{
			view.Children.Add(border2);
		}
		border1.BorderThickness = new Thickness(0.0, 1.0, 1.0, 1.0);
		border2.BorderThickness = new Thickness(1.0, 1.0, 0.0, 1.0);
		border1.Width = num2;
		border1.Height = num4 - num3;
		Canvas.SetLeft(border1, 0.0);
		Canvas.SetBottom(border1, (double)(255 - SHigh) * (SL_Bat3.Height - 10.0) / 255.0);
		border2.Width = (double)(255 - HLow) * (SL_Bat1.Width - 10.0) / 255.0;
		border2.Height = num4 - num3;
		Canvas.SetLeft(border2, num);
		Canvas.SetBottom(border2, (double)(255 - SHigh) * (SL_Bat3.Height - 10.0) / 255.0);
	}

	protected void ClipSilderVertical()
	{
		double width = SL_Bat3.Width;
		double num = SL_Bat3.Height;
		if (width != 0.0 && !double.IsNaN(width) && num != 0.0 && !double.IsNaN(num) && view.Width != 0.0 && !double.IsNaN(view.Width))
		{
			if (num < 10.0)
			{
				num = 100.0;
			}
			double num2 = SHigh / 2 + SLow / 2;
			double num3 = 255.0;
			double num4 = (num - (double)(2 * d)) * (1.0 - num2 / num3) + (double)d;
			StartRectClipVertical = new RectangleGeometry(new Rect(0.0, num4, width, num));
			EndRectClipVertical = new RectangleGeometry(new Rect(0.0, 0.0, width, num4));
			int num5 = (int)((double)SLow * (SL_Bat3.Height - 10.0) / 255.0);
			sLowPathLine.StartPoint = new Point(0.0, num5);
			sLowPathLine.EndPoint = new Point(view.Width, num5);
			int num6 = (int)((double)SHigh * (SL_Bat3.Height - 10.0) / 255.0);
			sHighPathLine.StartPoint = new Point(0.0, num6);
			sHighPathLine.EndPoint = new Point(view.Width, num6);
			DrawBorder();
		}
	}

	protected void SL_Bat1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		ClipSilderHorizon();
	}

	protected void SL_Bat2_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		ClipSilderHorizon();
	}

	protected void SL_Bat3_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		ClipSilderVertical();
	}

	protected void SL_Bat4_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
		ClipSilderVertical();
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
		(d as ChromaticDiagram).DoDataPointsChanged();
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
				int num4 = (int)((255.0 - DataPoints[i].Y) * (double)num2 / 255.0);
				Line line = new Line();
				line.X1 = num3;
				line.Y1 = num4;
				line.X2 = num3 + 1;
				line.Y2 = num4 + 1;
				line.Stroke = Brushes.Red;
				view.Children.Add(line);
			}
			DrawBorder();
		}
		else
		{
			ClearPoint();
		}
	}

	private void ClearPoint()
	{
		if (hLowPointLine != null && hHighPointLine != null && sLowPointLine != null && sHighPointLine != null && view != null)
		{
			Path element = hLowPointLine;
			Path element2 = hHighPointLine;
			Path element3 = sLowPointLine;
			Path element4 = sHighPointLine;
			view.Children.Clear();
			view.Children.Add(element);
			view.Children.Add(element2);
			view.Children.Add(element3);
			view.Children.Add(element4);
		}
	}
}
