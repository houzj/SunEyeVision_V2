using System;
using System.Collections.Generic;
using VM.Controls.Core.Utilities;

namespace VM.Controls.PropertyGrid.Editors;

public class CollectionEditor : TypeEditor<CollectionControlButton>
{
	protected override void SetValueDependencyProperty()
	{
		ValueProperty = CollectionControlButton.ItemsSourceProperty;
	}

	protected override void ResolveValueBinding(PropertyItem propertyItem)
	{
		Type propertyType = propertyItem.PropertyType;
		Editor.ItemsSourceType = propertyType;
		if (propertyType.BaseType == typeof(Array))
		{
			Editor.NewItemTypes = new List<Type> { propertyType.GetElementType() };
		}
		else if (propertyItem.DescriptorDefinition != null && propertyItem.DescriptorDefinition.NewItemTypes != null && propertyItem.DescriptorDefinition.NewItemTypes.Count > 0)
		{
			Editor.NewItemTypes = propertyItem.DescriptorDefinition.NewItemTypes;
		}
		else
		{
			Type[] dictionaryItemsType = ListUtilities.GetDictionaryItemsType(propertyType);
			if (dictionaryItemsType != null && dictionaryItemsType.Length == 2)
			{
				Type item = ListUtilities.CreateEditableKeyValuePairType(dictionaryItemsType[0], dictionaryItemsType[1]);
				Editor.NewItemTypes = new List<Type> { item };
			}
			else
			{
				Type listItemType = ListUtilities.GetListItemType(propertyType);
				if (listItemType != null)
				{
					Editor.NewItemTypes = new List<Type> { listItemType };
				}
				else
				{
					Type collectionItemType = ListUtilities.GetCollectionItemType(propertyType);
					if (collectionItemType != null)
					{
						Editor.NewItemTypes = new List<Type> { collectionItemType };
					}
				}
			}
		}
		base.ResolveValueBinding(propertyItem);
	}
}
