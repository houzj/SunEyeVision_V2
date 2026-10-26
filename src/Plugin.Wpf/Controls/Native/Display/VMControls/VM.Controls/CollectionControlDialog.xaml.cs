using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.CodeDom.Compiler;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Reflection;
using System.Runtime.Serialization;
using System.Security;
using System.Windows;
using System.Windows.Markup;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls;

public partial class CollectionControlDialog : CollectionControlDialogBase, IComponentConnector
{
	private IList originalData = new List<object>();

	public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IEnumerable), typeof(CollectionControlDialog), new UIPropertyMetadata(null));

	public static readonly DependencyProperty ItemsSourceTypeProperty = DependencyProperty.Register("ItemsSourceType", typeof(Type), typeof(CollectionControlDialog), new UIPropertyMetadata(null));

	public static readonly DependencyProperty NewItemTypesProperty = DependencyProperty.Register("NewItemTypes", typeof(IList), typeof(CollectionControlDialog), new UIPropertyMetadata(null));

	public static readonly DependencyProperty IsReadOnlyProperty = DependencyProperty.Register("IsReadOnly", typeof(bool), typeof(CollectionControlDialog), new UIPropertyMetadata(false));

	public IEnumerable ItemsSource
	{
		get
		{
			return (IEnumerable)GetValue(ItemsSourceProperty);
		}
		set
		{
			SetValue(ItemsSourceProperty, value);
		}
	}

	public Type ItemsSourceType
	{
		get
		{
			return (Type)GetValue(ItemsSourceTypeProperty);
		}
		set
		{
			SetValue(ItemsSourceTypeProperty, value);
		}
	}

	public IList<Type> NewItemTypes
	{
		get
		{
			return (IList<Type>)GetValue(NewItemTypesProperty);
		}
		set
		{
			SetValue(NewItemTypesProperty, value);
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

	public CollectionControl CollectionControl => _collectionControl;

	public CollectionControlDialog()
	{
		InitializeComponent();
	}

	public CollectionControlDialog(Type itemsourceType)
		: this()
	{
		ItemsSourceType = itemsourceType;
	}

	public CollectionControlDialog(Type itemsourceType, IList<Type> newItemTypes)
		: this(itemsourceType)
	{
		NewItemTypes = newItemTypes;
	}

	protected override void OnSourceInitialized(EventArgs e)
	{
		base.OnSourceInitialized(e);
		if (ItemsSource == null)
		{
			return;
		}
		foreach (object item in ItemsSource)
		{
			originalData.Add(Clone(item));
		}
	}

	private void OkButton_Click(object sender, RoutedEventArgs e)
	{
		if (ItemsSource is IDictionary && !AreDictionaryKeysValid())
		{
			MessageBox.Show("All dictionary items should have distinct non-null Key values.", "Warning");
			return;
		}
		_collectionControl.PersistChanges();
		DialogResult = true;
		Close();
	}

	private void CancelButton_Click(object sender, RoutedEventArgs e)
	{
		_collectionControl.PersistChanges(originalData);
		DialogResult = false;
		Close();
	}

	[SecuritySafeCritical]
	private object Clone(object source)
	{
		if (source == null)
		{
			return null;
		}
		object obj = null;
		Type type = source.GetType();
		if (ItemsSource is IDictionary && type.IsGenericType && typeof(KeyValuePair<, >).IsAssignableFrom(type.GetGenericTypeDefinition()))
		{
			obj = GenerateEditableKeyValuePair(source);
		}
		else
		{
			obj = FormatterServices.GetUninitializedObject(type);
			ConstructorInfo constructor = type.GetConstructor(Type.EmptyTypes);
			if (constructor == null)
			{
				return null;
			}
			constructor.Invoke(obj, null);
		}
		if (obj != null)
		{
			PropertyInfo[] properties = type.GetProperties();
			foreach (PropertyInfo propertyInfo in properties)
			{
				ParameterInfo[] indexParameters = propertyInfo.GetIndexParameters();
				object[] array = ((indexParameters.GetLength(0) == 0) ? null : new object[1] { indexParameters.GetLength(0) - 1 });
				object value = propertyInfo.GetValue(source, array);
				if (!propertyInfo.CanWrite)
				{
					continue;
				}
				if (propertyInfo.PropertyType.IsClass && propertyInfo.PropertyType != typeof(Transform) && !propertyInfo.PropertyType.Equals(typeof(string)))
				{
					if (propertyInfo.PropertyType.IsGenericType)
					{
						Type type2 = propertyInfo.PropertyType.GetGenericArguments().FirstOrDefault();
						if (type2 != null && !type2.IsPrimitive && !type2.Equals(typeof(string)) && !type2.IsEnum)
						{
							object value2 = Clone(value);
							propertyInfo.SetValue(obj, value2, null);
						}
						else
						{
							propertyInfo.SetValue(obj, value, null);
						}
						continue;
					}
					object obj2 = Clone(value);
					if (obj2 != null)
					{
						if (array != null)
						{
							obj.GetType().GetMethod("Add").Invoke(obj, new object[1] { obj2 });
						}
						else
						{
							propertyInfo.SetValue(obj, obj2, null);
						}
					}
				}
				else
				{
					propertyInfo.SetValue(obj, value, null);
				}
			}
		}
		return obj;
	}

	private object GenerateEditableKeyValuePair(object source)
	{
		Type type = source.GetType();
		if (type.GetGenericArguments() == null || type.GetGenericArguments().GetLength(0) != 2)
		{
			return null;
		}
		PropertyInfo property = type.GetProperty("Key");
		PropertyInfo property2 = type.GetProperty("Value");
		if (property != null && property2 != null)
		{
			return ListUtilities.CreateEditableKeyValuePair(property.GetValue(source, null), type.GetGenericArguments()[0], property2.GetValue(source, null), type.GetGenericArguments()[1]);
		}
		return null;
	}

	private bool AreDictionaryKeysValid()
	{
		IEnumerable<object> source = _collectionControl.Items.Select((object x) =>
		{
			PropertyInfo property = x.GetType().GetProperty("Key");
			return (property != null) ? property.GetValue(x, null) : null;
		});
		if (source.Distinct().Count() == _collectionControl.Items.Count)
		{
			return source.All((object x) => x != null);
		}
		return false;
	}

}
