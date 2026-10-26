using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
using System;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;

public partial class SliderArrangeFloat : SliderArrangeBase, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty DecimalDigitsProperty = DependencyProperty.Register("DecimalDigits", typeof(int), typeof(SliderArrangeFloat), new PropertyMetadata(2));

	public static readonly DependencyProperty NeedSliderProperty = DependencyProperty.Register("NeedSlider", typeof(bool), typeof(SliderArrangeFloat), new PropertyMetadata(true));

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

	public SliderArrangeFloat()
	{
		InitializeComponent();
	}

	private void PART_Popup_Opened(object sender, EventArgs e)
	{
		SilderWidth = (int)CBox.ActualWidth;
		ClipSilder();
	}

	protected override void T1_Loaded(object sender, RoutedEventArgs e)
	{
		try
		{
			base.T1_Loaded(sender, e);
			T1?.SetBinding(NumericControl.TextProperty, new Binding("StartValue")
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

	protected override void T2_Loaded(object sender, RoutedEventArgs e)
	{
		try
		{
			base.T2_Loaded(sender, e);
			T2?.SetBinding(NumericControl.TextProperty, new Binding("EndValue")
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

	private void PART_Popup_Closed(object sender, EventArgs e)
	{
		T1?.ForceUpdateSource();
		T2?.ForceUpdateSource();
		DoStartValueChange();
		DoEndValueChange();
	}

	private void CBox_PreviewMouseDown(object sender, MouseButtonEventArgs e)
	{
		object originalSource = e.OriginalSource;
		if (originalSource != null)
		{
			bool? flag = originalSource.ToString()?.Contains("TextBoxView");
			bool flag2 = true;
			if (flag == true == flag2 && flag.HasValue && !CBox.IsDropDownOpen)
			{
				CBox.IsDropDownOpen = true;
			}
		}
	}
}
