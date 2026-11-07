using System.Collections;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;

namespace DynamicObjectConfigControl;

public partial class DynamicRunParamRelationModulesConfig : Control, IComponentConnector
{
	public static readonly DependencyProperty ItemsSourceProperty = DependencyProperty.Register("ItemsSource", typeof(IList), typeof(DynamicRunParamRelationModulesConfig), new PropertyMetadata(null));

	public IList ItemsSource
	{
		get
		{
			return (IList)GetValue(ItemsSourceProperty);
		}
		set
		{
			SetValue(ItemsSourceProperty, value);
		}
	}

	public DynamicRunParamRelationModulesConfig()
	{
		InitializeComponent();
	}
}
