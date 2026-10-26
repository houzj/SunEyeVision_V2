using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

[TemplatePart(Name = "content", Type = typeof(ContentControl))]
public class PropertyItem : CustomPropertyItem
{
	public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(PropertyItem), new UIPropertyMetadata(false));

	public bool IsReadOnly
	{
		get
		{
			return (bool)GetValue(IsReadOnlyProperty);
		}
		set
		{
			SetValue(IsReadOnlyProperty, value);
		}
	}

	public PropertyDescriptor PropertyDescriptor { get; internal set; }

	public string PropertyName
	{
		get
		{
			if (DescriptorDefinition == null)
			{
				return null;
			}
			return DescriptorDefinition.PropertyName;
		}
	}

	public Type PropertyType
	{
		get
		{
			if (PropertyDescriptor == null)
			{
				return null;
			}
			return PropertyDescriptor.PropertyType;
		}
	}

	internal DescriptorPropertyDefinitionBase DescriptorDefinition { get; private set; }

	public object Instance { get; internal set; }

	protected override string GetPropertyItemName()
	{
		return PropertyName;
	}

	protected override Type GetPropertyItemType()
	{
		return PropertyType;
	}

	protected override void OnIsExpandedChanged(bool oldValue, bool newValue)
	{
		if (newValue && IsLoaded)
		{
			GenerateExpandedPropertyItems();
		}
	}

	protected override object OnCoerceValueChanged(object baseValue)
	{
		BindingExpression bindingExpression = GetBindingExpression(CustomPropertyItem.ValueProperty);
		SetRedInvalidBorder(bindingExpression);
		return baseValue;
	}

	protected override void OnValueChanged(object oldValue, object newValue)
	{
		base.OnValueChanged(oldValue, newValue);
		if (newValue == null && DescriptorDefinition != null && DescriptorDefinition.DefaultValue != null)
		{
			SetCurrentValue(CustomPropertyItem.ValueProperty, DescriptorDefinition.DefaultValue);
		}
		SetValueFontWeight();
	}

	internal void SetRedInvalidBorder(BindingExpression be)
	{
		if (be != null && be.DataItem is DescriptorPropertyDefinitionBase)
		{
			DescriptorPropertyDefinitionBase element = be.DataItem as DescriptorPropertyDefinitionBase;
			if (Validation.GetHasError(element))
			{
				ReadOnlyObservableCollection<ValidationError> errors = Validation.GetErrors(element);
				Validation.MarkInvalid(be, errors[0]);
			}
		}
	}

	private void OnDefinitionContainerHelperInvalidated(object sender, EventArgs e)
	{
		if (ContainerHelper != null)
		{
			ContainerHelper.ClearHelper();
		}
		ObjectContainerHelperBase objectContainerHelperBase = (ObjectContainerHelperBase)(ContainerHelper = DescriptorDefinition.CreateContainerHelper(this));
		if (IsExpanded)
		{
			objectContainerHelperBase.GenerateProperties();
		}
	}

	private void Init(DescriptorPropertyDefinitionBase definition)
	{
		if (definition == null)
		{
			throw new ArgumentNullException("definition");
		}
		if (ContainerHelper != null)
		{
			ContainerHelper.ClearHelper();
		}
		DescriptorDefinition = definition;
		ContainerHelper = definition.CreateContainerHelper(this);
		definition.ContainerHelperInvalidated += OnDefinitionContainerHelperInvalidated;
		Loaded += PropertyItem_Loaded;
	}

	private void GenerateExpandedPropertyItems()
	{
		if (IsExpanded && ContainerHelper is ObjectContainerHelperBase objectContainerHelperBase)
		{
			objectContainerHelperBase.GenerateProperties();
		}
	}

	private void SetValueFontWeight()
	{
		if (ValueContainer != null)
		{
			ValueContainer.FontWeight = ((DescriptorDefinition != null && DescriptorDefinition.DefaultValue != null && Value != null && Value.Equals(DescriptorDefinition.DefaultValue) && !(Editor is ComboBox)) ? FontWeights.Bold : FontWeights.Normal);
		}
	}

	private void PropertyItem_Loaded(object sender, RoutedEventArgs e)
	{
		GenerateExpandedPropertyItems();
		SetValueFontWeight();
	}

	internal PropertyItem(DescriptorPropertyDefinitionBase definition)
		: base(definition.IsPropertyGridCategorized, !definition.PropertyType.IsArray)
	{
		Init(definition);
	}
}
