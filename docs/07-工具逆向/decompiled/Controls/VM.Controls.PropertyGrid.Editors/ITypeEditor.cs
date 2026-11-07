using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public interface ITypeEditor
{
	FrameworkElement ResolveEditor(PropertyItem propertyItem);
}
