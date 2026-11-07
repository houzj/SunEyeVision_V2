using System.Windows;

namespace VM.Controls.PropertyGrid.Editors;

public class PropertyGridEditorCollectionControl : CollectionControlButton
{
	static PropertyGridEditorCollectionControl()
	{
		FrameworkElement.DefaultStyleKeyProperty.OverrideMetadata(typeof(PropertyGridEditorCollectionControl), new FrameworkPropertyMetadata(typeof(PropertyGridEditorCollectionControl)));
	}
}
