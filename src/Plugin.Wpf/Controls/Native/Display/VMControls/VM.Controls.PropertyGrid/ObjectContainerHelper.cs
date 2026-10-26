using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Attributes;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

internal class ObjectContainerHelper : ObjectContainerHelperBase
{
	private object _selectedObject;

	private object SelectedObject => _selectedObject;

	public ObjectContainerHelper(IPropertyContainer propertyContainer, object selectedObject)
		: base(propertyContainer)
	{
		_selectedObject = selectedObject;
	}

	protected override string GetDefaultPropertyName()
	{
		if (SelectedObject == null)
		{
			return null;
		}
		return ObjectContainerHelperBase.GetDefaultPropertyName(SelectedObject);
	}

	protected override IEnumerable<PropertyItem> GenerateSubPropertiesCore()
	{
		List<PropertyItem> list = new List<PropertyItem>();
		if (SelectedObject != null)
		{
			try
			{
				new List<PropertyDescriptor>();
				foreach (PropertyDescriptor propertyDescriptor in ObjectContainerHelperBase.GetPropertyDescriptors(SelectedObject, PropertyContainer.HideInheritedProperties))
				{
					PropertyDefinition propertyDefinition = GetPropertyDefinition(propertyDescriptor);
					bool flag = false;
					bool? flag2 = PropertyContainer.IsPropertyVisible(propertyDescriptor);
					if (flag2.HasValue)
					{
						flag = flag2.Value;
					}
					else
					{
						DisplayAttribute attribute = PropertyGridUtilities.GetAttribute<DisplayAttribute>(propertyDescriptor);
						if (attribute != null)
						{
							bool? autoGenerateField = attribute.GetAutoGenerateField();
							flag = PropertyContainer.AutoGenerateProperties && ((autoGenerateField.HasValue && autoGenerateField.Value) || !autoGenerateField.HasValue);
						}
						else
						{
							flag = propertyDescriptor.IsBrowsable && PropertyContainer.AutoGenerateProperties;
						}
						if (propertyDefinition != null)
						{
							flag = propertyDefinition.IsBrowsable ?? flag;
						}
					}
					if (flag)
					{
						PropertyItem propertyItem = CreatePropertyItem(propertyDescriptor, propertyDefinition);
						if (propertyItem != null)
						{
							list.Add(propertyItem);
						}
					}
				}
			}
			catch (Exception)
			{
			}
		}
		return list;
	}

	private PropertyItem CreatePropertyItem(PropertyDescriptor property, PropertyDefinition propertyDef)
	{
		DescriptorPropertyDefinition descriptorPropertyDefinition = new DescriptorPropertyDefinition(property, SelectedObject, PropertyContainer.IsCategorized);
		descriptorPropertyDefinition.InitProperties();
		InitializeDescriptorDefinition(descriptorPropertyDefinition, propertyDef);
		return new PropertyItem(descriptorPropertyDefinition)
		{
			Instance = SelectedObject,
			CategoryOrder = GetCategoryOrder(descriptorPropertyDefinition.CategoryValue),
			WillRefreshPropertyGrid = GetWillRefreshPropertyGrid(property)
		};
	}

	private int GetCategoryOrder(object categoryValue)
	{
		if (categoryValue == null)
		{
			return int.MaxValue;
		}
		int result = int.MaxValue;
		object selectedObject = SelectedObject;
		CategoryOrderAttribute categoryOrderAttribute = ((selectedObject != null) ? ((CategoryOrderAttribute[])selectedObject.GetType().GetCustomAttributes(typeof(CategoryOrderAttribute), inherit: true)) : new CategoryOrderAttribute[0]).FirstOrDefault((CategoryOrderAttribute a) => object.Equals(a.CategoryValue, categoryValue));
		if (categoryOrderAttribute != null)
		{
			result = categoryOrderAttribute.Order;
		}
		return result;
	}
}
