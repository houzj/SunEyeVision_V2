using System;
using System.Linq;
using System.Windows.Input;
using Mvvm.Commands;

namespace Apps.XmlParser.ParamTab;

public class PointPair : NameValueItem
{
	private DelegateCommand _deleteCommand;

	public bool CanDelete = false;

	public bool Fixed = false;

	public int DisplayIndex;

	public ButtonSelecter ObjectPoint;

	public ButtonSelecter TargetPoint;

	public ButtonSelecter TargetPointX;

	public ButtonSelecter ObjectPointX;

	public ButtonSelecter TargetPointY;

	public ButtonSelecter ObjectPointY;

	public ButtonSelecter ObjectPointAngle;

	public ButtonSelecter TargetPointAngle;

	public bool IsPointMode = true;

	public bool HasAngle;

	public ButtonSelecter ObjectPointA
	{
		get
		{
			return ObjectPoint;
		}
		set
		{
			ObjectPoint = value;
			OnPropertyChanged("ObjectPointA");
		}
	}

	public ButtonSelecter TargetPointA
	{
		get
		{
			return TargetPoint;
		}
		set
		{
			TargetPoint = value;
			OnPropertyChanged("TargetPointA");
		}
	}

	public ButtonSelecter ObjectPointXA
	{
		get
		{
			return ObjectPointX;
		}
		set
		{
			ObjectPointX = value;
			OnPropertyChanged("ObjectPointXA");
		}
	}

	public ButtonSelecter ObjectPointYA
	{
		get
		{
			return ObjectPointY;
		}
		set
		{
			ObjectPointY = value;
			OnPropertyChanged("ObjectPointYA");
		}
	}

	public ButtonSelecter TargetPointXA
	{
		get
		{
			return TargetPointX;
		}
		set
		{
			TargetPointX = value;
			OnPropertyChanged("TargetPointXA");
		}
	}

	public ButtonSelecter TargetPointYA
	{
		get
		{
			return TargetPointY;
		}
		set
		{
			TargetPointY = value;
			OnPropertyChanged("TargetPointYA");
		}
	}

	public ButtonSelecter ObjectPointAngleA
	{
		get
		{
			return ObjectPointAngle;
		}
		set
		{
			ObjectPointAngle = value;
			OnPropertyChanged("ObjectPointAngleA");
		}
	}

	public ButtonSelecter TargetPointAngleA
	{
		get
		{
			return TargetPointAngle;
		}
		set
		{
			TargetPointAngle = value;
			OnPropertyChanged("TargetPointAngleA");
		}
	}

	public bool IsPointModeA
	{
		get
		{
			return IsPointMode;
		}
		set
		{
			IsPointMode = value;
			if (IsPointMode)
			{
				TargetPoint.InniterBusiness();
				ObjectPoint.InniterBusiness();
			}
			else
			{
				TargetPointX.InniterBusiness();
				TargetPointY.InniterBusiness();
				if (TargetPointAngle != null)
				{
					TargetPointAngle.InniterBusiness();
				}
				ObjectPointX.InniterBusiness();
				ObjectPointY.InniterBusiness();
				if (ObjectPointAngle != null)
				{
					ObjectPointAngle.InniterBusiness();
				}
			}
			OnPropertyChanged("IsPointModeA");
		}
	}

	public bool HasAngleA
	{
		get
		{
			return HasAngle;
		}
		set
		{
			HasAngle = value;
			OnPropertyChanged("HasAngleA");
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
			GetParamUnit();
			try
			{
				if (Value != null)
				{
					bool flag = true;
					flag = (("1" == Value.Substring(Value.Length - 1, 1)) ? true : false);
					if (CustomVisible != flag)
					{
						CustomVisible = flag;
						ModifyDisplayIndex();
						ModifyDeleteBtnVisibility();
					}
				}
			}
			catch (Exception)
			{
			}
			return CustomVisible;
		}
		set
		{
			if (CustomVisible != value)
			{
				CustomVisible = value;
				if (CustomVisible)
				{
					Value = Name + "#1";
				}
				else
				{
					Value = Name + "#0";
				}
				SetParamUnit();
				OnPropertyChanged("CustomVisibleA");
				ModifyDisplayIndex();
				ModifyDeleteBtnVisibility();
			}
		}
	}

	public PointPair()
	{
		//IL_002a: Unknown result type (might be due to invalid IL or missing references)
		//IL_0034: Expected Obj, but got Unknown
		DeleteCommand = (ICommand)new DelegateCommand((Action)DeleteCommandCmdExecute);
	}

	private void ModifyDisplayIndex()
	{
		if (GetParentNode() is Category category)
		{
			PointPair[] array = (from p in category.ItemsA.OfType<PointPair>()
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
		PointPair[] array = (from p in category.ItemsA.OfType<PointPair>()
			where p.CustomVisibleA
			select p).ToArray();
		if (array.Length > 1)
		{
			PointPair[] array2 = array;
			foreach (PointPair pointPair in array2)
			{
				if (!pointPair.Fixed)
				{
					pointPair.CanDeleteA = true;
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
		if (ObjectPointA != null)
		{
			ObjectPointA.CurValueA = null;
		}
		if (TargetPointA != null)
		{
			TargetPointA.CurValueA = null;
		}
		if (ObjectPointXA != null)
		{
			ObjectPointXA.CurValueA = null;
		}
		if (TargetPointXA != null)
		{
			TargetPointXA.CurValueA = null;
		}
		if (ObjectPointYA != null)
		{
			ObjectPointYA.CurValueA = null;
		}
		if (TargetPointYA != null)
		{
			TargetPointYA.CurValueA = null;
		}
		if (TargetPointAngleA != null)
		{
			TargetPointAngleA.CurValueA = null;
		}
		if (ObjectPointAngleA != null)
		{
			ObjectPointAngleA.CurValueA = null;
		}
		CustomVisibleA = false;
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		if (ObjectPoint != null)
		{
			ObjectPoint.InniterBusiness();
		}
		if (TargetPoint != null)
		{
			TargetPoint.InniterBusiness();
		}
		if (ObjectPointX != null)
		{
			ObjectPointX.InniterBusiness();
		}
		if (TargetPointX != null)
		{
			TargetPointX.InniterBusiness();
		}
		if (ObjectPointY != null)
		{
			ObjectPointY.InniterBusiness();
		}
		if (TargetPointY != null)
		{
			TargetPointY.InniterBusiness();
		}
		if (ObjectPointAngle != null)
		{
			ObjectPointAngle.InniterBusiness();
		}
		if (TargetPointAngle != null)
		{
			TargetPointAngle.InniterBusiness();
		}
	}
}
