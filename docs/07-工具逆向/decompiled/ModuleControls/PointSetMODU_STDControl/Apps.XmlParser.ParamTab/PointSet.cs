using System;
using System.Linq;
using System.Text;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Mvvm.Commands;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class PointSet : ButtonSelecter
{
	private new string CurValue;

	public bool CanDelete = true;

	public int DisplayIndex;

	public bool IsPointCoordinate = false;

	public bool IsPoint = true;

	public ButtonSelecter XCoordButtonSelecter;

	public ButtonSelecter YCoordButtonSelecter;

	public bool isGet = false;

	private double _langparamwidthch = 89.0;

	private double _langparamwidthen = 170.0;

	private DelegateCommand _DeleteCommand = null;

	public double LangParamWidth
	{
		get
		{
			if ("English" == AppLocalizationService.GetLang("Language"))
			{
				return _langparamwidthen;
			}
			return _langparamwidthch;
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

	public bool IsPointCoordinateA
	{
		get
		{
			return IsPointCoordinate;
		}
		set
		{
			IsPointCoordinate = value;
			OnPropertyChanged("IsPointCoordinateA");
		}
	}

	public bool IsPointA
	{
		get
		{
			return IsPoint;
		}
		set
		{
			IsPoint = value;
			OnPropertyChanged("IsPointA");
		}
	}

	public ButtonSelecter XCoordButtonSelecterA
	{
		get
		{
			return XCoordButtonSelecter;
		}
		set
		{
			XCoordButtonSelecter = value;
			OnPropertyChanged("XCoordButtonSelecterA");
		}
	}

	public ButtonSelecter YCoordButtonSelecterA
	{
		get
		{
			return YCoordButtonSelecter;
		}
		set
		{
			YCoordButtonSelecter = value;
			OnPropertyChanged("YCoordButtonSelecterA");
		}
	}

	public override string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			if (IsPointCoordinateA && string.IsNullOrWhiteSpace(CurValue))
			{
				if (XCoordButtonSelecter != null)
				{
					XCoordButtonSelecter.SetPropertyValueByNameNotrigger("CurValueA", null);
				}
				if (YCoordButtonSelecter != null)
				{
					YCoordButtonSelecter.SetPropertyValueByNameNotrigger("CurValueA", null);
				}
			}
			OnPropertyChanged("CurValueA");
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
			if (CustomVisible == value)
			{
				return;
			}
			CustomVisible = value;
			OnPropertyChanged("CustomVisibleA");
			ModifyDisplayIndex(isDelete: false);
			ModifyDeleteBtnVisibility();
			SetParamUnit();
			VmModule vmModule = GetVmModule();
			if (vmModule != null && !isGet)
			{
				string text = (IsPointA ? "1" : "0");
				if (CustomVisible)
				{
					vmModule.UserData[Name] = StringUtil.UTF8GetBytesPadZero("1," + text + "," + DisplayIndexA);
				}
				else
				{
					vmModule.UserData[Name] = StringUtil.UTF8GetBytesPadZero("0," + text + ",0");
				}
			}
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
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_DeleteCommand == null)
			{
				_DeleteCommand = new DelegateCommand((Action)DeleteCommandCmdExecute);
			}
			return (ICommand)_DeleteCommand;
		}
	}

	private void ModifyDisplayIndex(bool isDelete)
	{
		if (!(GetParentNode() is Category { ItemsA: var itemsA }))
		{
			return;
		}
		PointSet[] array = (from PointSet p in itemsA.Where((object p) => p is PointSet)
			where p.CustomVisibleA
			select p).ToArray();
		VmModule vmModule = GetVmModule();
		if (isDelete)
		{
			for (int num = 0; num < array.Length; num++)
			{
				string text = (array[num].IsPointA ? "1" : "0");
				if (array[num].CustomVisible)
				{
					array[num].DisplayIndexA = num + 1;
					if (vmModule != null)
					{
						vmModule.UserData[array[num].Name] = StringUtil.UTF8GetBytesPadZero("1," + text + "," + array[num].DisplayIndexA);
					}
				}
			}
		}
		else
		{
			string text2 = (IsPointA ? "1" : "0");
			DisplayIndexA = array.Count();
		}
	}

	public override void InniterBusiness()
	{
		base.InniterBusiness();
		if (XCoordButtonSelecter != null)
		{
			XCoordButtonSelecter.InniterBusiness();
		}
		if (YCoordButtonSelecter != null)
		{
			YCoordButtonSelecter.InniterBusiness();
		}
	}

	private void ModifyDeleteBtnVisibility()
	{
		if (!(GetParentNode() is Category { ItemsA: var itemsA }))
		{
			return;
		}
		PointSet[] array = (from PointSet p in itemsA.Where((object p) => p is PointSet)
			where p.CustomVisibleA
			select p).ToArray();
		if (array.Length > 1)
		{
			for (int num = 0; num < array.Length; num++)
			{
				array[num].CanDeleteA = true;
			}
		}
		else if (array.Length == 1)
		{
			array[0].CanDeleteA = false;
		}
	}

	private void DeleteCommandCmdExecute()
	{
		if (!(GetParentNode() is Category { ItemsA: var itemsA }))
		{
			return;
		}
		PointSet[] array = (from PointSet p in itemsA.Where((object p) => p is PointSet)
			where p.CustomVisibleA
			select p).ToArray();
		if (DisplayIndexA != array.Length)
		{
			for (int num = DisplayIndexA; num < array.Length; num++)
			{
				if (array[num - 1].XCoordButtonSelecterA != null && array[num - 1].XCoordButtonSelecterA.CurValueA != null)
				{
					array[num - 1].XCoordButtonSelecterA.CurValueA = null;
				}
				if (array[num - 1].YCoordButtonSelecterA != null && array[num - 1].YCoordButtonSelecterA.CurValueA != null)
				{
					array[num - 1].YCoordButtonSelecterA.CurValueA = null;
				}
				array[num - 1].CustomVisibleA = false;
				array[num - 1].CurValueA = null;
				array[num - 1].IsPointA = true;
				array[num - 1].IsPointCoordinateA = false;
				array[num - 1].IsPointA = array[num].IsPointA;
				array[num - 1].IsPointCoordinateA = array[num].IsPointCoordinateA;
				array[num - 1].DisplayIndexA = array[num].DisplayIndexA;
				array[num - 1].CurValueA = (array[num - 1].IsPointA ? array[num].CurValueA : null);
				string curValueA = "";
				string curValueA2 = "";
				if (array[num - 1].XCoordButtonSelecterA != null && array[num].XCoordButtonSelecterA != null)
				{
					curValueA = array[num].XCoordButtonSelecterA.CurValueA;
				}
				if (array[num - 1].YCoordButtonSelecterA != null && array[num].YCoordButtonSelecterA != null)
				{
					curValueA2 = array[num].YCoordButtonSelecterA.CurValueA;
				}
				if (array[num - 1].XCoordButtonSelecterA == null)
				{
					array[num - 1].XCoordButtonSelecterA = new ButtonSelecter(this, "float", "PointX" + num);
					array[num - 1].XCoordButtonSelecterA.AccessModeA = "O";
					array[num - 1].XCoordButtonSelecterA.ReadOnlyA = false;
				}
				if (array[num - 1].YCoordButtonSelecterA == null)
				{
					array[num - 1].YCoordButtonSelecterA = new ButtonSelecter(this, "float", "PointY" + num);
					array[num - 1].XCoordButtonSelecterA.AccessModeA = "O";
					array[num - 1].XCoordButtonSelecterA.ReadOnlyA = false;
				}
				array[num - 1].XCoordButtonSelecterA.CurValueA = curValueA;
				array[num - 1].YCoordButtonSelecterA.CurValueA = curValueA2;
				array[num - 1].CustomVisibleA = true;
				if (array[num].XCoordButtonSelecterA != null && array[num].XCoordButtonSelecterA.CurValueA != null)
				{
					array[num].XCoordButtonSelecterA.CurValueA = null;
				}
				if (array[num].YCoordButtonSelecterA != null && array[num].YCoordButtonSelecterA.CurValueA != null)
				{
					array[num].YCoordButtonSelecterA.CurValueA = null;
				}
				array[num].CurValueA = null;
				array[num].IsPointA = true;
				array[num].IsPointCoordinateA = false;
				array[num].CustomVisibleA = false;
				array[num].DisplayIndexA = 0;
			}
		}
		else
		{
			if (XCoordButtonSelecterA != null && XCoordButtonSelecterA.CurValueA != null)
			{
				XCoordButtonSelecterA.CurValueA = null;
			}
			if (YCoordButtonSelecterA != null && YCoordButtonSelecterA.CurValueA != null)
			{
				YCoordButtonSelecterA.CurValueA = null;
			}
			CustomVisibleA = false;
			CurValueA = null;
			IsPointA = true;
			IsPointCoordinateA = false;
		}
		ModifyDisplayIndex(isDelete: true);
	}

	public override string ToString()
	{
		return Name + "_IsPoint[" + IsPointA + "]_IsPointCoordinate[" + IsPointCoordinateA + "]]_CustomVisible[" + CustomVisibleA + "]";
	}

	public override int GetParamUnit()
	{
		byte[] array = GetVmModule()?.UserData[NameA];
		if (array == null)
		{
			array = Encoding.UTF8.GetBytes("0,1,1");
		}
		string text = StringUtil.UTF8GetString(array);
		string[] array2 = text.Split(',');
		string text2 = array2[0];
		if (text2 == "1" || !CanDeleteA)
		{
			if (int.Parse(array2[2]) > 0)
			{
				isGet = true;
				CustomVisibleA = true;
				IsPointA = array2[1] == "1";
				IsPointCoordinateA = !IsPointA;
				DisplayIndexA = int.Parse(array2[2]);
				isGet = false;
			}
			else
			{
				CustomVisibleA = false;
			}
		}
		else
		{
			IParamsConfig moduleTagInfo = GetModuleTagInfo();
			if (moduleTagInfo != null)
			{
				string text3 = ((IUserStringData)moduleTagInfo)[Name];
				if (text3 != null && text3.Contains("#"))
				{
					string[] array3 = text3.Split('#');
					if (array3[2] == "1")
					{
						CustomVisibleA = true;
					}
					else
					{
						CustomVisibleA = false;
					}
					return 0;
				}
			}
			CustomVisibleA = false;
		}
		return 0;
	}

	public override int SetParamUnit()
	{
		string text = Name + "#";
		try
		{
			if (Triggers == null || Triggers.Count == 0)
			{
				return -2;
			}
			if (!(Triggers[0] is Trigger) || (Triggers[0] as Trigger).Setters.Count == 0)
			{
				return -2;
			}
			text += ((Triggers[0] as Trigger).Setters[0] as Setter).OperationParams;
			text = ((!CustomVisible) ? (text + "#0") : (text + "#1"));
		}
		catch (Exception)
		{
			return -2;
		}
		MyItemState = ItemState.Normal;
		IParamsConfig moduleTagInfo = GetModuleTagInfo();
		if (moduleTagInfo == null)
		{
			return -1;
		}
		int result = -1;
		try
		{
			((IUserStringData)moduleTagInfo)[Name] = text;
			string text2 = ((IUserStringData)moduleTagInfo)[Name];
		}
		catch (Exception)
		{
			return result;
		}
		return 0;
	}
}
