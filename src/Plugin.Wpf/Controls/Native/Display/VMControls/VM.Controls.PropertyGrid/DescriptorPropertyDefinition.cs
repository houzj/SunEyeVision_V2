using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Windows;
using System.Windows.Data;
using System.Windows.Markup.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Attributes;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

internal class DescriptorPropertyDefinition : DescriptorPropertyDefinitionBase
{
	private object _selectedObject;

	private PropertyDescriptor _propertyDescriptor;

	private DependencyPropertyDescriptor _dpDescriptor;

	private MarkupObject _markupObject;

	internal override PropertyDescriptor PropertyDescriptor => _propertyDescriptor;

	private object SelectedObject => _selectedObject;

	internal DescriptorPropertyDefinition(PropertyDescriptor propertyDescriptor, object selectedObject, bool isPropertyGridCategorized)
		: base(isPropertyGridCategorized)
	{
		Init(propertyDescriptor, selectedObject);
	}

	internal override ObjectContainerHelperBase CreateContainerHelper(IPropertyContainer parent)
	{
		return new ObjectContainerHelper(parent, Value);
	}

	internal override void OnValueChanged(object oldValue, object newValue)
	{
		base.OnValueChanged(oldValue, newValue);
		RaiseContainerHelperInvalidated();
	}

	protected override BindingBase CreateValueBinding()
	{
		object selectedObject = SelectedObject;
		return new Binding(PropertyDescriptor.Name)
		{
			Source = GetValueInstance(selectedObject),
			Mode = (PropertyDescriptor.IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay),
			ValidatesOnDataErrors = true,
			ValidatesOnExceptions = true,
			ConverterCulture = CultureInfo.CurrentCulture
		};
	}

	protected override bool ComputeIsReadOnly()
	{
		return PropertyDescriptor.IsReadOnly;
	}

	internal override ITypeEditor CreateDefaultEditor(PropertyItem propertyItem)
	{
		return PropertyGridUtilities.CreateDefaultEditor(PropertyDescriptor.PropertyType, PropertyDescriptor.Converter, propertyItem);
	}

	protected override bool ComputeCanResetValue()
	{
		if (PropertyDescriptor.CanResetValue(SelectedObject))
		{
			return !PropertyDescriptor.IsReadOnly;
		}
		return false;
	}

	protected override object ComputeAdvancedOptionsTooltip()
	{
		UpdateAdvanceOptionsForItem(_markupObject, SelectedObject as DependencyObject, _dpDescriptor, out var tooltip);
		return tooltip;
	}

	protected override string ComputeCategory()
	{
		DisplayAttribute attribute = PropertyGridUtilities.GetAttribute<DisplayAttribute>(PropertyDescriptor);
		if (attribute == null || attribute.GetGroupName() == null)
		{
			return PropertyDescriptor.Category;
		}
		return attribute.GetGroupName();
	}

	protected override string ComputeCategoryValue()
	{
		return PropertyDescriptor.Category;
	}

	protected override bool ComputeExpandableAttribute()
	{
		return (bool)ComputeExpandableAttributeForItem(PropertyDescriptor);
	}

	protected override object ComputeDefaultValueAttribute()
	{
		return ComputeDefaultValueAttributeForItem(PropertyDescriptor);
	}

	protected override bool ComputeIsExpandable()
	{
		return Value != null;
	}

	protected override IList<Type> ComputeNewItemTypes()
	{
		return (IList<Type>)ComputeNewItemTypesForItem(PropertyDescriptor);
	}

	protected override string ComputeDescription()
	{
		return (string)ComputeDescriptionForItem(PropertyDescriptor);
	}

	protected override int ComputeDisplayOrder(bool isPropertyGridCategorized)
	{
		IsPropertyGridCategorized = isPropertyGridCategorized;
		return (int)ComputeDisplayOrderForItem(PropertyDescriptor);
	}

	protected override void ResetValue()
	{
		PropertyDescriptor.ResetValue(SelectedObject);
	}

	internal override ITypeEditor CreateAttributeEditor()
	{
		EditorAttribute attribute = GetAttribute<EditorAttribute>();
		if (attribute != null)
		{
			Type type = Type.GetType(attribute.EditorTypeName, (AssemblyName name) => (from l in AppDomain.CurrentDomain.GetAssemblies()
				where l.FullName == name.FullName
				select l).FirstOrDefault(), null, throwOnError: true);
			if (typeof(ITypeEditor).IsAssignableFrom(type) && type.GetConstructor(new Type[0]) != null && Activator.CreateInstance(type) is ITypeEditor result)
			{
				return result;
			}
		}
		ItemsSourceAttribute attribute2 = GetAttribute<ItemsSourceAttribute>();
		if (attribute2 != null)
		{
			return new ItemsSourceAttributeEditor(attribute2);
		}
		return null;
	}

	private T GetAttribute<T>() where T : Attribute
	{
		return PropertyGridUtilities.GetAttribute<T>(PropertyDescriptor);
	}

	private void Init(PropertyDescriptor propertyDescriptor, object selectedObject)
	{
		if (propertyDescriptor == null)
		{
			throw new ArgumentNullException("propertyDescriptor");
		}
		if (selectedObject == null)
		{
			throw new ArgumentNullException("selectedObject");
		}
		_propertyDescriptor = propertyDescriptor;
		_selectedObject = selectedObject;
		_dpDescriptor = DependencyPropertyDescriptor.FromProperty(propertyDescriptor);
		_markupObject = MarkupWriter.GetMarkupObjectFor(SelectedObject);
	}
}
