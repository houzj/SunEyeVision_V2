using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Input;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Commands;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

[TemplatePart(Name = "PART_DragThumb", Type = typeof(Thumb))]
[TemplatePart(Name = "PART_PropertyItemsControl", Type = typeof(PropertyItemsControl))]
[StyleTypedProperty(Property = "PropertyContainerStyle", StyleTargetType = typeof(PropertyItemBase))]
public class PropertyGrid : Control, ISupportInitialize, IPropertyContainer, INotifyPropertyChanged
{
	private const string PART_DragThumb = "PART_DragThumb";

	internal const string PART_PropertyItemsControl = "PART_PropertyItemsControl";

	private static readonly ComponentResourceKey SelectedObjectAdvancedOptionsMenuKey;

	private Thumb _dragThumb;

	private bool _hasPendingSelectedObjectChanged;

	private int _initializationCount;

	private ContainerHelperBase _containerHelper;

	private PropertyDefinitionCollection _propertyDefinitions;

	private EditorDefinitionCollection _editorDefinitions;

	private WeakEventListener<NotifyCollectionChangedEventArgs> _propertyDefinitionsListener;

	private WeakEventListener<NotifyCollectionChangedEventArgs> _editorDefinitionsListener;

	public static readonly DependencyProperty AdvancedOptionsMenuProperty;

	public static readonly DependencyProperty AutoGeneratePropertiesProperty;

	public static readonly DependencyProperty ShowDescriptionByTooltipProperty;

	public static readonly DependencyProperty ShowSummaryProperty;

	public static readonly DependencyProperty FilterProperty;

	public static readonly DependencyProperty FilterWatermarkProperty;

	public static readonly DependencyProperty HideInheritedPropertiesProperty;

	public static readonly DependencyProperty IsCategorizedProperty;

	public static readonly DependencyProperty IsMiscCategoryLabelHiddenProperty;

	public static readonly DependencyProperty IsVirtualizingProperty;

	public static readonly DependencyProperty NameColumnWidthProperty;

	public static readonly DependencyProperty PropertyContainerStyleProperty;

	public static readonly DependencyProperty IsReadOnlyProperty;

	public static readonly DependencyProperty SelectedObjectProperty;

	public static readonly DependencyProperty SelectedObjectTypeProperty;

	public static readonly DependencyProperty SelectedObjectTypeNameProperty;

	public static readonly DependencyProperty SelectedObjectNameProperty;

	private static readonly DependencyPropertyKey SelectedPropertyItemPropertyKey;

	public static readonly DependencyProperty SelectedPropertyItemProperty;

	public static readonly DependencyProperty SelectedPropertyProperty;

	public static readonly DependencyProperty ShowAdvancedOptionsProperty;

	public static readonly DependencyProperty ShowPreviewProperty;

	public static readonly DependencyProperty ShowSearchBoxProperty;

	public static readonly DependencyProperty ShowSortOptionsProperty;

	public static readonly DependencyProperty ShowTitleProperty;

	public static readonly DependencyProperty UpdateTextBoxSourceOnEnterKeyProperty;

	public static readonly RoutedEvent PropertyValueChangedEvent;

	public static readonly RoutedEvent SelectedPropertyItemChangedEvent;

	public static readonly RoutedEvent SelectedObjectChangedEvent;

	public static readonly RoutedEvent PreparePropertyItemEvent;

	public static readonly RoutedEvent ClearPropertyItemEvent;

	public ContextMenu AdvancedOptionsMenu
	{
		get
		{
			return (ContextMenu)GetValue(AdvancedOptionsMenuProperty);
		}
		set
		{
			SetValue(AdvancedOptionsMenuProperty, value);
		}
	}

	public bool AutoGenerateProperties
	{
		get
		{
			return (bool)GetValue(AutoGeneratePropertiesProperty);
		}
		set
		{
			SetValue(AutoGeneratePropertiesProperty, value);
		}
	}

	public bool ShowDescriptionByTooltip
	{
		get
		{
			return (bool)GetValue(ShowDescriptionByTooltipProperty);
		}
		set
		{
			SetValue(ShowDescriptionByTooltipProperty, value);
		}
	}

