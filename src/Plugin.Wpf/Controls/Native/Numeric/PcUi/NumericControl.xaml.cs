using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;
using System.Windows.Media;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;

public partial class NumericControl : UserControl, INotifyPropertyChanged, IComponentConnector
{
	public delegate void NumericClick(object sender, EventArgs e);

	public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(NumericControl), new PropertyMetadata(false));

	public static readonly DependencyProperty TBtnBgNormalProperty = DependencyProperty.Register("TBtnBgNormal", typeof(string), typeof(NumericControl), new PropertyMetadata("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Numeric/VM.PcUI/combo_ic_top_normal.png"));

	public static readonly DependencyProperty TBtnBgHoverProperty = DependencyProperty.Register("TBtnBgHover", typeof(string), typeof(NumericControl), new PropertyMetadata("/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Numeric/VM.PcUI/combo_ic_top_click.png"));

	public static readonly DependencyProperty TBorderBrushProperty = DependencyProperty.Register("TBorderBrush", typeof(Brush), typeof(NumericControl), new PropertyMetadata(Brushes.Gray));

	public static readonly DependencyProperty TBackgroundProperty = DependencyProperty.Register("TBackground", typeof(Brush), typeof(NumericControl), new PropertyMetadata(Brushes.White));

	public static readonly DependencyProperty SymbolProperty = DependencyProperty.Register("Symbol", typeof(SymbolType), typeof(NumericControl), new PropertyMetadata(SymbolType.None, OnSymbolChange));

	private double _SymbolWidth = 0.0;

	private double _SymbolDir = -1.0;

	public static readonly DependencyProperty IsFocusedPProperty = DependencyProperty.Register("IsFocusedP", typeof(bool), typeof(NumericControl), new PropertyMetadata(false));

	public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(NumericControl), new PropertyMetadata("0"));

	private double _Value;

	public static readonly DependencyProperty IncrementProperty = DependencyProperty.Register("Increment", typeof(double), typeof(NumericControl), new PropertyMetadata(1.0));

	public static readonly DependencyProperty MinimumProperty = DependencyProperty.Register("Minimum", typeof(double), typeof(NumericControl), new PropertyMetadata(0.0));

	public static readonly DependencyProperty MaximumProperty = DependencyProperty.Register("Maximum", typeof(double), typeof(NumericControl), new PropertyMetadata(100.0));

	public static readonly DependencyProperty MinValueProperty = DependencyProperty.Register("MinValue", typeof(double), typeof(NumericControl), new PropertyMetadata(0.0));

	public static readonly DependencyProperty MaxValueProperty = DependencyProperty.Register("MaxValue", typeof(double), typeof(NumericControl), new PropertyMetadata(100.0));

	public static readonly DependencyProperty IsNeedCheckExtremumByXMLProperty = DependencyProperty.Register("IsNeedCheckExtremumByXML", typeof(bool), typeof(NumericControl), new PropertyMetadata(false));

	public bool IsReadOnly
	{
		get
		{
			return (bool)GetValue(IsReadOnlyProperty);
		}
		set
		{
			SetValue(IsReadOnlyProperty, value);
		}
	}

	public string TBtnBgNormal
	{
		get
		{
			return (string)GetValue(TBtnBgNormalProperty);
		}
		set
		{
			SetValue(TBtnBgNormalProperty, value);
		}
	}

	public string TBtnBgHover
	{
		get
		{
			return (string)GetValue(TBtnBgHoverProperty);
		}
		set
		{
			SetValue(TBtnBgHoverProperty, value);
		}
	}

	public Brush TBorderBrush
	{
		get
		{
			return (Brush)GetValue(TBorderBrushProperty);
		}
		set
		{
			SetValue(TBorderBrushProperty, value);
		}
	}

	public Brush TBackground
	{
		get
		{
			return (Brush)GetValue(TBackgroundProperty);
		}
		set
		{
			SetValue(TBackgroundProperty, value);
		}
	}

	public SymbolType Symbol
	{
		get
		{
			return (SymbolType)GetValue(SymbolProperty);
		}
		set
		{
			SetValue(SymbolProperty, value);
		}
	}

	public double SymbolWidth
	{
		get
		{
			return _SymbolWidth;
		}
		set
		{
			if (_SymbolWidth != value)
			{
				_SymbolWidth = value;
				NotifyPropertyChanged("SymbolWidth");
			}
		}
	}

	public double SymbolDir
	{
		get
		{
			return _SymbolDir;
		}
		set
		{
			if (_SymbolDir != value)
			{
				_SymbolDir = value;
				NotifyPropertyChanged("SymbolDir");
			}
		}
	}

	public bool IsFocusedP
	{
		get
		{
			return (bool)GetValue(IsFocusedPProperty);
		}
		set
		{
			SetValue(IsFocusedPProperty, value);
		}
	}

	public string Text
	{
		get
		{
			return (string)GetValue(TextProperty);
		}
		set
		{
			SetValue(TextProperty, value);
		}
	}

	public double Value
	{
		get
		{
			if (double.TryParse(Text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var result))
			{
				_Value = result;
			}
			return _Value;
		}
		set
		{
			_Value = value;
			Text = _Value.ToString(System.Globalization.CultureInfo.InvariantCulture);
		}
	}

	public double Increment
	{
		get
		{
			return (double)GetValue(IncrementProperty);
		}
		set
		{
			SetValue(IncrementProperty, value);
		}
	}

	public double Minimum
	{
		get
		{
			return (double)GetValue(MinimumProperty);
		}
		set
		{
			SetValue(MinimumProperty, value);
		}
	}

	public double Maximum
	{
		get
		{
			return (double)GetValue(MaximumProperty);
		}
		set
		{
			SetValue(MaximumProperty, value);
		}
	}

	public double MinValue
	{
		get
		{
			return (double)GetValue(MinValueProperty);
		}
		set
		{
			SetValue(MinValueProperty, value);
		}
	}

	public double MaxValue
	{
		get
		{
			return (double)GetValue(MaxValueProperty);
		}
		set
		{
			SetValue(MaxValueProperty, value);
		}
	}

	public bool IsNeedCheckExtremumByXML
	{
		get
		{
			return (bool)GetValue(IsNeedCheckExtremumByXMLProperty);
		}
		set
		{
			SetValue(IsNeedCheckExtremumByXMLProperty, value);
		}
	}

	public event NumericClick OnNumericClick;

	public event PropertyChangedEventHandler PropertyChanged;

	public NumericControl()
	{
		InitializeComponent();
		ValueText.GotFocus += TextBox_GotFocus;
		RangeRule item = new RangeRule
		{
			Container = this
		};
		Binding binding = BindingOperations.GetBinding(ValueText, TextBox.TextProperty);
		binding.ValidationRules.Clear();
		binding.ValidationRules.Add(item);
	}

	private static void OnSymbolChange(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		if (d is NumericControl numericControl)
		{
			switch ((SymbolType)e.NewValue)
			{
			case SymbolType.Greater:
				numericControl.SymbolWidth = 10.0;
				numericControl.SymbolDir = -1.0;
				break;
			case SymbolType.Less:
				numericControl.SymbolWidth = 10.0;
				numericControl.SymbolDir = 1.0;
				break;
			case SymbolType.None:
				numericControl.SymbolWidth = 0.0;
				break;
			}
		}
	}

	private void TextBox_GotFocus(object sender, RoutedEventArgs e)
	{
		if (!IsReadOnly)
		{
			IsFocusedP = true;
		}
	}

	private void DoClick()
	{
		if (OnNumericClick != null)
		{
			OnNumericClick(this, null);
		}
	}

	private void UpButton_Click(object sender, RoutedEventArgs e)
	{
		DoUp();
		DoClick();
	}

	private void DoUp()
	{
		double num;
		if (IsNeedCheckExtremumByXML)
		{
			num = ((Maximum <= MaxValue) ? Maximum : MaxValue);
		}
		else
		{
			num = Maximum;
		}
		if (Value + Increment < num)
		{
			Value += Increment;
		}
		else
		{
			Value = num;
		}
	}

	private void DownButton_Click(object sender, RoutedEventArgs e)
	{
		DoDown();
		DoClick();
	}

	private void DoDown()
	{
		double num;
		if (IsNeedCheckExtremumByXML)
		{
			num = ((Minimum >= MinValue) ? Minimum : MinValue);
		}
		else
		{
			num = Minimum;
		}
		if (Value - Increment > num)
		{
			Value -= Increment;
		}
		else
		{
			Value = num;
		}
	}

	public void NotifyPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	private void ValueText_MouseWheel(object sender, MouseWheelEventArgs e)
	{
		if (!IsReadOnly && ValueText.IsFocused)
		{
			if (e.Delta > 0)
			{
				DoUp();
			}
			else
			{
				DoDown();
			}
			e.Handled = true;
		}
	}

	private void ValueText_PreviewKeyDown(object sender, KeyEventArgs e)
	{
		if (e.Key == Key.Space)
		{
			e.Handled = true;
		}
	}

	internal void ForceUpdateSource()
	{
		ValueText.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
	}
}
