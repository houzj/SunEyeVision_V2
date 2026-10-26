using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Windows;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Markup.Primitives;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Attributes;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Commands;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

internal abstract class DescriptorPropertyDefinitionBase : DependencyObject
{
	private string _category;

	private string _categoryValue;

	private string _description;

	private string _displayName;

	private object _defaultValue;

	private int _displayOrder;

	private bool _expandableAttribute;

	private bool _isReadOnly;

	private IList<Type> _newItemTypes;

	private IEnumerable<CommandBinding> _commandBindings;

	public static readonly DependencyProperty AdvancedOptionsIconProperty = DependencyProperty.Register("AdvancedOptionsIcon", typeof(ImageSource), typeof(DescriptorPropertyDefinitionBase), new UIPropertyMetadata(null));

	public static readonly DependencyProperty AdvancedOptionsTooltipProperty = DependencyProperty.Register("AdvancedOptionsTooltip", typeof(object), typeof(DescriptorPropertyDefinitionBase), new UIPropertyMetadata(null));

	public static readonly DependencyProperty IsExpandableProperty = DependencyProperty.Register("IsExpandable", typeof(bool), typeof(DescriptorPropertyDefinitionBase), new UIPropertyMetadata(false));

	public static readonly DependencyProperty ValueProperty = DependencyProperty.Register("Value", typeof(object), typeof(DescriptorPropertyDefinitionBase), new UIPropertyMetadata(null, OnValueChanged));

	internal abstract PropertyDescriptor PropertyDescriptor { get; }

	public ImageSource AdvancedOptionsIcon
	{
		get
		{
			return (ImageSource)GetValue(AdvancedOptionsIconProperty);
		}
		set
		{
			SetValue(AdvancedOptionsIconProperty, value);
		}
	}

	public object AdvancedOptionsTooltip
	{
		get
		{
			return GetValue(AdvancedOptionsTooltipProperty);
		}
		set
		{
			SetValue(AdvancedOptionsTooltipProperty, value);
		}
	}

	public bool IsExpandable
	{
		get
		{
			return (bool)GetValue(IsExpandableProperty);
		}
		set
		{
			SetValue(IsExpandableProperty, value);
		}
	}

	public string Category
	{
		get
		{
			return _category;
		}
		internal set
		{
			_category = value;
		}
	}

	public string CategoryValue
	{
		get
		{
			return _categoryValue;
		}
		internal set
		{
			_categoryValue = value;
		}
	}

	public IEnumerable<CommandBinding> CommandBindings => _commandBindings;

	public string DisplayName
	{
		get
		{
			return _displayName;
		}
		internal set
		{
			_displayName = value;
		}
	}

	public object DefaultValue
	{
		get
		{
			return _defaultValue;
		}
		set
		{
			_defaultValue = value;
		}
	}

	public string Description
	{
		get
		{
			return _description;
		}
		internal set
		{
			_description = value;
		}
	}

	public int DisplayOrder
	{
		get
		{
			return _displayOrder;
		}
		internal set
		{
			_displayOrder = value;
		}
	}

	public bool IsReadOnly => _isReadOnly;

	public IList<Type> NewItemTypes => _newItemTypes;

	public string PropertyName => PropertyDescriptor.Name;

	public Type PropertyType => PropertyDescriptor.PropertyType;

	internal bool ExpandableAttribute
	{
		get
		{
			return _expandableAttribute;
		}
		set
		{
			_expandableAttribute = value;
			UpdateIsExpandable();
		}
	}

	internal bool IsPropertyGridCategorized { get; set; }

	public object Value
	{
		get
		{
			return GetValue(ValueProperty);
		}
		set
		{
			SetValue(ValueProperty, value);
		}
	}

	public event EventHandler ContainerHelperInvalidated;

	internal DescriptorPropertyDefinitionBase(bool isPropertyGridCategorized)
	{
		IsPropertyGridCategorized = isPropertyGridCategorized;
	}

	protected virtual string ComputeCategory()
	{
		return null;
	}

	protected virtual string ComputeCategoryValue()
	{
		return null;
	}

	protected virtual string ComputeDescription()
	{
		return null;
	}

	protected virtual int ComputeDisplayOrder(bool isPropertyGridCategorized)
	{
		return int.MaxValue;
	}

	protected virtual bool ComputeExpandableAttribute()
	{
		return false;
	}

	protected virtual object ComputeDefaultValueAttribute()
	{
		return null;
	}

