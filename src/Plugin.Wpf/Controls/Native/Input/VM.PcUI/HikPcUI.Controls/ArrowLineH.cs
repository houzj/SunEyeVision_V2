using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;
using System.Windows.Shapes;

namespace SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;

public class ArrowLineH : ArrowLine
{
	public static readonly DependencyProperty StartObjProperty = DependencyProperty.Register("StartObj", typeof(object), typeof(ArrowLineH), new PropertyMetadata(null));

	public static readonly DependencyProperty EndObjProperty = DependencyProperty.Register("EndObj", typeof(object), typeof(ArrowLineH), new PropertyMetadata(null));

	private double _lineSpan;

	public object StartObj
	{
		get
		{
			return GetValue(StartObjProperty);
		}
		set
		{
			SetValue(StartObjProperty, value);
			OnPropertyChanged("StartObj");
		}
	}

	public object EndObj
	{
		get
		{
			return GetValue(EndObjProperty);
		}
		set
		{
			SetValue(EndObjProperty, value);
			OnPropertyChanged("EndObj");
		}
	}

	public double NowX { get; set; }

	public double BaseX { get; set; }

	public double LineSpan
	{
		get
		{
			return _lineSpan;
		}
		set
		{
			if (_lineSpan != value)
			{
				_lineSpan = value;
			}
		}
	}

	public ArrowLineH()
	{
		Focusable = true;
		Stroke = Brushes.Gray;
		StrokeThickness = 2.0;
		SetBinding(Shape.FillProperty, new Binding("Stroke")
		{
			Source = this,
			Mode = BindingMode.OneWay
		});
	}

	public void BindingDataContext(object dataContext)
	{
		SetBinding(StartObjProperty, new Binding("StartObj")
		{
			Source = dataContext,
			Mode = BindingMode.OneWay
		});
		SetBinding(EndObjProperty, new Binding("EndObj")
		{
			Source = dataContext,
			Mode = BindingMode.OneWay
		});
	}
}