	public bool ShowSummary
	{
		get
		{
			return (bool)GetValue(ShowSummaryProperty);
		}
		set
		{
			SetValue(ShowSummaryProperty, value);
		}
	}

	public EditorDefinitionCollection EditorDefinitions
	{
		get
		{
			return _editorDefinitions;
		}
		set
		{
			if (_editorDefinitions != value)
			{
				EditorDefinitionCollection editorDefinitions = _editorDefinitions;
				_editorDefinitions = value;
				OnEditorDefinitionsChanged(editorDefinitions, value);
			}
		}
	}

	public string Filter
	{
		get
		{
			return (string)GetValue(FilterProperty);
		}
		set
		{
			SetValue(FilterProperty, value);
		}
	}

	public string FilterWatermark
	{
		get
		{
			return (string)GetValue(FilterWatermarkProperty);
		}
		set
		{
			SetValue(FilterWatermarkProperty, value);
		}
	}

	public bool HideInheritedProperties
	{
		get
		{
			return (bool)GetValue(HideInheritedPropertiesProperty);
		}
		set
		{
			SetValue(HideInheritedPropertiesProperty, value);
		}
	}

	public bool IsCategorized
	{
		get
		{
			return (bool)GetValue(IsCategorizedProperty);
		}
		set
		{
			SetValue(IsCategorizedProperty, value);
		}
	}

	public bool IsMiscCategoryLabelHidden
	{
		get
		{
			return (bool)GetValue(IsMiscCategoryLabelHiddenProperty);
		}
		set
		{
			SetValue(IsMiscCategoryLabelHiddenProperty, value);
		}
	}

	public bool IsVirtualizing
	{
		get
		{
			return (bool)GetValue(IsVirtualizingProperty);
		}
		set
		{
			SetValue(IsVirtualizingProperty, value);
		}
	}

	public double NameColumnWidth
	{
		get
		{
			return (double)GetValue(NameColumnWidthProperty);
		}
		set
		{
			SetValue(NameColumnWidthProperty, value);
		}
	}

	public IList Properties
	{
		get
		{
			if (_containerHelper == null)
			{
				return null;
			}
			return _containerHelper.Properties;
		}
	}

	public Style PropertyContainerStyle
	{
		get
		{
			return (Style)GetValue(PropertyContainerStyleProperty);
		}
		set
		{
			SetValue(PropertyContainerStyleProperty, value);
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
			if (_propertyDefinitions != value)
			{
				PropertyDefinitionCollection propertyDefinitions = _propertyDefinitions;
				_propertyDefinitions = value;
				OnPropertyDefinitionsChanged(propertyDefinitions, value);
			}
		}
	}

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

	public object SelectedObject
	{
		get
		{
			return GetValue(SelectedObjectProperty);
		}
		set
		{
			SetValue(SelectedObjectProperty, value);
		}
	}

	public Type SelectedObjectType
	{
		get
		{
			return (Type)GetValue(SelectedObjectTypeProperty);
		}
		set
		{
			SetValue(SelectedObjectTypeProperty, value);
		}
	}

	public string SelectedObjectTypeName
	{
		get
		{
			return (string)GetValue(SelectedObjectTypeNameProperty);
		}
		set
		{
			SetValue(SelectedObjectTypeNameProperty, value);
		}
	}

	public string SelectedObjectName
	{
		get
		{
			return (string)GetValue(SelectedObjectNameProperty);
		}
		set
		{
			SetValue(SelectedObjectNameProperty, value);
		}
	}

	public PropertyItemBase SelectedPropertyItem
	{
		get
		{
			return (PropertyItemBase)GetValue(SelectedPropertyItemProperty);
		}
		internal set
		{
			SetValue(SelectedPropertyItemPropertyKey, value);
		}
	}

	public object SelectedProperty
	{
		get
		{
			return GetValue(SelectedPropertyProperty);
		}
		set
		{
			SetValue(SelectedPropertyProperty, value);
		}
	}

