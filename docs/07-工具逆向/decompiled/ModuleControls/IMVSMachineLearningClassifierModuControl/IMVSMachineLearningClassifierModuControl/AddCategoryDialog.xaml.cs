using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using Apps.Localization;
using VM.Controls;

namespace IMVSMachineLearningClassifierModuControl;

public partial class AddCategoryDialog : BaseWindow, IRegisterClassify, INotifyPropertyChanged, IComponentConnector, IStyleConnector
{
	private ObservableCollection<CategoryData> Categories = new ObservableCollection<CategoryData>();

	public static readonly DependencyProperty NewCategoriesProperty = DependencyProperty.Register("NewCategories", typeof(ObservableCollection<CategoryData>), typeof(AddCategoryDialog), new PropertyMetadata((object)null));

	public ObservableCollection<CategoryData> NewCategories
	{
		get
		{
			return (ObservableCollection<CategoryData>)GetValue(NewCategoriesProperty);
		}
		set
		{
			SetValue(NewCategoriesProperty, value);
		}
	}

	IList IRegisterClassify.Items => Categories;

	public AddCategoryDialog()
	{
		InitializeComponent();
		NewCategories = new ObservableCollection<CategoryData>();
	}

	private void AddCategory_OnClick(object sender, RoutedEventArgs e)
	{
		if ((long)Categories.Count >= 200L)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " : " + 200u, MessageButtons.Normal, MessageIcons.Warning);
			return;
		}
		CategoryData categoryData = new CategoryData();
		categoryData.SetParentNode(this);
		NewCategories.Add(categoryData);
		Categories.Add(categoryData);
	}

	private void DeleteCategory_OnClick(object sender, RoutedEventArgs e)
	{
		if (sender is Button { DataContext: CategoryData dataContext })
		{
			if (NewCategories.Contains(dataContext))
			{
				NewCategories.Remove(dataContext);
			}
			if (Categories.Contains(dataContext))
			{
				Categories.Remove(dataContext);
			}
		}
	}

	private void Confirm_OnClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		if (Enumerable.Count(Categories, (CategoryData p) => string.IsNullOrWhiteSpace(p.CategoryName)) > 0)
		{
			DialogResult dialogResult = HikMessageBox.Show("Lang_Sure", "Lang_CategoryNameEmptyEnquiry", MessageButtons.YesOrNo, MessageIcons.Question, isTranslate: true);
			if (dialogResult != VM.Controls.DialogResult.Yes && dialogResult != VM.Controls.DialogResult.SDKModel)
			{
				e.Cancel = true;
				return;
			}
		}
		DialogResult = true;
		base.OnClosing(e);
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
