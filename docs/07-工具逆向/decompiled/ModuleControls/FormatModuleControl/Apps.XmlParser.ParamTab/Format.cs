using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Apps.UIHelper.Extern;
using Apps.XmlParser.Variable;
using FormatModuleControl;
using Hik.Util;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VM.Utility;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class Format : NameValueItem, IBottomCommand
{
	private class FMapping
	{
		public string filterString;

		public int index;

		public string valuetype;
	}

	public Setter DropOpenSetter;

	private List<VmIO> filterList;

	private ObservableCollection<FormatRow> formatRows = new ObservableCollection<FormatRow>();

	private FormatRow currentRow = null;

	private FormatTextInfo currentInfo = null;

	private ComboxEditForFormat delimiter;

	private ComboxEditForFormat arrayDelimiter;

	private const string ParamNamePattern = "<(int|float|string)\\:var\\d*\\(%(\\d+d|\\d+\\.\\d+f|s)\\)>";

	private List<FMapping> fmapList = new List<FMapping>();

	private List<string> filterStringList = new List<string>();

	private IList EnumEntrys;

	private Setter IndexOpenSetter = new Setter
	{
		TargetName = "EnumEntrys",
		OperationName = "GetFrontFilterTreeOperation",
		OperationParams = "int"
	};

	private DelegateCommand _DropDownCommand = null;

	private DelegateCommand<ObjectEventArgs> _ClickCommand = null;

	private DelegateCommand<ObjectEventArgs> _AddTextCommand = null;

	private DelegateCommand<ObjectEventArgs> _AddPairCommand = null;

	private DelegateCommand<ObjectEventArgs> _ClearCommand = null;

	private DelegateCommand<ObjectEventArgs> _AddRowCommand = null;

	private DelegateCommand<ObjectEventArgs> _MoveUpCommand = null;

	private DelegateCommand<ObjectEventArgs> _MoveDownCommand = null;

	private DelegateCommand<ObjectEventArgs> _MoveLeftCommand = null;

	private DelegateCommand<ObjectEventArgs> _MoveRightCommand = null;

	private bool canMoveUp;

	private bool canMoveDown;

	private bool canMoveLeft;

	private bool canMoveRight;

	private const int MaxLen = 4096;

	private string _tempvalue;

	private bool _IsDropDownOpen;

	private DelegateCommand<object> _PreviewMouseWheelCommand = null;

	private bool hasRegistered;

	private DelegateCommand<KeyEventArgs> _KeyDownCommand = null;

	private string message = string.Empty;

	private bool result = false;

	private Visibility isShowMessageBox = System.Windows.Visibility.Collapsed;

	private bool IsTextOk = false;

	private DelegateCommand popSubscribeWindowCommand = null;

	public override string DisplayType => "FormatConfig";

	public Setter DropOpenSetterA
	{
		get
		{
			return DropOpenSetter;
		}
		set
		{
			DropOpenSetter = value;
			OnPropertyChanged("DropOpenSetterA");
		}
	}

	public List<VmIO> FilterList => filterList;

	public ObservableCollection<FormatRow> FormatRows
	{
		get
		{
			return formatRows;
		}
		set
		{
			if (formatRows != value)
			{
				formatRows = value;
				OnPropertyChanged("FormatRows");
			}
		}
	}

	public FormatRow CurrentRow
	{
		get
		{
			return currentRow;
		}
		set
		{
			if (currentRow == value)
			{
				return;
			}
			currentRow = value;
			OnPropertyChanged("CurrentRow");
			OnPropertyChanged("CanAdd");
			if (currentRow == null)
			{
				CanMoveUp = false;
				CanMoveDown = false;
				foreach (FormatRow formatRow in FormatRows)
				{
					foreach (FormatTextInfo item in formatRow.FormatInfoCollection)
					{
						item.IsFocus = false;
					}
				}
			}
			else
			{
				if (FormatRows.IndexOf(currentRow) == 0)
				{
					CanMoveUp = false;
				}
				else
				{
					CanMoveUp = true;
				}
				if (FormatRows.IndexOf(currentRow) == FormatRows.Count - 1)
				{
					CanMoveDown = false;
				}
				else
				{
					CanMoveDown = true;
				}
				foreach (FormatRow formatRow2 in FormatRows)
				{
					if (formatRow2 != currentRow)
					{
						foreach (FormatTextInfo item2 in formatRow2.FormatInfoCollection)
						{
							item2.IsFocus = false;
						}
						continue;
					}
					foreach (FormatTextInfo item3 in formatRow2.FormatInfoCollection)
					{
						item3.IsFocus = true;
					}
				}
			}
			ResetSelectedSubscribeParam();
		}
	}

	public FormatTextInfo CurrentInfo
	{
		get
		{
			return currentInfo;
		}
		set
		{
			if (currentInfo != value)
			{
				currentInfo = value;
				OnPropertyChanged("CurrentInfo");
				foreach (FormatRow formatRow2 in FormatRows)
				{
					foreach (FormatTextInfo item in formatRow2.FormatInfoCollection)
					{
						item.IsFocus = false;
					}
				}
				if (currentInfo != null)
				{
					currentInfo.IsFocus = true;
				}
			}
			OnPropertyChanged("CanAdd");
			if (currentInfo == null)
			{
				CanMoveLeft = false;
				CanMoveRight = false;
				return;
			}
			FormatRow formatRow = currentInfo.GetParentNode<FormatRow>();
			if (formatRow == null)
			{
				CanMoveLeft = false;
				CanMoveRight = false;
				return;
			}
			if (formatRow.FormatInfoCollection.IndexOf(currentInfo) == 0)
			{
				CanMoveLeft = false;
			}
			else
			{
				CanMoveLeft = true;
			}
			if (formatRow.FormatInfoCollection.IndexOf(currentInfo) == formatRow.FormatInfoCollection.Count - 1)
			{
				CanMoveRight = false;
			}
			else
			{
				CanMoveRight = true;
			}
		}
	}

	public ComboxEditForFormat Delimiter
	{
		get
		{
			if (delimiter == null)
			{
				delimiter = new ComboxEditForFormat
				{
					DisplayName = "RunParam_Split Symbol",
					Description = "RunParam_Split Symbol",
					AccessMode = "RW",
					CurValue = ";"
				};
				delimiter.EnumEntrysA = new ObservableCollection<object>
				{
					new EnumEntryStr
					{
						Description = "Dot",
						DisplayName = "RunParam_Dot",
						Value = ","
					},
					new EnumEntryStr
					{
						Description = "Colon",
						DisplayName = "RunParam_Colon",
						Value = ":"
					},
					new EnumEntryStr
					{
						Description = "Title",
						DisplayName = "RunParam_Title",
						Value = ";"
					},
					new EnumEntryStr
					{
						Description = "Underline",
						DisplayName = "RunParam_Underline",
						Value = "_"
					}
				};
			}
			return delimiter;
		}
	}

	public ComboxEditForFormat ArrayDelimiter
	{
		get
		{
			if (arrayDelimiter == null)
			{
				arrayDelimiter = new ComboxEditForFormat
				{
					DisplayName = "RunParam_Array Split Symbol",
					Description = "RunParam_Array Split Symbol",
					AccessMode = "RW",
					CurValue = "()"
				};
				arrayDelimiter.EnumEntrysA = new ObservableCollection<object>
				{
					new EnumEntryStr
					{
						Description = "Brackets",
						DisplayName = "RunParam_Brackets",
						Value = "()"
					},
					new EnumEntryStr
					{
						Description = "Dot",
						DisplayName = "RunParam_Dot",
						Value = ","
					},
					new EnumEntryStr
					{
						Description = "Colon",
						DisplayName = "RunParam_Colon",
						Value = ":"
					},
					new EnumEntryStr
					{
						Description = "Title",
						DisplayName = "RunParam_Title",
						Value = ";"
					},
					new EnumEntryStr
					{
						Description = "Underline",
						DisplayName = "RunParam_Underline",
						Value = "_"
					}
				};
			}
			return arrayDelimiter;
		}
	}

	public IList EnumEntrysA
	{
		get
		{
			return EnumEntrys;
		}
		set
		{
			if (EnumEntrys != value)
			{
				EnumEntrys = value;
			}
		}
	}

	public ICommand DropDownCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_DropDownCommand == null)
			{
				_DropDownCommand = new DelegateCommand((Action)DropDownCommandExecute);
			}
			return (ICommand)_DropDownCommand;
		}
	}

	public ICommand ClickCommand
	{
		get
		{
			if (_ClickCommand == null)
			{
				_ClickCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)ClickCommandExecute);
			}
			return (ICommand)_ClickCommand;
		}
	}

	public ICommand AddTextCommand
	{
		get
		{
			if (_AddTextCommand == null)
			{
				_AddTextCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)AddTextExcute);
			}
			return (ICommand)_AddTextCommand;
		}
	}

	public ICommand AddPairCommand
	{
		get
		{
			if (_AddPairCommand == null)
			{
				_AddPairCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)AddPairExcute);
			}
			return (ICommand)_AddPairCommand;
		}
	}

	public ICommand ClearCommand
	{
		get
		{
			if (_ClearCommand == null)
			{
				_ClearCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)ClearExcute);
			}
			return (ICommand)_ClearCommand;
		}
	}

	public ICommand AddRowCommand
	{
		get
		{
			if (_AddRowCommand == null)
			{
				_AddRowCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)AddRowExcute);
			}
			return (ICommand)_AddRowCommand;
		}
	}

	public ICommand MoveUpCommand
	{
		get
		{
			if (_MoveUpCommand == null)
			{
				_MoveUpCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)MoveUpCommandExcute);
			}
			return (ICommand)_MoveUpCommand;
		}
	}

	public ICommand MoveDownCommand
	{
		get
		{
			if (_MoveDownCommand == null)
			{
				_MoveDownCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)MoveDownCommandExcute);
			}
			return (ICommand)_MoveDownCommand;
		}
	}

	public ICommand MoveLeftCommand
	{
		get
		{
			if (_MoveLeftCommand == null)
			{
				_MoveLeftCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)MoveLeftCommandExcute);
			}
			return (ICommand)_MoveLeftCommand;
		}
	}

	public ICommand MoveRightCommand
	{
		get
		{
			if (_MoveRightCommand == null)
			{
				_MoveRightCommand = new DelegateCommand<ObjectEventArgs>((Action<ObjectEventArgs>)MoveRightCommandExcute);
			}
			return (ICommand)_MoveRightCommand;
		}
	}

	public bool CanAdd
	{
		get
		{
			if (currentRow == null && currentInfo == null)
			{
				return false;
			}
			return true;
		}
	}

	public bool CanMoveUp
	{
		get
		{
			return canMoveUp;
		}
		set
		{
			if (canMoveUp != value)
			{
				canMoveUp = value;
				OnPropertyChanged("CanMoveUp");
			}
		}
	}

	public bool CanMoveDown
	{
		get
		{
			return canMoveDown;
		}
		set
		{
			if (canMoveDown != value)
			{
				canMoveDown = value;
				OnPropertyChanged("CanMoveDown");
			}
		}
	}

	public bool CanMoveLeft
	{
		get
		{
			return canMoveLeft;
		}
		set
		{
			if (canMoveLeft != value)
			{
				canMoveLeft = value;
				OnPropertyChanged("CanMoveLeft");
			}
		}
	}

	public bool CanMoveRight
	{
		get
		{
			return canMoveRight;
		}
		set
		{
			if (canMoveRight != value)
			{
				canMoveRight = value;
				OnPropertyChanged("CanMoveRight");
			}
		}
	}

	public string TempValue
	{
		get
		{
			return _tempvalue;
		}
		set
		{
			if (!(value == _tempvalue))
			{
				if (value.ByteCount() > 4096)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Params Settings"), AppLocalizationService.GetLang("Lang_ParamLenErr"), MessageIcons.Warning);
					return;
				}
				_tempvalue = value;
				OnPropertyChanged("TempValue");
			}
		}
	}

	public bool IsDropDownOpen
	{
		get
		{
			return _IsDropDownOpen;
		}
		set
		{
			_IsDropDownOpen = value;
			OnPropertyChanged("IsDropDownOpen");
		}
	}

	public ICommand PreviewMouseWheelCommand
	{
		get
		{
			if (_PreviewMouseWheelCommand == null)
			{
				_PreviewMouseWheelCommand = new DelegateCommand<object>((Action<object>)PreviewMouseWheelCommandExcute);
			}
			return (ICommand)_PreviewMouseWheelCommand;
		}
	}

	public ICommand KeyDownCommand
	{
		get
		{
			if (_KeyDownCommand == null)
			{
				_KeyDownCommand = new DelegateCommand<KeyEventArgs>((Action<KeyEventArgs>)KeyDownCommandExcute);
			}
			return (ICommand)_KeyDownCommand;
		}
	}

	public string Message
	{
		get
		{
			return message;
		}
		set
		{
			if (message != value)
			{
				message = value;
				OnPropertyChanged("Message");
			}
		}
	}

	public bool Result
	{
		get
		{
			return result;
		}
		set
		{
			if (result != value)
			{
				result = value;
				OnPropertyChanged("Result");
			}
		}
	}

	public Visibility IsShowMessageBox
	{
		get
		{
			return isShowMessageBox;
		}
		set
		{
			if (isShowMessageBox != value)
			{
				isShowMessageBox = value;
				OnPropertyChanged("IsShowMessageBox");
			}
		}
	}

	public ICommand PopSubscribeWindowCommand
	{
		get
		{
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_0026: Expected Obj, but got Unknown
			if (popSubscribeWindowCommand == null)
			{
				popSubscribeWindowCommand = new DelegateCommand((Action)PopSubscribeWindowCommandExecute);
			}
			return (ICommand)popSubscribeWindowCommand;
		}
	}

	public override int GetParamUnit()
	{
		CurrentRow = null;
		CurrentInfo = null;
		if (string.IsNullOrEmpty(AccessMode) || !AccessMode.ToLower().Contains("r"))
		{
			OnGetValueCompleted(success: false);
			return -2;
		}
		MyItemState = ItemState.Normal;
		VmModule vmModule = GetVmModule();
		if (vmModule != null)
		{
			byte[] array = null;
			while (true)
			{
				try
				{
					array = ((IUserBytesData)vmModule.Params)["StringInput"];
					if (array != null)
					{
						int num = array.Length;
						if (num == 1)
						{
							throw new Exception("E0000001");
						}
						array = array.Take(num - 1).ToArray();
					}
				}
				catch (VmException ex)
				{
					if (ex.errorCode == -536869617)
					{
						Thread.Sleep(10);
						continue;
					}
				}
				catch (Exception ex2)
				{
					if (ex2.Message == "E0000001")
					{
						OnGetValueCompleted(success: true);
						FormatRows.Clear();
						return 0;
					}
					OnGetValueCompleted(success: true);
					MyItemState = ItemState.GetParamErro;
					return -1;
				}
				break;
			}
			try
			{
				string text = Encoding.UTF8.GetString(array).TrimEnd(default(char));
				Check(text);
				try
				{
					FormatData formatData = text.DeSerializationFromJson<FormatData>();
					if (formatData == null)
					{
						return 0;
					}
					if (formatData.ArrayDelimiter == null)
					{
						formatData.ArrayDelimiter = "()";
					}
					FormatRows.Clear();
					Delimiter.CurValueA = formatData.DelimDelimiter;
					ArrayDelimiter.CurValueA = formatData.ArrayDelimiter;
					for (int i = 0; i < formatData.Rows.Count; i++)
					{
						FormatRow formatRow = new FormatRow
						{
							Index = i
						};
						formatRow.SetParentNode(this);
						List<FormatInfo> formats = formatData.Rows[i].Formats;
						for (int j = 0; j < formats.Count; j++)
						{
							FormatTextInfo formatTextInfo = new FormatTextInfo
							{
								TextInfoType = formats[j].TextInfoType
							};
							formatTextInfo.SetParentNode(formatRow);
							if (formats[j].TextInfoType == TextType.Symbol)
							{
								MatchEvaluator evaluator = (Match m) => "\\r";
								MatchEvaluator evaluator2 = (Match m) => "\\n";
								formatTextInfo.TempValue = Regex.Replace(Regex.Replace(formats[j].TempValue, "\r", evaluator), "\n", evaluator2);
							}
							else
							{
								formatTextInfo.TempValue = GetShownFormula(formats[j].TempValue);
								if (formatTextInfo.TextInfoType == TextType.Pair)
								{
									string pairInfo = formats[j].PairInfo;
									formatTextInfo.ArrayData = ArrayData.GetArrayDataFromString(pairInfo);
								}
								if (Regex.Matches(formatTextInfo.TempValue, "<(int|float|string)\\:var\\d*\\(%(\\d+d|\\d+\\.\\d+f|s)\\)>").Count > 0)
								{
									continue;
								}
							}
							formatRow.FormatInfoCollection.Add(formatTextInfo);
						}
						FormatRows.Add(formatRow);
					}
					if (FormatRows.All((FormatRow a) => a.FormatInfoCollection.Count == 0))
					{
						FormatRows.Clear();
					}
				}
				catch
				{
					try
					{
						FormatRows.Clear();
						List<FormatInfo> list = text.DeSerializationFromJson<List<FormatInfo>>();
						Delimiter.CurValueA = "";
						ArrayDelimiter.CurValueA = "()";
						FormatRow formatRow2 = new FormatRow
						{
							Index = 0
						};
						formatRow2.SetParentNode(this);
						for (int num2 = 0; num2 < list.Count; num2++)
						{
							FormatTextInfo formatTextInfo2 = new FormatTextInfo
							{
								TextInfoType = list[num2].TextInfoType
							};
							formatTextInfo2.SetParentNode(formatRow2);
							if (list[num2].TextInfoType == TextType.Symbol)
							{
								MatchEvaluator evaluator3 = (Match m) => "\\r";
								MatchEvaluator evaluator4 = (Match m) => "\\n";
								formatTextInfo2.TempValue = Regex.Replace(Regex.Replace(list[num2].TempValue, "\r", evaluator3), "\n", evaluator4);
							}
							else
							{
								formatTextInfo2.TempValue = GetShownFormula(list[num2].TempValue);
								if (formatTextInfo2.TextInfoType == TextType.Pair)
								{
									string pairInfo2 = list[num2].PairInfo;
									formatTextInfo2.ArrayData = ArrayData.GetArrayDataFromString(pairInfo2);
								}
							}
							formatRow2.FormatInfoCollection.Add(formatTextInfo2);
						}
						if (formatRow2.FormatInfoCollection.Count > 0)
						{
							FormatRows.Add(formatRow2);
						}
					}
					catch
					{
						FormatRows.Clear();
						Delimiter.CurValueA = "";
						ArrayDelimiter.CurValueA = "()";
						FormatRow formatRow3 = new FormatRow
						{
							Index = 0
						};
						formatRow3.SetParentNode(this);
						int num3 = 0;
						int num4 = 0;
						while (num4 < text.Length)
						{
							if (text[num4] == '\r')
							{
								if (num4 + 1 < text.Length && text[num4 + 1] == '\n')
								{
									if (num4 > num3)
									{
										FormatTextInfo formatTextInfo3 = new FormatTextInfo
										{
											TextInfoType = TextType.Text,
											TempValue = GetShownFormula(text.Substring(num3, num4 - num3))
										};
										formatTextInfo3.SetParentNode(formatRow3);
										formatRow3.FormatInfoCollection.Add(formatTextInfo3);
									}
									FormatTextInfo formatTextInfo4 = new FormatTextInfo
									{
										TextInfoType = TextType.Symbol,
										TempValue = "\\r\\n"
									};
									formatTextInfo4.SetParentNode(formatRow3);
									formatRow3.FormatInfoCollection.Add(formatTextInfo4);
									num3 = num4 + 2;
									num4 += 2;
								}
								else
								{
									if (num4 > num3)
									{
										FormatTextInfo formatTextInfo5 = new FormatTextInfo
										{
											TextInfoType = TextType.Text,
											TempValue = GetShownFormula(text.Substring(num3, num4 - num3))
										};
										formatTextInfo5.SetParentNode(formatRow3);
										formatRow3.FormatInfoCollection.Add(formatTextInfo5);
									}
									FormatTextInfo formatTextInfo6 = new FormatTextInfo
									{
										TextInfoType = TextType.Symbol,
										TempValue = "\\r"
									};
									formatTextInfo6.SetParentNode(formatRow3);
									formatRow3.FormatInfoCollection.Add(formatTextInfo6);
									num3 = num4 + 1;
									num4++;
								}
							}
							else if (text[num4] == '\n')
							{
								if (num4 > num3)
								{
									FormatTextInfo formatTextInfo7 = new FormatTextInfo
									{
										TextInfoType = TextType.Text,
										TempValue = GetShownFormula(text.Substring(num3, num4 - num3))
									};
									formatTextInfo7.SetParentNode(formatRow3);
									formatRow3.FormatInfoCollection.Add(formatTextInfo7);
								}
								FormatTextInfo formatTextInfo8 = new FormatTextInfo
								{
									TextInfoType = TextType.Symbol,
									TempValue = "\\n"
								};
								formatTextInfo8.SetParentNode(formatRow3);
								formatRow3.FormatInfoCollection.Add(formatTextInfo8);
								num3 = num4 + 1;
								num4++;
							}
							else
							{
								num4++;
							}
						}
						if (num3 < text.Length)
						{
							FormatTextInfo formatTextInfo9 = new FormatTextInfo
							{
								TextInfoType = TextType.Text,
								TempValue = GetShownFormula(text.Substring(num3, text.Length - num3))
							};
							formatTextInfo9.SetParentNode(formatRow3);
							formatRow3.FormatInfoCollection.Add(formatTextInfo9);
						}
						if (formatRow3.FormatInfoCollection.Count > 0)
						{
							FormatRows.Add(formatRow3);
						}
					}
				}
				return 0;
			}
			catch (Exception)
			{
				OnGetValueCompleted(success: true);
				MyItemState = ItemState.GetParamErro;
				return -1;
			}
		}
		OnGetValueCompleted(success: true);
		MyItemState = ItemState.GetParamErro;
		return -1;
	}

	private string GetShownFormula(string value)
	{
		string text = value;
		if (GetTopBaseNode() is AlgorithmTabRoot { Owner: not null } algorithmTabRoot)
		{
			VmModule vmModule = algorithmTabRoot.Owner as VmModule;
			List<VmIO> list = vmModule.Inputs.OrderBy((VmIO a) => a.Name.Length).ToList();
			for (int num = list.Count - 1; num >= 0; num--)
			{
				VmIO vmIO = list[num];
				if (vmIO != null && vmIO.RelateIO != null && vmIO.Name.StartsWith("%") && vmIO.Name.EndsWith("%"))
				{
					string text2 = vmIO.Name.Substring(1, vmIO.Name.Length - 2);
					text = text.Replace(vmIO.TypeName + ":" + text2, (vmIO.RelateIO.Root == null) ? vmIO.RelateIO.Name : vmIO.RelateIO.ModulePathName);
				}
			}
		}
		return text;
	}

	internal string GetCalculatorFormula(string Value, bool needClear = true)
	{
		if (Value == null)
		{
			return null;
		}
		List<string> list = new List<string>();
		GetBracket(list, Value, '<', '>');
		if (needClear)
		{
			filterList = new List<VmIO>();
			fmapList.Clear();
			filterStringList = new List<string>();
		}
		if (filterList == null)
		{
			filterList = new List<VmIO>();
		}
		List<VmIO> list2 = filterList.ToList();
		foreach (string item in list)
		{
			string[] results = Regex.Replace(item, "\\(%(\\d+d|\\d+\\.\\d+f|s)\\)", (Match m) => string.Empty).Split('.');
			int num = ((results.Length > 1) ? 1 : 0);
			string filtername = Regex.Replace(results[num], "\\(%(\\d+d|\\d+\\.\\d+f|s)\\)", (Match m) => string.Empty);
			List<VmModule> list3 = new List<VmModule>();
			GetVmModule()?.GetFrontOutputItems(list3);
			VmModule vmModule = GetVmModule();
			IVmModule localVarModule = vmModule.GetLocalVarModule();
			if (localVarModule != null)
			{
				list3.Add(localVarModule as VmModule);
			}
			VmIO vmIO = null;
			if (num == 0)
			{
				vmIO = ((!(filtername == "TRIGGER_STRING")) ? null : VmSolution.ExternIO);
			}
			else
			{
				VmModule vmModule2 = list3.Where((VmModule a) => a.ID + " " + a.Name == results[0]).FirstOrDefault();
				if (vmModule2 != null)
				{
					vmIO = vmModule2.Outputs.GetIO(filtername);
					if (vmIO == null)
					{
						vmIO = vmModule2.Inputs.GetIO(filtername);
					}
				}
			}
			if (vmIO == null)
			{
				vmIO = VmSolution.Instance.Modules.FirstOrDefault((VmModule x) => x.ID == 13000)?.Outputs.FirstOrDefault((VmIO x) => x.Name == filtername || "%" + filtername + "%" == x.Name);
				if (vmIO == null)
				{
					return "";
				}
			}
			if (vmIO != null && !filterList.Contains(vmIO))
			{
				filterList.Add(vmIO);
				filterStringList.Add(Regex.Replace(item, "\\(%(\\d+d|\\d+\\.\\d+f|s)\\)", (Match m) => string.Empty));
			}
		}
		List<string> list4 = new List<string>();
		GetBracket(list4, Value, '[', ']');
		foreach (string item2 in list4)
		{
			string[] array = item2.Split('.');
			if (array != null && array.Length != 2)
			{
			}
		}
		IOrderedEnumerable<VmIO> orderedEnumerable = filterList.OrderByDescending((VmIO p) => p.Name.Length);
		foreach (VmIO item3 in orderedEnumerable)
		{
			int index = filterList.IndexOf(item3);
			if (list2.Count != filterList.Count)
			{
				fmapList.Add(new FMapping
				{
					filterString = filterStringList[index],
					index = index,
					valuetype = filterList[index].TypeName
				});
			}
		}
		List<FMapping> list5 = fmapList.OrderByDescending((FMapping p) => p.filterString.Length).ToList();
		foreach (FMapping item4 in list5)
		{
			Value = Value.Replace(item4.filterString, item4.valuetype + ":var" + item4.index);
		}
		return Value;
	}

	private void GetBracket(List<string> results, string Value, char left, char right)
	{
		string pattern = "\\" + left + ".*?\\" + right;
		Regex regex = new Regex(pattern, RegexOptions.IgnoreCase);
		MatchCollection matchCollection = regex.Matches(Value);
		foreach (Match item in matchCollection)
		{
			results.Add(item.Value.Trim(left, right));
		}
	}

	private void DropDownCommandExecute()
	{
		if (DropOpenSetter == null)
		{
			return;
		}
		if (DropOpenSetter.Operation == null)
		{
			DropOpenSetter.CreatOperateInstance();
		}
		if (DropOpenSetter.Operation != null)
		{
			DropOpenSetter.Operate(null);
		}
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			algorithmTabRoot.IsSubscribeIndex = false;
			if (algorithmTabRoot.IsSubscribeInWindow)
			{
				algorithmTabRoot.SelectedSubscribeParam = this;
				IsDropDownOpen = false;
			}
		}
	}

	private void ClickCommandExecute(ObjectEventArgs e)
	{
		if (e.Param != null && e.Param is VmIO)
		{
			VmIO vmIO = e.Param as VmIO;
			string insertValue = ((vmIO.Root == null) ? vmIO.Name : vmIO.ModulePathName);
			string typeName = vmIO.TypeName;
			insertValue = SetInsertFormat(insertValue, typeName, isArray: false);
			FormatTextInfo formatTextInfo = new FormatTextInfo
			{
				TextInfoType = TextType.Subscription
			};
			formatTextInfo.TempValue = "<" + insertValue + ">[0]";
			if (CurrentRow != null)
			{
				CurrentRow.FormatInfoCollection.Add(formatTextInfo);
				formatTextInfo.SetParentNode(CurrentRow);
			}
			else if (CurrentInfo != null)
			{
				FormatRow formatRow = CurrentInfo.GetParentNode<FormatRow>();
				if (formatRow != null)
				{
					int num = formatRow.FormatInfoCollection.IndexOf(CurrentInfo);
					formatRow.FormatInfoCollection.Insert(++num, formatTextInfo);
					formatTextInfo.SetParentNode(formatRow);
				}
			}
			CurrentRow = null;
			CurrentInfo = formatTextInfo;
			IsDropDownOpen = false;
		}
		else if (e.Param is Holder && (e.Param as Holder).HolderTarget != null && (e.Param as Holder).HolderTarget.ToString() != "null")
		{
			string displayNameA = (e.Param as Holder).DisplayNameA;
			if (displayNameA.Equals("Lang_VariableManager"))
			{
				VmIO subFilteFromGlobalVariable = GetSubFilteFromGlobalVariable(e.Param);
				if (subFilteFromGlobalVariable != null)
				{
					string modulePathName = subFilteFromGlobalVariable.ModulePathName;
					if (subFilteFromGlobalVariable != null)
					{
						string typeName2 = subFilteFromGlobalVariable.TypeName;
						modulePathName = SetInsertFormat(modulePathName, typeName2, isArray: false);
						FormatTextInfo formatTextInfo2 = new FormatTextInfo
						{
							TextInfoType = TextType.Subscription
						};
						formatTextInfo2.TempValue = "<" + modulePathName + ">[0]";
						if (CurrentRow != null)
						{
							CurrentRow.FormatInfoCollection.Add(formatTextInfo2);
							formatTextInfo2.SetParentNode(CurrentRow);
						}
						else if (CurrentInfo != null)
						{
							FormatRow formatRow2 = CurrentInfo.GetParentNode<FormatRow>();
							if (formatRow2 != null)
							{
								int num2 = formatRow2.FormatInfoCollection.IndexOf(CurrentInfo);
								formatRow2.FormatInfoCollection.Insert(++num2, formatTextInfo2);
								formatTextInfo2.SetParentNode(formatRow2);
							}
						}
						CurrentRow = null;
						CurrentInfo = formatTextInfo2;
					}
				}
			}
		}
		ResetSelectedSubscribeParam();
	}

	private VmIO GetSubFilteFromGlobalVariable(object obj)
	{
		if (obj == null)
		{
			return null;
		}
		if ((obj as Holder).HolderTarget != null)
		{
			string[] array = new string[0];
			if ((obj as Holder).HolderTarget is List<object> list)
			{
				if (list.Count < 2)
				{
					return null;
				}
				array = list[0] as string[];
			}
			else if ((obj as Holder).HolderTarget is string[] array2)
			{
				array = array2;
			}
			object obj2 = ActivatorWrap.CreateInstance("Apps.XmlUI.ParamTab.GlobalVarDlgHelper");
			string[] array3 = null;
			array3 = ((array.Length == 0) ? new string[3] { "int", "float", "string" } : array);
			VmModule vmModule = GetVmModule();
			IVmModule localVarModule = vmModule.GetLocalVarModule();
			object returnObj = obj2?.GetType().InvokeMember("ShowVarSelectDlg", BindingFlags.InvokeMethod, null, obj2, new object[3] { true, localVarModule, array3 });
			if (returnObj != null)
			{
				object owner = (returnObj as VarItem).Owner;
				if (owner != null)
				{
					return (owner as VmModule).Outputs.FirstOrDefault((VmIO x) => x.Name == (returnObj as VarItem).Name);
				}
			}
		}
		return null;
	}

	private string SetInsertFormat(string insertValue, string valueType, bool isArray)
	{
		string text = "";
		if (string.IsNullOrEmpty(insertValue) || string.IsNullOrEmpty(valueType))
		{
			return text;
		}
		string arg = "%s";
		switch (valueType)
		{
		case "string":
			arg = "%s";
			break;
		case "float":
			arg = "%1.3f";
			break;
		case "int":
			arg = "%0d";
			break;
		}
		return string.Format("{0}{2}({1})", insertValue, arg, isArray ? "[* {,}]" : string.Empty);
	}

	private void AddTextExcute(ObjectEventArgs e)
	{
		FormatTextInfo formatTextInfo = new FormatTextInfo
		{
			TextInfoType = TextType.Text,
			TempValue = string.Empty,
			IsAdd = true
		};
		if (CurrentRow != null)
		{
			CurrentRow.FormatInfoCollection.Add(formatTextInfo);
			formatTextInfo.SetParentNode(CurrentRow);
		}
		else if (CurrentInfo != null)
		{
			FormatRow formatRow = CurrentInfo.GetParentNode<FormatRow>();
			if (formatRow != null)
			{
				int num = formatRow.FormatInfoCollection.IndexOf(CurrentInfo);
				formatRow.FormatInfoCollection.Insert(++num, formatTextInfo);
				formatTextInfo.SetParentNode(formatRow);
			}
		}
		CurrentRow = null;
		CurrentInfo = formatTextInfo;
	}

	private void AddPairExcute(ObjectEventArgs e)
	{
		InsertArrayWindow insertArrayWindow = new InsertArrayWindow(null);
		insertArrayWindow.ArrayData.SetParentNode(this);
		insertArrayWindow.ShowDialog();
		if (!insertArrayWindow.IsFinish)
		{
			return;
		}
		FormatTextInfo formatTextInfo = new FormatTextInfo
		{
			TextInfoType = TextType.Pair
		};
		string text = string.Empty;
		for (int i = 0; i < insertArrayWindow.ArrayData.ArrayList.Count; i++)
		{
			text = text + "<" + (insertArrayWindow.ArrayData.ArrayList[i] as ArrayInfo).IndexInfo.CurValueA + ">";
			if (i != insertArrayWindow.ArrayData.ArrayList.Count - 1)
			{
				text += insertArrayWindow.ArrayData.Delimiter.CurValueA;
			}
		}
		formatTextInfo.TempValue = "{" + text + "}[" + insertArrayWindow.ArrayData.IndexInfo.CurValueA + "]";
		formatTextInfo.ArrayData = insertArrayWindow.ArrayData;
		if (CurrentRow != null)
		{
			CurrentRow.FormatInfoCollection.Add(formatTextInfo);
			formatTextInfo.SetParentNode(CurrentRow);
		}
		else if (CurrentInfo != null)
		{
			FormatRow formatRow = CurrentInfo.GetParentNode<FormatRow>();
			if (formatRow != null)
			{
				int num = formatRow.FormatInfoCollection.IndexOf(CurrentInfo);
				formatRow.FormatInfoCollection.Insert(++num, formatTextInfo);
				formatTextInfo.SetParentNode(formatRow);
			}
		}
		CurrentRow = null;
		CurrentInfo = formatTextInfo;
	}

	private void ClearExcute(ObjectEventArgs e)
	{
		if (HikMessageBox.Show(AppLocalizationService.GetLang("确定要删除所有配置内容吗"), "", MessageButtons.YesOrNo, MessageIcons.Question) == DialogResult.Yes)
		{
			if (FormatRows != null)
			{
				FormatRows.Clear();
			}
			CurrentInfo = null;
			CurrentRow = null;
			OnPropertyChanged("CanAdd");
		}
	}

	private void AddRowExcute(ObjectEventArgs e)
	{
		if (FormatRows == null)
		{
			return;
		}
		FormatRow formatRow = new FormatRow();
		if (CurrentRow == null)
		{
			FormatRows.Add(formatRow);
		}
		else
		{
			int num = FormatRows.IndexOf(CurrentRow);
			FormatRows.Insert(++num, formatRow);
		}
		CurrentInfo = null;
		CurrentRow = formatRow;
		formatRow.SetParentNode(this);
		foreach (FormatRow formatRow2 in FormatRows)
		{
			formatRow2.Index = FormatRows.IndexOf(formatRow2);
		}
	}

	private void MoveUpCommandExcute(ObjectEventArgs e)
	{
		if (CurrentRow == null || formatRows.Count <= 1)
		{
			return;
		}
		int num = formatRows.IndexOf(CurrentRow);
		if (num == 0)
		{
			return;
		}
		FormatRow item = CurrentRow;
		formatRows.Remove(CurrentRow);
		formatRows.Insert(--num, item);
		foreach (FormatRow formatRow in FormatRows)
		{
			formatRow.Index = FormatRows.IndexOf(formatRow);
		}
		CurrentInfo = null;
		CurrentRow = item;
	}

	private void MoveDownCommandExcute(ObjectEventArgs e)
	{
		if (CurrentRow == null || formatRows.Count <= 1)
		{
			return;
		}
		int num = formatRows.IndexOf(CurrentRow);
		if (num == formatRows.Count - 1)
		{
			return;
		}
		FormatRow item = CurrentRow;
		formatRows.Remove(CurrentRow);
		formatRows.Insert(++num, item);
		foreach (FormatRow formatRow in FormatRows)
		{
			formatRow.Index = FormatRows.IndexOf(formatRow);
		}
		CurrentInfo = null;
		CurrentRow = item;
	}

	private void MoveLeftCommandExcute(ObjectEventArgs e)
	{
		if (CurrentInfo == null)
		{
			return;
		}
		FormatRow formatRow = CurrentInfo.GetParentNode<FormatRow>();
		if (formatRow != null)
		{
			int num = formatRow.FormatInfoCollection.IndexOf(CurrentInfo);
			if (num != 0)
			{
				FormatTextInfo item = CurrentInfo;
				formatRow.FormatInfoCollection.Remove(CurrentInfo);
				formatRow.FormatInfoCollection.Insert(--num, item);
				CurrentInfo = item;
			}
		}
	}

	private void MoveRightCommandExcute(ObjectEventArgs e)
	{
		if (CurrentInfo == null)
		{
			return;
		}
		FormatRow formatRow = CurrentInfo.GetParentNode<FormatRow>();
		if (formatRow != null)
		{
			int num = formatRow.FormatInfoCollection.IndexOf(CurrentInfo);
			if (num != formatRow.FormatInfoCollection.Count - 1)
			{
				FormatTextInfo item = CurrentInfo;
				formatRow.FormatInfoCollection.Remove(CurrentInfo);
				formatRow.FormatInfoCollection.Insert(++num, item);
				CurrentInfo = item;
			}
		}
	}

	private void PreviewMouseWheelCommandExcute(object obj)
	{
		if (hasRegistered)
		{
			return;
		}
		ScrollViewer scrollviewer = null;
		if (obj is ScrollViewer)
		{
			scrollviewer = obj as ScrollViewer;
			scrollviewer.PreviewMouseWheel += (object sender, MouseWheelEventArgs e) =>
			{
				MouseWheelEventArgs e2 = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
				{
					RoutedEvent = UIElement.MouseWheelEvent,
					Source = sender
				};
				scrollviewer.RaiseEvent(e2);
			};
			hasRegistered = true;
		}
	}

	private void KeyDownCommandExcute(KeyEventArgs e)
	{
		if ((e.KeyStates == Keyboard.GetKeyStates(Key.Delete) || e.KeyStates == Keyboard.GetKeyStates(Key.Back)) && CurrentRow != null)
		{
			CurrentRow.DeleteSelf();
		}
	}

	private string MsgFindResoure(string strMsg)
	{
		string text = "";
		object lang = AppLocalizationService.GetLang(strMsg);
		if (lang != null)
		{
			return lang.ToString();
		}
		return strMsg;
	}

	private void ShowMessageBox(string message, bool result)
	{
		int waitTime = 1;
		new Thread(() =>
		{
			TimeSpan timeout = new TimeSpan(0, 0, 0, 1);
			for (int num = waitTime; num > 0; num--)
			{
				Thread.Sleep(timeout);
			}
			IsShowMessageBox = System.Windows.Visibility.Collapsed;
		}).Start();
		IsShowMessageBox = System.Windows.Visibility.Visible;
		Result = result;
		Message = MsgFindResoure(message);
	}

	public void OkExcute()
	{
		IsTextOk = false;
		if (FormatRows.Count == 0 || FormatRows.Where((FormatRow a) => a.FormatInfoCollection.Count == 0).Count() == FormatRows.Count)
		{
			ShowMessageBox("Lang_FmtTestFail", result: false);
			return;
		}
		string empty = string.Empty;
		FormatData formatData = new FormatData
		{
			DelimDelimiter = Delimiter.CurValueA,
			ArrayDelimiter = ArrayDelimiter.CurValueA
		};
		bool needClear = true;
		bool flag = false;
		formatData.Rows = new List<FormatRowData>();
		foreach (FormatRow formatRow in FormatRows)
		{
			FormatRowData formatRowData = new FormatRowData
			{
				Index = formatRow.Index,
				Formats = new List<FormatInfo>()
			};
			foreach (FormatTextInfo item in formatRow.FormatInfoCollection)
			{
				if (item.TextInfoType == TextType.Symbol)
				{
					string calculatorFormula = GetCalculatorFormula(item.TempValue.Replace("\\r", "\r").Replace("\\n", "\n"), needClear);
					formatRowData.Formats.Add(new FormatInfo
					{
						TempValue = calculatorFormula,
						TextInfoType = item.TextInfoType
					});
					needClear = false;
					flag = true;
					continue;
				}
				if (item.TextInfoType == TextType.Pair)
				{
					int num = item.TempValue.IndexOf("{");
					int num2 = item.TempValue.IndexOf("}");
					if ((num == -1 && num2 == -1) || num + 1 == num2)
					{
						item.MyItemState = ItemState.SetParamErro;
						ShowMessageBox("Lang_FmtTestFail", result: false);
						return;
					}
					num = item.TempValue.IndexOf("[");
					num2 = item.TempValue.IndexOf("]");
					if ((num == -1 && num2 == -1) || num + 1 == num2)
					{
						item.MyItemState = ItemState.SetParamErro;
						ShowMessageBox("Lang_FmtTestFail", result: false);
						return;
					}
					string text = item.TempValue.Substring(num + 1, num2 - num - 1);
					bool flag2 = false;
					if (text.Equals("*"))
					{
						flag2 = true;
					}
					else if (Regex.IsMatch(text, "^\\d+(,\\d+)*$"))
					{
						flag2 = true;
					}
					else if (text.StartsWith("<") && text.EndsWith(">") && GetCalculatorFormula(text, needClear: false) != string.Empty)
					{
						flag2 = true;
					}
					if (!flag2)
					{
						item.MyItemState = ItemState.SetParamErro;
						ShowMessageBox("Lang_FmtTestFail", result: false);
						return;
					}
					if (!CheckSpace(item.TempValue))
					{
						item.MyItemState = ItemState.SetParamErro;
						ShowMessageBox("Lang_FmtTestFail", result: false);
						return;
					}
				}
				else if (item.TextInfoType == TextType.Text)
				{
					if (Regex.IsMatch(item.TempValue, "{?[0-9]+}"))
					{
						item.MyItemState = ItemState.SetParamErro;
						ShowMessageBox("Lang_FmtTestFail", result: false);
						return;
					}
					flag = true;
				}
				else if (item.TextInfoType == TextType.Subscription && !CheckSpace(item.TempValue))
				{
					item.MyItemState = ItemState.SetParamErro;
					ShowMessageBox("Lang_FmtTestFail", result: false);
					return;
				}
				string calculatorFormula2 = GetCalculatorFormula(item.TempValue, needClear);
				if (calculatorFormula2 == string.Empty)
				{
					item.MyItemState = ItemState.SetParamErro;
					ShowMessageBox("Lang_FmtTestFail", result: false);
					return;
				}
				needClear = false;
				item.MyItemState = ItemState.Normal;
				formatRowData.Formats.Add(new FormatInfo
				{
					TempValue = calculatorFormula2,
					TextInfoType = item.TextInfoType,
					PairInfo = ((item.TextInfoType != TextType.Pair) ? string.Empty : item.ArrayData?.ToString())
				});
			}
			formatData.Rows.Add(formatRowData);
		}
		try
		{
			empty = formatData.ToJson();
		}
		catch (Exception)
		{
			ShowMessageBox("Lang_FmtTestFail", result: false);
			return;
		}
		if (empty.ByteCount() > 4494)
		{
			ShowMessageBox("Lang_ContentTooLarge", result: false);
			return;
		}
		try
		{
			VmModule vmModule = GetVmModule();
			if (vmModule == null)
			{
				return;
			}
			string empty2 = string.Empty;
			try
			{
				byte[] bytes = ((IUserBytesData)vmModule.Params)["StringInput"];
				empty2 = Encoding.UTF8.GetString(bytes).TrimEnd(default(char));
			}
			catch (Exception ex2)
			{
				if (!(ex2.Message == "E0000001"))
				{
					ShowMessageBox("Lang_FmtTestFail", result: false);
					return;
				}
				empty2 = string.Empty;
			}
			if (empty != empty2)
			{
				((IUserBytesData)vmModule.Params)["StringInput"] = Encoding.UTF8.GetBytes(empty);
			}
			VmIOCollection inputs = vmModule.Inputs;
			if (inputs == null)
			{
				inputs = new VmIOCollection();
				return;
			}
			List<VmIO> list = new List<VmIO>();
			foreach (VmIO input in vmModule.Inputs)
			{
				if (!input.Name.Contains("输入图像"))
				{
					list.Add(input);
				}
			}
			for (int num3 = 0; num3 < FilterList.Count; num3++)
			{
				VmIO filter = new DynamicVmIO
				{
					Root = vmModule,
					Name = "var" + num3,
					TypeName = FilterList[num3].TypeName,
					Parent = vmModule
				};
				VmIO vmIO = list.Where((VmIO a) => a.Name == filter.Name).FirstOrDefault();
				if (vmIO != null)
				{
					list.Remove(vmIO);
					if (vmIO.RelateIO == FilterList[num3])
					{
						continue;
					}
					vmModule.Inputs.Remove(vmIO);
				}
				vmModule.Inputs.Add(filter);
				filter.RelateIO = FilterList[num3];
			}
			for (int num4 = list.Count - 1; num4 >= 0; num4--)
			{
				vmModule.Inputs.Remove(list[num4]);
			}
			if (FilterList.Count == vmModule.Inputs.Count)
			{
				vmModule.UserData["HasContent"] = VM.PlatformSDKCS.StringUtil.UTF8GetBytesPadZero("True");
				vmModule.UserData["HasConstString"] = VM.PlatformSDKCS.StringUtil.UTF8GetBytesPadZero(flag ? "True" : "False");
				vmModule.ReFlashReadyState();
				ShowMessageBox("Lang_FmtSetSuc", result: true);
				IsTextOk = true;
			}
			else
			{
				ShowMessageBox("Lang_FmtTestFail", result: false);
				IsTextOk = false;
			}
		}
		catch (Exception)
		{
			ShowMessageBox("Lang_FmtTestFail", result: false);
			IsTextOk = false;
		}
	}

	private bool CheckSpace(string value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return false;
		}
		int num = value.IndexOf("[");
		int num2 = value.IndexOf("]");
		if (num == -1 || num2 == -1 || num > num2)
		{
			return false;
		}
		string text = value.Substring(num + 1, num2 - num - 1);
		if (text == string.Empty)
		{
			return false;
		}
		if (text.Contains("<") && text.Contains(">"))
		{
			int num3 = text.IndexOf("<");
			int num4 = text.IndexOf(">");
			for (int i = 0; i < text.Length; i++)
			{
				if (text[i] == ' ' && (i < num3 || i > num4))
				{
					return false;
				}
			}
			return true;
		}
		if (text.Contains(" "))
		{
			return false;
		}
		return true;
	}

	public object ExecuteFunction(object obj = null)
	{
		if (obj != null && obj.ToString() == "OKCommand")
		{
			OkExcute();
			return IsTextOk;
		}
		return null;
	}

	private void PopSubscribeWindowCommandExecute()
	{
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			algorithmTabRoot.SelectedSubscribeParam = this;
			algorithmTabRoot.IsSubscribeInWindow = true;
		}
	}

	protected override void ParamItemSelectedCommandExecute()
	{
		if (GetTopBaseNode() is AlgorithmTabRoot { IsSubscribeInWindow: not false, IsSubscribeIndex: not false })
		{
			return;
		}
		if (DropOpenSetter != null)
		{
			if (DropOpenSetter.Operation == null)
			{
				DropOpenSetter.CreatOperateInstance();
			}
			if (DropOpenSetter.Operation != null)
			{
				DropOpenSetter.Operate(null);
			}
		}
		ResetSelectedSubscribeParam();
	}

	private void ResetSelectedSubscribeParam()
	{
		if (GetTopBaseNode() is AlgorithmTabRoot algorithmTabRoot)
		{
			if (CanAdd && algorithmTabRoot.IsSubscribeInWindow)
			{
				algorithmTabRoot.SelectedSubscribeParam = this;
			}
			else
			{
				algorithmTabRoot.SelectedSubscribeParam = null;
			}
		}
	}
}
