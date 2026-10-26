using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

public class PropertyItemsControl : ItemsControl
{
	internal static readonly RoutedEvent PreparePropertyItemEvent = EventManager.RegisterRoutedEvent("PreparePropertyItem", RoutingStrategy.Bubble, typeof(PropertyItemEventHandler), typeof(PropertyItemsControl));

	internal static readonly RoutedEvent ClearPropertyItemEvent = EventManager.RegisterRoutedEvent("ClearPropertyItem", RoutingStrategy.Bubble, typeof(PropertyItemEventHandler), typeof(PropertyItemsControl));

	internal event PropertyItemEventHandler PreparePropertyItem
	{
		add
		{
			AddHandler(PreparePropertyItemEvent, value);
		}
		remove
		{
			RemoveHandler(PreparePropertyItemEvent, value);
		}
	}

	internal event PropertyItemEventHandler ClearPropertyItem
	{
		add
		{
			AddHandler(ClearPropertyItemEvent, value);
		}
		remove
		{
			RemoveHandler(ClearPropertyItemEvent, value);
		}
	}

	public PropertyItemsControl()
	{
		PropertyDescriptorCollection properties = TypeDescriptor.GetProperties(this, new Attribute[1]
		{
			new PropertyFilterAttribute(PropertyFilterOptions.All)
		});
		properties.Find("VirtualizingPanel.IsVirtualizingWhenGrouping", ignoreCase: false)?.SetValue(this, true);
		PropertyDescriptor propertyDescriptor = properties.Find("VirtualizingPanel.CacheLengthUnit", ignoreCase: false);
		propertyDescriptor?.SetValue(this, Enum.ToObject(propertyDescriptor.PropertyType, 1));
	}

	private void RaisePreparePropertyItemEvent(PropertyItemBase propertyItem, object item)
	{
		RaiseEvent(new PropertyItemEventArgs(PreparePropertyItemEvent, this, propertyItem, item));
	}

	private void RaiseClearPropertyItemEvent(PropertyItemBase propertyItem, object item)
	{
		RaiseEvent(new PropertyItemEventArgs(ClearPropertyItemEvent, this, propertyItem, item));
	}

	protected override bool IsItemItsOwnContainerOverride(object item)
	{
		return item is PropertyItemBase;
	}

	protected override void PrepareContainerForItemOverride(DependencyObject element, object item)
	{
		base.PrepareContainerForItemOverride(element, item);
		RaisePreparePropertyItemEvent((PropertyItemBase)element, item);
	}

	protected override void ClearContainerForItemOverride(DependencyObject element, object item)
	{
		RaiseClearPropertyItemEvent((PropertyItemBase)element, item);
		base.ClearContainerForItemOverride(element, item);
	}
}
