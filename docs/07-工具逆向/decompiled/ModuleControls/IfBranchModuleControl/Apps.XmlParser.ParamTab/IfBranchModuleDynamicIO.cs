using System;
using System.Collections.ObjectModel;
using Apps.Interface;
using Apps.Log;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class IfBranchModuleDynamicIO : DynamicIOContainer<IfBranchDynamicIO>
{
	private int _selectTypeIndex;

	private bool _isAllowSet = true;

	private int _decimalDigits;

	public ObservableCollection<object> EnumEntrys;

	public int SelectTypeIndex
	{
		get
		{
			return _selectTypeIndex;
		}
		set
		{
			_selectTypeIndex = value;
			OnPropertyChanged("SelectTypeIndex");
		}
	}

	public bool IsAllowSet
	{
		get
		{
			return _isAllowSet;
		}
		set
		{
			if (_isAllowSet != value)
			{
				_isAllowSet = value;
				OnPropertyChanged("IsAllowSet");
			}
		}
	}

	public int DecimalDigits
	{
		get
		{
			return _decimalDigits;
		}
		set
		{
			if (_decimalDigits != value)
			{
				_decimalDigits = value;
				OnPropertyChanged("DecimalDigits");
			}
		}
	}

	public new ObservableCollection<object> EnumEntrysA
	{
		get
		{
			if (EnumEntrys == null)
			{
				EnumEntrys = new ObservableCollection<object>
				{
					new EnumEntry
					{
						Description = "int",
						DisplayName = "int",
						StrValue = "int"
					},
					new EnumEntry
					{
						Description = "float",
						DisplayName = "float",
						StrValue = "float"
					}
				};
			}
			return EnumEntrys;
		}
	}

	public override string DefaultTypeName
	{
		get
		{
			if (SelectTypeIndex != 0)
			{
				return "float";
			}
			return "int";
		}
	}

	public override string DefaultName
	{
		get
		{
			if (SelectTypeIndex != 0)
			{
				return "float";
			}
			return "int";
		}
	}

	public IfBranchModuleDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	public override object AddIO(string name = null, string type = null, int index = -1)
	{
		type = ((SelectTypeIndex == 0) ? "int" : "float");
		return base.AddIO(name, type, index);
	}

	public override void RemoveIO(object dymaicIO)
	{
		base.RemoveIO(dymaicIO);
	}

	public void InitSource(int decimaldigit)
	{
		DecimalDigits = decimaldigit;
		GetParam();
	}

	public void GetParam()
	{
		IsAllowSet = false;
		if (Owner == null)
		{
			return;
		}
		byte[] array = null;
		try
		{
			array = ((IUserBytesData)(Owner as VmModule).Params)["IfBranchText"];
		}
		catch (Exception ex)
		{
			LogHelper.Error("获取IfBranchText失败：" + ex.Message);
			IsAllowSet = true;
			return;
		}
		string[] array2 = StringUtil.UTF8GetString(array).Split(';');
		if (array2.Length >= 1)
		{
			foreach (IfBranchDynamicIO item in ItemsA)
			{
				if (!item.CustomVisibleA)
				{
					continue;
				}
				try
				{
					string[] array3 = array2;
					for (int i = 0; i < array3.Length; i++)
					{
						string[] array4 = array3[i].Split(':');
						if (array4.Length > 4 && array4[0] == item.NameA)
						{
							(item.Floatbetween.FloatsA[0] as Float).Value = array4[1];
							(item.Floatbetween.FloatsA[1] as Float).Value = array4[2];
							item.State.CurValueA = int.Parse(array4[3]);
							if (array4[4] == "")
							{
								item.RunModuleId = -1;
							}
							else
							{
								item.RunModuleId = int.Parse(array4[4]);
							}
						}
					}
				}
				catch (Exception)
				{
				}
			}
		}
		IsAllowSet = true;
	}
}
