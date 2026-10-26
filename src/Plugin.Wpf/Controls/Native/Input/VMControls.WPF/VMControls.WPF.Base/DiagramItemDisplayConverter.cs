using System;
using System.Globalization;
using System.Windows.Data;
using SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.DataForUI;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Base;

/// <summary>
/// 主显示类型转换
/// </summary>
public class DiagramItemDisplayConverter : IValueConverter
{
	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is ModuleDisplayType moduleDisplayType)
		{
			switch (moduleDisplayType)
			{
			case ModuleDisplayType.NORMAL:
			case ModuleDisplayType.IMAGE:
			case ModuleDisplayType.IFBRANCH:
			case ModuleDisplayType.FUNCTION:
			case ModuleDisplayType.SHELL:
			case ModuleDisplayType.COMMUNCATION:
			case ModuleDisplayType.TIMER:
				return DiagramItemDisplayEnum.MODULE;
			case ModuleDisplayType.NORMALADORNER:
			case ModuleDisplayType.IMAGEADORNER:
			case ModuleDisplayType.IFBRANCHADORNER:
			case ModuleDisplayType.FUNCTIONADORNER:
			case ModuleDisplayType.SHELLADORNER:
			case ModuleDisplayType.COMMUNCATIONADORNER:
			case ModuleDisplayType.TIMERADORNER:
				return DiagramItemDisplayEnum.ADORNER;
			}
		}
		return DiagramItemDisplayEnum.DEFAULT;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Convert(value, targetType, parameter, culture);
	}
}
