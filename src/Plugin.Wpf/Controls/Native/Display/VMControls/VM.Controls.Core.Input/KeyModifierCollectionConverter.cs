using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.ComponentModel;
using System.ComponentModel.Design.Serialization;
using System.Globalization;
using System.Security;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Input;

public sealed class KeyModifierCollectionConverter : TypeConverter
{
	private static readonly TypeConverter _keyModifierConverter = TypeDescriptor.GetConverter(typeof(KeyModifier));

	public override bool CanConvertFrom(ITypeDescriptorContext typeDescriptorContext, Type type)
	{
		return _keyModifierConverter.CanConvertFrom(typeDescriptorContext, type);
	}

	public override bool CanConvertTo(ITypeDescriptorContext typeDescriptorContext, Type type)
	{
		if (!(type == typeof(InstanceDescriptor)) && !(type == typeof(KeyModifierCollection)))
		{
			return type == typeof(string);
		}
		return true;
	}

	public override object ConvertFrom(ITypeDescriptorContext typeDescriptorContext, CultureInfo cultureInfo, object value)
	{
		KeyModifierCollection keyModifierCollection = new KeyModifierCollection();
		string text = value as string;
		if (value == null || (text != null && text.Trim() == string.Empty))
		{
			keyModifierCollection.Add(KeyModifier.None);
		}
		else
		{
			if (text != null)
			{
				string[] array = text.Split(new char[4] { '+', ' ', '|', ',' }, StringSplitOptions.RemoveEmptyEntries);
				foreach (string value2 in array)
				{
					keyModifierCollection.Add((KeyModifier)_keyModifierConverter.ConvertFrom(typeDescriptorContext, cultureInfo, value2));
				}
			}
			if (keyModifierCollection.Count == 0)
			{
				keyModifierCollection.Add(KeyModifier.None);
			}
		}
		return keyModifierCollection;
	}

	public override object ConvertTo(ITypeDescriptorContext typeDescriptorContext, CultureInfo cultureInfo, object value, Type destinationType)
	{
		if (value == null || ((KeyModifierCollection)value).Count == 0)
		{
			if (destinationType == typeof(InstanceDescriptor))
			{
				object result = null;
				try
				{
					result = ConstructInstanceDescriptor();
				}
				catch (SecurityException)
				{
				}
				return result;
			}
			if (destinationType == typeof(string))
			{
				return _keyModifierConverter.ConvertTo(typeDescriptorContext, cultureInfo, KeyModifier.None, destinationType);
			}
		}
		if (destinationType == typeof(string))
		{
			string text = string.Empty;
			if (value != null)
			{
				foreach (KeyModifier item in (KeyModifierCollection)value)
				{
					if (text != string.Empty)
					{
						text += "+";
					}
					text += _keyModifierConverter.ConvertTo(typeDescriptorContext, cultureInfo, item, destinationType);
				}
			}
			return text;
		}
		return null;
	}

	private static object ConstructInstanceDescriptor()
	{
		return new InstanceDescriptor(typeof(KeyModifierCollection).GetConstructor(new Type[0]), new object[0]);
	}
}
