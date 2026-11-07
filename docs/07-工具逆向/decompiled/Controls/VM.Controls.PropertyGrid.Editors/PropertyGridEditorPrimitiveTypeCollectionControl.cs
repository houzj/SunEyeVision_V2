using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorPrimitiveTypeCollectionControl : PrimitiveTypeCollectionControl
{
	static PropertyGridEditorPrimitiveTypeCollectionControl()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorPrimitiveTypeCollectionControl), new FrameworkPropertyMetadata(typeof(PropertyGridEditorPrimitiveTypeCollectionControl)));
	}
}
