using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Localization;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;
using VMControls.BaseInterface;

namespace Apps.XmlParser.ParamTab;

public class SaveTextItem : GlobalVarItemBase
{
	public const int TYPE_CSV = 1;

	public string ColumnName;

	public Enumeration GlobalVarItemList;

	private DelegateCommand _SelectedChangedCommand = null;

	public bool IfOthers = true;

	private DelegateCommand _DeleteCommand = null;

	public string ColumnNameA
	{
		get
		{
			return ColumnName;
		}
		set
		{
			if (value.Contains("#"))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NameContainError") + " #", MessageIcons.Error, isTranslate: true);
			}
			else if (ColumnName != value)
			{
				ColumnName = value;
				OnPropertyChanged("ColumnNameA");
				SetParamUnit();
			}
		}
	}

	public Enumeration GlobalVarItemListA
	{
		get
		{
			if (GlobalVarItemList == null)
			{
				GlobalVarItemList = new Enumeration();
				GlobalVarItemList.Name = "GlobalVarItemList";
				GlobalVarItemList.AccessMode = "O";
				GlobalVarItemList.CurValue = 1;
				GlobalVarItemList.DefaultValue = 1;
				GlobalVarItemList.SetParentNode(this);
				ObservableCollection<object> enumEntrysA = new ObservableCollection<object>
				{
					new EnumEntry
					{
						DisplayNameA = "Lang_Time",
						ValueA = 0
					},
					new EnumEntry
					{
						DisplayNameA = "RunParm_Other",
						ValueA = 1
					}
				};
				GlobalVarItemList.EnumEntrysA = enumEntrysA;
			}
			GlobalVarItemList.SelectedChangedCommand = SelectedChangedCommand;
			return GlobalVarItemList;
		}
		set
		{
			GlobalVarItemList = value;
			OnPropertyChanged("GlobalVarItemListA");
		}
	}

	public ICommand SelectedChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_SelectedChangedCommand == null)
			{
				_SelectedChangedCommand = new DelegateCommand((Action)SelectedChangedCmdExecute);
			}
			return (ICommand)_SelectedChangedCommand;
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
			if (CustomVisible != value)
			{
				if (value)
				{
					CustomVisible = IsAddedA;
				}
				else
				{
					CustomVisible = value;
				}
				OnPropertyChanged("CustomVisibleA");
				ModifyDeleteBtnVisibility();
			}
		}
	}

	public bool IfOthersA
	{
		get
		{
			return IfOthers;
		}
		set
		{
			if (IfOthers != value)
			{
				IfOthers = value;
				GlobalVarItemListA.CurValueA = (IfOthers ? 1 : 0);
				OnPropertyChanged("IfOthersA");
				SetParamUnit();
			}
		}
	}

	public override bool IsAddedA
	{
		get
		{
			return IsAdded;
		}
		set
		{
			if (IsAdded != value)
			{
				IsAdded = value;
				OnPropertyChanged("IsAddedA");
				SetParamUnit();
			}
		}
	}

	public override ICommand DeleteCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (_DeleteCommand == null)
			{
				_DeleteCommand = new DelegateCommand((Action)DeleteCommandCmdExecute);
			}
			return (ICommand)_DeleteCommand;
		}
	}

	private void SelectedChangedCmdExecute()
	{
		if (!(IfOthersA = GlobalVarItemList.Value == "1"))
		{
			CurValueA = null;
		}
	}

	public override int SetParamUnit()
	{
		string text = ColumnNameA + "#" + IsAddedA + "#" + (IfOthersA ? "1" : "0");
		if (string.IsNullOrWhiteSpace(text))
		{
			return -2;
		}
		IParamsConfig moduleTagInfo = GetModuleTagInfo();
		int result = -1;
		if (moduleTagInfo != null)
		{
			try
			{
				((IUserStringData)moduleTagInfo)[Name] = text;
			}
			catch
			{
				OnSetValueCompleted(success: false);
				return result;
			}
		}
		OldValue = text;
		OnSetValueCompleted(success: true);
		return 0;
	}

	public override int GetParamUnit()
	{
		string empty = string.Empty;
		IParamsConfig moduleTagInfo = GetModuleTagInfo();
		int result = -1;
		if (moduleTagInfo != null)
		{
			try
			{
				empty = ((IUserStringData)moduleTagInfo)[Name];
				string text = empty;
				Check(text);
				if (ColumnNameA + "#" + IsAddedA + "#" + (IfOthersA ? "1" : "0") != text)
				{
					OldValue = text;
					ColumnName = text.Split('#')[0];
					IsAdded = text.Split('#')[1] == "True";
					if (IfOthers != (text.Split('#')[2] == "1"))
					{
						IfOthers = text.Split('#')[2] == "1";
						OnPropertyChanged("IfOthersA");
						GlobalVarItemListA.CurValueA = (IfOthers ? 1 : 0);
					}
					int num = -1;
					if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
					{
						BaseNode baseNode = algorithmTabRoot.FindBaseNodeByName("OutputType");
						if (baseNode != null && baseNode is RadioSelecterEx2)
						{
							num = (baseNode as RadioSelecterEx2).CurValueA;
						}
					}
					if (num == 1)
					{
						CustomVisibleA = IsAdded;
					}
					else
					{
						CustomVisibleA = false;
					}
				}
			}
			catch
			{
				OnSetValueCompleted(success: false);
				string lang = AppLocalizationService.GetLang("Lang_ParamGet");
				return result;
			}
			return 0;
		}
		return -1;
	}

	public override void DeleteCommandCmdExecute()
	{
		ResetItems();
	}

	public void ResetItems()
	{
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot && algorithmTabRoot.FindBaseNodeByName("btnAddGlobalVar") is AddButton_SaveText { GlobalVarItemSource: not null } addButton_SaveText && addButton_SaveText.GlobalVarItemSource.Contains(this))
		{
			int num = addButton_SaveText.GlobalVarItemSource.IndexOf(this);
			int curCount = addButton_SaveText.GetCurCount();
			for (int i = num; i <= curCount - 2; i++)
			{
				addButton_SaveText.GlobalVarItemSource[i].IsAddedA = addButton_SaveText.GlobalVarItemSource[i + 1].IsAddedA;
				addButton_SaveText.GlobalVarItemSource[i].IfOthersA = addButton_SaveText.GlobalVarItemSource[i + 1].IfOthersA;
				addButton_SaveText.GlobalVarItemSource[i].DropOpenSetterA.Value = addButton_SaveText.GlobalVarItemSource[i + 1].DropOpenSetterA.Value;
				addButton_SaveText.GlobalVarItemSource[i].CurValueA = addButton_SaveText.GlobalVarItemSource[i + 1].CurValueA;
				addButton_SaveText.GlobalVarItemSource[i].ColumnNameA = addButton_SaveText.GlobalVarItemSource[i + 1].ColumnNameA;
			}
			addButton_SaveText.GlobalVarItemSource[curCount - 1].CustomVisibleA = false;
			addButton_SaveText.GlobalVarItemSource[curCount - 1].CurValueA = null;
			addButton_SaveText.GlobalVarItemSource[curCount - 1].IsAddedA = false;
			addButton_SaveText.GlobalVarItemSource[curCount - 1].ColumnNameA = "column" + curCount;
			addButton_SaveText.GlobalVarItemSource[curCount - 1].IfOthersA = true;
		}
	}
}
