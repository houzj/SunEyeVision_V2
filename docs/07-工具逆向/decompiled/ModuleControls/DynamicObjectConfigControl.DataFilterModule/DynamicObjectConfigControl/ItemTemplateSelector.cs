using System.Windows;
using System.Windows.Controls;

namespace DynamicObjectConfigControl;

public class ItemTemplateSelector : DataTemplateSelector
{
	public DataTemplate FirstTemplate { get; set; }

	public DataTemplate SecondTemplate { get; set; }

	public override DataTemplate SelectTemplate(object item, DependencyObject container)
	{
		if (item is DisplayObjectDecorate { IsDisplayForIO: var isDisplayForIO })
		{
			switch (isDisplayForIO)
			{
			case false:
				return FirstTemplate;
			case true:
				return SecondTemplate;
			}
		}
		return base.SelectTemplate(item, container);
	}
}
