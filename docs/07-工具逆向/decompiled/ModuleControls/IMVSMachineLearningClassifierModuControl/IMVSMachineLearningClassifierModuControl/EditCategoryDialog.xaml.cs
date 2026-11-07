using System.Collections;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Windows;
using System.Windows.Markup;
using Apps.Localization;
using VM.Controls;

namespace IMVSMachineLearningClassifierModuControl;

public partial class EditCategoryDialog : BaseWindow, IRegisterClassify, INotifyPropertyChanged, IComponentConnector
{
	public string _CategoryName;

	private ObservableCollection<CategoryData> Categories = new ObservableCollection<CategoryData>();

	public string CategoryName
	{
		get
		{
			return _CategoryName;
		}
		set
		{
			if (value != _CategoryName)
			{
				if (Categories.Where((CategoryData c) => c.CategoryName == value).Count() > 0)
				{
					HikMessageBox.Show("Lang_RenameTip", MessageIcons.Warning, isTranslate: true);
					return;
				}
				if (!MLClassifyMgr.Instance.CheckCategoryName(value))
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_CategoryNameIllegalCharTip") + "\n\\ / : * ? \" < > |", MessageIcons.Warning);
					return;
				}
				_CategoryName = value;
				OnPropertyChanged("CategoryName");
			}
		}
	}

	IList IRegisterClassify.Items => Categories;

	public EditCategoryDialog()
	{
		InitializeComponent();
	}

	private void Confirm_OnClick(object sender, RoutedEventArgs e)
	{
		Close();
	}

	protected override void OnClosing(CancelEventArgs e)
	{
		DialogResult = true;
		base.OnClosing(e);
	}

	public void Init(string categoryName, ObservableCollection<CategoryData> _Categories)
	{
		Categories = _Categories;
		_CategoryName = categoryName;
	}
}
