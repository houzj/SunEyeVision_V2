using System;
using System.Collections.Generic;
using System.Windows.Input;
using Apps.Data;
using Apps.Log;
using Mvvm.Commands;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class Enumeration_ModuleSelector : Enumeration
{
	private DelegateCommand _ClickCommand = null;

	public override string DisplayType
	{
		get
		{
			return "EnumerationCamera";
		}
		set
		{
		}
	}

	public ICommand ClickCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand((Action)ClickCmdExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	public override void OnInniterBusinessFinish()
	{
		FunctionCameraManager.UnRegister("GetCamera");
		FunctionCameraManager.Register("GetCamera", GetCamera);
	}

	public void ClickCmdExecute()
	{
		OpenGlobalCamera();
	}

	public virtual bool OpenGlobalCamera()
	{
		try
		{
			FunctionManager.Execute("GlobalCameraSet");
			return true;
		}
		catch (Exception ex)
		{
			LogHelper.Error("function GlobalCameraSet execute error:" + ex.ToString());
			return false;
		}
	}

	public void GetCamera()
	{
		if (!(GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot))
		{
			return;
		}
		IEnumerable<NameValueItem> enumerable = algorithmTabRoot?.GetModuleSelector();
		if (enumerable == null)
		{
			return;
		}
		LogHelper.Info("GetCamera(): " + (algorithmTabRoot.Owner as VmModule).ID + " " + (algorithmTabRoot.Owner as VmModule).Name);
		foreach (NameValueItem item in enumerable)
		{
			item.InniterBusiness();
			item.GetParamUnit();
			item.OnPropertyChanged("CurValueA");
		}
	}

	public override void SelectedChangedCmdExecute(object data)
	{
		if (data == null)
		{
			return;
		}
		int num = (int)data;
		if (num >= 0 && !IsUnConnect() && OldValue != Value)
		{
			string oldValue = OldValue;
			int num2 = SetParamUnit();
			if (num2 >= 0 && (oldValue != null || IsSetValue))
			{
				PreExecuteRun();
			}
			OldValue = Value;
			FunctionCameraManager.Execute("GetCameraParam");
		}
	}
}
