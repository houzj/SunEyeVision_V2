using System;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;

public partial class SliderArrangeFloat : SliderArrangeBase, IComponentConnector, IStyleConnector
{
	public static readonly DependencyProperty DecimalDigitsProperty = DependencyProperty.Register("DecimalDigits", typeof(int), typeof(SliderArrangeFloat), new PropertyMetadata(2));

	public static readonly DependencyProperty NeedSliderProperty = DependencyProperty.Register("NeedSlider", typeof(bool), typeof(SliderArrangeFloat), new PropertyMetadata(true, NeedSliderChanged));

	private bool bTypeChange = false;

	private readonly object _lock = new object();

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

	private static void NeedSliderChanged(DependencyObject d, DependencyPropertyChangedEventArgs e)
	{
		(d as SliderArrangeFloat).OnNeedSliderChanged();
	}

	public SliderArrangeFloat()
	{
		InitializeComponent();
	}

	private void OnNeedSliderChanged()
	{
		ChangedSliderVisibility();
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

	private new void TypeChangeLeft_Click(object sender, RoutedEventArgs e)
	{
		bTypeChange = true;
		base.TypeChangeLeft_Click(sender, e);
	}

	private new void TypeChangeRight_Click(object sender, RoutedEventArgs e)
	{
		bTypeChange = true;
		base.TypeChangeRight_Click(sender, e);
	}

	protected override void ChangedSliderVisibility()
	{
		if (!NeedSlider || IsSubscribeLeft || IsSubscribeRight)
		{
			SliderVisibility = Visibility.Collapsed;
		}
		else
		{
			SliderVisibility = Visibility.Visible;
		}
	}
}
