using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;
using System.Windows.Data;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.Core.Utilities;

internal sealed class GeneralUtilities : DependencyObject
{
	internal static readonly DependencyProperty StubValueProperty = DependencyProperty.RegisterAttached("StubValue", typeof(object), typeof(GeneralUtilities), new FrameworkPropertyMetadata(null, FrameworkPropertyMetadataOptions.BindsTwoWayByDefault));

	private GeneralUtilities()
	{
	}

	internal static object GetStubValue(DependencyObject obj)
	{
		return obj.GetValue(StubValueProperty);
	}

	internal static void SetStubValue(DependencyObject obj, object value)
	{
		obj.SetValue(StubValueProperty, value);
	}

	public static object GetPathValue(object sourceObject, string path)
	{
		GeneralUtilities generalUtilities = new GeneralUtilities();
		BindingOperations.SetBinding(generalUtilities, StubValueProperty, new Binding(path)
		{
			Source = sourceObject
		});
		object stubValue = GetStubValue(generalUtilities);
		BindingOperations.ClearBinding(generalUtilities, StubValueProperty);
		return stubValue;
	}

	public static object GetBindingValue(object sourceObject, Binding binding)
	{
		Binding binding2 = new Binding
		{
			BindsDirectlyToSource = binding.BindsDirectlyToSource,
			Converter = binding.Converter,
			ConverterCulture = binding.ConverterCulture,
			ConverterParameter = binding.ConverterParameter,
			FallbackValue = binding.FallbackValue,
			Mode = BindingMode.OneTime,
			Path = binding.Path,
			StringFormat = binding.StringFormat,
			TargetNullValue = binding.TargetNullValue,
			XPath = binding.XPath
		};
		binding2.Source = sourceObject;
		GeneralUtilities generalUtilities = new GeneralUtilities();
		BindingOperations.SetBinding(generalUtilities, StubValueProperty, binding2);
		object stubValue = GetStubValue(generalUtilities);
		BindingOperations.ClearBinding(generalUtilities, StubValueProperty);
		return stubValue;
	}

	internal static bool CanConvertValue(object value, object targetType)
	{
		if (value != null && !object.Equals(value.GetType(), targetType))
		{
			return !object.Equals(targetType, typeof(object));
		}
		return false;
	}
}
