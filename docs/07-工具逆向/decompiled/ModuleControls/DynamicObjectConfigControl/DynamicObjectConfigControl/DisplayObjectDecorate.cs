using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using DynamicObjectConfigControl.UI;
using Mvvm.Commands;
using VM.CommonData;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace DynamicObjectConfigControl;

public class DisplayObjectDecorate : NotifyPropertyChangedBase, IButtonSelecter
{
	private DynamicObject _DecorateSource;

	protected ButtonSelecter _imageFilterSelecter;

	private GroupOutputParam groupOutputParam = null;

	private Apps.XmlParser.ParamTab.Boolean isAccumulatedDisplay;

	private DelegateCommand deeteIOCommand = null;

	private DelegateCommand<string> _TextChangedCommand = null;

	private DelegateCommand _ConfigShapeCommand = null;

	public GroupDisplayParam Owner { get; set; }

	public DynamicObject DecorateSource
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

	public string Name
	{
		get
		{
			return DecorateSource.NameA;
		}
		set
		{
			if (DecorateSource.NameA != AppLocalizationService.UnDoTranslate(value))
			{
				if (CheckName(value))
				{
					string nameA = DecorateSource.NameA;
					NameChanding(nameA, AppLocalizationService.UnDoTranslate(value));
					DecorateSource.NameA = value;
					NameChanded(nameA, AppLocalizationService.UnDoTranslate(value));
					_imageFilterSelecter = null;
					((NotifyPropertyChangedBase)this).OnPropertyChanged("ImageFilterSelecter");
				}
				((NotifyPropertyChangedBase)this).OnPropertyChanged("Name");
			}
		}
	}

	public bool IsDisplayForIO => DecorateSource.RelateIO != null;

	public string RelateIOPre => "(" + AppLocalizationService.GetLang(DecorateSource.RelateIO.Root.Inputs.Contains(DecorateSource.RelateIO) ? "输入" : "Output") + ")";

	public object RelateIO => DecorateSource.RelateIO;

	public string ValueType
	{
		get
		{
			if (DecorateSource.TypeA.Equals("outlinepoint"))
			{
				return "pointset";
			}
			if (DecorateSource.TypeA.Equals("annularsector"))
			{
				return "annulus";
			}
			return DecorateSource.TypeA;
		}
	}

	public virtual ButtonSelecter ImageFilterSelecter
	{
		get
		{
			if (_imageFilterSelecter == null)
			{
				_imageFilterSelecter = DecorateSource.GetButtonSelecterFromGroup(Owner);
				if (_imageFilterSelecter != null && IsShowAccumulatedDisplay)
				{
					_imageFilterSelecter.OnCurValueChanged += ParamValueChanged;
				}
			}
			return _imageFilterSelecter;
		}
	}

	ButtonSelecter IButtonSelecter.Selecter => ImageFilterSelecter;

	internal GroupOutputParam GroupOutputParam
	{
		get
		{
			if (groupOutputParam == null)
			{
				groupOutputParam = (Owner.GetTopBaseNode() as AlgorithmTabRoot).FindBaseNodeByName("GroupOutputParam") as GroupOutputParam;
			}
			return groupOutputParam;
		}
	}

	public Apps.XmlParser.ParamTab.Boolean IsAccumulatedDisplay
	{
		get
		{
			if (isAccumulatedDisplay == null)
			{
				isAccumulatedDisplay = new Apps.XmlParser.ParamTab.Boolean
				{
					DisplayName = AppLocalizationService.GetLang("Lang_AccumulatedDisplay"),
					Description = AppLocalizationService.GetLang("Lang_AccumulatedDisplay"),
					AccessMode = "O",
					DefaultValue = false
				};
				if (GroupOutputParam != null && GroupOutputParam.IOModes != null)
				{
					foreach (FeatureBase item in DecorateSource.FeaturesB)
					{
						if (item.vmIOs.Count > 0)
						{
							string name = item.vmIOs.First().Name;
							if (groupOutputParam.IOModes.Keys.Contains(name))
							{
								isAccumulatedDisplay.CurValue = groupOutputParam.IOModes[name] == 0;
								break;
							}
						}
					}
				}
				else
				{
					isAccumulatedDisplay.CurValue = false;
				}
				isAccumulatedDisplay.OnCurValueChanged += ParamValueChanged;
			}
			return isAccumulatedDisplay;
		}
	}

	public bool IsShowAccumulatedDisplay => (Owner.Owner is IMVSGroup && !ValueType.Equals("image") && !ValueType.Equals("pixelinfoimage")) ? true : false;

