using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Definitions;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid;

public abstract class PropertyDefinitionBaseCollection<T> : DefinitionCollectionBase<T> where T : PropertyDefinitionBase
{
	public virtual T this[object propertyId]
	{
		get
		{
			foreach (T item in Items)
			{
				if (item.TargetProperties.Contains(propertyId))
				{
					return item;
				}
				List<string> list = item.TargetProperties.OfType<string>().ToList();
				if (list != null && list.Count > 0)
				{
					if (!(propertyId is string))
					{
						continue;
					}
					string text = (string)propertyId;
					foreach (string item2 in list)
					{
						if (item2.Contains("*"))
						{
							string value = item2.Replace("*", "");
							if (text.StartsWith(value) || text.EndsWith(value))
							{
								return item;
							}
						}
					}
					continue;
				}
				Type type = propertyId as Type;
				if (!(type != null))
				{
					continue;
				}
				foreach (Type targetProperty in item.TargetProperties)
				{
					if (targetProperty.IsAssignableFrom(type))
					{
						return item;
					}
				}
			}
			return null;
		}
	}

	internal T GetRecursiveBaseTypes(Type type)
	{
		T val = null;
		while (val == null && type != null)
		{
			val = this[type];
			type = type.BaseType;
		}
		return val;
	}
}
