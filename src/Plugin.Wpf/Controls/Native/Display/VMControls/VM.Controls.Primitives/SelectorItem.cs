using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Controls;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Primitives;

public class SelectorItem : ContentControl
{
	public static readonly DependencyProperty IsSelectedProperty;

	public static readonly RoutedEvent SelectedEvent;

	public static readonly RoutedEvent UnselectedEvent;

	public bool IsSelected
	{
		get
		{
			return (bool)GetValue(IsSelectedProperty);
		}
		set
		{
			SetValue(IsSelectedProperty, value);
		}
	}

	internal Selector ParentSelector => ItemsControl.ItemsControlFromItemContainer(this) as Selector;

	static SelectorItem()
	{
		IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(SelectorItem), new UIPropertyMetadata(false, OnIsSelectedChanged));
		SelectedEvent = Selector.SelectedEvent.AddOwner(typeof(SelectorItem));
		UnselectedEvent = Selector.UnSelectedEvent.AddOwner(typeof(SelectorItem));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(SelectorItem), new FrameworkPropertyMetadata(typeof(SelectorItem)));
	}

	private static void OnIsSelectedChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is SelectorItem selectorItem)
		{
			selectorItem.OnIsSelectedChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnIsSelectedChanged(bool oldValue, bool newValue)
	{
		if (newValue)
		{
			RaiseEvent(new RoutedEventArgs(Selector.SelectedEvent, this));
		}
		else
		{
			RaiseEvent(new RoutedEventArgs(Selector.UnSelectedEvent, this));
		}
	}
}
