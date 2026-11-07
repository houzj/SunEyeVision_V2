using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Apps.UIHelper.Extern;
using Mvvm.Commands;
using VM.Controls;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class PixelModuRangeConfigList : NameValueItem, IDynamicChild
{
	private AlgorithmTabRoot root;

	protected ObservableCollection<object> _ConfigList = new ObservableCollection<object>();

	private const string GetNumStr = "Threshold_nums";

	private const string MinStr = "min_Threshold_";

	private const string MaxStr = "max_Threshold_";

	private const string AddStr = "add_Threshold";

	private const int MaxNum = 32;

	internal const string DelStr = "del_Threshold_";

	private ICommand _AddCommand = null;

	private ICommand _DeleteCommand = null;

	private AlgorithmTabRoot Root
	{
		get
		{
			if (root == null)
			{
				root = GetTopBaseNode() as AlgorithmTabRoot;
			}
			return root;
		}
	}

	internal IParamsConfig UserStringData => Root.UserStringData;

	public ObservableCollection<object> ConfigList => _ConfigList;

	public ICommand AddCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = _AddCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)AddCommandCmdExecute);
				ICommand command2 = (ICommand)val;
				_AddCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public ICommand DeleteCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			ICommand command = _DeleteCommand;
			if (command == null)
			{
				DelegateCommand val = new DelegateCommand((Action)DeleteCommandExecute);
				ICommand command2 = (ICommand)val;
				_DeleteCommand = (ICommand)val;
				command = command2;
			}
			return command;
		}
	}

	public bool DelEnable => _ConfigList.Count > 1;

	public override int GetParamUnit()
	{
		if (_ConfigList.Count == 0)
		{
			IUserStringData userStringData = UserStringData;
			string text = userStringData["Threshold_nums"];
			if (!string.IsNullOrEmpty(text) && int.TryParse(text, out var result))
			{
				for (int i = 0; i < result; i++)
				{
					AddNewItem();
				}
			}
		}
		else
		{
			foreach (NameValueItem config in _ConfigList)
			{
				config.GetParamUnit();
			}
		}
		OnPropertyChanged("DelEnable");
		return 0;
	}

	protected virtual void AddNewItem()
	{
		PixelModuRangeConfigItem pixelModuRangeConfigItem = new PixelModuRangeConfigItem
		{
			MinValue = 0,
			MaxValue = 255,
			IncValue = 1
		};
		pixelModuRangeConfigItem.SetParentNode(this);
		pixelModuRangeConfigItem.Integers = new List<object>();
		Integer integer = new Integer
		{
			Name = string.Format("{0}{1}", "min_Threshold_", _ConfigList.Count),
			AccessMode = "RW",
			MinValue = 0,
			MaxValue = 255,
			IncValue = 1
		};
		integer.SetParentNode(pixelModuRangeConfigItem);
		pixelModuRangeConfigItem.Integers.Add(integer);
		integer = new Integer
		{
			Name = string.Format("{0}{1}", "max_Threshold_", _ConfigList.Count),
			AccessMode = "RW",
			MinValue = 0,
			MaxValue = 255,
			IncValue = 1
		};
		integer.SetParentNode(pixelModuRangeConfigItem);
		pixelModuRangeConfigItem.Integers.Add(integer);
		_ConfigList.Add(pixelModuRangeConfigItem);
		pixelModuRangeConfigItem.GetParamUnit();
		(pixelModuRangeConfigItem.Integers[0] as NameValueItem).DisplayName = pixelModuRangeConfigItem.DisplayNameA + "_min";
		(pixelModuRangeConfigItem.Integers[1] as NameValueItem).DisplayName = pixelModuRangeConfigItem.DisplayNameA + "_max";
	}

	internal void DelNewItem()
	{
		if (_ConfigList.Count > 0)
		{
			_ConfigList.RemoveAt(_ConfigList.Count - 1);
		}
	}

	private void AddCommandCmdExecute()
	{
		if (_ConfigList.Count >= 32)
		{
			HikMessageBox.Show(string.Format("{0}{1}", AppLocalizationService.GetLang("Lang_NumOutErr"), 32), MessageIcons.Warning);
			return;
		}
		IUserStringData userStringData = UserStringData;
		try
		{
			userStringData["add_Threshold"] = "Add";
		}
		catch (VmException vmException)
		{
			ShowMessage.ShowVmExceptionMessage(vmException);
			return;
		}
		AddNewItem();
		(_ConfigList.Last() as NameValueItem).GetParamUnit();
		OnPropertyChanged("DelEnable");
	}

	private void DeleteCommandExecute()
	{
		if (_ConfigList.Count == 0)
		{
			HikMessageBox.Show(string.Format("{0}", AppLocalizationService.GetLang("Lang_DeleteFail")), MessageIcons.Warning);
			return;
		}
		IUserStringData userStringData = UserStringData;
		try
		{
			userStringData[string.Format("{0}{1}", "del_Threshold_", _ConfigList.Count - 1)] = "Del";
		}
		catch (VmException vmException)
		{
			ShowMessage.ShowVmExceptionMessage(vmException);
			return;
		}
		DelNewItem();
		OnPropertyChanged("DelEnable");
	}

	IList IDynamicChild.GetDynamicChild()
	{
		List<object> list = null;
		if (_ConfigList.Count == 0)
		{
			GetParamUnit();
		}
		return new List<object>(_ConfigList);
	}
}