	protected abstract bool ComputeIsExpandable();

	protected virtual IList<Type> ComputeNewItemTypes()
	{
		return null;
	}

	protected virtual bool ComputeIsReadOnly()
	{
		return false;
	}

	protected virtual bool ComputeCanResetValue()
	{
		return false;
	}

	protected virtual object ComputeAdvancedOptionsTooltip()
	{
		return null;
	}

	protected virtual void ResetValue()
	{
	}

	protected abstract BindingBase CreateValueBinding();

	internal abstract ObjectContainerHelperBase CreateContainerHelper(IPropertyContainer parent);

	internal void RaiseContainerHelperInvalidated()
	{
		if (ContainerHelperInvalidated != null)
		{
			ContainerHelperInvalidated(this, EventArgs.Empty);
		}
	}

	internal virtual ITypeEditor CreateDefaultEditor(PropertyItem propertyItem)
	{
		return null;
	}

	internal virtual ITypeEditor CreateAttributeEditor()
	{
		return null;
	}

	internal void UpdateAdvanceOptionsForItem(MarkupObject markupObject, DependencyObject dependencyObject, DependencyPropertyDescriptor dpDescriptor, out object tooltip)
	{
		tooltip = StringConstants.AdvancedProperties;
		bool flag = false;
		bool flag2 = false;
		MarkupProperty markupProperty = markupObject.Properties.FirstOrDefault((MarkupProperty p) => p.Name == PropertyName);
		if (markupProperty != null)
		{
			flag = typeof(Style).IsAssignableFrom(markupProperty.PropertyType);
			flag2 = typeof(DynamicResourceExtension).IsAssignableFrom(markupProperty.PropertyType);
		}
		if (flag | flag2)
		{
			tooltip = StringConstants.Resource;
		}
		else
		{
			if (dependencyObject == null || dpDescriptor == null)
			{
				return;
			}
			if (BindingOperations.GetBindingExpressionBase(dependencyObject, dpDescriptor.DependencyProperty) != null)
			{
				tooltip = StringConstants.Databinding;
				return;
			}
			switch (DependencyPropertyHelper.GetValueSource(dependencyObject, dpDescriptor.DependencyProperty).BaseValueSource)
			{
			case BaseValueSource.Inherited:
			case BaseValueSource.DefaultStyle:
			case BaseValueSource.ImplicitStyleReference:
				tooltip = StringConstants.Inheritance;
				break;
			case BaseValueSource.Style:
				tooltip = StringConstants.StyleSetter;
				break;
			case BaseValueSource.Local:
				tooltip = StringConstants.Local;
				break;
			case BaseValueSource.DefaultStyleTrigger:
			case BaseValueSource.TemplateTrigger:
			case BaseValueSource.StyleTrigger:
			case BaseValueSource.ParentTemplate:
			case BaseValueSource.ParentTemplateTrigger:
				break;
			}
		}
	}

	internal void UpdateAdvanceOptions()
	{
		AdvancedOptionsTooltip = ComputeAdvancedOptionsTooltip();
	}

	internal void UpdateIsExpandable()
	{
		IsExpandable = ExpandableAttribute && ComputeIsExpandable();
	}

	internal void UpdateValueFromSource()
	{
		BindingOperations.GetBindingExpressionBase(this, ValueProperty)?.UpdateTarget();
	}

	internal object ComputeDescriptionForItem(object item)
	{
		PropertyDescriptor propertyDescriptor = item as PropertyDescriptor;
		DisplayAttribute attribute = PropertyGridUtilities.GetAttribute<DisplayAttribute>(propertyDescriptor);
		if (attribute != null)
		{
			return attribute.GetDescription();
		}
		DescriptionAttribute attribute2 = PropertyGridUtilities.GetAttribute<DescriptionAttribute>(propertyDescriptor);
		if (attribute2 == null)
		{
			return propertyDescriptor.Description;
		}
		return attribute2.Description;
	}

	internal object ComputeNewItemTypesForItem(object item)
	{
		return PropertyGridUtilities.GetAttribute<NewItemTypesAttribute>(item as PropertyDescriptor)?.Types;
	}

