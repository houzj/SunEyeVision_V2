using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;
using System;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.Controls;

public partial class NumericUpDown : UserControl, IComponentConnector
{
	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(double), typeof(NumericUpDown), new FrameworkPropertyMetadata(0.0, ValuePropertyChangeCallback), null);

	protected static readonly DependencyProperty _decimation = DependencyProperty.Register("Decimation", typeof(uint), typeof(NumericUpDown), new FrameworkPropertyMetadata(0u, DecimationCallback));

	public static readonly DependencyProperty _step = DependencyProperty.Register("Step", typeof(double), typeof(NumericUpDown), new FrameworkPropertyMetadata(1.0));

	public static readonly DependencyProperty _showButtons = DependencyProperty.Register("ShowButtons", typeof(bool), typeof(NumericUpDown), new FrameworkPropertyMetadata(true));

	public static RoutedEvent ValueChangedEvent = EventManager.RegisterRoutedEvent("ValueChanged", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(NumericUpDown));

	private static NumericUpDown _thisReference;

	private double _minvalue = double.MinValue;

	private double _maxvalue = double.MaxValue;

	public uint Decimation
	{
		get
		{
			return (uint)GetValue(_decimation);
		}
		set
		{
			SetValue(_decimation, value);
			SetDecimationBinding(value);
		}
	}

	public double Value
	{
		get
		{
			return (double)GetValue(ValueProperty);
		}
		set
		{
			double num = value;
			if (num < _minvalue)
			{
				num = _minvalue;
			}
			else if (num > _maxvalue)
			{
				num = _maxvalue;
			}
			SetValue(ValueProperty, num);
		}
	}

	public bool ShowButtons
	{
		get
		{
			return (bool)GetValue(ValueProperty);
		}
		set
		{
			SetValue(ValueProperty, value);
		}
	}

	public double MinValue
	{
		get
		{
			return _minvalue;
		}
		set
		{
			_minvalue = value;
		}
	}

	public double MaxValue
	{
		get
		{
			return _maxvalue;
		}
		set
		{
			_maxvalue = value;
		}
	}

	public double Step
	{
		get
		{
			return (double)GetValue(_step);
		}
		set
		{
			SetValue(_step, value);
		}
	}

	public event RoutedEventHandler ValueChanged
	{
		add
		{
			AddHandler(ValueChangedEvent, value);
		}
		remove
		{
			RemoveHandler(ValueChangedEvent, value);
		}
	}

	protected virtual void OnValueChanged()
	{
		RoutedEventArgs e = new RoutedEventArgs
		{
			RoutedEvent = ValueChangedEvent
		};
		RaiseEvent(e);
	}

	public NumericUpDown()
	{
		InitializeComponent();
		_thisReference = this;
	}

	~NumericUpDown()
	{
		_thisReference = null;
	}

	public void Increment()
	{
		try
		{
			Value += Step;
		}
		catch (ArgumentException)
		{
		}
	}

	public void Decrement()
	{
		try
		{
			Value -= Step;
		}
		catch (ArgumentException)
		{
		}
	}

	private void btnUp_Click(object sender, RoutedEventArgs e)
	{
		Increment();
	}

	private void btnDown_Click(object sender, RoutedEventArgs e)
	{
		Decrement();
	}

	private void SetDecimationBinding(uint decimation)
	{
		Binding binding = new Binding("Value")
		{
			Converter = new DecimationConverter(),
			UpdateSourceTrigger = UpdateSourceTrigger.PropertyChanged,
			ConverterParameter = decimation
		};
		binding.ValidationRules.Add(new ExceptionValidationRule());
		tbValue.SetBinding(TextBox.TextProperty, binding);
	}

	private static void ValuePropertyChangeCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (_thisReference != null)
		{
			_thisReference.OnValueChanged();
		}
	}

	private static void DecimationCallback(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (_thisReference != null)
		{
			_thisReference.SetDecimationBinding(Convert.ToUInt32(e.NewValue));
		}
		if (_thisReference != null)
		{
			_thisReference.Value++;
			_thisReference.Value--;
		}
	}

	private void ucNumericUpDown_Loaded(object sender, RoutedEventArgs e)
	{
		SetDecimationBinding(Decimation);
		tbValue.LostFocus += (object s1, RoutedEventArgs e2) =>
		{
			try
			{
				double num = Convert.ToDouble(tbValue.Text);
				if (num < _minvalue)
				{
					tbValue.Text = _minvalue.ToString(CultureInfo.InvariantCulture);
				}
				if (num > _maxvalue)
				{
					tbValue.Text = _maxvalue.ToString(CultureInfo.InvariantCulture);
				}
			}
			catch (Exception)
			{
				tbValue.Text = Value.ToString(CultureInfo.InvariantCulture);
			}
		};
	}
}
