using System;
using System.Linq;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Mvvm.Commands;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class LinePair : NameValueItem
{
	private DelegateCommand _deleteCommand;

	public bool CanDelete = false;

	public bool Fixed = false;

	public int DisplayIndex;

	public int ShowType;

	public ButtonSelecter ObjectLine;

	public ButtonSelecter TargetLine;

	public ButtonSelecter ObjectStartPoint;

	public ButtonSelecter TargetStartPoint;

	public ButtonSelecter ObjectEndPoint;

	public ButtonSelecter TargetEndPoint;

	public ButtonSelecter ObjectStartPointX;

	public ButtonSelecter TargetStartPointX;

	public ButtonSelecter ObjectEndPointX;

	public ButtonSelecter TargetEndPointX;

	public ButtonSelecter ObjectStartPointY;

	public ButtonSelecter TargetStartPointY;

	public ButtonSelecter ObjectEndPointY;

	public ButtonSelecter TargetEndPointY;

	public ButtonSelecter ObjectAngle;

	public ButtonSelecter TargetAngle;

	public int _childleftwidthch = 50;

	public int _childleftwidthen = 80;

	public int _childuiwidthch = 140;

	public int _childuiwidthen = 120;

	public ButtonSelecter ObjectLineA
	{
		get
		{
			return ObjectLine;
		}
		set
		{
			ObjectLine = value;
			OnPropertyChanged("ObjectLineA");
		}
	}

	public ButtonSelecter TargetLineA
	{
		get
		{
			return TargetLine;
		}
		set
		{
			TargetLine = value;
			OnPropertyChanged("TargetLineA");
		}
	}

	public ButtonSelecter ObjectStartPointA
	{
		get
		{
			return ObjectStartPoint;
		}
		set
		{
			ObjectStartPoint = value;
			OnPropertyChanged("ObjectStartPointA");
		}
	}

	public ButtonSelecter TargetStartPointA
	{
		get
		{
			return TargetStartPoint;
		}
		set
		{
			TargetStartPoint = value;
			OnPropertyChanged("TargetStartPointA");
		}
	}

	public ButtonSelecter ObjectEndPointA
	{
		get
		{
			return ObjectEndPoint;
		}
		set
		{
			ObjectEndPoint = value;
			OnPropertyChanged("ObjectEndPointA");
		}
	}

	public ButtonSelecter TargetEndPointA
	{
		get
		{
			return TargetEndPoint;
		}
		set
		{
			TargetEndPoint = value;
			OnPropertyChanged("TargetEndPointA");
		}
	}

	public ButtonSelecter ObjectStartPointXA
	{
		get
		{
			return ObjectStartPointX;
		}
		set
		{
			ObjectStartPointX = value;
			OnPropertyChanged("ObjectStartPointXA");
		}
	}

	public ButtonSelecter TargetStartPointXA
	{
		get
		{
			return TargetStartPointX;
		}
		set
		{
			TargetStartPointX = value;
			OnPropertyChanged("TargetStartPointXA");
		}
	}

	public ButtonSelecter ObjectEndPointXA
	{
		get
		{
			return ObjectEndPointX;
		}
		set
		{
			ObjectEndPointX = value;
			OnPropertyChanged("ObjectEndPointXA");
		}
	}

	public ButtonSelecter TargetEndPointXA
	{
		get
		{
			return TargetEndPointX;
		}
		set
		{
			TargetEndPointX = value;
			OnPropertyChanged("TargetEndPointXA");
		}
	}

	public ButtonSelecter ObjectStartPointYA
	{
		get
		{
			return ObjectStartPointY;
		}
		set
		{
			ObjectStartPointY = value;
			OnPropertyChanged("ObjectStartPointYA");
		}
	}

	public ButtonSelecter TargetStartPointYA
	{
		get
		{
			return TargetStartPointY;
		}
		set
		{
			TargetStartPointY = value;
			OnPropertyChanged("TargetStartPointYA");
		}
	}

	public ButtonSelecter ObjectEndPointYA
	{
		get
		{
			return ObjectEndPointY;
		}
		set
		{
			ObjectEndPointY = value;
			OnPropertyChanged("ObjectEndPointYA");
		}
	}

	public ButtonSelecter TargetEndPointYA
	{
		get
		{
			return TargetEndPointY;
		}
		set
		{
			TargetEndPointY = value;
			OnPropertyChanged("TargetEndPointYA");
		}
	}

	public ButtonSelecter ObjectAngleA
	{
		get
		{
			return ObjectAngle;
		}
		set
		{
			ObjectAngle = value;
			OnPropertyChanged("ObjectAngleA");
		}
	}

	public ButtonSelecter TargetAngleA
	{
		get
		{
			return TargetAngle;
		}
		set
		{
			TargetAngle = value;
			OnPropertyChanged("TargetAngleA");
		}
	}

	public int ShowTypeA
	{
		get
		{
			return ShowType;
		}
		set
		{
			ShowType = value;
			OnPropertyChanged("ShowTypeA");
		}
	}

	public int ChildLeftWidth
	{
		get
		{
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				return _childleftwidthen;
			}
			return _childleftwidthch;
		}
	}

	public int ChildUIWidth
	{
		get
		{
			if (AppLocalizationService.GetLang("Language") == "English")
			{
				return _childuiwidthen;
			}
			return _childuiwidthch;
		}
	}

	public int DisplayIndexA
	{
		get
		{
			return DisplayIndex;
		}
		set
		{
			DisplayIndex = value;
			OnPropertyChanged("DisplayIndexA");
		}
	}

	public bool CanDeleteA
	{
		get
		{
			return CanDelete;
		}
		set
		{
			CanDelete = value;
			OnPropertyChanged("CanDeleteA");
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			return (ICommand)_deleteCommand;
		}
		set
		{
			_deleteCommand = (DelegateCommand)((value is DelegateCommand) ? value : null);
			OnPropertyChanged("DeleteCommand");
		}
	}

	public override bool CustomVisibleA
	{
		get
		{
			return CustomVisible;
		}
		set
		{
			if (CustomVisible != value)
			{
				CustomVisible = value;
				OnPropertyChanged("CustomVisibleA");
				ModifyDisplayIndex();
				ModifyDeleteBtnVisibility();
				SetParamUnit();
			}
		}
	}

	public LinePair()
	{
		//IL_0046: Unknown result type (might be due to invalid IL or missing references)
		//IL_0050: Expected Obj, but got Unknown
		DeleteCommand = (ICommand)new DelegateCommand((Action)DeleteCommandCmdExecute);
	}

	private void ModifyDisplayIndex()
	{
		if (GetParentNode() is Category category)
		{
			LinePair[] array = (from p in category.ItemsA.OfType<LinePair>()
				where p.CustomVisibleA
				select p).ToArray();
			for (int num = 0; num < array.Length; num++)
			{
				array[num].DisplayIndexA = num + 1;
			}
		}
	}

	private void ModifyDeleteBtnVisibility()
	{
		if (!(GetParentNode() is Category category))
		{
			return;
		}
		LinePair[] array = (from p in category.ItemsA.OfType<LinePair>()
			where p.CustomVisibleA
			select p).ToArray();
		if (array.Length > 1)
		{
			LinePair[] array2 = array;
			foreach (LinePair linePair in array2)
			{
				if (!linePair.Fixed)
				{
					linePair.CanDeleteA = true;
				}
			}
		}
		else if (array.Length == 1)
		{
			array[0].CanDeleteA = false;
		}
	}

	private void DeleteCommandCmdExecute()
	{
		if (TargetLineA != null)
		{
			TargetLineA.CurValueA = null;
		}
		if (ObjectLineA != null)
		{
			ObjectLineA.CurValueA = null;
		}
		if (TargetStartPointA != null)
		{
			TargetStartPointA.CurValueA = null;
		}
		if (TargetEndPointA != null)
		{
			TargetEndPointA.CurValueA = null;
		}
		if (ObjectStartPointA != null)
		{
			ObjectStartPointA.CurValueA = null;
		}
		if (ObjectEndPointA != null)
		{
			ObjectEndPointA.CurValueA = null;
		}
		if (TargetStartPointXA != null)
		{
			TargetStartPointXA.CurValueA = null;
		}
		if (TargetEndPointXA != null)
		{
			TargetEndPointXA.CurValueA = null;
		}
		if (ObjectStartPointXA != null)
		{
			ObjectStartPointXA.CurValueA = null;
		}
		if (ObjectEndPointXA != null)
		{
			ObjectEndPointXA.CurValueA = null;
		}
		if (TargetStartPointYA != null)
		{
			TargetStartPointYA.CurValueA = null;
		}
		if (TargetEndPointYA != null)
		{
			TargetEndPointYA.CurValueA = null;
		}
		if (ObjectStartPointYA != null)
		{
			ObjectStartPointYA.CurValueA = null;
		}
		if (ObjectEndPointYA != null)
		{
			ObjectEndPointYA.CurValueA = null;
		}
		if (TargetAngleA != null)
		{
			TargetAngleA.CurValueA = null;
		}
		if (ObjectAngleA != null)
		{
			ObjectAngleA.CurValueA = null;
		}
		CustomVisibleA = false;
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	public override int SetParamUnit()
	{
		MyItemState = ItemState.Normal;
		IParamsConfig moduleTagInfo = GetModuleTagInfo();
		if (moduleTagInfo == null)
		{
			return -1;
		}
		return 0;
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		if (TargetLineA != null)
		{
			TargetLineA.InniterBusiness();
		}
		if (ObjectLineA != null)
		{
			ObjectLineA.InniterBusiness();
		}
		if (TargetStartPointA != null)
		{
			TargetStartPointA.InniterBusiness();
		}
		if (TargetEndPointA != null)
		{
			TargetEndPointA.InniterBusiness();
		}
		if (ObjectStartPointA != null)
		{
			ObjectStartPointA.InniterBusiness();
		}
		if (ObjectEndPointA != null)
		{
			ObjectEndPointA.InniterBusiness();
		}
		if (TargetStartPointXA != null)
		{
			TargetStartPointXA.InniterBusiness();
		}
		if (TargetEndPointXA != null)
		{
			TargetEndPointXA.InniterBusiness();
		}
		if (ObjectStartPointXA != null)
		{
			ObjectStartPointXA.InniterBusiness();
		}
		if (ObjectEndPointXA != null)
		{
			ObjectEndPointXA.InniterBusiness();
		}
		if (TargetStartPointYA != null)
		{
			TargetStartPointYA.InniterBusiness();
		}
		if (TargetEndPointYA != null)
		{
			TargetEndPointYA.InniterBusiness();
		}
		if (ObjectStartPointYA != null)
		{
			ObjectStartPointYA.InniterBusiness();
		}
		if (ObjectEndPointYA != null)
		{
			ObjectEndPointYA.InniterBusiness();
		}
		if (ObjectAngleA != null)
		{
			ObjectAngleA.InniterBusiness();
		}
		if (TargetAngleA != null)
		{
			TargetAngleA.InniterBusiness();
		}
	}
}
