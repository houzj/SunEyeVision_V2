using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;

public partial class SliderArrangePoint : SliderArrangeBase, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty IsXProperty = DependencyProperty.Register("IsX", typeof(bool), typeof(SliderArrangePoint), new PropertyMetadata(true));

	public static readonly DependencyProperty ImageLenthProperty = DependencyProperty.Register("ImageLenth", typeof(int), typeof(SliderArrangePoint), new PropertyMetadata(0));

	public static readonly DependencyProperty OriPointsProperty = DependencyProperty.Register("OriPoints", typeof(IList<Point>), typeof(SliderArrangePoint), new PropertyMetadata(null));

	public static readonly DependencyProperty PointSourceProperty = DependencyProperty.Register("PointSource", typeof(Point), typeof(SliderArrangePoint), new PropertyMetadata(default(Point), OnPointSourceChanged));

	private bool isPropertyChanged = true;

	public static readonly DependencyProperty DecimalDigitsProperty = DependencyProperty.Register("DecimalDigits", typeof(int), typeof(SliderArrangePoint), new PropertyMetadata(2));

	public static readonly DependencyProperty NeedSliderProperty = DependencyProperty.Register("NeedSlider", typeof(bool), typeof(SliderArrangePoint), new PropertyMetadata(true));

	private double verticalOffset;

	private double horizontalOffset;

	public bool IsX
	{
		get
		{
			return (bool)GetValue(IsXProperty);
		}
		set
		{
			SetValue(IsXProperty, value);
		}
	}

	public int ImageLenth
	{
		get
		{
			return (int)GetValue(ImageLenthProperty);
		}
		set
		{
			SetValue(ImageLenthProperty, value);
		}
	}

	public IList<Point> OriPoints
	{
		get
		{
			return (IList<Point>)GetValue(OriPointsProperty);
		}
		set
		{
			SetValue(OriPointsProperty, value);
		}
	}

	public Point PointSource
	{
		get
		{
			return (Point)GetValue(PointSourceProperty);
		}
		set
		{
			SetValue(PointSourceProperty, value);
		}
	}

	public int DecimalDigits
	{
		get
		{
			return (int)GetValue(DecimalDigitsProperty);
		}
		set
		{
			SetValue(DecimalDigitsProperty, value);
		}
	}

	public bool NeedSlider
	{
		get
		{
			return (bool)GetValue(NeedSliderProperty);
		}
		set
		{
			SetValue(NeedSliderProperty, value);
		}
	}

	private static void OnPointSourceChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		SliderArrangePoint sliderArrangePoint = d as SliderArrangePoint;
		Point point = (Point)e.NewValue;
		sliderArrangePoint.isPropertyChanged = false;
		if (sliderArrangePoint.DataContext == null || !(sliderArrangePoint.DataContext.ToString() == "{DisconnectedItem}"))
		{
			if (sliderArrangePoint.IsX)
			{
				sliderArrangePoint.StartValue = point.X;
			}
			else
			{
				sliderArrangePoint.StartValue = point.Y;
			}
			sliderArrangePoint.isPropertyChanged = true;
		}
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		string name = e.Property.Name;
		if (!(name == "StartValue") || !isPropertyChanged)
		{
			return;
		}
		int num = OriPoints?.OfType<object>().Select((object p) => p.GetHashCode()).ToList()
			.IndexOf(DataContext.GetHashCode()) ?? (-1);
		if (num >= 0)
		{
			if (IsX)
			{
				OriPoints[num] = new Point(StartValue / (double)ImageLenth, OriPoints[num].Y);
			}
			else
			{
				OriPoints[num] = new Point(OriPoints[num].X, StartValue / (double)ImageLenth);
			}
		}
	}

	public SliderArrangePoint()
	{
		InitializeComponent();
	}

	private void PART_Popup_Opened(object sender, EventArgs e)
	{
		SilderWidth = (int)ToBtn.ActualWidth;
		ClipSilder();
		try
		{
			for (DependencyObject parent = VisualTreeHelper.GetParent((DependencyObject)sender); parent != null; parent = VisualTreeHelper.GetParent(parent))
			{
				if (parent is ScrollViewer scrollViewer)
				{
					verticalOffset = scrollViewer.VerticalOffset;
					horizontalOffset = scrollViewer.HorizontalOffset;
					scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
					scrollViewer.ScrollChanged += ScrollViewer_ScrollChanged;
					break;
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private new void T1_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is NumericControl numericControl)
		{
			try
			{
				numericControl.SetBinding(NumericControl.TextProperty, new Binding("StartValue")
				{
					Source = this,
					Mode = BindingMode.TwoWay,
					StringFormat = GetDecimalStr(DecimalDigits)
				});
			}
			catch
			{
			}
		}
	}

	private new void T2_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is NumericControl numericControl)
		{
			try
			{
				numericControl.SetBinding(NumericControl.TextProperty, new Binding("EndValue")
				{
					Source = this,
					Mode = BindingMode.TwoWay,
					StringFormat = GetDecimalStr(DecimalDigits)
				});
			}
			catch
			{
			}
		}
	}

	private string GetDecimalStr(int n)
	{
		string result = "0.000";
		if (n != 3 && n >= 0 && n < 10)
		{
			StringBuilder stringBuilder = new StringBuilder("0.");
			while (n > 0)
			{
				n--;
				stringBuilder.Append('0');
			}
			return stringBuilder.ToString();
		}
		return result;
	}

	private void PART_Popup_GotFocus(object sender, RoutedEventArgs e)
	{
		ToBtn.IsChecked = true;
	}

	private void PART_Popup_LostFocus(object sender, RoutedEventArgs e)
	{
		ToBtn.IsChecked = false;
	}

	private void T1_GotFocus(object sender, RoutedEventArgs e)
	{
		ToBtn.IsChecked = true;
	}

	private new void T1_LostFocus(object sender, RoutedEventArgs e)
	{
		base.T1_LostFocus(sender, e);
		ToBtn.IsChecked = false;
	}

	private void T2_GotFocus(object sender, RoutedEventArgs e)
	{
		ToBtn.IsChecked = true;
	}

	private new void T2_LostFocus(object sender, RoutedEventArgs e)
	{
		base.T2_LostFocus(sender, e);
		ToBtn.IsChecked = false;
	}

	private void NumericControl_KeyDown(object sender, KeyEventArgs e)
	{
		if (e.KeyStates == Keyboard.GetKeyStates(Key.Return))
		{
			ToBtn.Focus();
			ToBtn.IsChecked = false;
		}
	}

	private void ScrollViewer_ScrollChanged(object sender, ScrollChangedEventArgs e)
	{
		if (sender is ScrollViewer scrollViewer)
		{
			double num = Math.Abs(verticalOffset - scrollViewer.VerticalOffset);
			double num2 = Math.Abs(horizontalOffset - scrollViewer.HorizontalOffset);
			if ((num > 3.0 || num2 > 3.0) && ToBtn.IsChecked == true)
			{
				ToBtn.Focus();
				ToBtn.IsChecked = false;
			}
		}
	}

	private void PART_Popup_Closed(object sender, EventArgs e)
	{
		try
		{
			for (DependencyObject parent = VisualTreeHelper.GetParent((DependencyObject)sender); parent != null; parent = VisualTreeHelper.GetParent(parent))
			{
				if (parent is ScrollViewer scrollViewer)
				{
					scrollViewer.ScrollChanged -= ScrollViewer_ScrollChanged;
					break;
				}
			}
		}
		catch (Exception)
		{
		}
	}

	private new void SL_Bat1_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
	{
	}
}
