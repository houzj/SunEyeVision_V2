using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

internal abstract class ObjectContainerHelperBase : ContainerHelperBase
{
	private bool _isPreparingItemFlag;

	private PropertyItemCollection _propertyItemCollection;

	public override IList Properties => _propertyItemCollection;

	private PropertyItem DefaultProperty
	{
		get
		{
			PropertyItem result = null;
			string defaultName = GetDefaultPropertyName();
			if (defaultName != null)
			{
				result = _propertyItemCollection.FirstOrDefault((PropertyItem prop) => object.Equals(defaultName, prop.PropertyDescriptor.Name));
			}
			return result;
		}
	}

	protected PropertyItemCollection PropertyItems => _propertyItemCollection;

	public ObjectContainerHelperBase(IPropertyContainer propertyContainer)
		: base(propertyContainer)
	{
		_propertyItemCollection = new PropertyItemCollection(new ObservableCollection<PropertyItem>());
		UpdateFilter();
		UpdateCategorization(updateSubPropertiesCategorization: false);
	}

	public override PropertyItemBase ContainerFromItem(object item)
	{
		if (item == null)
		{
			return null;
		}
		if (item is PropertyItem result)
		{
			return result;
		}
		string propertyStr = item as string;
		if (propertyStr != null)
		{
			return PropertyItems.FirstOrDefault((PropertyItem prop) => propertyStr == prop.PropertyDescriptor.Name);
		}
		return null;
	}

	public override object ItemFromContainer(PropertyItemBase container)
	{
		if (!(container is PropertyItem propertyItem))
		{
			return null;
		}
		return propertyItem.PropertyDescriptor.Name;
	}

	public override void UpdateValuesFromSource()
	{
		foreach (PropertyItem propertyItem in PropertyItems)
		{
			propertyItem.DescriptorDefinition.UpdateValueFromSource();
			propertyItem.ContainerHelper.UpdateValuesFromSource();
		}
	}

	public void GenerateProperties()
	{
		if (PropertyItems.Count == 0)
		{
			RegenerateProperties();
		}
	}

	protected override void OnFilterChanged()
	{
		UpdateFilter();
	}

	protected override void OnCategorizationChanged()
	{
		UpdateCategorization(updateSubPropertiesCategorization: true);
	}

	protected override void OnAutoGeneratePropertiesChanged()
	{
		RegenerateProperties();
	}

	protected override void OnHideInheritedPropertiesChanged()
	{
		RegenerateProperties();
	}

	protected override void OnEditorDefinitionsChanged()
	{
		RegenerateProperties();
	}

	protected override void OnPropertyDefinitionsChanged()
	{
		RegenerateProperties();
	}

	protected internal override void SetPropertiesExpansion(bool isExpanded)
	{
		if (Properties.Count == 0)
		{
			GenerateProperties();
		}
		base.SetPropertiesExpansion(isExpanded);
	}

	protected internal override void SetPropertiesExpansion(string propertyName, bool isExpanded)
	{
		if (Properties.Count == 0)
		{
			GenerateProperties();
		}
		base.SetPropertiesExpansion(propertyName, isExpanded);
	}

	private void UpdateFilter()
	{
		FilterInfo filterInfo = PropertyContainer.FilterInfo;
		PropertyItems.FilterPredicate = filterInfo.Predicate ?? PropertyItemCollection.CreateFilter(filterInfo.InputString);
	}

	private void UpdateCategorization(bool updateSubPropertiesCategorization)
	{
		_propertyItemCollection.UpdateCategorization(ComputeCategoryGroupDescription(), PropertyContainer.IsCategorized, PropertyContainer.IsSortedAlphabetically);
		if (!updateSubPropertiesCategorization || _propertyItemCollection.Count <= 0)
		{
			return;
		}
		foreach (PropertyItem item in _propertyItemCollection)
		{
			if (item.Properties is PropertyItemCollection propertyItemCollection)
			{
				propertyItemCollection.UpdateCategorization(ComputeCategoryGroupDescription(), PropertyContainer.IsCategorized, PropertyContainer.IsSortedAlphabetically);
			}
		}
	}

	private GroupDescription ComputeCategoryGroupDescription()
	{
		if (!PropertyContainer.IsCategorized)
		{
			return null;
		}
		return new PropertyGroupDescription(PropertyItemCollection.CategoryPropertyName);
	}

	private string GetCategoryGroupingPropertyName()
	{
		if (!(ComputeCategoryGroupDescription() is PropertyGroupDescription propertyGroupDescription))
		{
			return null;
		}
		return propertyGroupDescription.PropertyName;
	}

