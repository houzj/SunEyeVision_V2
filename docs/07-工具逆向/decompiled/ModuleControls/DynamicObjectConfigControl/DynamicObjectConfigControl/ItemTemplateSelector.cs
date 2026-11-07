using System.Windows;
using System.Windows.Controls;

namespace DynamicObjectConfigControl;

public class ItemTemplateSelector : DataTemplateSelector
{
	public DataTemplate FirstTemplate { get; set; }

	public DataTemplate SecondTemplate { get; set; }

	public override DataTemplate SelectTemplate(object item, DependencyObject container)
	{
		if (item is DisplayObjectDecorate displayObjectDecorate)
		{
			if (!displayObjectDecorate.IsDisplayForIO)
			{
				return FirstTemplate;
			}
			return SecondTemplate;
		}
		return base.SelectTemplate(item, container);
	}
}
