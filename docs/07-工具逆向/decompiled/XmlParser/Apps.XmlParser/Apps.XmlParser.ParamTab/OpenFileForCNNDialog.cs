using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Windows.Input;
using Apps.Localization;
using Mvvm.Commands;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class OpenFileForCNNDialog : OpenFileDialog
{
	public List<EnumEntry> Msgs;

	private bool SetValueRisingEdge;

	public bool IsLoadModelFailed;

	private ICommand _MsgCopyCommand = null;

	public List<EnumEntry> MsgsA => Msgs;

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

	public ICommand MsgCopyCommand => _MsgCopyCommand ?? (_MsgCopyCommand = (ICommand)new DelegateCommand<object>((Action<object>)MsgCopyExecute));

	public OpenFileForCNNDialog()
	{
		SetValueCompleted += OpenFileForCNNDialog_SetValueCompleted;
		PropertyChanged += OpenFileForCNNDialog_PropertyChanged;
	}

	private void OpenFileForCNNDialog_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		if (e.PropertyName == "CurValueA" && !string.IsNullOrEmpty(CurValueA) && !IsLoadModelFailed)
		{
			SetValueRisingEdge = false;
			SetValueRisingEdgeA = true;
		}
	}

	private void OpenFileForCNNDialog_SetValueCompleted(object sender, EventArgs e)
	{
		if (e != null)
		{
			SetValueRisingEdge = false;
			SetValueRisingEdgeA = true;
		}
	}

	protected override void ValueChangedCmdExecute()
	{
		if (CurValueA == null)
		{
			return;
		}
		try
		{
			if (SetParamUnit() != 0)
			{
				IsLoadModelFailed = true;
			}
			else
			{
				PreExecuteRun();
			}
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

	private void MsgCopyExecute(object obj)
	{
		if (obj is EnumEntry enumEntry)
		{
			CurValueA = enumEntry.StrValue;
		}
	}
}
