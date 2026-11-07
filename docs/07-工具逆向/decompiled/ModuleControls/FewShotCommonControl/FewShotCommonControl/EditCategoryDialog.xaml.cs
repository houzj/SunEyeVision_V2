using System.Collections;
using System.Collections.ObjectModel;
using System.Windows.Markup;
using Apps.PcUI.Common;

namespace FewShotCommonControl;

public partial class EditCategoryDialog : ConfirmBaseWindow, IEdgeLearning, IComponentConnector
{
	private ObservableCollection<CategoryData> Categories = new ObservableCollection<CategoryData>();

	IList IEdgeLearning.Items => Categories;

	public EditCategoryDialog()
	{
		InitializeComponent();
	}

	public void Init(ObservableCollection<CategoryData> categoryList)
	{
		if (categoryList == null)
		{
			return;
		}
		foreach (CategoryData category in categoryList)
		{
			Categories.Add(category);
		}
	}
}
