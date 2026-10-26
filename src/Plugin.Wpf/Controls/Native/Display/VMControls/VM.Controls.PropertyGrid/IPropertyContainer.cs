using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

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
