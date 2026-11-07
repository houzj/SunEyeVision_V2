using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Hik.Util;
using VM.Controls;
using VM.Core;
using VM.Utility;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

[Obsolete("已废弃，请使用Integer数据类型")]
public class IntegerAffine : NameValueItem
{
	private bool isInit;

	private bool IsSub;

	private DelegateCommand _TypeChangeCommand = null;

	private ButtonSelecter _SubData;

	public int MinValue;

	public int MaxValue;

	public int IncValue = 1;

	public int CurValue;

	public int DefaultValue;

	public string Unit;

	private DelegateCommand _ValueChangedCommand = null;

	public bool IsSubA
	{
		get
		{
			InitIsSub();
			return IsSub;
		}
		set
		{
			if (IsSub != value)
			{
				IsSub = value;
				OnPropertyChanged("IsSubA");
			}
		}
	}

	public ICommand TypeChangeCommand
	{
		get
		{
			if (_TypeChangeCommand == null)
			{
				_TypeChangeCommand = new DelegateCommand(TypeChangeExecute);
			}
			return _TypeChangeCommand;
		}
	}

	public string ComType { get; set; } = "int";

	public ButtonSelecter SubData
	{
		get
		{
			if (_SubData == null)
			{
				_SubData = new ButtonSelecter();
				_SubData.ReadOnlyA = true;
				_SubData.DropOpenSetter = new Setter
				{
					TargetName = "EnumEntrys",
					OperationName = "GetFrontFilterTreeOperation",
					OperationParams = ComType
				};
				_SubData.DropOpenSetter.SetParentNode(_SubData);
				_SubData.Triggers = new ObservableCollection<object>();
				Trigger trigger = new Trigger
				{
					Property = "CurValue"
				};
				Setter setter = new Setter
				{
					OperationName = "SetModuleParamBindOperation",
					OperationParams = ComName
				};
				setter.CreatOperateInstance();
				trigger.Setters = new ObservableCollection<object>();
				trigger.Setters.Add(setter);
				setter.SetParentNode(trigger);
				_SubData.Triggers.Add(trigger);
				trigger.SetParentNode(_SubData);
				_SubData.Initers = new ObservableCollection<object>();
				Setter setter2 = new Setter
				{
					TargetName = "CurValue",
					OperationName = "GetModuleParamBindOperation",
					OperationParams = ComName
				};
				setter2.CreatOperateInstance();
				_SubData.Initers.Add(setter2);
				_SubData.SetParentNode(this);
				VmModule vmModule = GetVmModule();
				if (vmModule != null)
				{
					string moduleParamBinding = vmModule.ParamSubManager.GetModuleParamBinding(ComName);
					_SubData.SetPropertyValueByNameNotrigger("CurValueA", moduleParamBinding);
				}
			}
			return _SubData;
		}
	}

	public bool IsRuning
	{
		get
		{
			if (!(GetProcedure() is VmProcedure vmProcedure))
			{
				return true;
			}
			return vmProcedure.ContinuousRunEnable || ((IVMRun)VmSolution.Instance).ContinuousRunEnable;
		}
	}

	public int CurValueA
	{
		get
		{
			if (string.IsNullOrEmpty(OldValue))
			{
				OldValue = CurValue.ToString();
			}
			return CurValue;
		}
		set
		{
			if (CurValue == value)
			{
				return;
			}
			if (IsRuning)
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("当前流程处于忙碌状态!"), MessageIcons.Error);
				return;
			}
			if (value > MaxValue || value < MinValue)
			{
				CurValue = value;
			}
			else
			{
				int num = (value - MinValue) / IncValue;
				CurValue = num * IncValue + MinValue;
			}
			OnPropertyChanged("CurValueA");
		}
	}

	public int DefaultValueA
	{
		get
		{
			return DefaultValue;
		}
		set
		{
			DefaultValue = value;
			OnPropertyChanged("DefaultValueA");
		}
	}

	public string UnitA
	{
		get
		{
			return Unit;
		}
		set
		{
			Unit = value;
			OnPropertyChanged("UnitA");
		}
	}

	public int MinValueA
	{
		get
		{
			return MinValue;
		}
		set
		{
			MinValue = value;
			OnPropertyChanged("MinValueA");
		}
	}

	public int MaxValueA
	{
		get
		{
			return MaxValue;
		}
		set
		{
			MaxValue = value;
			OnPropertyChanged("MaxValueA");
		}
	}

	public int IncValueA
	{
		get
		{
			return IncValue;
		}
		set
		{
			IncValue = value;
			OnPropertyChanged("IncValueA");
		}
	}

	public override string Value
	{
		get
		{
			return CurValueA.ToString();
		}
		set
		{
			if (IsRuning)
			{
				VmModule vmModule = GetVmModule();
				if (vmModule != null)
				{
					string text = vmModule.Params[NameA];
					if (text == value)
					{
						CurValue = int.Parse(value);
						OnPropertyChanged("CurValueA");
					}
				}
			}
			else
			{
				int curValueA = int.Parse(value);
				CurValueA = curValueA;
			}
		}
	}

	public ICommand ValueChangedCommand
	{
		get
		{
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand(ValueChangedCmdExecute);
			}
			return _ValueChangedCommand;
		}
	}

	private void InitIsSub()
	{
		if (isInit)
		{
			return;
		}
		try
		{
			VmModule vmModule = GetVmModule();
			byte[] array = vmModule.UserData[vmModule.ID + "@" + NameA];
			if (array != null)
			{
				string value = StringUtil.UTF8GetString(array);
				if (bool.TryParse(value, out var result))
				{
					IsSubA = result;
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message);
		}
		isInit = true;
	}

	private void TypeChangeExecute()
	{
		try
		{
			IsSubA = !IsSubA;
			VmModule vmModule = GetVmModule();
			if (!IsSubA)
			{
				vmModule.ParamSubManager.DelModuleParamBinding(NameA, ComType);
			}
			else
			{
				(SubData.InitersA[0] as Setter).Operate(new object[1] { SubData });
			}
			vmModule.UserData[vmModule.ID + "@" + NameA] = StringUtil.UTF8GetBytesPadZero(IsSubA.ToString());
		}
		catch
		{
		}
	}

	protected void ValueChangedCmdExecute()
	{
		if (!IsUnConnect() && !IsRuning && OldValue != Value && SetParamUnit() >= 0)
		{
			OldValue = Value;
		}
	}

	public override void Check(string value)
	{
		RangeChecker rangeChecker = new RangeChecker();
		rangeChecker.Check(new object[2] { MinValue, MaxValue }, value, DisplayNameA);
	}

	private object GetProcedure()
	{
		VmModule vmModule = GetVmModule().Parent as VmModule;
		if (vmModule == null)
		{
			return null;
		}
		while (!(vmModule is VmProcedure))
		{
			vmModule = vmModule.Parent as VmModule;
			if (vmModule == null)
			{
				return null;
			}
		}
		return vmModule;
	}
}
