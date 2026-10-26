using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Release.Front;

internal static class StyleHelper
{
	/// <summary>
	/// 重载样式
	/// </summary>
	/// <param name="styleProperty">样式属性</param>
	/// <param name="controlType">控件类型</param>
	internal static void OverrideMetadata(this DependencyProperty styleProperty, Type controlType)
	{
		try
		{
			ResourceDictionary resourceDictionary = new ResourceDictionary
			{
				Source = new Uri($"/SunEyeVision.Plugin.Wpf;component/Themes/Native/Input/VMControls.WPF.Release/front/styles/{controlType.Name}.xaml", UriKind.Relative)
			};
			if (resourceDictionary.Contains(controlType.Name))
			{
				styleProperty.OverrideMetadata(controlType, new FrameworkPropertyMetadata(resourceDictionary[controlType.Name]));
			}
		}
		catch
		{
		}
	}
}
