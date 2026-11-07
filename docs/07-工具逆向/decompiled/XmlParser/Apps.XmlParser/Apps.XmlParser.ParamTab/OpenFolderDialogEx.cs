using System;
using System.Collections.ObjectModel;
using Apps.Localization;
using Apps.Log;
using Hik.Util;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class OpenFolderDialogEx : OpenFolderDialog, ISubInfo
{
	private bool isInit = false;

	private bool IsSub = false;

	private ButtonSelecter _SubData;

	public override string DisplayType => "OpenFileDialogEx";

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
						GetParamUnit();
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
				GetParamUnit();
			}
			OnPropertyChanged("CustomVisibleA");
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
}
