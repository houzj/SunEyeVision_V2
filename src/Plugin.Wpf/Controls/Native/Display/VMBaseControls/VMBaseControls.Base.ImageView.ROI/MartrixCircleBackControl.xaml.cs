using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class MartrixCircleBackControl : Control, INotifyPropertyChanged, IComponentConnector
{
	public static readonly DependencyProperty MProperty = DependencyProperty.Register("M", typeof(uint), typeof(MartrixCircleBackControl), new PropertyMetadata(uint.MaxValue, OnMNChanged));

	public static readonly DependencyProperty NProperty = DependencyProperty.Register("N", typeof(uint), typeof(MartrixCircleBackControl), new PropertyMetadata(uint.MaxValue, OnMNChanged));

	public static readonly DependencyProperty RadiusProperty = DependencyProperty.Register("Radius", typeof(double), typeof(MartrixCircleBackControl), new PropertyMetadata(double.NaN, OnRadiusChanged));

	public static readonly DependencyProperty MinRadiusProperty = DependencyProperty.Register("MinRadius", typeof(double), typeof(MartrixCircleBackControl), new PropertyMetadata(double.NaN, OnRadiusChanged));

	public static readonly DependencyProperty ControlToImageRadioProperty = DependencyProperty.Register("ControlToImageRadio", typeof(double), typeof(MartrixCircleBackControl), new PropertyMetadata(1.0, OnRadiusChanged));

	public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register("StrokeThickness", typeof(double), typeof(MartrixCircleBackControl), new PropertyMetadata(1.0));

	public static readonly DependencyProperty RectAngleProperty = DependencyProperty.Register("RectAngle", typeof(double), typeof(MartrixCircleBackControl), new PropertyMetadata(0.0, OnRectAngleChanged));

	private static Bitmap image;

	private double LeftTopCursorAngle = double.NaN;

	private Cursor _LeftTopCursor = CreaterCursor(ROIControlFrame.GetRotateImage(GetBiArrowImage(), 90));

	private Canvas Cv;

	private GeometryGroup Gt;

	private System.Windows.Shapes.Rectangle SizeRect;

	private Grid RadiusRect;

	private System.Windows.Shapes.Rectangle InnerSizeRect;

	private Grid InnerRadiusRect;

	public MouseButtonEventHandler ControlMouseLeftButtonDown;

	public uint M
	{
		get
		{
			return (uint)GetValue(MProperty);
		}
		set
		{
			SetValue(MProperty, value);
		}
	}

	public uint N
	{
		get
		{
			return (uint)GetValue(NProperty);
		}
		set
		{
			SetValue(NProperty, value);
		}
	}

	public double Radius
	{
		get
		{
			return (double)GetValue(RadiusProperty);
		}
		set
		{
			SetValue(RadiusProperty, value);
		}
	}

	public double MinRadius
	{
		get
		{
			return (double)GetValue(MinRadiusProperty);
		}
		set
		{
			SetValue(MinRadiusProperty, value);
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

	public double StrokeThickness
	{
		get
		{
			return (double)GetValue(StrokeThicknessProperty);
		}
		set
		{
			SetValue(StrokeThicknessProperty, value);
		}
	}

	public double RectAngle
	{
		get
		{
			return (double)GetValue(RectAngleProperty);
		}
		set
		{
			SetValue(RectAngleProperty, value);
		}
	}

	public Cursor LeftTopCursor
	{
		get
		{
			return _LeftTopCursor;
		}
		set
		{
			if (_LeftTopCursor != value)
			{
				_LeftTopCursor = value;
				OnPropertyChanged("LeftTopCursor");
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	private bool Check()
	{
		return M != uint.MaxValue && N != uint.MaxValue && !double.IsNaN(ActualWidth) && !double.IsNaN(ActualWidth) && Gt != null && M != uint.MaxValue && N != uint.MaxValue;
	}

	public void OnPropertyChanged(string strPropertyInfo)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(strPropertyInfo));
		}
	}

	private void DrawCircles(bool needClear)
	{
		if (!Check())
		{
			return;
		}
		if (needClear || Gt.Children.Count == 0)
		{
			Gt.Children.Clear();
			for (int i = 0; i < N; i++)
			{
				for (int j = 0; j < M; j++)
				{
					Gt.Children.Add(new EllipseGeometry());
				}
			}
		}
		if (needClear && Gt.Children.Count == M * N)
		{
			for (int k = 0; k < N; k++)
			{
				for (int l = 0; l < M; l++)
				{
					Gt.Children.Add(new EllipseGeometry());
				}
			}
		}
		double actualHeight = ActualHeight;
		double actualWidth = ActualWidth;
		double num = ControlToImageRadio * Radius;
		double num2 = ControlToImageRadio * MinRadius;
		double num3 = ((N > 1) ? (actualHeight / (double)(N - 1)) : 0.0);
		double num4 = ((M > 1) ? (actualWidth / (double)(M - 1)) : 0.0);
		List<TextBlock> list = Cv.Children.OfType<TextBlock>().ToList();
		list.ForEach((TextBlock x) =>
		{
			Cv.Children.Remove(x);
		});
		for (int num5 = 0; num5 < N; num5++)
		{
			double num6 = (double)num5 * num3;
			for (int num7 = 0; num7 < M; num7++)
			{
				double num8 = (double)num7 * num4;
				EllipseGeometry ellipseGeometry = Gt.Children[(int)(num5 * M) + num7] as EllipseGeometry;
				ellipseGeometry.Center = new System.Windows.Point(num8, num6);
				ellipseGeometry.RadiusX = num;
				ellipseGeometry.RadiusY = num;
				TextBlock element = new TextBlock
				{
					Text = ((int)(num5 * M) + num7).ToString(),
					Foreground = System.Windows.Media.Brushes.Blue
				};
				Cv.Children.Add(element);
				Canvas.SetTop(element, (2.0 * num6 - num) / 2.0);
				Canvas.SetLeft(element, num8);
				int num9 = int.Parse((M * N).ToString());
				if (Gt.Children.Count > num9)
				{
					EllipseGeometry ellipseGeometry2 = Gt.Children[(int)(num5 * M) + num7 + num9] as EllipseGeometry;
					ellipseGeometry2.Center = new System.Windows.Point(num8, num6);
					ellipseGeometry2.RadiusX = num2;
					ellipseGeometry2.RadiusY = num2;
				}
			}
		}
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		switch (e.Property.Name)
		{
		case "ControlToImageRadio":
		case "ActualWidth":
		case "ActualHeight":
		case "RadiusProperty":
		case "MinRadiusProperty":
			DrawCircles(needClear: false);
			break;
		}
	}

	private static void OnMNChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		MartrixCircleBackControl martrixCircleBackControl = d as MartrixCircleBackControl;
		martrixCircleBackControl.DrawCircles(needClear: true);
	}

	private static void OnRadiusChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		MartrixCircleBackControl martrixCircleBackControl = d as MartrixCircleBackControl;
		martrixCircleBackControl.DrawCircles(needClear: false);
		Canvas.SetTop(martrixCircleBackControl.RadiusRect, (0.0 - martrixCircleBackControl.Radius) * martrixCircleBackControl.ControlToImageRadio);
		Canvas.SetTop(martrixCircleBackControl.InnerRadiusRect, (0.0 - martrixCircleBackControl.MinRadius) * martrixCircleBackControl.ControlToImageRadio);
	}

	private static void OnRectAngleChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		double newValue = (double)e.NewValue;
		MartrixCircleBackControl martrixCircleBackControl = d as MartrixCircleBackControl;
		martrixCircleBackControl.OnCursorPropertyChanged(newValue);
	}

	private void OnCursorPropertyChanged(double newValue)
	{
		RectAngle = newValue;
		if (LeftTopCursorAngle != RectAngle && (double.IsNaN(LeftTopCursorAngle) || Math.Abs(LeftTopCursorAngle - RectAngle) > 1.0))
		{
			LeftTopCursorAngle = RectAngle;
			LeftTopCursor?.Dispose();
			Bitmap rotateImage = ROIControlFrame.GetRotateImage(GetBiArrowImage(), (int)newValue + 90);
			LeftTopCursor = CreaterCursor(rotateImage);
		}
	}

	public static Bitmap GetBiArrowImage()
	{
		if (image == null)
		{
			Stream manifestResourceStream = Assembly.GetExecutingAssembly().GetManifestResourceStream("SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Resources.鼠标双向箭头.png");
			image = (Bitmap)System.Drawing.Image.FromStream(manifestResourceStream);
			manifestResourceStream.Dispose();
		}
		return image;
	}

	private static Cursor CreaterCursor(Bitmap dimage)
	{
		BitmapCursor cursorHandle = new BitmapCursor(dimage);
		return CursorInteropHelper.Create(cursorHandle);
	}

	public MartrixCircleBackControl()
	{
		InitializeComponent();
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		Gt = Template.FindName("Gt", this) as GeometryGroup;
		Cv = Template.FindName("Cv", this) as Canvas;
		if (Check())
		{
			DrawCircles(needClear: true);
		}
		SizeRect = Template.FindName("SizeRect", this) as System.Windows.Shapes.Rectangle;
		SizeRect.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		RadiusRect = Template.FindName("RadiusRect", this) as Grid;
		InnerSizeRect = Template.FindName("InnerSizeRect", this) as System.Windows.Shapes.Rectangle;
		InnerSizeRect.MouseLeftButtonDown += Move_MouseLeftButtonDown;
		InnerRadiusRect = Template.FindName("InnerRadiusRect", this) as Grid;
	}

	private void Move_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		ControlMouseLeftButtonDown?.Invoke(sender, e);
	}
}