	public ICommand DeleteCommand
	{
		get
		{
			//IL_0018: Unknown result type (might be due to invalid IL or missing references)
			//IL_001d: Unknown result type (might be due to invalid IL or missing references)
			//IL_001f: Expected Obj, but got Unknown
			//IL_0024: Expected Obj, but got Unknown
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

	public ICommand TextChangedCommand => (ICommand)(_TextChangedCommand ?? (_TextChangedCommand = new DelegateCommand<string>((Action<string>)TextChangedExecute)));

	public ICommand ConfigShapeCommand
	{
		get
		{
			//IL_0017: Unknown result type (might be due to invalid IL or missing references)
			//IL_001c: Unknown result type (might be due to invalid IL or missing references)
			//IL_001e: Expected Obj, but got Unknown
			//IL_0023: Expected Obj, but got Unknown
			DelegateCommand val = _ConfigShapeCommand;
			if (val == null)
			{
				DelegateCommand val2 = new DelegateCommand((Action)ConfigShapeCommandExecute);
				DelegateCommand val3 = val2;
				_ConfigShapeCommand = val2;
				val = val3;
			}
			return (ICommand)val;
		}
	}

	public string NoSignName
	{
		get
		{
			string name = Name;
			if (name != null && name.Contains('%'))
			{
				return Name.Substring(1, Name.Length - 2);
			}
			return Name;
		}
	}

	protected virtual void NameChanding(string oldVal, string newVal)
	{
	}

	protected virtual void NameChanded(string oldVal, string newVal)
	{
	}

	private bool CheckName(string newName)
	{
		if (!VmIODecorate.CommonNameCheck(newName))
		{
			return false;
		}
		IEnumerable<string> source = from p in (Owner.Owner["[Display]"] as ParamRoot).GetDisplayList().OfType<Apps.XmlParser.Object>()
			select p.NameA;
		if (source.Any((string p) => p == AppLocalizationService.UnDoTranslate(newName)))
		{
			HikMessageBox.Show("Lang_SetCnfg", "Lang_PmNameExited", MessageIcons.Error, isTranslate: true);
			return false;
		}
		return true;
	}

	private void ParamValueChanged(object sender, EventArgs e)
	{
		ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO_LIST groupIOReportMode = new ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO_LIST
		{
			nGroupId = Owner.Owner.ID,
			stGroupIOMode = new ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO[1024]
		};
		uint num = 0u;
		foreach (FeatureBase item in DecorateSource.FeaturesB)
		{
			foreach (VmIO vmIO in item.vmIOs)
			{
				uint nMode = ((!IsAccumulatedDisplay.CurValueA) ? 1u : 0u);
				if (num < 1024)
				{
					ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO iMVS_GROUP_IO_REPORT_MODE_INFO = new ImvsSdkDefine.IMVS_GROUP_IO_REPORT_MODE_INFO
					{
						nMode = nMode,
						strParamName = StringUtil.UTF8GetFixLenBytes(vmIO.Name, 128)
					};
					groupIOReportMode.stGroupIOMode[num++] = iMVS_GROUP_IO_REPORT_MODE_INFO;
				}
			}
		}
		groupIOReportMode.nIONum = num;
		try
		{
			if (num != 0)
			{
				ServerSDKManager.serverSDKManager.mModuleManager.SetGroupIOReportMode(groupIOReportMode);
			}
		}
		catch (VmException ex)
		{
			int errorCode = ex.errorCode;
			string text = AppLocalizationService.GetLang("errCode:") + errorCode.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(errorCode);
			LogHelper.Error(text);
			HikMessageBox.Show(text, MessageIcons.Error);
		}
	}

	protected virtual void DeleteIOCommandExecute()
	{
		Owner.DisplayItemsA.Remove(this);
		List<VmIO> allTopMappingIO = DecorateSource.GetAllTopMappingIO();
		VmModule vmModule = DecorateSource.Owner as VmModule;
		foreach (VmIO item in allTopMappingIO)
		{
			item.RemoveSelf();
		}
		if (allTopMappingIO.Count > 0)
		{
			(Owner.Owner as IVMGroup)?.SetIoParams();
			(Owner.Owner as IVmProcedure)?.SetIoParams();
		}
		DecorateSource.RemoveSelf();
		AlgorithmTabRoot algorithmTabRoot = Owner.Owner?.Params.GetParamRoot();
		if (algorithmTabRoot != null)
		{
			algorithmTabRoot.SelectedSubscribeParam = null;
		}
	}

	private void TextChangedExecute(string newName)
	{
		Name = newName;
	}

	private void ConfigShapeCommandExecute()
	{
		ShapePropertyConfigRoot instance = ShapePropertyConfigRoot.GetInstance();
		Tab tab = instance.Items.OfType<Tab>().FirstOrDefault((Tab p) => p.Name == DecorateSource.TypeA) ?? instance.Items.OfType<Tab>().Single((Tab p) => p.Name == "default");
		Tab tab2 = tab.Clone<Tab>();
		tab2.SetParentNode(DecorateSource);
		tab2.InitCollection();
		tab2.OnXmlLoaded();
		tab2.DoIniter();
		SetTabItemProperty(tab2);
		ShapePropertyConfigWin shapePropertyConfigWin = new ShapePropertyConfigWin();
		shapePropertyConfigWin.DataContext = tab2;
		shapePropertyConfigWin.TargetObj = DecorateSource;
		shapePropertyConfigWin.Loaded += ShapePropertyConfigWin_Loaded;
		shapePropertyConfigWin.ShowDialog();
	}

	private void ShapePropertyConfigWin_Loaded(object sender, RoutedEventArgs e)
	{
		if (sender is ShapePropertyConfigWin shapePropertyConfigWin)
		{
			shapePropertyConfigWin.Loaded -= ShapePropertyConfigWin_Loaded;
			object obj = DecorateSource?.GetTopBaseNode();
			if (obj is ParamRoot)
			{
				obj = ((obj as ParamRoot).Owner as VmModule)?.Params?.GetParamRoot();
			}
			if (obj is AlgorithmTabRoot { IsSubscribeInWindow: not false } algorithmTabRoot)
			{
				algorithmTabRoot.IsSubscribeInWindow = false;
				algorithmTabRoot.IsSubscribeInWindow = true;
			}
		}
	}

	protected virtual void SetTabItemProperty(Tab cloneTab)
	{
	}
}
