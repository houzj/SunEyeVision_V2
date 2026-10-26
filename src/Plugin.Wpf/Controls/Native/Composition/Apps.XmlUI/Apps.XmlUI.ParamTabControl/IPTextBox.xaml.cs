using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;

public partial class IPTextBox : UserControl, INotifyPropertyChanged, IComponentConnector
{
	public delegate void IPValueChange(object sender, RoutedPropertyChangedEventArgs<double> e);

	public static readonly DependencyProperty IPProperty = DependencyProperty.Register("IP", typeof(string), typeof(IPTextBox), new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.BindsTwoWayByDefault, IPChangedCallback));

	public static readonly Regex IpRegex = new Regex("^((2[0-4]\\d|25[0-5]|(1\\d{2})|([1-9]?[0-9]))\\.){3}(2[0-4]\\d|25[0-4]|(1\\d{2})|([1-9][0-9])|([0-9]))$");

	private const string DefaultIP = "";

	private static readonly Regex PartIprRegex = new Regex("^(\\.?(2[0-4]\\d|25[0-5]|(1\\d{2})|([1-9]?[0-9]))\\.?)+$");

	private readonly List<NumbericTextBox> numbericTextBoxs = new List<NumbericTextBox>();

	private NumbericTextBox currentNumbericTextBox;

	public static readonly DependencyProperty IsValidProperty = DependencyProperty.Register("IsValid", typeof(bool), typeof(IPTextBox), new PropertyMetadata(true));

	public string IP
	{
		get
		{
			return (string)GetValue(IPProperty);
		}
		set
		{
			SetValue(IPProperty, value);
		}
	}

	public bool IsValid
	{
		get
		{
			return (bool)GetValue(IsValidProperty);
		}
		set
		{
			SetValue(IsValidProperty, value);
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event IPValueChange OnIPValueChange;

	protected virtual void OnPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public IPTextBox()
	{
		InitializeComponent();
		numbericTextBoxs.Add(IPPart1);
		numbericTextBoxs.Add(IPPart2);
		numbericTextBoxs.Add(IPPart3);
		numbericTextBoxs.Add(IPPart4);
		KeyUp += IpAddressControlKeyUp;
		foreach (NumbericTextBox numbericTextBox in numbericTextBoxs)
		{
			numbericTextBox.PreviewTextChanged += NumbericTextBox_OnPreviewTextChanged;
		}
		foreach (NumbericTextBox numbericTextBox2 in numbericTextBoxs)
		{
			numbericTextBox2.TextChanged += TextBoxBase_OnTextChanged;
		}
	}

	private static void IPChangedCallback(DependencyObject dependencyObject, DependencyPropertyChangedEventArgs dependencyPropertyChangedEventArgs)
	{
		if (dependencyPropertyChangedEventArgs.NewValue == null)
		{
			throw new Exception("IP can not be null");
		}
		if (dependencyObject is IPTextBox iPTextBox)
		{
			iPTextBox.UpdateParts(iPTextBox);
		}
	}

	private void UpdateParts(IPTextBox control)
	{
		if (!string.IsNullOrWhiteSpace(control.IP))
		{
			string[] array = control.IP.Split('.');
			if (array.Length >= 4)
			{
				control.IPPart1.Text = array[0];
				control.IPPart2.Text = array[1];
				control.IPPart3.Text = array[2];
				control.IPPart4.Text = array[3];
			}
		}
	}

	private void IpAddressControlKeyUp(object sender, KeyEventArgs e)
	{
		if ((e.Key == Key.OemPeriod || e.Key == Key.Decimal) && currentNumbericTextBox != null)
		{
			int num = numbericTextBoxs.IndexOf(currentNumbericTextBox);
			int index = (num + 1) % numbericTextBoxs.Count;
			numbericTextBoxs[index].Focus();
			numbericTextBoxs[index].SelectionStart = numbericTextBoxs[index].Text.Length;
			numbericTextBoxs[index].SelectAll();
		}
	}

	private void TextBox_OnGotFocus(object sender, RoutedEventArgs e)
	{
		currentNumbericTextBox = e.OriginalSource as NumbericTextBox;
	}

	private void NumbericTextBox_OnPreviewTextChanged(object sender, TextChangedEventArgs e)
	{
		NumbericTextBox numbericTextBox = sender as NumbericTextBox;
		if (!PartIprRegex.IsMatch(numbericTextBox.Text))
		{
			return;
		}
		string[] array = numbericTextBox.Text.Split('.');
		if (array.Length == 1)
		{
			return;
		}
		int num = numbericTextBoxs.IndexOf(numbericTextBox);
		int i = 0;
		for (int j = num; j < numbericTextBoxs.Count; j++)
		{
			for (; i < array.Length && string.IsNullOrEmpty(array[i]); i++)
			{
			}
			if (i >= array.Length)
			{
				break;
			}
			numbericTextBoxs[j].Text = array[i];
			i++;
		}
	}

	private void TextBoxBase_OnTextChanged(object sender, TextChangedEventArgs e)
	{
		string text = $"{IPPart1.Text}.{IPPart2.Text}.{IPPart3.Text}.{IPPart4.Text}";
		if (IpRegex.IsMatch(text))
		{
			IP = text;
			if (OnIPValueChange != null)
			{
				OnIPValueChange(sender, null);
			}
		}
	}

}
