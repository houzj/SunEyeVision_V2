using System;
using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PrimitiveTypeCollectionEditor : TypeEditor<PrimitiveTypeCollectionControl>
{
	protected override void SetControlProperties(PropertyItem propertyItem)
	{
		Editor.BorderThickness = new Thickness(0.0);
		Editor.Content = "(Collection)";
	}

	protected override void SetValueDependencyProperty()
	{
		ValueProperty = PrimitiveTypeCollectionControl.ItemsSourceProperty;
	}

	protected override void ResolveValueBinding(PropertyItem propertyItem)
	{
		Type propertyType = propertyItem.PropertyType;
		Editor.ItemsSourceType = propertyType;
		if (propertyType.BaseType == typeof(Array))
		{
			Editor.ItemType = propertyType.GetElementType();
		}
		else
		{
			Type[] genericArguments = propertyType.GetGenericArguments();
			if (genericArguments.Length != 0)
			{
				Editor.ItemType = genericArguments[0];
			}
		}
		base.ResolveValueBinding(propertyItem);
	}
}