	internal object ComputeDisplayOrderForItem(object item)
	{
		PropertyDescriptor propertyDescriptor = item as PropertyDescriptor;
		DisplayAttribute attribute = PropertyGridUtilities.GetAttribute<DisplayAttribute>(PropertyDescriptor);
		if (attribute != null && attribute.GetOrder().HasValue)
		{
			return attribute.GetOrder();
		}
		List<PropertyOrderAttribute> list = propertyDescriptor.Attributes.OfType<PropertyOrderAttribute>().ToList();
		if (list.Count > 0)
		{
			ValidatePropertyOrderAttributes(list);
			if (IsPropertyGridCategorized)
			{
				PropertyOrderAttribute propertyOrderAttribute = list.FirstOrDefault((PropertyOrderAttribute x) => x.UsageContext == UsageContextEnum.Categorized || x.UsageContext == UsageContextEnum.Both);
				if (propertyOrderAttribute != null)
				{
					return propertyOrderAttribute.Order;
				}
			}
			else
			{
				PropertyOrderAttribute propertyOrderAttribute2 = list.FirstOrDefault((PropertyOrderAttribute x) => x.UsageContext == UsageContextEnum.Alphabetical || x.UsageContext == UsageContextEnum.Both);
				if (propertyOrderAttribute2 != null)
				{
					return propertyOrderAttribute2.Order;
				}
			}
		}
		return int.MaxValue;
	}

	internal object ComputeExpandableAttributeForItem(object item)
	{
		return PropertyGridUtilities.GetAttribute<ExpandableObjectAttribute>((PropertyDescriptor)item) != null;
	}

	internal int ComputeDisplayOrderInternal(bool isPropertyGridCategorized)
	{
		return ComputeDisplayOrder(isPropertyGridCategorized);
	}

	internal object GetValueInstance(object sourceObject)
	{
		if (sourceObject is ICustomTypeDescriptor customTypeDescriptor)
		{
			sourceObject = customTypeDescriptor.GetPropertyOwner(PropertyDescriptor);
		}
		return sourceObject;
	}

	internal object ComputeDefaultValueAttributeForItem(object item)
	{
		return PropertyGridUtilities.GetAttribute<DefaultValueAttribute>((PropertyDescriptor)item)?.Value;
	}

	private void ExecuteResetValueCommand(object sender, ExecutedRoutedEventArgs e)
	{
		if (ComputeCanResetValue())
		{
			ResetValue();
		}
	}

	private void CanExecuteResetValueCommand(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = ComputeCanResetValue();
	}

	private string ComputeDisplayName()
	{
		DisplayAttribute attribute = PropertyGridUtilities.GetAttribute<DisplayAttribute>(PropertyDescriptor);
		string text = ((attribute != null) ? attribute.GetName() : PropertyDescriptor.DisplayName);
		ParenthesizePropertyNameAttribute attribute2 = PropertyGridUtilities.GetAttribute<ParenthesizePropertyNameAttribute>(PropertyDescriptor);
		if (attribute2 != null && attribute2.NeedParenthesis)
		{
			text = "(" + text + ")";
		}
		return text;
	}

	private void ValidatePropertyOrderAttributes(List<PropertyOrderAttribute> list)
	{
		if (list.Count > 0 && list.FirstOrDefault((PropertyOrderAttribute x) => x.UsageContext == UsageContextEnum.Both) != null)
		{
			_ = list.Count;
			_ = 1;
		}
	}

	private static void OnValueChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		((DescriptorPropertyDefinitionBase)o).OnValueChanged(e.OldValue, e.NewValue);
	}

	internal virtual void OnValueChanged(object oldValue, object newValue)
	{
		UpdateIsExpandable();
		UpdateAdvanceOptions();
		CommandManager.InvalidateRequerySuggested();
	}

	public virtual void InitProperties()
	{
		_isReadOnly = ComputeIsReadOnly();
		_category = ComputeCategory();
		_categoryValue = ComputeCategoryValue();
		_description = ComputeDescription();
		_displayName = ComputeDisplayName();
		_defaultValue = ComputeDefaultValueAttribute();
		_displayOrder = ComputeDisplayOrder(IsPropertyGridCategorized);
		_expandableAttribute = ComputeExpandableAttribute();
		_newItemTypes = ComputeNewItemTypes();
		_commandBindings = new CommandBinding[1]
		{
			new CommandBinding(PropertyItemCommands.ResetValue, ExecuteResetValueCommand, CanExecuteResetValueCommand)
		};
		UpdateIsExpandable();
		UpdateAdvanceOptions();
		BindingBase binding = CreateValueBinding();
		BindingOperations.SetBinding(this, ValueProperty, binding);
	}
}
