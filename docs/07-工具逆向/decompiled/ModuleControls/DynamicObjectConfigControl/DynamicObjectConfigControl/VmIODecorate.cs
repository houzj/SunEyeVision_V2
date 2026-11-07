using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using DynamicObjectConfigControl.Common;
using DynamicObjectConfigControl.UI;
using Mvvm.Commands;
using VM.CommonData;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace DynamicObjectConfigControl;

internal class VmIODecorate : NotifyPropertyChangedBase, IDecorate, IButtonSelecter
{
	private VmIO _DecorateSource;

	private bool isDisplayForImage;

	private const string ConnectInfo = "ObjectConnectWithIOInfo_";

	private ButtonSelecter _FilterSelecter;

	private Apps.XmlParser.ParamTab.Boolean isAccumulatedOutput;

	private DelegateCommand<string> _TextChangedCommand = null;

	private DelegateCommand deeteIOCommand = null;

	private DelegateCommand displayChildrenCommand = null;

	public GroupParam Owner { get; set; }

	public VmIO DecorateSource
	{
		get
		{
			return _DecorateSource;
		}
		set
		{
			if (value != _DecorateSource)
			{
				_DecorateSource = value;
			}
		}
	}

	object IDecorate.DecorateSource
	{
		get
		{
			return DecorateSource;
		}
		set
		{
			DecorateSource = value as VmIO;
		}
	}

	public string Name
	{
		get
		{
			return DecorateSource.Name;
		}
		set
		{
			if (DecorateSource.Name != AppLocalizationService.UnDoTranslate(value))
			{
				if (CheckName(value))
				{
					DecorateSource.Name = value;
					_FilterSelecter = null;
					((NotifyPropertyChangedBase)this).OnPropertyChanged("FilterSelecter");
				}
				((NotifyPropertyChangedBase)this).OnPropertyChanged("Name");
			}
		}
	}

	public string ValueType
	{
		get
		{
			if (Enum.TryParse<CombinationIOTypeEnum>(DecorateSource.TypeName, out var result))
			{
				return DynamicIO.GetDefaultObjectName(result);
			}
			if (CheckAdditionExist.IsExistAddition && Enum.TryParse<CombinationIOTypeEnumEx>(DecorateSource.TypeName, out var result2))
			{
				return DynamicIO.GetDefaultObjectName(result2);
			}
			return DecorateSource.TypeName;
		}
	}

	public bool IsShowDisplayCheckBox
	{
		get
		{
			if (DecorateSource.IsOutput)
			{
				return DynamicObjectManager.OutputDisplayDic.Keys.Contains(DecorateSource.TypeName);
			}
			return (DecorateSource.TypeName == "IMAGE" || DecorateSource.TypeName == "PIXELIMAGE") ? true : false;
		}
	}

	public bool IsDisplayForImage
	{
		get
		{
			ParamRoot paramRoot = DecorateSource.Root["[Display]"] as ParamRoot;
			isDisplayForImage = paramRoot.GetDisplayList().OfType<DynamicObject>().Any((DynamicObject p) => p.RelateIO == DecorateSource);
			return isDisplayForImage;
		}
		set
		{
			if (isDisplayForImage != value)
			{
				isDisplayForImage = value;
				if (value)
				{
					DynamicObjectManager.AddDisplayObject(DecorateSource);
				}
				else
				{
					DynamicObjectManager.RemoveDisplayObject(DecorateSource);
				}
				Owner.ChangedCommandExecute();
				((NotifyPropertyChangedBase)this).OnPropertyChanged("IsDisplayForImage");
			}
		}
	}

	public bool IsShowChildButton => false;

	public ButtonSelecter FilterSelecter
	{
		get
		{
			if (_FilterSelecter == null)
			{
				_FilterSelecter = DecorateSource.GetButtonSelecter(Owner, Owner.Owner.Inputs.Contains(DecorateSource));
			}
			return _FilterSelecter;
		}
	}

	ButtonSelecter IButtonSelecter.Selecter => FilterSelecter;

	public Apps.XmlParser.ParamTab.Boolean IsAccumulatedOutput
	{
		get
		{
			if (isAccumulatedOutput == null)
			{
				isAccumulatedOutput = new Apps.XmlParser.ParamTab.Boolean
				{
					DisplayName = "RunParam_AccumulatedOutput",
					Description = "RunParam_AccumulatedOutput",
					AccessMode = "O",
					DefaultValue = false
				};
				if (Owner is GroupOutputParam { IOModes: not null } groupOutputParam)
				{
					if (DecorateSource.IsLeafNode)
					{
						if (groupOutputParam.IOModes.Keys.Contains(Name))
						{
							isAccumulatedOutput.CurValue = groupOutputParam.IOModes[Name] == 0;
						}
					}
					else
					{
						foreach (VmIO item in DecorateSource.GetAllLeafIO())
						{
							if (groupOutputParam.IOModes.Keys.Contains(item.Name))
							{
								isAccumulatedOutput.CurValue = groupOutputParam.IOModes[item.Name] == 0;
								break;
							}
						}
					}
				}
				else
				{
					isAccumulatedOutput.CurValue = false;
				}
				isAccumulatedOutput.OnCurValueChanged += ParamValueChanged;
			}
			return isAccumulatedOutput;
		}
	}

