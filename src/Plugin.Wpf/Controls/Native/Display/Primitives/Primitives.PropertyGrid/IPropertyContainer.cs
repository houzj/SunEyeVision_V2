using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid;

internal interface IPropertyContainer
{
	ContainerHelperBase ContainerHelper { get; }

	Style PropertyContainerStyle { get; }

	EditorDefinitionCollection EditorDefinitions { get; }

	PropertyDefinitionCollection PropertyDefinitions { get; }

	bool IsCategorized { get; }

	bool IsSortedAlphabetically { get; }

	bool AutoGenerateProperties { get; }

	bool HideInheritedProperties { get; }

	FilterInfo FilterInfo { get; }

	bool? IsPropertyVisible(PropertyDescriptor pd);
}
