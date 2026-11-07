using System.Windows;
using System.Windows.Controls;
using Apps.UIHelper.Extern;
using VMControls.Interface;

namespace VMBaseControls;

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
