using System;
using Apps.Data;
using Apps.Localization;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class CommandSelecter_Frame : CommandSelecter
{
	private static object lockObj = new object();

	public int ImgCount;

	public int ImgCountA
	{
		get
		{
			return ImgCount;
		}
		set
		{
			lock (lockObj)
			{
				if (ImgCount != value)
				{
					ImgCount = value;
					OnPropertyChanged("ImgCountA");
				}
			}
		}
	}

	public override void OnInniterBusinessFinish()
	{
		FunctionCameraManager.Register("GetFrameMeanCount", GetFrameMeanCount);
	}

	private void GetFrameMeanCount()
	{
		GetParamUnit();
	}

	public override int GetParamUnit()
	{
		lock (lockObj)
		{
			try
			{
				MyItemState = ItemState.Normal;
				_ = string.Empty;
				VmModule vmModule = GetVmModule();
				if (vmModule == null)
				{
					return -1;
				}
				string text = vmModule.Params[Name];
				string text2 = "3";
				string[] array = text.ToString().Split('*');
				if (array.Length >= 2)
				{
					text2 = array[0];
					ImgCountA = Convert.ToInt32(array[1]);
				}
				try
				{
					OldValue = text2;
					Value = text2;
				}
				catch (Exception ex)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ParamGet") + ex.Message, MessageIcons.Error);
				}
				return 0;
			}
			catch (Exception)
			{
				return -1;
			}
		}
	}

	public bool GetMeanResultFlag()
	{
		bool result = false;
		try
		{
			_ = string.Empty;
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return false;
			}
			string[] array = vmModule.Params[Name].Split('*');
			if (array.Length >= 3)
			{
				result = (("1" == array[2]) ? true : false);
			}
			return result;
		}
		catch (Exception)
		{
			return false;
		}
	}
}
