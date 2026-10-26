using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Converters;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Definitions;

public abstract class PropertyDefinitionBase : DefinitionBase
{
	private IList _targetProperties;

	private PropertyDefinitionCollection _propertyDefinitions;

	[TypeConverter(typeof(ListConverter))]
	public IList TargetProperties
	{
		get
		{
			return _targetProperties;
		}
		set
		{
			ThrowIfLocked(() => TargetProperties);
			_targetProperties = value;
		}
	}

	public PropertyDefinitionCollection PropertyDefinitions
	{
		get
		{
			return _propertyDefinitions;
		}
		set
		{
			ThrowIfLocked(() => PropertyDefinitions);
			_propertyDefinitions = value;
		}
	}

	internal PropertyDefinitionBase()
	{
		_targetProperties = new List<object>();
		PropertyDefinitions = new PropertyDefinitionCollection();
	}

	internal override void Lock()
	{
		if (IsLocked)
		{
			return;
		}
		base.Lock();
		List<object> list = new List<object>();
		if (_targetProperties != null)
		{
			foreach (object targetProperty in _targetProperties)
			{
				object obj = targetProperty;
				if (obj is TargetPropertyType targetPropertyType)
				{
					obj = targetPropertyType.Type;
				}
				list.Add(obj);
			}
		}
		IList targetProperties;
		if (!DesignerProperties.GetIsInDesignMode(this))
		{
			IList list2 = new ReadOnlyCollection<object>(list);
			targetProperties = list2;
		}
		else
		{
			IList list2 = new Collection<object>(list);
			targetProperties = list2;
		}
		_targetProperties = targetProperties;
	}
}
