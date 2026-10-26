using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.ComponentModel;
using System.Linq.Expressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

[TemplatePart(Name = "PART_PropertyItemsControl", Type = typeof(PropertyItemsControl))]
[TemplatePart(Name = "PART_ValueContainer", Type = typeof(ContentControl))]
public abstract class PropertyItemBase : Control, IPropertyContainer, INotifyPropertyChanged
{
	internal const string PART_ValueContainer = "PART_ValueContainer";

	private ContentControl _valueContainer;

	private ContainerHelperBase _containerHelper;

	private IPropertyContainer _parentNode;

	internal bool _isPropertyGridCategorized;

	internal bool _isSortedAlphabetically = true;

	public static readonly DependencyProperty AdvancedOptionsIconProperty;

	public static readonly DependencyProperty AdvancedOptionsTooltipProperty;

	public static readonly DependencyProperty DescriptionProperty;

	public static readonly DependencyProperty DisplayNameProperty;

	public static readonly DependencyProperty EditorProperty;

	public static readonly DependencyProperty IsExpandedProperty;

	public static readonly DependencyProperty IsExpandableProperty;

	public static readonly DependencyProperty IsSelectedProperty;

	public static readonly DependencyProperty WillRefreshPropertyGridProperty;

	internal static readonly RoutedEvent ItemSelectionChangedEvent;

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

	public string Description
	{
		get
		{
			return (string)GetValue(DescriptionProperty);
		}
		set
		{
			SetValue(DescriptionProperty, value);
		}
	}

	public string DisplayName
	{
		get
		{
			return (string)GetValue(DisplayNameProperty);
		}
		set
		{
			SetValue(DisplayNameProperty, value);
		}
	}

	public FrameworkElement Editor
	{
		get
		{
			return (FrameworkElement)GetValue(EditorProperty);
		}
		set
		{
			SetValue(EditorProperty, value);
		}
	}

	public bool IsExpanded
	{
		get
		{
			return (bool)GetValue(IsExpandedProperty);
		}
		set
		{
			SetValue(IsExpandedProperty, value);
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

	public bool IsSelected
	{
		get
		{
			return (bool)GetValue(IsSelectedProperty);
		}
		set
		{
			SetValue(IsSelectedProperty, value);
		}
	}

	public FrameworkElement ParentElement => ParentNode as FrameworkElement;

	internal IPropertyContainer ParentNode
	{
		get
		{
			return _parentNode;
		}
		set
		{
			_parentNode = value;
		}
	}

	internal ContentControl ValueContainer => _valueContainer;

	public int Level { get; internal set; }

	public IList Properties
	{
		get
		{
			if (_containerHelper == null)
			{
				_containerHelper = new ObjectContainerHelper(this, null);
			}
			return _containerHelper.Properties;
		}
	}

	public Style PropertyContainerStyle
	{
		get
		{
			if (ParentNode == null)
			{
				return null;
			}
			return ParentNode.PropertyContainerStyle;
		}
	}

	internal ContainerHelperBase ContainerHelper
	{
		get
		{
			return _containerHelper;
		}
		set
		{
			if (value == null)
			{
				throw new ArgumentNullException("value");
			}
			_containerHelper = value;
			RaisePropertyChanged(() => Properties);
		}
	}

	public bool WillRefreshPropertyGrid
	{
		get
		{
			return (bool)GetValue(WillRefreshPropertyGridProperty);
		}
		set
		{
			SetValue(WillRefreshPropertyGridProperty, value);
		}
	}

	Style IPropertyContainer.PropertyContainerStyle => PropertyContainerStyle;

	EditorDefinitionCollection IPropertyContainer.EditorDefinitions
	{
		get
		{
			if (ParentNode == null)
			{
				return null;
			}
			return ParentNode.EditorDefinitions;
		}
	}

	PropertyDefinitionCollection IPropertyContainer.PropertyDefinitions => GetPropertItemPropertyDefinitions();

	ContainerHelperBase IPropertyContainer.ContainerHelper => ContainerHelper;

	bool IPropertyContainer.IsCategorized => _isPropertyGridCategorized;

	bool IPropertyContainer.IsSortedAlphabetically => _isSortedAlphabetically;

	bool IPropertyContainer.AutoGenerateProperties
	{
		get
		{
			if (ParentNode != null)
			{
				PropertyDefinitionCollection propertItemPropertyDefinitions = GetPropertItemPropertyDefinitions();
				if (propertItemPropertyDefinitions == null || propertItemPropertyDefinitions.Count == 0)
				{
					return true;
				}
				return ParentNode.AutoGenerateProperties;
			}
			return true;
		}
	}

	bool IPropertyContainer.HideInheritedProperties => false;

	FilterInfo IPropertyContainer.FilterInfo => default;

	public event PropertyChangedEventHandler PropertyChanged;

	private static void OnEditorChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PropertyItemBase propertyItemBase)
		{
			propertyItemBase.OnEditorChanged((FrameworkElement)e.OldValue, (FrameworkElement)e.NewValue);
		}
	}

	protected virtual void OnEditorChanged(FrameworkElement oldValue, FrameworkElement newValue)
	{
	}

