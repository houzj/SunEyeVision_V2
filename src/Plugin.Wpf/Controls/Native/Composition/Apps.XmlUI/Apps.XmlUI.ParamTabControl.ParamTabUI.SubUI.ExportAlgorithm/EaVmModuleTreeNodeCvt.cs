using System;
using System.Globalization;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTabControl.ParamTabUI.SubUI.ExportAlgorithm;

public class EaVmModuleTreeNodeCvt : IValueConverter
{
	public static EaVmModuleTreeNodeCvt Cvt = new EaVmModuleTreeNodeCvt();

	public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
	{
		if (value is EaVmModuleTreeNode node)
		{
			return node.IsEnableByVmModule();
		}
		return true;
	}

	public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
	{
		return Binding.DoNothing;
	}
}
