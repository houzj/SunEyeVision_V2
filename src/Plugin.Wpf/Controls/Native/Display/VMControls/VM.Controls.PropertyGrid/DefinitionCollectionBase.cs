using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.ObjectModel;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Definitions;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

public abstract class DefinitionCollectionBase<T> : ObservableCollection<T> where T : DefinitionBase
{
	internal DefinitionCollectionBase()
	{
	}

	protected override void InsertItem(int index, T item)
	{
		if (item == null)
		{
			throw new InvalidOperationException("Cannot insert null items in the collection.");
		}
		item.Lock();
		base.InsertItem(index, item);
	}

	protected override void SetItem(int index, T item)
	{
		if (item == null)
		{
			throw new InvalidOperationException("Cannot insert null items in the collection.");
		}
		item.Lock();
		base.SetItem(index, item);
	}
}
