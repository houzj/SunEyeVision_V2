using System.ComponentModel;
using System.Linq;
using System.Windows.Media;
using Apps.Localization;
using VM.Controls;

namespace FewShotCommonControl;

public class CategoryData : INotifyPropertyChanged
{
	private object _parent;

	private string _CategoryName;

	private uint _RegisteredNum = 0u;

	private uint _CalibratedNum = 0u;

	private ImageSource _ImageSource;

	private string _LabelColor = "#00FF00";

	private string _LabelText = "标记为此类";

	private bool _isEdit = false;

	private uint _RegisteredROINum = 0u;

	private uint _CalibratedROINum = 0u;

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
				if (_parent is IEdgeLearning edgeLearning && Enumerable.Count(edgeLearning.Items.OfType<CategoryData>().ToList(), (CategoryData p) => p.CategoryName == value) > 0)
				{
					HikMessageBox.Show("Lang_RenameTip", MessageIcons.Warning, isTranslate: true);
					return;
				}
				if (_isEdit && string.IsNullOrWhiteSpace(value))
				{
					HikMessageBox.Show("Lang_CategoryNameEmptyWarn", MessageIcons.Warning, isTranslate: true);
					return;
				}
				if (!EdgeLearningData.Instance.CheckCategoryName(value))
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
				_CalibratedNum = value;
				OnPropertyChanged("CalibratedNum");
				OnPropertyChanged("StrCalibratedNum");
			}
		}
	}

	public uint RegisteredROINum
	{
		get
		{
			return _RegisteredROINum;
		}
		set
		{
			if (_RegisteredROINum != value)
			{
				_RegisteredROINum = value;
				OnPropertyChanged("RegisteredROINum");
				OnPropertyChanged("StrRegisteredROINum");
			}
		}
	}

	public uint CalibratedROINum
	{
		get
		{
			return _CalibratedROINum;
		}
		set
		{
			if (_CalibratedROINum != value)
			{
				_CalibratedROINum = value;
				OnPropertyChanged("CalibratedROINum");
				OnPropertyChanged("StrCalibratedROINum");
			}
		}
	}

	public uint RemainedNum => 10 - RegisteredNum;

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

	public string StrRegisteredNum => RegisteredNum.ToString();

	public string StrCalibratedNum => CalibratedNum.ToString();

	public string StrRegisteredROINum => RegisteredROINum.ToString();

	public string StrCalibratedROINum => CalibratedROINum.ToString();

	public string LabelColor
	{
		get
		{
			return _LabelColor;
		}
		set
		{
			if (_LabelColor != value)
			{
				_LabelColor = value;
				OnPropertyChanged("LabelColor");
			}
		}
	}

	public string LabelText
	{
		get
		{
			return _LabelText;
		}
		set
		{
			if (_LabelText != value)
			{
				_LabelText = value;
				OnPropertyChanged("LabelText");
			}
		}
	}

	internal bool IsEdit
	{
		get
		{
			return _isEdit;
		}
		set
		{
			if (value != _isEdit)
			{
				_isEdit = value;
			}
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
}
