using System;
using System.IO;
using System.Linq;
using Apps.Localization;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class OpenFileForCNNUSDialog : OpenFileDialog
{
	private const int MODLE_MAX_VALUE = 524288000;

	private bool SetValueRisingEdge;

	public override string DisplayType
	{
		get
		{
			return GetType().BaseType.Name;
		}
		set
		{
		}
	}

	public bool SetValueRisingEdgeA
	{
		get
		{
			return SetValueRisingEdge;
		}
		set
		{
			if (SetValueRisingEdge != value)
			{
				SetValueRisingEdge = value;
				OnPropertyChanged("SetValueRisingEdgeA");
			}
		}
	}

	public OpenFileForCNNUSDialog()
	{
		SetValueCompleted += OpenFileForCNNDUSialog_SetValueCompleted;
	}

	private void OpenFileForCNNDUSialog_SetValueCompleted(object sender, EventArgs e)
	{
		if (e != null)
		{
			SetValueRisingEdge = false;
			SetValueRisingEdgeA = true;
		}
	}

	protected override void ValueChangedCmdExecute()
	{
		Boolean boolean = ((GetParentNode() is Category { ItemsA: var itemsA }) ? itemsA.FirstOrDefault((object p) => p is Boolean && (p as NameValueItem).Name == "SaveModelDataEnable") : null) as Boolean;
		if (boolean != null)
		{
			boolean.CustomVisibleA = true;
		}
		if (CurValueA == null)
		{
			return;
		}
		try
		{
			if (SetParamUnit() != 0)
			{
				return;
			}
			if (File.Exists(Value))
			{
				FileInfo fileInfo = new FileInfo(Value);
				if (boolean != null)
				{
					if (fileInfo.Length > 524288000)
					{
						if (boolean.CurValueA)
						{
							boolean.CurValueA = false;
						}
						boolean.CustomVisibleA = false;
					}
					else
					{
						boolean.CustomVisibleA = true;
					}
				}
			}
			PreExecuteRun();
		}
		catch (Exception ex)
		{
			string[] strArrMsg = new string[3]
			{
				AppLocalizationService.GetLang("Lang_SysErr"),
				":",
				ex.Message
			};
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_FileOpenFail"), strArrMsg, MessageIcons.Error);
		}
	}

	public override int GetParamUnit()
	{
		int paramUnit = base.GetParamUnit();
		Boolean boolean = ((GetParentNode() is Category { ItemsA: var itemsA }) ? itemsA.FirstOrDefault((object p) => p is Boolean && (p as NameValueItem).Name == "SaveModelDataEnable") : null) as Boolean;
		if (boolean != null)
		{
			boolean.CustomVisibleA = true;
		}
		if (File.Exists(Value))
		{
			FileInfo fileInfo = new FileInfo(Value);
			if (fileInfo.Length > 524288000 && boolean != null)
			{
				if (boolean.CurValueA)
				{
					boolean.CurValueA = false;
				}
				boolean.CustomVisibleA = false;
			}
		}
		return paramUnit;
	}
}
