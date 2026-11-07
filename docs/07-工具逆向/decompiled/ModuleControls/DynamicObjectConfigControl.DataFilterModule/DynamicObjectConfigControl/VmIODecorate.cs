using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Apps.Localization;
using Apps.XmlParser;
using Apps.XmlParser.ParamTab;
using DynamicObjectConfigControl.UI;
using Mvvm.Commands;
using VM.CommonData;
using VM.Controls;
using VM.Core;
using VMControls.Interface;

namespace DynamicObjectConfigControl;

internal class VmIODecorate : NotifyPropertyChangedBase, IDecorate, IButtonSelecter
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

	private ButtonSelecter _FilterSelecter;

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
		DynamicObject dynamicObject = GroupDisplayParam.AddDynamicObject(decorateSource.Root, OutputDisplayDic[decorateSource.TypeName]);
		if (dynamicObject != null)
		{
			if (decorateSource.TypeName == "IMAGE" || decorateSource.TypeName == "PIXELIMAGE")
			{
				SetInternalObjectToParentObjectOperation.SetImageObjectMapByIO(dynamicObject, decorateSource);
			}
			else
			{
				SetObjectMapByIO(dynamicObject, decorateSource);
			}
			dynamicObject.InitVmIOs();
			ConnectObjectWithIO(decorateSource.Root, dynamicObject, decorateSource);
		}
	}

	private void SetObjectMapByIO(Apps.XmlParser.Object displayObj, VmIO io)
	{
		List<VmIO> allLeafIO = io.GetAllLeafIO();
		switch (io.TypeName)
		{
		case "CIRCLE":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("centerx")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("centery")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MajorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("radius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MinorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("radius")).Name;
			break;
		case "POINT":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("pointx")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("pointy")).Name;
			break;
		case "ROIBOX":
			displayObj.okcolorA = "#0000ff";
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roicenterx")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roicentery")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Width").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roiwidth")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Height").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roiheight")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Angle").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("roiangle")).Name;
			break;
		case "Rect":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Width").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectWidth")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Height").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlobRectHeight")).Name;
			break;
		case "LINE":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "StartLineX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineStartPointX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "StartLineY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineStartPointY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "EndLineX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineEndPointX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "EndLineY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("LineEndPointY")).Name;
			break;
		case "CONTOURPOINTS":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "OutLineInfo").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("ContourInfo")).Name;
			break;
		case "ROIPOLYGON":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PointNum").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlindPolygonPointNum")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PointXArray").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlindPolygonPointsX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "PointYArray").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("BlindPolygonPointsY")).Name;
			break;
		case "ROIANNULUS":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusCenterX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusCenterY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "OuterRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusOuterRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "InnerRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusInnerRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "StartAngle").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusStartAngle")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "AngleRange").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("DetectAnnulusAngleExtend")).Name;
			break;
		case "ELLIPSE":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterX").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("CenterX")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "CenterY").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("CenterY")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MajorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("MajorRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "MinorRadius").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("MinorRadius")).Name;
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Angle").MappingA = allLeafIO.Single((VmIO p) => p.Name.Contains("Angle")).Name;
			break;
		case "pointset":
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "OutLineInfo").MappingA = io.Name;
			break;
		default:
			if (io.TypeName == "STRING".ToLower())
			{
				displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Content").MappingA = io.Name;
			}
			break;
		}
		if (!(io.TypeName != "CONTOURPOINTS") || !(io.TypeName != "pointset"))
		{
			return;
		}
		VmIO vmIO = io.RelateIO?.Root.Outputs.FirstOrDefault((VmIO x) => x.Name == "ModuStatus");
		VmIO vmIO2 = io.Root.Outputs.FirstOrDefault((VmIO x) => x.Name.Contains("ModuStatus"));
		if (vmIO != null && (vmIO2 == null || vmIO2.RelateIO.ModulePathName != vmIO.ModulePathName))
		{
			VmIO vmIO3 = SetInternalObjectToParentObjectOperation.CreatOutputIOByOldIO(io.Root, vmIO);
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Status").MappingA = vmIO3.Name;
		}
		else if (vmIO2 != null)
		{
			displayObj.FeaturesB.Single((FeatureBase p) => p.Name == "Status").MappingA = vmIO2.Name;
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
	}

	private void DisplayChildrenCommandExecute()
	{
		ShowChildWin showChildWin = new ShowChildWin();
		showChildWin.DataContext = this;
		showChildWin.ShowDialog();
	}
}
