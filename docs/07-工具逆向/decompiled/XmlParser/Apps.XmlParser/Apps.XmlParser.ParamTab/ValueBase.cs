using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Localization;
using Apps.Log;
using Hik.Util;
using Mvvm.Commands;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ValueBase : NameValueItem, ISubInfo, IGroupRunParam
{
	public delegate void IsSubChangedHandler(bool isSub);

	private bool isInit;

	private bool IsSub;

	private DelegateCommand _TypeChangeCommand = null;

	private ButtonSelecter _SubData;

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
			if (IsSub == value)
			{
				return;
			}
			IsSub = value;
			if (IsSub)
			{
				NameValueItem.logManager?.LogCreate("信息", AppLocalizationService.GetLang("Lang_SwitchSubscribe") ?? "");
			}
			else
			{
				NameValueItem.logManager?.LogCreate("信息", AppLocalizationService.GetLang("Lang_SwitchEdit") ?? "");
			}
			try
			{
				VmModule vmModule = GetVmModule();
				if (vmModule != null)
				{
					if (IsSub)
					{
						(SubData.InitersA[0] as Setter).Operate(new object[1] { SubData });
					}
					else if (_SubData != null && !string.IsNullOrEmpty(_SubData.CurValueA))
					{
						_SubData.CurValueA = null;
						IsGetFromModule = true;
						GetParamUnit();
						IsGetFromModule = false;
					}
					vmModule.UserData["@" + ComName] = StringUtil.UTF8GetBytesPadZero(IsSub.ToString());
				}
			}
			catch (Exception ex)
			{
				LogHelper.Error(ex.Message);
			}
			OnPropertyChanged("IsSubA");
		}
	}

	public ICommand TypeChangeCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_TypeChangeCommand == null)
			{
				_TypeChangeCommand = new DelegateCommand((Action)TypeChangeExecute);
			}
			return (ICommand)_TypeChangeCommand;
		}
	}

	public virtual string ComType { get; set; } = "int";

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
			else
			{
				string text = _SubData.CurValueA;
				if (IsSubscribeStr(text))
				{
					int num = text.IndexOf('[', 0);
					if (num > 0)
					{
						text = text.Remove(num);
					}
					VmModule vmModule2 = GetVmModule();
					if (vmModule2 != null)
					{
						VmIO ioByPathFromAround = vmModule2.GetIoByPathFromAround(text);
						if (ioByPathFromAround == null)
						{
							_SubData.CurValueA = null;
						}
					}
				}
			}
			return _SubData;
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
			bool flag = true;
			if (parentNode is Category category)
			{
				flag = category.IsDeleteSubscribeA;
				if (flag)
				{
					flag = IsDeleteSubscribeA;
				}
			}
			if ((!CustomVisible & flag) && _SubData != null && !string.IsNullOrEmpty(_SubData.CurValueA))
			{
				_SubData.CurValueA = null;
				IsGetFromModule = true;
				GetParamUnit();
				IsGetFromModule = false;
			}
			OnPropertyChanged("CustomVisibleA");
		}
	}

	public ICommand ValueChangedCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand((Action)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
		set
		{
			//IL_0003: Unknown result type (might be due to invalid IL or missing references)
			//IL_000d: Expected Obj, but got Unknown
			_ValueChangedCommand = (DelegateCommand)value;
		}
	}

	string IGroupRunParam.DisplayForGroupType => null;

	public event IsSubChangedHandler OnIsSubChanged;

	private void InitIsSub()
	{
		if (isInit)
		{
			return;
		}
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule != null)
			{
				byte[] array = vmModule.UserData["@" + ComName];
				if (array != null)
				{
					string value = StringUtil.UTF8GetString(array);
					if (bool.TryParse(value, out var result))
					{
						IsSubA = result;
					}
				}
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.Message);
		}
		isInit = true;
	}

	public void SetInit()
	{
		isInit = false;
		_SubData = null;
	}

	private void TypeChangeExecute()
	{
		OnIsSubChanged?.Invoke(IsSubA);
	}

	protected virtual bool CheckBeforSet()
	{
		return OldValue != Value;
	}

	protected virtual void ValueChangedCmdExecute()
	{
		if (!IsUnConnect() && CheckBeforSet())
		{
			int num = 0;
			num = ((!(GetParentNode() is ISubChild subChild)) ? SetParamUnit() : SetParamUnit(subChild as NameValueItem));
			if (num >= 0)
			{
				OldValue = Value;
				PreExecuteRun();
			}
		}
	}

	IDecorate IGroupRunParam.SupportDisplayForGroupData()
	{
		return null;
	}
}
