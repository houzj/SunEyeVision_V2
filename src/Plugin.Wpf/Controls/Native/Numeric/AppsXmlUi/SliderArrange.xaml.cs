using SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;
using System;
using System.Windows.Input;
using System.Windows.Markup;

namespace SunEyeVision.Plugin.Wpf.Native.Numeric.Apps.XmlUI.ParamTabControl;

public partial class SliderArrange : SliderArrangeBase, IComponentConnector, IStyleConnector
{
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
}
