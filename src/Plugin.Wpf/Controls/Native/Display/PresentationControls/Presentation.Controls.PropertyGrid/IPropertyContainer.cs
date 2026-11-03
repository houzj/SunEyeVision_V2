using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.Presentation.Controls.PropertyGrid.Editors;
using System.ComponentModel;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.PresentationControls.PropertyGrid;

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
