using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls.Base.ImageView;

public partial class ContainerItem : ListViewItem, IComponentConnector
{
	public static readonly DependencyProperty AngleProperty = DependencyProperty.Register("Angle", typeof(double), typeof(ContainerItem), new PropertyMetadata(0.0));

	public static readonly DependencyProperty SkewAngleXProperty = DependencyProperty.Register("SkewAngleX", typeof(double), typeof(ContainerItem), new PropertyMetadata(0.0));

	public static readonly DependencyProperty SkewAngleYProperty = DependencyProperty.Register("SkewAngleY", typeof(double), typeof(ContainerItem), new PropertyMetadata(0.0));

	public static readonly DependencyProperty PositionProperty = DependencyProperty.Register("Position", typeof(Point), typeof(ContainerItem), new PropertyMetadata(default(Point), PositionChanged));

	public static readonly DependencyProperty ParentItemsControlProperty = DependencyProperty.Register("ParentItemsControl", typeof(ItemsControl), typeof(ContainerItem), new PropertyMetadata(null));

	public static readonly DependencyProperty MoveCommandProperty = DependencyProperty.Register("MoveCommand", typeof(ICommand), typeof(ContainerItem), new PropertyMetadata(null));

	public double Angle
	{
		get
		{
			return (double)GetValue(AngleProperty);
		}
		set
		{
			SetValue(AngleProperty, value);
		}
	}

	public double SkewAngleX
	{
		get
		{
			return (double)GetValue(SkewAngleXProperty);
		}
		set
		{
			SetValue(SkewAngleXProperty, value);
		}
	}

	public double SkewAngleY
	{
		get
		{
			return (double)GetValue(SkewAngleYProperty);
		}
		set
		{
			SetValue(SkewAngleYProperty, value);
		}
	}

	public Point Position
	{
		get
		{
			return (Point)GetValue(PositionProperty);
		}
		set
		{
			SetValue(PositionProperty, value);
		}
	}

	public ItemsControl ParentItemsControl
	{
		get
		{
			return (ItemsControl)GetValue(ParentItemsControlProperty);
		}
		set
		{
			SetValue(ParentItemsControlProperty, value);
		}
	}

	public ICommand MoveCommand
	{
		get
		{
			return (ICommand)GetValue(MoveCommandProperty);
		}
		set
		{
			SetValue(MoveCommandProperty, value);
		}
	}

	private static void PositionChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		Point point = (Point)e.NewValue;
		ContainerItem element = d as ContainerItem;
		Canvas.SetLeft(element, point.X);
		Canvas.SetTop(element, point.Y);
	}

	public ContainerItem()
	{
		InitializeComponent();
	}
}
