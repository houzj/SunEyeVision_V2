using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;
using System.Collections;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Media;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

internal class PropertyGridUtilities
{
	private class EditorTypeDescriptorContext : ITypeDescriptorContext, IServiceProvider
	{
		private IContainer _container;

		private object _instance;

		private PropertyDescriptor _propertyDescriptor;

		IContainer ITypeDescriptorContext.Container => _container;

		object ITypeDescriptorContext.Instance => _instance;

		PropertyDescriptor ITypeDescriptorContext.PropertyDescriptor => _propertyDescriptor;

		internal EditorTypeDescriptorContext(IContainer container, object instance, PropertyDescriptor pd)
		{
			_container = container;
			_instance = instance;
			_propertyDescriptor = pd;
		}

		void ITypeDescriptorContext.OnComponentChanged()
		{
		}

		bool ITypeDescriptorContext.OnComponentChanging()
		{
			return false;
		}

		object IServiceProvider.GetService(Type serviceType)
		{
			return null;
		}
	}

	internal static T GetAttribute<T>(PropertyDescriptor property) where T : Attribute
	{
		return property.Attributes.OfType<T>().FirstOrDefault();
	}

	internal static ITypeEditor CreateDefaultEditor(Type propertyType, TypeConverter typeConverter, PropertyItem propertyItem)
	{
		ITypeEditor typeEditor = null;
		EditorTypeDescriptorContext context = new EditorTypeDescriptorContext(null, propertyItem.Instance, propertyItem.PropertyDescriptor);
		if (typeConverter != null && typeConverter.GetStandardValuesSupported(context) && typeConverter.GetStandardValuesExclusive(context) && propertyType != typeof(bool) && propertyType != typeof(bool?))
		{
			return new SourceComboBoxEditor(typeConverter.GetStandardValues(context), typeConverter);
		}
		if (propertyType == typeof(string))
		{
			return new TextBoxEditor();
		}
		if (propertyType == typeof(bool) || propertyType == typeof(bool?))
		{
			return new CheckBoxEditor();
		}
		if (propertyType == typeof(decimal) || propertyType == typeof(decimal?))
		{
			return new DecimalUpDownEditor();
		}
		if (propertyType == typeof(double) || propertyType == typeof(double?))
		{
			return new DoubleUpDownEditor();
		}
		if (propertyType == typeof(int) || propertyType == typeof(int?))
		{
			return new IntegerUpDownEditor();
		}
		if (propertyType == typeof(short) || propertyType == typeof(short?))
		{
			return new ShortUpDownEditor();
		}
		if (propertyType == typeof(long) || propertyType == typeof(long?))
		{
			return new LongUpDownEditor();
		}
		if (propertyType == typeof(float) || propertyType == typeof(float?))
		{
			return new SingleUpDownEditor();
		}
		if (propertyType == typeof(byte) || propertyType == typeof(byte?))
		{
			return new ByteUpDownEditor();
		}
		if (propertyType == typeof(sbyte) || propertyType == typeof(sbyte?))
		{
			return new SByteUpDownEditor();
		}
		if (propertyType == typeof(uint) || propertyType == typeof(uint?))
		{
			return new UIntegerUpDownEditor();
		}
		if (propertyType == typeof(ulong) || propertyType == typeof(ulong?))
		{
			return new ULongUpDownEditor();
		}
		if (propertyType == typeof(ushort) || propertyType == typeof(ushort?))
		{
			return new UShortUpDownEditor();
		}
		if (propertyType == typeof(DateTime) || propertyType == typeof(DateTime?))
		{
			return new DateTimeUpDownEditor();
		}
		if (propertyType == typeof(Color) || propertyType == typeof(Color?))
		{
			return new ColorEditor();
		}
		if (propertyType.IsEnum)
		{
			return new EnumComboBoxEditor();
		}
		if (propertyType == typeof(TimeSpan) || propertyType == typeof(TimeSpan?))
		{
			return new TimeSpanUpDownEditor();
		}
		if (propertyType == typeof(FontFamily) || propertyType == typeof(FontWeight) || propertyType == typeof(FontStyle) || propertyType == typeof(FontStretch))
		{
			return new FontComboBoxEditor();
		}
		if (propertyType == typeof(Guid) || propertyType == typeof(Guid?))
		{
			return new MaskedTextBoxEditor
			{
				ValueDataType = propertyType,
				Mask = "AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA"
			};
		}
		if (propertyType == typeof(char) || propertyType == typeof(char?))
		{
			return new MaskedTextBoxEditor
			{
				ValueDataType = propertyType,
				Mask = "&"
			};
		}
		if (propertyType == typeof(object))
		{
			return new TextBoxEditor();
		}
		Type listItemType = ListUtilities.GetListItemType(propertyType);
		if (listItemType != null)
		{
			if (!listItemType.IsPrimitive && !listItemType.Equals(typeof(string)) && !listItemType.IsEnum)
			{
				return new CollectionEditor();
			}
			return new PrimitiveTypeCollectionEditor();
		}
		Type[] dictionaryItemsType = ListUtilities.GetDictionaryItemsType(propertyType);
		Type collectionItemType = ListUtilities.GetCollectionItemType(propertyType);
		if (dictionaryItemsType != null || collectionItemType != null || typeof(ICollection).IsAssignableFrom(propertyType))
		{
			return new CollectionEditor();
		}
		ITypeEditor result;
		if (typeConverter == null || !typeConverter.CanConvertFrom(typeof(string)))
		{
			ITypeEditor typeEditor2 = new TextBlockEditor();
			result = typeEditor2;
		}
		else
		{
			ITypeEditor typeEditor2 = new TextBoxEditor();
			result = typeEditor2;
		}
		return result;
	}
}
