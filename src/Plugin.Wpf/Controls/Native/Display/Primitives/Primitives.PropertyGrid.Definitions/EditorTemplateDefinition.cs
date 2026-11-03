using SunEyeVision.Plugin.Wpf.Native.Display.Primitives;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown;
using SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Editors;
using SunEyeVision.Plugin.Wpf.Native.Numeric.UpDown.PropertyGrid.Editors;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Display.Primitives.PropertyGrid.Definitions;

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
