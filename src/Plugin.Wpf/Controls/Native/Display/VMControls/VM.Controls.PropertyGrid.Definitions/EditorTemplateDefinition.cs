using SunEyeVision.Plugin.Wpf.Native.Display.VMControls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Definitions;

public class EditorTemplateDefinition : EditorDefinitionBase
{
	public static readonly DependencyProperty EditingTemplateProperty = DependencyProperty.Register("EditingTemplate", typeof(DataTemplate), typeof(EditorTemplateDefinition), new UIPropertyMetadata(null));

	public DataTemplate EditingTemplate
	{
		get
		{
			return (DataTemplate)GetValue(EditingTemplateProperty);
		}
		set
		{
			SetValue(EditingTemplateProperty, value);
		}
	}

	protected sealed override FrameworkElement GenerateEditingElement(PropertyItemBase propertyItem)
	{
		if (EditingTemplate == null)
		{
			return null;
		}
		return EditingTemplate.LoadContent() as FrameworkElement;
	}
}
