using System;
using System.ComponentModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Media;
using Apps.Localization;
using Apps.Log;
using Mvvm.Commands;
using VM.Controls;
using VM.ModuleControl;
using VMControls.BaseInterface;

namespace IMVSMachineLearningClassifierModuControl;

public class CategoryData : INotifyPropertyChanged
{
	private object _parent;

	private string _CategoryName;

	private uint _RegisteredNum = 0u;

	private uint _CalibratedNum = 0u;

	private ImageSource _ImageSource;

	private int _CategoryIndex;

	private string _CategoryBrush = "#FF0000";

	private string _MarkLableText = AppLocalizationService.GetLang("标记到此类");

	public int IsNeedClear = 0;

	private ICommand _UpdateCategoryNameCommond = null;

	public string CategoryName
	{
		get
		{
			return _CategoryName;
		}
		set
		{
			if (_CategoryName != value)
			{
				if (_parent is IRegisterClassify registerClassify && Enumerable.Count(registerClassify.Items.OfType<CategoryData>().ToList(), (CategoryData p) => p.CategoryName == value) > 0)
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

	public uint RegisteredNum
	{
		get
		{
			return _RegisteredNum;
		}
		set
		{
			if (_RegisteredNum != value)
			{
				_RegisteredNum = value;
				OnPropertyChanged("RegisteredNum");
				OnPropertyChanged("StrRegisteredNum");
			}
		}
	}

	public uint CalibratedNum
	{
		get
		{
			return _CalibratedNum;
		}
		set
		{
			if (_CalibratedNum != value)
			{
				if (value == uint.MaxValue)
				{
					value = 0u;
				}
				_CalibratedNum = value;
				OnPropertyChanged("CalibratedNum");
				OnPropertyChanged("StrCalibratedNum");
			}
		}
	}

	public uint RemainedNum => 100 - RegisteredNum;

	public ImageSource ImageSource
	{
		get
		{
			return _ImageSource;
		}
		set
		{
			if (_ImageSource != value)
			{
				_ImageSource = value;
				OnPropertyChanged("ImageSource");
			}
		}
	}

	public int CategoryIndex
	{
		get
		{
			return _CategoryIndex;
		}
		set
		{
			if (_CategoryIndex != value)
			{
				_CategoryIndex = value;
				OnPropertyChanged("CategoryIndex");
			}
		}
	}

	public Brush CategoryBrush => CategoryBrushData.GetCategoryBrush(CategoryIndex);

	public string CategoryBrushStr => _CategoryBrush;

	public string MarkLableText
	{
		get
		{
			return _MarkLableText;
		}
		set
		{
			if (_MarkLableText != value)
			{
				_MarkLableText = value;
				OnPropertyChanged("MarkLableText");
			}
		}
	}

	public string StrRegisteredNum => RegisteredNum.ToString();

	public string StrCalibratedNum => CalibratedNum.ToString();

	public ICommand UpdateCategoryNameCommond
	{
		get
		{
			if (_UpdateCategoryNameCommond == null)
			{
				_UpdateCategoryNameCommond = (ICommand)new DelegateCommand<object>((Action<object>)UpdateCategoryNameCmdExecute);
			}
			return _UpdateCategoryNameCommond;
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public void OnPropertyChanged(string propertyName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(propertyName));
		}
	}

	public void SetParentNode(object parentNode)
	{
		_parent = parentNode;
	}

	private void UpdateCategoryNameCmdExecute(object category)
	{
		EditCategoryDialog editCategoryDialog = new EditCategoryDialog();
		if (!(category is MLImagePage mLImagePage))
		{
			return;
		}
		editCategoryDialog.Init(CategoryName, mLImagePage.CategoryList);
		if (editCategoryDialog.ShowDialog() != true)
		{
			return;
		}
		string categoryName = CategoryName;
		string imageLabel = (CategoryName = editCategoryDialog.CategoryName);
		foreach (ImageEntity image in mLImagePage.ImageList)
		{
			if (image.ImageLabel == categoryName)
			{
				image.ImageLabel = imageLabel;
			}
		}
		try
		{
			((IUserStringData)MLClassifyMgr.Instance.Param)["UpdateCategoryName"] = CategoryIndex + ";" + CategoryName;
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}
}
