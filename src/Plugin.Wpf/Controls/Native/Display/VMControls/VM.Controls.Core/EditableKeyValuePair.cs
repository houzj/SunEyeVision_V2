using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core;

public class EditableKeyValuePair<TKey, TValue> : CustomTypeDescriptor
{
	private PropertyDescriptorCollection _properties;

	public TKey Key { get; set; }

	public TValue Value { get; set; }

	public EditableKeyValuePair()
	{
		List<PropertyDescriptor> list = new List<PropertyDescriptor>();
		PropertyDescriptor item = TypeDescriptor.CreateProperty(GetType(), "Key", typeof(TKey));
		list.Add(item);
		PropertyDescriptor item2 = TypeDescriptor.CreateProperty(GetType(), "Value", typeof(TValue));
		list.Add(item2);
		_properties = new PropertyDescriptorCollection(list.ToArray());
	}

	public EditableKeyValuePair(TKey key, TValue value)
		: this()
	{
		Key = key;
		Value = value;
	}

	public override PropertyDescriptorCollection GetProperties(Attribute[] attributes)
	{
		return GetProperties();
	}

	public override PropertyDescriptorCollection GetProperties()
	{
		return _properties;
	}

	public override object GetPropertyOwner(PropertyDescriptor pd)
	{
		return this;
	}

	public override string ToString()
	{
		return string.Concat("[", Key, ",", Value, "]");
	}
}