	public bool ShowAdvancedOptions
	{
		get
		{
			return (bool)GetValue(ShowAdvancedOptionsProperty);
		}
		set
		{
			SetValue(ShowAdvancedOptionsProperty, value);
		}
	}

	public bool ShowPreview
	{
		get
		{
			return (bool)GetValue(ShowPreviewProperty);
		}
		set
		{
			SetValue(ShowPreviewProperty, value);
		}
	}

	public bool ShowSearchBox
	{
		get
		{
			return (bool)GetValue(ShowSearchBoxProperty);
		}
		set
		{
			SetValue(ShowSearchBoxProperty, value);
		}
	}

	public bool ShowSortOptions
	{
		get
		{
			return (bool)GetValue(ShowSortOptionsProperty);
		}
		set
		{
			SetValue(ShowSortOptionsProperty, value);
		}
	}

	public bool ShowTitle
	{
		get
		{
			return (bool)GetValue(ShowTitleProperty);
		}
		set
		{
			SetValue(ShowTitleProperty, value);
		}
	}

	public bool UpdateTextBoxSourceOnEnterKey
	{
		get
		{
			return (bool)GetValue(UpdateTextBoxSourceOnEnterKeyProperty);
		}
		set
		{
			SetValue(UpdateTextBoxSourceOnEnterKeyProperty, value);
		}
	}

	FilterInfo IPropertyContainer.FilterInfo => new FilterInfo
	{
		Predicate = CreateFilter(Filter),
		InputString = Filter
	};

	ContainerHelperBase IPropertyContainer.ContainerHelper => _containerHelper;

	bool IPropertyContainer.IsSortedAlphabetically => true;

	public event PropertyChangedEventHandler PropertyChanged;

	public event PropertyValueChangedEventHandler PropertyValueChanged
	{
		add
		{
			AddHandler(PropertyValueChangedEvent, value);
		}
		remove
		{
			RemoveHandler(PropertyValueChangedEvent, value);
		}
	}

	public event RoutedPropertyChangedEventHandler<PropertyItemBase> SelectedPropertyItemChanged
	{
		add
		{
			AddHandler(SelectedPropertyItemChangedEvent, value);
		}
		remove
		{
			RemoveHandler(SelectedPropertyItemChangedEvent, value);
		}
	}

	public event RoutedPropertyChangedEventHandler<object> SelectedObjectChanged
	{
		add
		{
			AddHandler(SelectedObjectChangedEvent, value);
		}
		remove
		{
			RemoveHandler(SelectedObjectChangedEvent, value);
		}
	}

	public event IsPropertyBrowsableHandler IsPropertyBrowsable;

	public event PropertyItemEventHandler PreparePropertyItem
	{
		add
		{
			AddHandler(PreparePropertyItemEvent, value);
		}
		remove
		{
			RemoveHandler(PreparePropertyItemEvent, value);
		}
	}

	public event PropertyItemEventHandler ClearPropertyItem
	{
		add
		{
			AddHandler(ClearPropertyItemEvent, value);
		}
		remove
		{
			RemoveHandler(ClearPropertyItemEvent, value);
		}
	}

	protected virtual void OnEditorDefinitionsChanged(EditorDefinitionCollection oldValue, EditorDefinitionCollection newValue)
	{
		if (oldValue != null)
		{
			CollectionChangedEventManager.RemoveListener(oldValue, _editorDefinitionsListener);
		}
		if (newValue != null)
		{
			CollectionChangedEventManager.AddListener(newValue, _editorDefinitionsListener);
		}
		this.Notify(PropertyChanged, () => EditorDefinitions);
	}