	private static void OnIsExpandedChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PropertyItemBase propertyItemBase)
		{
			propertyItemBase.OnIsExpandedChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnIsExpandedChanged(bool oldValue, bool newValue)
	{
	}

	private static void OnIsSelectedChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PropertyItemBase propertyItemBase)
		{
			propertyItemBase.OnIsSelectedChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnIsSelectedChanged(bool oldValue, bool newValue)
	{
		RaiseItemSelectionChangedEvent();
	}

	private void RaiseItemSelectionChangedEvent()
	{
		RaiseEvent(new RoutedEventArgs(ItemSelectionChangedEvent));
	}

	internal void RaisePropertyChanged<TMember>(Expression<Func<TMember>> propertyExpression)
	{
		this.Notify(PropertyChanged, propertyExpression);
	}

	internal void RaisePropertyChanged(string name)
	{
		this.Notify(PropertyChanged, name);
	}

	static PropertyItemBase()
	{
		AdvancedOptionsIconProperty = DependencyProperty.Register("AdvancedOptionsIcon", typeof(ImageSource), typeof(PropertyItemBase), new UIPropertyMetadata(null));
		AdvancedOptionsTooltipProperty = DependencyProperty.Register("AdvancedOptionsTooltip", typeof(object), typeof(PropertyItemBase), new UIPropertyMetadata(null));
		DescriptionProperty = DependencyProperty.Register("Description", typeof(string), typeof(PropertyItemBase), new UIPropertyMetadata(null));
		DisplayNameProperty = DependencyProperty.Register("DisplayName", typeof(string), typeof(PropertyItemBase), new UIPropertyMetadata(null));
		EditorProperty = DependencyProperty.Register("Editor", typeof(FrameworkElement), typeof(PropertyItemBase), new UIPropertyMetadata(null, OnEditorChanged));
		IsExpandedProperty = DependencyProperty.Register("IsExpanded", typeof(bool), typeof(PropertyItemBase), new UIPropertyMetadata(false, OnIsExpandedChanged));
		IsExpandableProperty = DependencyProperty.Register("IsExpandable", typeof(bool), typeof(PropertyItemBase), new UIPropertyMetadata(false));
		IsSelectedProperty = DependencyProperty.Register("IsSelected", typeof(bool), typeof(PropertyItemBase), new UIPropertyMetadata(false, OnIsSelectedChanged));
		WillRefreshPropertyGridProperty = DependencyProperty.Register("WillRefreshPropertyGrid", typeof(bool), typeof(PropertyItemBase), new UIPropertyMetadata(false));
		ItemSelectionChangedEvent = EventManager.RegisterRoutedEvent("ItemSelectionChangedEvent", RoutingStrategy.Bubble, typeof(RoutedEventHandler), typeof(PropertyItemBase));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyItemBase), new FrameworkPropertyMetadata(typeof(PropertyItemBase)));
	}

	internal PropertyItemBase()
	{
		GotFocus += PropertyItemBase_GotFocus;
		RequestBringIntoView += PropertyItemBase_RequestBringIntoView;
		AddHandler(PropertyItemsControl.PreparePropertyItemEvent, new PropertyItemEventHandler(OnPreparePropertyItemInternal));
		AddHandler(PropertyItemsControl.ClearPropertyItemEvent, new PropertyItemEventHandler(OnClearPropertyItemInternal));
	}

	private void OnPreparePropertyItemInternal(object sender, PropertyItemEventArgs args)
	{
		args.PropertyItem.Level = Level + 1;
		_containerHelper.PrepareChildrenPropertyItem(args.PropertyItem, args.Item);
		args.Handled = true;
	}

	private void OnClearPropertyItemInternal(object sender, PropertyItemEventArgs args)
	{
		_containerHelper.ClearChildrenPropertyItem(args.PropertyItem, args.Item);
		args.PropertyItem.Level = 0;
		args.Handled = true;
	}

	private void PropertyItemBase_RequestBringIntoView(object sender, RequestBringIntoViewEventArgs e)
	{
		e.Handled = true;
	}

	protected virtual Type GetPropertyItemType()
	{
		return null;
	}

	protected virtual string GetPropertyItemName()
	{
		return DisplayName;
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		_containerHelper.ChildrenItemsControl = GetTemplateChild("PART_PropertyItemsControl") as PropertyItemsControl;
		_valueContainer = GetTemplateChild("PART_ValueContainer") as ContentControl;
	}

	protected override void OnMouseDown(MouseButtonEventArgs e)
	{
		IsSelected = true;
		if (!IsKeyboardFocusWithin)
		{
			Focus();
		}
		e.Handled = true;
	}

	private void PropertyItemBase_GotFocus(object sender, RoutedEventArgs e)
	{
		IsSelected = true;
		e.Handled = true;
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (ReflectionHelper.IsPublicInstanceProperty(GetType(), e.Property.Name) && IsLoaded && _parentNode != null && !_parentNode.ContainerHelper.IsCleaning)
		{
			RaisePropertyChanged(e.Property.Name);
		}
	}

	private PropertyDefinitionCollection GetPropertItemPropertyDefinitions()
	{
		if (ParentNode != null && ParentNode.PropertyDefinitions != null)
		{
			string propertyItemName = GetPropertyItemName();
			foreach (PropertyDefinition propertyDefinition in ParentNode.PropertyDefinitions)
			{
				if (propertyDefinition.TargetProperties.Contains(propertyItemName))
				{
					return propertyDefinition.PropertyDefinitions;
				}
				Type propertyItemType = GetPropertyItemType();
				if (!(propertyItemType != null))
				{
					continue;
				}
				foreach (object targetProperty in propertyDefinition.TargetProperties)
				{
					Type type = targetProperty as Type;
					if (type != null && type.IsAssignableFrom(propertyItemType))
					{
						return propertyDefinition.PropertyDefinitions;
					}
				}
			}
		}
		return null;
	}

	bool? IPropertyContainer.IsPropertyVisible(PropertyDescriptor pd)
	{
		if (_parentNode != null)
		{
			return _parentNode.IsPropertyVisible(pd);
		}
		return null;
	}
}
