using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView.ROI;

public partial class MoveAreaPage : ItemsControl, IComponentConnector
{
	public static readonly DependencyProperty StrokeThicknessProperty = DependencyProperty.Register("StrokeThickness", typeof(double), typeof(MoveAreaPage), new PropertyMetadata(0.0));

	public static readonly DependencyProperty RectMarginProperty = DependencyProperty.Register("RectMargin", typeof(Thickness), typeof(MoveAreaPage), new PropertyMetadata(default(Thickness)));

	public static readonly DependencyProperty SelectedIndexProperty = DependencyProperty.Register("SelectedIndex", typeof(int), typeof(MoveAreaPage), new PropertyMetadata(0));

	public static readonly DependencyProperty OriPointProperty = DependencyProperty.Register("OriPoint", typeof(Point), typeof(MoveAreaPage), new PropertyMetadata(default(Point)));

	public static readonly DependencyProperty PointsWidthProperty = DependencyProperty.Register("PointsWidth", typeof(double), typeof(MoveAreaPage), new PropertyMetadata(-1.0));

	public static readonly DependencyProperty PointsHeightProperty = DependencyProperty.Register("PointsHeight", typeof(double), typeof(MoveAreaPage), new PropertyMetadata(-1.0));

	private static PointFillConverter pConverter = new PointFillConverter();

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

	public Thickness RectMargin
	{
		get
		{
			return (Thickness)GetValue(RectMarginProperty);
		}
		set
		{
			SetValue(RectMarginProperty, value);
		}
	}

	public int SelectedIndex
	{
		get
		{
			return (int)GetValue(SelectedIndexProperty);
		}
		set
		{
			SetValue(SelectedIndexProperty, value);
		}
	}

	public Point OriPoint
	{
		get
		{
			return (Point)GetValue(OriPointProperty);
		}
		set
		{
			SetValue(OriPointProperty, value);
		}
	}

	public double PointsWidth
	{
		get
		{
			return (double)GetValue(PointsWidthProperty);
		}
		set
		{
			SetValue(PointsWidthProperty, value);
		}
	}

	public double PointsHeight
	{
		get
		{
			return (double)GetValue(PointsHeightProperty);
		}
		set
		{
			SetValue(PointsHeightProperty, value);
		}
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		string name = e.Property.Name;
		if (name == "ItemsSource")
		{
			CalculateSize();
		}
	}

	public MoveAreaPage()
	{
		InitializeComponent();
	}

	internal void CalculateSize()
	{
		if (ItemsSource != null)
		{
			IEnumerable<Point> source = ItemsSource.OfType<Point>();
			OriPoint = new Point(source.Min((Point p) => p.X), source.Min((Point p) => p.Y));
			PointsWidth = source.Max((Point p) => p.X) - OriPoint.X;
			PointsHeight = source.Max((Point p) => p.Y) - OriPoint.Y;
		}
		else
		{
			PointsWidth = -1.0;
			PointsHeight = -1.0;
		}
	}

	protected override DependencyObject GetContainerForItemOverride()
	{
		MoveArea moveArea = new MoveArea();
		moveArea.SetBinding(MoveArea.StrokeThicknessProperty, new Binding("StrokeThickness")
		{
			Source = this
		});
		moveArea.SetBinding(MoveArea.RectMarginProperty, new Binding("RectMargin")
		{
			Source = this
		});
		MultiBinding multiBinding = new MultiBinding
		{
			Converter = pConverter,
			ConverterParameter = this
		};
		multiBinding.Bindings.Add(new Binding(""));
		multiBinding.Bindings.Add(new Binding("ActualWidth")
		{
			Source = this
		});
		multiBinding.Bindings.Add(new Binding("ActualHeight")
		{
			Source = this
		});
		multiBinding.Bindings.Add(new Binding("PointsWidth")
		{
			Source = this
		});
		multiBinding.Bindings.Add(new Binding("PointsHeight")
		{
			Source = this
		});
		multiBinding.Bindings.Add(new Binding("OriPoint")
		{
			Source = this
		});
		moveArea.SetBinding(MoveArea.PositionProperty, multiBinding);
		moveArea.MouseLeftButtonDown += Container_MouseLeftButtonDown;
		return moveArea;
	}

	private void Container_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
	{
		if (ItemsSource != null)
		{
			SelectedIndex = (from p in ItemsSource.OfType<Point>()
				select p.GetHashCode()).ToList().IndexOf((sender as FrameworkElement).DataContext.GetHashCode());
		}
	}

	protected override void OnIsMouseCapturedChanged(DependencyPropertyChangedEventArgs e)
	{
	}
}