	private void OnEditorDefinitionsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (_containerHelper != null)
		{
			_containerHelper.NotifyEditorDefinitionsCollectionChanged();
		}
	}

	private static void OnFilterChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PropertyGrid propertyGrid)
		{
			propertyGrid.OnFilterChanged((string)e.OldValue, (string)e.NewValue);
		}
	}

	protected virtual void OnFilterChanged(string oldValue, string newValue)
	{
		this.Notify(PropertyChanged, () => ((IPropertyContainer)this).FilterInfo);
	}

	private static void OnIsCategorizedChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PropertyGrid propertyGrid)
		{
			propertyGrid.OnIsCategorizedChanged((bool)e.OldValue, (bool)e.NewValue);
		}
	}

	protected virtual void OnIsCategorizedChanged(bool oldValue, bool newValue)
	{
		UpdateThumb();
	}

	private static void OnNameColumnWidthChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PropertyGrid propertyGrid)
		{
			propertyGrid.OnNameColumnWidthChanged((double)e.OldValue, (double)e.NewValue);
		}
	}

	protected virtual void OnNameColumnWidthChanged(double oldValue, double newValue)
	{
		if (_dragThumb != null)
		{
			((TranslateTransform)_dragThumb.RenderTransform).X = newValue;
		}
	}

	private static void OnPropertyContainerStyleChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PropertyGrid propertyGrid)
		{
			propertyGrid.OnPropertyContainerStyleChanged((Style)e.OldValue, (Style)e.NewValue);
		}
	}

	protected virtual void OnPropertyContainerStyleChanged(Style oldValue, Style newValue)
	{
	}

	protected virtual void OnPropertyDefinitionsChanged(PropertyDefinitionCollection oldValue, PropertyDefinitionCollection newValue)
	{
		if (oldValue != null)
		{
			CollectionChangedEventManager.RemoveListener(oldValue, _propertyDefinitionsListener);
		}
		if (newValue != null)
		{
			CollectionChangedEventManager.AddListener(newValue, _propertyDefinitionsListener);
		}
		this.Notify(PropertyChanged, () => PropertyDefinitions);
	}

	private void OnPropertyDefinitionsCollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
	{
		if (_containerHelper != null)
		{
			_containerHelper.NotifyPropertyDefinitionsCollectionChanged();
		}
		if (IsLoaded)
		{
			UpdateContainerHelper();
		}
	}

	private static void OnSelectedObjectChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PropertyGrid propertyGrid)
		{
			propertyGrid.OnSelectedObjectChanged(e.OldValue, e.NewValue);
		}
	}

	protected virtual void OnSelectedObjectChanged(object oldValue, object newValue)
	{
		if (_initializationCount != 0)
		{
			_hasPendingSelectedObjectChanged = true;
			return;
		}
		UpdateContainerHelper();
		RaiseEvent(new RoutedPropertyChangedEventArgs<object>(oldValue, newValue, SelectedObjectChangedEvent));
	}

	private static void OnSelectedObjectTypeChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PropertyGrid propertyGrid)
		{
			propertyGrid.OnSelectedObjectTypeChanged((Type)e.OldValue, (Type)e.NewValue);
		}
	}

	protected virtual void OnSelectedObjectTypeChanged(Type oldValue, Type newValue)
	{
	}

	private static object OnCoerceSelectedObjectName(DependencyObject o, object baseValue)
	{
		if (o is PropertyGrid propertyGrid && propertyGrid.SelectedObject is FrameworkElement && string.IsNullOrEmpty((string)baseValue))
		{
			return "<no name>";
		}
		return baseValue;
	}

	private static void OnSelectedObjectNameChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PropertyGrid propertyGrid)
		{
			propertyGrid.SelectedObjectNameChanged((string)e.OldValue, (string)e.NewValue);
		}
	}

	protected virtual void SelectedObjectNameChanged(string oldValue, string newValue)
	{
	}

	private static void OnSelectedPropertyItemChanged(DependencyObject o, DependencyPropertyChangedEventArgs e)
	{
		if (o is PropertyGrid propertyGrid)
		{
			propertyGrid.OnSelectedPropertyItemChanged((PropertyItemBase)e.OldValue, (PropertyItemBase)e.NewValue);
		}
	}

	protected virtual void OnSelectedPropertyItemChanged(PropertyItemBase oldValue, PropertyItemBase newValue)
	{
		if (oldValue != null)
		{
			oldValue.IsSelected = false;
		}
		if (newValue != null)
		{
			newValue.IsSelected = true;
		}
		SelectedProperty = ((newValue != null && _containerHelper != null) ? _containerHelper.ItemFromContainer(newValue) : null);
		RaiseEvent(new RoutedPropertyChangedEventArgs<PropertyItemBase>(oldValue, newValue, SelectedPropertyItemChangedEvent));
	}

	private static void OnSelectedPropertyChanged(DependencyObject sender, DependencyPropertyChangedEventArgs args)
	{
		if (sender is PropertyGrid propertyGrid)
		{
			propertyGrid.OnSelectedPropertyChanged(args.OldValue, args.NewValue);
		}
	}

	private void OnSelectedPropertyChanged(object oldValue, object newValue)
	{
		if (_containerHelper != null && !object.Equals(_containerHelper.ItemFromContainer(SelectedPropertyItem), newValue))
		{
			SelectedPropertyItem = _containerHelper.ContainerFromItem(newValue);
		}
	}

	static PropertyGrid()
	{
		SelectedObjectAdvancedOptionsMenuKey = new ComponentResourceKey(typeof(PropertyGrid), "SelectedObjectAdvancedOptionsMenu");
		AdvancedOptionsMenuProperty = DependencyProperty.Register("AdvancedOptionsMenu", typeof(ContextMenu), typeof(PropertyGrid), new UIPropertyMetadata(null));
		AutoGeneratePropertiesProperty = DependencyProperty.Register("AutoGenerateProperties", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(true));
		ShowDescriptionByTooltipProperty = DependencyProperty.Register("ShowDescriptionByTooltip", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(false));
		ShowSummaryProperty = DependencyProperty.Register("ShowSummary", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(true));
		FilterProperty = DependencyProperty.Register("Filter", typeof(string), typeof(PropertyGrid), new UIPropertyMetadata(null, OnFilterChanged));
		FilterWatermarkProperty = DependencyProperty.Register("FilterWatermark", typeof(string), typeof(PropertyGrid), new UIPropertyMetadata("Search"));
		HideInheritedPropertiesProperty = DependencyProperty.Register("HideInheritedProperties", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(false));
		IsCategorizedProperty = DependencyProperty.Register("IsCategorized", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(true, OnIsCategorizedChanged));
		IsMiscCategoryLabelHiddenProperty = DependencyProperty.Register("IsMiscCategoryLabelHidden", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(false));
		IsVirtualizingProperty = DependencyProperty.Register("IsVirtualizing", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(false));
		NameColumnWidthProperty = DependencyProperty.Register("NameColumnWidth", typeof(double), typeof(PropertyGrid), new UIPropertyMetadata(150.0, OnNameColumnWidthChanged));
		PropertyContainerStyleProperty = DependencyProperty.Register("PropertyContainerStyle", typeof(Style), typeof(PropertyGrid), new UIPropertyMetadata(null, OnPropertyContainerStyleChanged));
		IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(false));
		SelectedObjectProperty = DependencyProperty.Register("SelectedObject", typeof(object), typeof(PropertyGrid), new UIPropertyMetadata(null, OnSelectedObjectChanged));
		SelectedObjectTypeProperty = DependencyProperty.Register("SelectedObjectType", typeof(Type), typeof(PropertyGrid), new UIPropertyMetadata(null, OnSelectedObjectTypeChanged));
		SelectedObjectTypeNameProperty = DependencyProperty.Register("SelectedObjectTypeName", typeof(string), typeof(PropertyGrid), new UIPropertyMetadata(string.Empty));
		SelectedObjectNameProperty = DependencyProperty.Register("SelectedObjectName", typeof(string), typeof(PropertyGrid), new UIPropertyMetadata(string.Empty, OnSelectedObjectNameChanged, OnCoerceSelectedObjectName));
		SelectedPropertyItemPropertyKey = DependencyProperty.RegisterReadOnly("SelectedPropertyItem", typeof(PropertyItemBase), typeof(PropertyGrid), new UIPropertyMetadata(null, OnSelectedPropertyItemChanged));
		SelectedPropertyItemProperty = SelectedPropertyItemPropertyKey.DependencyProperty;
		SelectedPropertyProperty = DependencyProperty.Register("SelectedProperty", typeof(object), typeof(PropertyGrid), new UIPropertyMetadata(null, OnSelectedPropertyChanged));
		ShowAdvancedOptionsProperty = DependencyProperty.Register("ShowAdvancedOptions", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(false));
		ShowPreviewProperty = DependencyProperty.Register("ShowPreview", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(false));
		ShowSearchBoxProperty = DependencyProperty.Register("ShowSearchBox", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(true));
		ShowSortOptionsProperty = DependencyProperty.Register("ShowSortOptions", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(true));
		ShowTitleProperty = DependencyProperty.Register("ShowTitle", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(true));
		UpdateTextBoxSourceOnEnterKeyProperty = DependencyProperty.Register("UpdateTextBoxSourceOnEnterKey", typeof(bool), typeof(PropertyGrid), new UIPropertyMetadata(true));
		PropertyValueChangedEvent = EventManager.RegisterRoutedEvent("PropertyValueChanged", RoutingStrategy.Bubble, typeof(PropertyValueChangedEventHandler), typeof(PropertyGrid));
		SelectedPropertyItemChangedEvent = EventManager.RegisterRoutedEvent("SelectedPropertyItemChanged", RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<PropertyItemBase>), typeof(PropertyGrid));
		SelectedObjectChangedEvent = EventManager.RegisterRoutedEvent("SelectedObjectChanged", RoutingStrategy.Bubble, typeof(RoutedPropertyChangedEventHandler<object>), typeof(PropertyGrid));
		PreparePropertyItemEvent = EventManager.RegisterRoutedEvent("PreparePropertyItem", RoutingStrategy.Bubble, typeof(PropertyItemEventHandler), typeof(PropertyGrid));
		ClearPropertyItemEvent = EventManager.RegisterRoutedEvent("ClearPropertyItem", RoutingStrategy.Bubble, typeof(PropertyItemEventHandler), typeof(PropertyGrid));
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGrid), new FrameworkPropertyMetadata(typeof(PropertyGrid)));
	}

	public PropertyGrid()
	{
		_propertyDefinitionsListener = new WeakEventListener<NotifyCollectionChangedEventArgs>(OnPropertyDefinitionsCollectionChanged);
		_editorDefinitionsListener = new WeakEventListener<NotifyCollectionChangedEventArgs>(OnEditorDefinitionsCollectionChanged);
		UpdateContainerHelper();
		EditorDefinitions = new EditorDefinitionCollection();
		PropertyDefinitions = new PropertyDefinitionCollection();
		PropertyValueChanged += PropertyGrid_PropertyValueChanged;
		AddHandler(PropertyItemBase.ItemSelectionChangedEvent, new RoutedEventHandler(OnItemSelectionChanged));
		AddHandler(PropertyItemsControl.PreparePropertyItemEvent, new PropertyItemEventHandler(OnPreparePropertyItemInternal));
		AddHandler(PropertyItemsControl.ClearPropertyItemEvent, new PropertyItemEventHandler(OnClearPropertyItemInternal));
		CommandBindings.Add(new CommandBinding(PropertyGridCommands.ClearFilter, ClearFilter, CanClearFilter));
	}

	public override void OnApplyTemplate()
	{
		base.OnApplyTemplate();
		if (_dragThumb != null)
		{
			_dragThumb.DragDelta -= DragThumb_DragDelta;
		}
		_dragThumb = GetTemplateChild("PART_DragThumb") as Thumb;
		if (_dragThumb != null)
		{
			_dragThumb.DragDelta += DragThumb_DragDelta;
		}
		if (_containerHelper != null)
		{
			_containerHelper.ChildrenItemsControl = GetTemplateChild("PART_PropertyItemsControl") as PropertyItemsControl;
		}
		TranslateTransform translateTransform = new TranslateTransform();
		translateTransform.X = NameColumnWidth;
		if (_dragThumb != null)
		{
			_dragThumb.RenderTransform = translateTransform;
		}
		UpdateThumb();
	}

	protected override void OnPreviewKeyDown(KeyEventArgs e)
	{
		TextBox textBox = e.OriginalSource as TextBox;
		if (SelectedPropertyItem != null && e.Key == Key.Return && UpdateTextBoxSourceOnEnterKey && textBox != null && !textBox.AcceptsReturn)
		{
			textBox.GetBindingExpression(TextBox.TextProperty)?.UpdateSource();
		}
	}

	protected override void OnPropertyChanged(DependencyPropertyChangedEventArgs e)
	{
		base.OnPropertyChanged(e);
		if (ReflectionHelper.IsPublicInstanceProperty(GetType(), e.Property.Name))
		{
			this.Notify(PropertyChanged, e.Property.Name);
		}
	}

	private void OnItemSelectionChanged(object sender, RoutedEventArgs args)
	{
		PropertyItemBase propertyItemBase = (PropertyItemBase)args.OriginalSource;
		if (propertyItemBase.IsSelected)
		{
			SelectedPropertyItem = propertyItemBase;
		}
		else if (propertyItemBase == SelectedPropertyItem)
		{
			SelectedPropertyItem = null;
		}
	}

	private void OnPreparePropertyItemInternal(object sender, PropertyItemEventArgs args)
	{
		if (_containerHelper != null)
		{
			_containerHelper.PrepareChildrenPropertyItem(args.PropertyItem, args.Item);
		}
		args.Handled = true;
	}

	private void OnClearPropertyItemInternal(object sender, PropertyItemEventArgs args)
	{
		if (_containerHelper != null)
		{
			_containerHelper.ClearChildrenPropertyItem(args.PropertyItem, args.Item);
		}
		args.Handled = true;
	}

	private void DragThumb_DragDelta(object sender, DragDeltaEventArgs e)
	{
		NameColumnWidth = Math.Max(0.0, NameColumnWidth + e.HorizontalChange);
	}

	private void PropertyGrid_PropertyValueChanged(object sender, PropertyValueChangedEventArgs e)
	{
		if (e.OriginalSource is PropertyItem propertyItem)
		{
			if (propertyItem.WillRefreshPropertyGrid)
			{
				UpdateContainerHelper();
			}
			if (propertyItem.ParentNode is PropertyItem { IsExpandable: not false } propertyItem2)
			{
				RebuildEditor(propertyItem2);
			}
		}
	}

	private void ClearFilter(object sender, ExecutedRoutedEventArgs e)
	{
		Filter = string.Empty;
	}

	private void CanClearFilter(object sender, CanExecuteRoutedEventArgs e)
	{
		e.CanExecute = !string.IsNullOrEmpty(Filter);
	}

	public double GetScrollPosition()
	{
		return GetScrollViewer()?.VerticalOffset ?? 0.0;
	}

	public void ScrollToPosition(double position)
	{
		GetScrollViewer()?.ScrollToVerticalOffset(position);
	}

	public void ScrollToTop()
	{
		GetScrollViewer()?.ScrollToTop();
	}

	public void ScrollToBottom()
	{
		GetScrollViewer()?.ScrollToBottom();
	}

	public void CollapseAllProperties()
	{
		if (_containerHelper != null)
		{
			_containerHelper.SetPropertiesExpansion(isExpanded: false);
		}
	}

	public void ExpandAllProperties()
	{
		if (_containerHelper != null)
		{
			_containerHelper.SetPropertiesExpansion(isExpanded: true);
		}
	}

	public void ExpandProperty(string propertyName)
	{
		if (_containerHelper != null)
		{
			_containerHelper.SetPropertiesExpansion(propertyName, isExpanded: true);
		}
	}

	public void CollapseProperty(string propertyName)
	{
		if (_containerHelper != null)
		{
			_containerHelper.SetPropertiesExpansion(propertyName, isExpanded: false);
		}
	}

	private ScrollViewer GetScrollViewer()
	{
		if (_containerHelper != null && _containerHelper.ChildrenItemsControl != null)
		{
			return TreeHelper.FindChild<ScrollViewer>(_containerHelper.ChildrenItemsControl);
		}
		return null;
	}

	private void RebuildEditor(PropertyItem propertyItem)
	{
		FrameworkElement frameworkElement = (propertyItem.ContainerHelper as ObjectContainerHelperBase).GenerateChildrenEditorElement(propertyItem);
		if (frameworkElement != null)
		{
			ContainerHelperBase.SetIsGenerated(frameworkElement, value: true);
			propertyItem.Editor = frameworkElement;
			BindingExpression bindingExpression = propertyItem.GetBindingExpression(CustomPropertyItem.ValueProperty);
			if (bindingExpression != null)
			{
				bindingExpression.UpdateSource();
				propertyItem.SetRedInvalidBorder(bindingExpression);
			}
		}
	}

	private void UpdateContainerHelper()
	{
		ItemsControl childrenItemsControl = null;
		if (_containerHelper != null)
		{
			childrenItemsControl = _containerHelper.ChildrenItemsControl;
			_containerHelper.ClearHelper();
			if (_containerHelper is ObjectContainerHelperBase)
			{
				AdvancedOptionsMenu = null;
			}
		}
		if (SelectedObject != null)
		{
			_containerHelper = new ObjectContainerHelper(this, SelectedObject);
			((ObjectContainerHelper)_containerHelper).GenerateProperties();
		}
		if (_containerHelper != null)
		{
			_containerHelper.ChildrenItemsControl = childrenItemsControl;
		}
		ScrollToTop();
		this.Notify(PropertyChanged, () => Properties);
	}

	private void UpdateThumb()
	{
		if (_dragThumb != null)
		{
			if (IsCategorized)
			{
				_dragThumb.Margin = new Thickness(6.0, 0.0, 0.0, 0.0);
			}
			else
			{
				_dragThumb.Margin = new Thickness(-1.0, 0.0, 0.0, 0.0);
			}
		}
	}

	protected virtual Predicate<object> CreateFilter(string filter)
	{
		return null;
	}

	public void Update()
	{
		if (_containerHelper != null)
		{
			_containerHelper.UpdateValuesFromSource();
		}
	}

	public static void AddPreparePropertyItemHandler(UIElement element, PropertyItemEventHandler handler)
	{
		element.AddHandler(PreparePropertyItemEvent, handler);
	}

	public static void RemovePreparePropertyItemHandler(UIElement element, PropertyItemEventHandler handler)
	{
		element.RemoveHandler(PreparePropertyItemEvent, handler);
	}

	internal static void RaisePreparePropertyItemEvent(UIElement source, PropertyItemBase propertyItem, object item)
	{
		source.RaiseEvent(new PropertyItemEventArgs(PreparePropertyItemEvent, source, propertyItem, item));
	}

	public static void AddClearPropertyItemHandler(UIElement element, PropertyItemEventHandler handler)
	{
		element.AddHandler(ClearPropertyItemEvent, handler);
	}

	public static void RemoveClearPropertyItemHandler(UIElement element, PropertyItemEventHandler handler)
	{
		element.RemoveHandler(ClearPropertyItemEvent, handler);
	}

	internal static void RaiseClearPropertyItemEvent(UIElement source, PropertyItemBase propertyItem, object item)
	{
		source.RaiseEvent(new PropertyItemEventArgs(ClearPropertyItemEvent, source, propertyItem, item));
	}

	public override void BeginInit()
	{
		base.BeginInit();
		_initializationCount++;
	}

	public override void EndInit()
	{
		base.EndInit();
		if (--_initializationCount == 0)
		{
			if (_hasPendingSelectedObjectChanged)
			{
				UpdateContainerHelper();
				_hasPendingSelectedObjectChanged = false;
			}
			if (_containerHelper != null)
			{
				_containerHelper.OnEndInit();
			}
		}
	}

	bool? IPropertyContainer.IsPropertyVisible(PropertyDescriptor pd)
	{
		IsPropertyBrowsableHandler isPropertyBrowsableHandler = IsPropertyBrowsable;
		if (isPropertyBrowsableHandler != null)
		{
			IsPropertyBrowsableArgs isPropertyBrowsableArgs = new IsPropertyBrowsableArgs(pd);
			isPropertyBrowsableHandler(this, isPropertyBrowsableArgs);
			return isPropertyBrowsableArgs.IsBrowsable;
		}
		return null;
	}
}
