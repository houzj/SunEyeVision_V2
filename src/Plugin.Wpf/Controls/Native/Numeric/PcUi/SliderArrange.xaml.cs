using SunEyeVision.Plugin.Wpf.Native.Input.HikPcUI.ListView;
using SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;
using System;
using System.Windows;
using System.Windows.Input;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.HikPcUI.ListView;

public partial class SliderArrange : SliderArrangeBase, IComponentConnector, IStyleConnector
{
	private bool bTypeChange = false;

	private readonly object _lock = new object();

	public SliderArrange()
	{
		InitializeComponent();
	}

	private void PART_Popup_Opened(object sender, EventArgs e)
	{
		SilderWidth = (int)CBox.ActualWidth;
		ClipSilder();
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
		if (IsSubscribeLeft || IsSubscribeRight)
		{
			SliderVisibility = Visibility.Collapsed;
		}
		else
		{
			SliderVisibility = Visibility.Visible;
		}
	}
}
