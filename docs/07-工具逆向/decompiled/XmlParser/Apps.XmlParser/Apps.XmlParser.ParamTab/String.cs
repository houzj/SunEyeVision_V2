using System;
using System.Collections.ObjectModel;
using System.Text;
using System.Windows.Input;
using Apps.Log;
using Hik.Util;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.Utility;

namespace Apps.XmlParser.ParamTab;

public class String : NameValueItem, ISubInfo
{
	private bool isInit;

	private bool IsSub;

	private ButtonSelecter _SubData;

	public string CurValue;

	public string DefaultValue;

	public int Length;

	public int MaxLen;

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
						if (CurValue.ByteCount() > MaxLen)
						{
							byte[] bytes = Encoding.UTF8.GetBytes(CurValue);
							string text = Encoding.UTF8.GetString(bytes, 0, MaxLen - 1);
							if (text.ByteCount() > MaxLen)
							{
								text = text.Substring(0, text.Length - 1);
							}
							CurValueA = text;
							ValueChangedCmdExecute();
						}
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

	public string ComType { get; set; } = "string";

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
				try
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
				catch (Exception ex)
				{
					LogHelper.Error(ex.Message);
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

	public string CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (MaxLen > 0 && value.ByteCount() > MaxLen && !IsSubA)
			{
				HikMessageBox.Show("Lang_ParamErr", "Lang_ContentLong", MessageIcons.Warning, isTranslate: true);
			}
			else if (CurValue != value)
			{
				CurValue = value;
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public string DefaultValueA
	{
		get
		{
			return DefaultValue;
		}
		set
		{
			DefaultValue = value;
		}
	}

	public int LengthA
	{
		get
		{
			return Length;
		}
		set
		{
			Length = value;
		}
	}

	public override string Value
	{
		get
		{
			return CurValueA;
		}
		set
		{
			CurValueA = value;
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

	protected virtual void ValueChangedCmdExecute()
	{
		if (!IsUnConnect() && OldValue != Value)
		{
			int num = SetParamUnit();
			OldValue = Value;
			if (num >= 0)
			{
				PreExecuteRun();
			}
		}
	}
}