	public bool IsShowAccumulatedOutput => (Owner.Owner is IMVSGroup && DecorateSource.IsOutput) ? true : false;

	public ICommand TextChangedCommand => (ICommand)(_TextChangedCommand ?? (_TextChangedCommand = new DelegateCommand<string>((Action<string>)TextChangedExecute)));

	public ICommand DeleteCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = deeteIOCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)DeleteIOCommandExecute);
				DelegateCommand val3 = val2;
				deeteIOCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public object LeafIOs => from p in _DecorateSource.GetAllLeafIO()
		select p.Name;

	public ICommand DisplayChildrenCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = displayChildrenCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)DisplayChildrenCommandExecute);
				DelegateCommand val3 = val2;
				displayChildrenCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	private bool CheckName(string newName)
	{
		if (!CommonNameCheck(newName))
		{
			return false;
		}
		if (Owner.Owner.Inputs.Any((VmIO p) => p.Name == AppLocalizationService.UnDoTranslate(newName)) || ((IVmModule)Owner.Owner).IsExistName(AppLocalizationService.UnDoTranslate(newName)))
		{
			HikMessageBox.Show("Lang_SetCnfg", "Lang_PmNameExited", MessageIcons.Error, isTranslate: true);
			return false;
		}
		return true;
	}

	public static bool CommonNameCheck(string newName)
	{
		if (string.IsNullOrWhiteSpace(newName))
		{
			HikMessageBox.Show("Lang_SetCnfg", "Lang_PmNameEmpty", MessageIcons.Error, isTranslate: true);
			return false;
		}
		if (newName.Contains('?') || newName.Contains(';') || newName.Contains('.') || newName.Contains('/') || newName.Contains('\\') || newName.Contains('`') || newName.Contains('{') || newName.Contains('}') || newName.Contains('[') || newName.Contains(']') || newName.Contains('<') || newName.Contains('>') || newName.Contains('(') || newName.Contains(')') || newName.Contains('%'))
		{
			HikMessageBox.Show("Lang_SetCnfg", "Lang_ContainsIllegalChars", MessageIcons.Error, isTranslate: true);
			return false;
		}
		return true;
	}

	private void ParamValueChanged(object sender, EventArgs e)
	{
		List<VmIODecorate> list = Owner.ItemsA.OfType<VmIODecorate>().ToList();
		ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO_LIST groupIOReportMode = new ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO_LIST
		{
			nGroupId = Owner.Owner.ID,
			stGroupIOMode = new ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO[1024]
		};
		uint num = 0u;
		foreach (VmIODecorate item in list)
		{
			uint nMode = ((!item.IsAccumulatedOutput.CurValueA) ? 1u : 0u);
			if (item.DecorateSource.IsLeafNode)
			{
				if (num < 1024)
				{
					ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO iMVS_GROUP_IO_REPORT_MODE_INFO = new ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO
					{
						nMode = nMode,
						strParamName = StringUtil.UTF8GetFixLenBytes(item.Name, 128)
					};
					groupIOReportMode.stGroupIOMode[num++] = iMVS_GROUP_IO_REPORT_MODE_INFO;
				}
				continue;
			}
			foreach (VmIO item2 in item.DecorateSource.GetAllLeafIO())
			{
				if (num < 1024)
				{
					ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO iMVS_GROUP_IO_REPORT_MODE_INFO2 = new ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO
					{
						nMode = nMode,
						strParamName = StringUtil.UTF8GetFixLenBytes(item2.Name, 128)
					};
					groupIOReportMode.stGroupIOMode[num++] = iMVS_GROUP_IO_REPORT_MODE_INFO2;
				}
			}
		}
		groupIOReportMode.nIONum = num;
		try
		{
			ServerSDKManager.serverSDKManager.mModuleManager.SetGroupIOReportMode(groupIOReportMode);
		}
		catch (VmException ex)
		{
			int errorCode = ex.errorCode;
			string text = AppLocalizationService.GetLang("errCode:") + errorCode.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(errorCode);
			LogHelper.Error(text);
			HikMessageBox.Show(text, MessageIcons.Error);
		}
	}

	private void SetIoParams()
	{
		Owner.SetIoParams();
	}

	private void TextChangedExecute(string newName)
	{
		Name = newName;
		SetIoParams();
	}

	private void DeleteIOCommandExecute()
	{
		Owner.ItemsA.Remove(this);
		if (IsDisplayForImage)
		{
			ParamRoot paramRoot = DecorateSource.Root["[Display]"] as ParamRoot;
			DynamicObject dynamicObject = paramRoot.GetDisplayList().OfType<DynamicObject>().Single((DynamicObject p) => p.RelateIO == DecorateSource);
			dynamicObject.RemoveSelf();
			Owner.ChangedCommandExecute();
		}
		DecorateSource.RemoveSelf();
		SetIoParams();
		AlgorithmTabRoot algorithmTabRoot = DecorateSource.Root?.Params.GetParamRoot();
		if (algorithmTabRoot != null)
		{
			algorithmTabRoot.SelectedSubscribeParam = null;
		}
	}

	private void DisplayChildrenCommandExecute()
	{
		ShowChildWin showChildWin = new ShowChildWin();
		showChildWin.DataContext = this;
		showChildWin.ShowDialog();
	}
}