	private void OnChildrenPropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if ((IsItemOrderingProperty(e.PropertyName) || GetCategoryGroupingPropertyName() == e.PropertyName) && ChildrenItemsControl.ItemContainerGenerator.Status != GeneratorStatus.GeneratingContainers && !_isPreparingItemFlag)
		{
			PropertyItems.RefreshView();
		}
	}

	protected abstract string GetDefaultPropertyName();

	protected abstract IEnumerable<PropertyItem> GenerateSubPropertiesCore();

	private void RegenerateProperties()
	{
		IEnumerable<PropertyItem> enumerable = GenerateSubPropertiesCore();
		foreach (PropertyItem item in enumerable)
		{
			InitializePropertyItem(item);
		}
		foreach (PropertyItem propertyItem in PropertyItems)
		{
			propertyItem.PropertyChanged -= OnChildrenPropertyChanged;
		}
		PropertyItems.UpdateItems(enumerable);
		foreach (PropertyItem propertyItem2 in PropertyItems)
		{
			propertyItem2.PropertyChanged += OnChildrenPropertyChanged;
		}
		if (PropertyContainer is PropertyGrid propertyGrid)
		{
			propertyGrid.SelectedPropertyItem = DefaultProperty;
		}
	}

	protected static List<PropertyDescriptor> GetPropertyDescriptors(object instance, bool hideInheritedProperties)
	{
		TypeConverter converter = TypeDescriptor.GetConverter(instance);
		PropertyDescriptorCollection propertyDescriptorCollection;
		if (converter != null && converter.GetPropertiesSupported())
		{
			propertyDescriptorCollection = converter.GetProperties(instance);
		}
		else if (instance is ICustomTypeDescriptor)
		{
			propertyDescriptorCollection = ((ICustomTypeDescriptor)instance).GetProperties();
		}
		else
		{
			propertyDescriptorCollection = ((!(instance.GetType().GetInterface("ICustomTypeProvider", ignoreCase: true) != null)) ? TypeDescriptor.GetProperties(instance.GetType()) : TypeDescriptor.GetProperties(instance.GetType().GetMethod("GetCustomType").Invoke(instance, null) as Type));
		}
		if (propertyDescriptorCollection != null)
		{
			IEnumerable<PropertyDescriptor> source = propertyDescriptorCollection.Cast<PropertyDescriptor>();
			if (hideInheritedProperties)
			{
				return source.Where((PropertyDescriptor p) => p.ComponentType == instance.GetType()).ToList();
			}
			return source.ToList();
		}
		return null;
	}

	protected bool GetWillRefreshPropertyGrid(PropertyDescriptor propertyDescriptor)
	{
		if (propertyDescriptor == null)
		{
			return false;
		}
		RefreshPropertiesAttribute attribute = PropertyGridUtilities.GetAttribute<RefreshPropertiesAttribute>(propertyDescriptor);
		if (attribute != null)
		{
			return attribute.RefreshProperties != RefreshProperties.None;
		}
		return false;
	}

	internal void InitializeDescriptorDefinition(DescriptorPropertyDefinitionBase descriptorDef, PropertyDefinition propertyDefinition)
	{
		if (descriptorDef == null)
		{
			throw new ArgumentNullException("descriptorDef");
		}
		if (propertyDefinition != null && propertyDefinition != null)
		{
			if (propertyDefinition.Category != null)
			{
				descriptorDef.Category = propertyDefinition.Category;
				descriptorDef.CategoryValue = propertyDefinition.Category;
			}
			if (propertyDefinition.Description != null)
			{
				descriptorDef.Description = propertyDefinition.Description;
			}
			if (propertyDefinition.DisplayName != null)
			{
				descriptorDef.DisplayName = propertyDefinition.DisplayName;
			}
			if (propertyDefinition.DisplayOrder.HasValue)
			{
				descriptorDef.DisplayOrder = propertyDefinition.DisplayOrder.Value;
			}
			if (propertyDefinition.IsExpandable.HasValue)
			{
				descriptorDef.ExpandableAttribute = propertyDefinition.IsExpandable.Value;
			}
		}
	}

	private void InitializePropertyItem(PropertyItem propertyItem)
	{
		DescriptorPropertyDefinitionBase pd = propertyItem.DescriptorDefinition;
		propertyItem.PropertyDescriptor = pd.PropertyDescriptor;
		propertyItem.IsReadOnly = pd.IsReadOnly;
		propertyItem.DisplayName = pd.DisplayName;
		propertyItem.Description = pd.Description;
		propertyItem.Category = pd.Category;
		propertyItem.PropertyOrder = pd.DisplayOrder;
		if (pd.PropertyDescriptor.Converter is ExpandableObjectConverter)
		{
			propertyItem.IsExpandable = true;
		}
		else
		{
			SetupDefinitionBinding(propertyItem, PropertyItemBase.IsExpandableProperty, pd, () => pd.IsExpandable, BindingMode.OneWay);
		}
		SetupDefinitionBinding(propertyItem, PropertyItemBase.AdvancedOptionsIconProperty, pd, () => pd.AdvancedOptionsIcon, BindingMode.OneWay);
		SetupDefinitionBinding(propertyItem, PropertyItemBase.AdvancedOptionsTooltipProperty, pd, () => pd.AdvancedOptionsTooltip, BindingMode.OneWay);
		SetupDefinitionBinding(propertyItem, CustomPropertyItem.ValueProperty, pd, () => pd.Value, BindingMode.TwoWay);
		if (pd.CommandBindings != null)
		{
			foreach (CommandBinding commandBinding in pd.CommandBindings)
			{
				propertyItem.CommandBindings.Add(commandBinding);
			}
		}
		if (pd.DefaultValue != null)
		{
			object typeDefaultValue = GetTypeDefaultValue(propertyItem.PropertyType);
			if ((propertyItem.Value != null && propertyItem.Value.Equals(typeDefaultValue)) || (propertyItem.Value == null && typeDefaultValue == propertyItem.Value))
			{
				propertyItem.SetCurrentValue(CustomPropertyItem.ValueProperty, pd.DefaultValue);
			}
		}
	}

	private object GetTypeDefaultValue(Type type)
	{
		if (type.IsGenericType && type.GetGenericTypeDefinition() == typeof(Nullable<>))
		{
			type = type.GetProperty("Value").PropertyType;
		}
		if (!type.IsValueType)
		{
			return null;
		}
		return Activator.CreateInstance(type);
	}

	private void SetupDefinitionBinding<T>(PropertyItem propertyItem, DependencyProperty itemProperty, DescriptorPropertyDefinitionBase pd, Expression<Func<T>> definitionProperty, BindingMode bindingMode)
	{
		Binding binding = new Binding(ReflectionHelper.GetPropertyOrFieldName(definitionProperty))
		{
			Source = pd,
			Mode = bindingMode
		};
		propertyItem.SetBinding(itemProperty, binding);
	}

	internal FrameworkElement GenerateChildrenEditorElement(PropertyItem propertyItem)
	{
		FrameworkElement frameworkElement = null;
		DescriptorPropertyDefinitionBase descriptorDefinition = propertyItem.DescriptorDefinition;
		object obj = null;
		Type type = obj as Type;
		ITypeEditor typeEditor = null;
		if (typeEditor == null)
		{
			typeEditor = descriptorDefinition.CreateAttributeEditor();
		}
		if (typeEditor != null)
		{
			frameworkElement = typeEditor.ResolveEditor(propertyItem);
		}
		if (frameworkElement == null && obj == null && propertyItem.PropertyDescriptor != null)
		{
			frameworkElement = GenerateCustomEditingElement(propertyItem.PropertyDescriptor.Name, propertyItem);
		}
		if (frameworkElement == null && type == null)
		{
			frameworkElement = GenerateCustomEditingElement(propertyItem.PropertyType, propertyItem);
		}
		if (frameworkElement == null)
		{
			if (descriptorDefinition.IsReadOnly)
			{
				typeEditor = new TextBlockEditor();
			}
			if (typeEditor == null)
			{
				typeEditor = descriptorDefinition.CreateDefaultEditor(propertyItem);
			}
			frameworkElement = typeEditor.ResolveEditor(propertyItem);
		}
		return frameworkElement;
	}

	internal PropertyDefinition GetPropertyDefinition(PropertyDescriptor descriptor)
	{
		PropertyDefinition propertyDefinition = null;
		PropertyDefinitionCollection propertyDefinitions = PropertyContainer.PropertyDefinitions;
		if (propertyDefinitions != null)
		{
			propertyDefinition = propertyDefinitions[descriptor.Name];
			if (propertyDefinition == null)
			{
				propertyDefinition = propertyDefinitions.GetRecursiveBaseTypes(descriptor.PropertyType);
			}
		}
		return propertyDefinition;
	}

	public override void PrepareChildrenPropertyItem(PropertyItemBase propertyItem, object item)
	{
		_isPreparingItemFlag = true;
		base.PrepareChildrenPropertyItem(propertyItem, item);
		if (propertyItem.Editor == null)
		{
			FrameworkElement frameworkElement = GenerateChildrenEditorElement((PropertyItem)propertyItem);
			if (frameworkElement != null)
			{
				ContainerHelperBase.SetIsGenerated(frameworkElement, value: true);
				propertyItem.Editor = frameworkElement;
			}
		}
		_isPreparingItemFlag = false;
	}

	public override void ClearChildrenPropertyItem(PropertyItemBase propertyItem, object item)
	{
		if (propertyItem.Editor != null && ContainerHelperBase.GetIsGenerated(propertyItem.Editor))
		{
			propertyItem.Editor = null;
		}
		base.ClearChildrenPropertyItem(propertyItem, item);
	}

	public override Binding CreateChildrenDefaultBinding(PropertyItemBase propertyItem)
	{
		return new Binding("Value")
		{
			Mode = (((PropertyItem)propertyItem).IsReadOnly ? BindingMode.OneWay : BindingMode.TwoWay)
		};
	}

	protected static string GetDefaultPropertyName(object instance)
	{
		return ((DefaultPropertyAttribute)TypeDescriptor.GetAttributes(instance)[typeof(DefaultPropertyAttribute)])?.Name;
	}

	private static bool IsItemOrderingProperty(string propertyName)
	{
		if (!string.Equals(propertyName, PropertyItemCollection.DisplayNamePropertyName) && !string.Equals(propertyName, PropertyItemCollection.CategoryOrderPropertyName))
		{
			return string.Equals(propertyName, PropertyItemCollection.PropertyOrderPropertyName);
		}
		return true;
	}
}
