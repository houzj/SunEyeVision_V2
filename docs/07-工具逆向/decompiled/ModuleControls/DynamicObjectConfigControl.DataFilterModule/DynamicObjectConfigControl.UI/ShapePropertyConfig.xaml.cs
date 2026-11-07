using System.Windows.Controls;
using System.Windows.Markup;
using Apps.Localization;

namespace DynamicObjectConfigControl.UI;

public partial class ShapePropertyConfig : Control, IComponentConnector
{
	public ShapePropertyConfig()
	{
		InitializeComponent();
		if (AppLocalizationService.GetLang("Language") == "English")
		{
			Width = 500.0;
		}
	}
}
