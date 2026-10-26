using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.Plugin.Wpf.Native.Dependencies.Apps.UIHelper.Extern;
using VMControls.Interface;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMBaseControls;

public class AdditionTemplateSelector : DataTemplateSelector
{
	public override DataTemplate SelectTemplate(object item, DependencyObject container)
	{
		if (Ioc.HotGet<IAdditionService>() != null)
		{
			Ioc.Get<IAdditionService>()?.AdditionResource(container.GetParent<IImageViewBox>(), ResourceType.imagebox);
			return (DataTemplate)(container as FrameworkElement).FindResource("PointCloundCanvas");
		}
		return null;
	}
}
