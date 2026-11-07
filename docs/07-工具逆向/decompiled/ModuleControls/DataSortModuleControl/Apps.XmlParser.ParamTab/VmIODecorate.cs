using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Input;
using Apps.Data;
using Apps.Localization;
using Apps.Log;
using DynamicObjectConfigControl.UI;
using Hik.Xml;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class VmIODecorate : NotifyPropertyChangedBase, IDecorate, IButtonSelecter
{
	private VmIO _DecorateSource;

	private bool isDisplayForImage;

	private static Dictionary<string, ObjectTypeEnum> OutputDisplayDic = new Dictionary<string, ObjectTypeEnum>
	{
		{
			"IMAGE",
			ObjectTypeEnum.image
		},
		{
			"PIXELIMAGE",
			ObjectTypeEnum.pixelinfoimage
		},
		{
			"CIRCLE",
			ObjectTypeEnum.circle
		},
		{
			"POINT",
			ObjectTypeEnum.point
		},
		{
			"ROIBOX",
			ObjectTypeEnum.rect
		},
		{
			"Rect",
			ObjectTypeEnum.rect
		},
		{
			"LINE",
			ObjectTypeEnum.line
		},
		{
			"CONTOURPOINTS",
			ObjectTypeEnum.outlinepoint
		},
		{
			"ROIPOLYGON",
			ObjectTypeEnum.polygon
		},
		{
			"ROIANNULUS",
			ObjectTypeEnum.annularsector
		},
		{
			"ELLIPSE",
			ObjectTypeEnum.ellipse
		},
		{
			"pointset",
			ObjectTypeEnum.outlinepoint
		},
		{
			"STRING".ToLower(),
			ObjectTypeEnum.text
		}
	};

	private const string ConnectInfo = "ObjectConnectWithIOInfo_";

	private ButtonSelecter _SortSelecter;

	private DelegateCommand<string> _TextChangedCommand = null;

	private DelegateCommand deeteIOCommand = null;

	private DelegateCommand displayChildrenCommand = null;

	public DataSortBaseParam Owner { get; set; }

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
					_SortSelecter = null;
					OnPropertyChanged("SortSelecter");
				}
				OnPropertyChanged("Name");
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
			return DecorateSource.TypeName;
		}
	}

	public bool IsShowDisplayCheckBox => OutputDisplayDic.Keys.Contains(DecorateSource.TypeName);

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
					AddDisplayObject(DecorateSource);
				}
				else
				{
					RemoveDisplayObject(DecorateSource);
				}
				Owner.ChangedCommandExecute();
				OnPropertyChanged("IsDisplayForImage");
			}
		}
	}

	public bool IsShowChildButton => false;

	public ButtonSelecter SortSelecter
	{
		get
		{
			if (_SortSelecter == null)
			{
				_SortSelecter = GetButtonSelecter(DecorateSource, Owner);
			}
			return _SortSelecter;
		}
	}

	ButtonSelecter IButtonSelecter.Selecter => SortSelecter;

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
		if (Owner.Owner.Inputs.Any((VmIO p) => p.Name == AppLocalizationService.UnDoTranslate(newName)) || Owner.Owner.Outputs.Any((VmIO p) => p.Name == AppLocalizationService.UnDoTranslate(newName)))
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
		if (newName.Count() > 32)
		{
			HikMessageBox.Show("Lang_SetCnfg", "Lang_PmNameLength", MessageIcons.Error, isTranslate: true);
			return false;
		}
		if (newName.Contains('?') || newName.Contains(';') || newName.Contains('.') || newName.Contains('/') || newName.Contains('\\') || newName.Contains('`') || newName.Contains('{') || newName.Contains('}') || newName.Contains('[') || newName.Contains(']') || newName.Contains('<') || newName.Contains('>') || newName.Contains('(') || newName.Contains(')'))
		{
			HikMessageBox.Show("Lang_SetCnfg", "Lang_Containsillegal", MessageIcons.Error, isTranslate: true);
			return false;
		}
		return true;
	}

	private void AddDisplayObject(VmIO decorateSource)
	{
		if (!OutputDisplayDic.Keys.Contains(decorateSource.TypeName))
		{
			return;
		}
		DynamicObject dynamicObject = GroupDisplayParam.AddDynamicObject(decorateSource.Root, OutputDisplayDic[decorateSource.TypeName], (Object)null, false);
		if (dynamicObject != null)
		{
			if (decorateSource.TypeName == "IMAGE" || decorateSource.TypeName == "PIXELIMAGE")
			{
				SetInternalObjectToParentObjectOperation.SetImageObjectMapByIO((Object)dynamicObject, decorateSource);
			}
			else
			{
				SetObjectMapByIO(dynamicObject, decorateSource);
			}
			dynamicObject.InitVmIOs();
			ConnectObjectWithIO(decorateSource.Root, dynamicObject, decorateSource);
		}
	}

	private void SetObjectMapByIO(Object displayObj, VmIO io)
	{
		List<VmIO> allLeafIO = io.GetAllLeafIO();
		if (io.TypeName == "CIRCLE")
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("centerx")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("centery")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MajorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("radius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MinorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("radius")).Name;
		}
		else if (io.TypeName == "POINT")
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("pointx")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("pointy")).Name;
		}
		else if (io.TypeName == "ROIBOX")
		{
			displayObj.okcolorA = "#0000ff";
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roicenterx")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roicentery")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Width").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roiwidth")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Height").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roiheight")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Angle").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roiangle")).Name;
		}
		else if (io.TypeName == "Rect")
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Width").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectWidth")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Height").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectHeight")).Name;
		}
		else if (io.TypeName == "LINE")
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "StartLineX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineStartPointX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "StartLineY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineStartPointY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "EndLineX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineEndPointX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "EndLineY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineEndPointY")).Name;
		}
		else if (io.TypeName == "CONTOURPOINTS")
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "OutLineInfo").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ContourInfo")).Name;
		}
		else if (io.TypeName == "ROIPOLYGON")
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PointNum").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlindPolygonPointNum")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PointXArray").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlindPolygonPointsX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PointYArray").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlindPolygonPointsY")).Name;
		}
		else if (io.TypeName == "ROIANNULUS")
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusCenterX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusCenterY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "OuterRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusOuterRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "InnerRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusInnerRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "StartAngle").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusStartAngle")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "AngleRange").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusAngleExtend")).Name;
		}
		else if (io.TypeName == "ELLIPSE")
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("CenterX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("CenterY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MajorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("MajorRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MinorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("MinorRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Angle").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("Angle")).Name;
		}
		else if (io.TypeName == "pointset")
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "OutLineInfo").MappingA = io.Name;
		}
		else if (io.TypeName == "STRING".ToLower())
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Content").MappingA = io.Name;
		}
		try
		{
			if (!(io.TypeName != "CONTOURPOINTS") || !(io.TypeName != "pointset"))
			{
				return;
			}
			VmIO vmIO = io.RelateIO.Root.Outputs.FirstOrDefault((VmIO x) => x.Name == "ModuStatus");
			VmIO vmIO2 = io.Root.Outputs.FirstOrDefault((VmIO x) => x.Name.Contains("ModuStatus"));
			if (vmIO2 == null || vmIO2.RelateIO.ModulePathName != vmIO.ModulePathName)
			{
				VmIO vmIO3 = SetInternalObjectToParentObjectOperation.CreatOutputIOByOldIO(io.Root, vmIO);
				displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Status").MappingA = vmIO3.Name;
			}
			else
			{
				displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Status").MappingA = vmIO2.Name;
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error("Add status io error: " + ex.Message);
		}
	}

	private void ConnectObjectWithIO(VmModule root, DynamicObject newObject, VmIO decorateSource)
	{
		newObject.RelateIO = decorateSource;
	}

	private void RemoveDisplayObject(VmIO decorateSource)
	{
		RemoveConnectObjectWithIO(decorateSource.Root, decorateSource);
	}

	private void RemoveConnectObjectWithIO(VmModule root, VmIO decorateSource)
	{
		ParamRoot paramRoot = root["[Display]"] as ParamRoot;
		DynamicObject dynamicObject = paramRoot.GetDisplayList().OfType<DynamicObject>().Single((DynamicObject p) => p.RelateIO == decorateSource);
		dynamicObject.RemoveSelf();
	}

	public ButtonSelecter GetButtonSelecter(VmIO vmIO, object owner)
	{
		ButtonSelecter buttonSelecter = null;
		BaseNode baseNode = null;
		baseNode = ((!vmIO.IsLeafNode) ? GroupsRoot.GetInstance()["SubscriptionCombinationSort"] : GroupsRoot.GetInstance()["SubscriptionFilterSort"]);
		if (baseNode is GroupInfoItem)
		{
			GroupInfoItem groupInfoItem = (baseNode as GroupInfoItem).Clone<GroupInfoItem>();
			if (groupInfoItem != null && groupInfoItem.Items != null)
			{
				using IEnumerator<object> enumerator = groupInfoItem.Items.GetEnumerator();
				if (enumerator.MoveNext())
				{
					BaseNode baseNode2 = (BaseNode)enumerator.Current;
					buttonSelecter = baseNode2 as ButtonSelecter;
				}
			}
		}
		if (buttonSelecter != null)
		{
			buttonSelecter.SetParentNode(owner);
			buttonSelecter.DropOpenSetter.OperationParams = vmIO.TypeName;
			if (vmIO.IsLeafNode)
			{
				buttonSelecter.DropOpenSetter.OperationParams = vmIO.Name;
			}
			buttonSelecter.Triggers.OfType<Trigger>().First().Setters.OfType<Setter>().First().OperationParams = vmIO.Name;
			buttonSelecter.Initers.OfType<Setter>().First().OperationParams = vmIO.Name;
			buttonSelecter.InniterBusiness();
			buttonSelecter.OnCurValueChanged -= SortSelecterParamValueChanged;
			buttonSelecter.OnCurValueChanged += SortSelecterParamValueChanged;
			return buttonSelecter;
		}
		return null;
	}

	public void SortSelecterParamValueChanged(object sender, EventArgs e)
	{
		DataSortConfig dataSortConfig = ((Owner.Owner.Params.Data as AlgorithmTabRoot).Tabs[1] as Tab).Collection[0] as DataSortConfig;
		dataSortConfig.SetSortParam();
	}

	private void SetIoParams()
	{
		Owner.SetIoParams();
	}

	private void TextChangedExecute(string newName)
	{
		try
		{
			if (newName.Contains("%"))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_UnSptChar") + "%");
			}
			else
			{
				if (!CommonNameCheck(newName))
				{
					return;
				}
				DataSortConfig dataSortConfig = ((Owner.Owner.Params.Data as AlgorithmTabRoot).Tabs[1] as Tab).Collection[0] as DataSortConfig;
				foreach (SortItem sortItem in dataSortConfig.SortItems)
				{
					if (sortItem?.SortSelecter == null)
					{
						continue;
					}
					string curValue = sortItem.SortSelecter.CurValue;
					if (!string.IsNullOrEmpty(curValue) && curValue.Length >= 2)
					{
						curValue = curValue.Substring(0, curValue.Length - 2);
						if (DecorateSource != null && curValue == DecorateSource.ModulePathName)
						{
							sortItem.SortSelecter.CurValue = curValue.Split('.')[0] + "." + newName + "[]";
						}
					}
				}
				Name = newName;
				SetIoParams();
				dataSortConfig.SetSortParam();
				dataSortConfig.SetSortConfig();
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
	}

	private void DeleteIOCommandExecute()
	{
		DeleteIO();
	}

	public void DeleteIO()
	{
		try
		{
			List<VmIO> allIO = DecorateSource.GetAllIO();
			DataSortConfig dataSortConfig = ((Owner.Owner.Params.Data as AlgorithmTabRoot).Tabs[1] as Tab).Collection[0] as DataSortConfig;
			List<SortItem> list = new List<SortItem>();
			foreach (SortItem sortItem in dataSortConfig.SortItems)
			{
				if (sortItem?.SortSelecter != null)
				{
					string subStr = sortItem.SortSelecter.CurValue;
					string text = subStr;
					if (text != null && text.Length > 2 && allIO.Where((VmIO c) => c.ModulePathName == subStr.Substring(0, subStr.Length - 2)).Count() > 0)
					{
						list.Add(sortItem);
					}
				}
			}
			foreach (SortItem item in list)
			{
				item.DeleteSortItemCommandExe();
			}
		}
		catch (Exception ex)
		{
			LogHelper.Error(ex.ToString());
		}
		try
		{
			DataSortConfig dataSortConfig2 = ((Owner.Owner.Params.Data as AlgorithmTabRoot).Tabs[1] as Tab).Collection[0] as DataSortConfig;
			dataSortConfig2.SetSortParam();
			Owner.ItemsA.Remove(this);
			DecorateSource.RemoveSelf();
		}
		catch (Exception ex2)
		{
			LogHelper.Error(ex2.ToString());
		}
	}

	private void DisplayChildrenCommandExecute()
	{
		//IL_0001: Unknown result type (might be due to invalid IL or missing references)
		//IL_0007: Expected Obj, but got Unknown
		ShowChildWin val = new ShowChildWin();
		((FrameworkElement)(object)val).DataContext = this;
		((BaseWindow)(object)val).ShowDialog();
	}
}
