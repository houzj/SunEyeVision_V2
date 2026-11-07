using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Apps.Localization;
using DynamicObjectConfigControl.Common;
using ShellCommonControl;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ShellDynamicIO : DynamicIO
{
	private string _displayValueType;

	private bool _isDisplayForImage;

	private ObservableCollection<string> _shellInputVar = new ObservableCollection<string>();

	private ObservableCollection<string> _shellOutputVar = new ObservableCollection<string>();

	public override List<string> IoSelectValueTypes => new List<string>
	{
		"int", "int[]", "float", "float[]", "string", "string[]", "byte", "IMAGE", "ROIBOX", "ROIBOX[]",
		"ANNULUS", "POLYGON", "POINT", "LINE", "FIXTURE", "RECT", "ELLIPSE", "pointset"
	};

	public string DisplayValueTypeA
	{
		get
		{
			if (string.IsNullOrEmpty(_displayValueType))
			{
				if (ValueTypeA == "ROIANNULUS")
				{
					_displayValueType = "ANNULUS";
				}
				else if (ValueTypeA == "ROIPOLYGON")
				{
					_displayValueType = "POLYGON";
				}
				else if (ValueTypeA == "Rect")
				{
					_displayValueType = "RECT";
				}
				else
				{
					_displayValueType = ValueTypeA;
				}
			}
			return _displayValueType;
		}
		set
		{
			if (!(value != _displayValueType))
			{
				return;
			}
			_displayValueType = value;
			OnPropertyChanged("DisplayValueTypeA");
			switch (value)
			{
			case "ANNULUS":
				ValueTypeA = "ROIANNULUS";
				return;
			case "POLYGON":
				ValueTypeA = "ROIPOLYGON";
				return;
			case "RECT":
				ValueTypeA = "Rect";
				return;
			}
			if (value.Contains("[]"))
			{
				ValueTypeA = value.Substring(0, value.Length - 2);
			}
			else
			{
				ValueTypeA = value;
			}
		}
	}

	public override string ValueTypeA
	{
		get
		{
			return base.ValueTypeA;
		}
		set
		{
			if (!(value != IoDataSource.TypeName))
			{
				return;
			}
			if (CombineType.Contains(value) || CombineType.Contains(IoDataSource.TypeName))
			{
				if (GetParentNode() is ShellModuleDynamicIO shellModuleDynamicIO)
				{
					int num = (string.IsNullOrEmpty(IndexA) ? (-1) : int.Parse(IndexA));
					shellModuleDynamicIO.ChangeRemoveIO(this);
					shellModuleDynamicIO.AddIO(NameA, value, num, _displayValueType);
				}
			}
			else
			{
				bool flag = false;
				if (value.Equals("pointset") || IoDataSource.TypeName.Equals("pointset"))
				{
					flag = true;
				}
				IoDataSource.TypeName = value;
				if (flag && GetParentNode() is ShellModuleDynamicIO { IsOutput: not false } shellModuleDynamicIO2)
				{
					ShellHelper.ChangeRecordObject(shellModuleDynamicIO2.Owner as VmModule, shellModuleDynamicIO2.ItemsA.ToList());
				}
			}
			SetParamUnit();
			OnPropertyChanged("ValueTypeA");
			OnPropertyChanged("FilterSelecter");
			OnPropertyChanged("IsShowDisplayCheckBox");
		}
	}

	public override string NameA
	{
		get
		{
			return base.NameA;
		}
		set
		{
			string nameA = base.NameA;
			if (ShellHepler.IsValidIdentifier(value))
			{
				OnPropertyChanged("NameA");
				return;
			}
			base.NameA = value;
			if (nameA != base.NameA && GetParentNode() is ShellModuleDynamicIO { IsOutput: not false } shellModuleDynamicIO)
			{
				shellModuleDynamicIO.UpdateDisplayObjectName(IoDataSource, nameA, base.NameA);
				ShellHelper.ChangeRecordObject(shellModuleDynamicIO.Owner as VmModule, shellModuleDynamicIO.ItemsA.ToList());
			}
		}
	}

	public bool IsShowDisplayCheckBox => DynamicObjectManager.OutputDisplayDic.Keys.Contains(ValueTypeA);

	public bool IsDisplayForImage
	{
		get
		{
			ParamRoot paramRoot = IoDataSource.Root["[Display]"] as ParamRoot;
			_isDisplayForImage = paramRoot.GetDisplayList().OfType<DynamicObject>().Any((DynamicObject p) => p.RelateIO == IoDataSource);
			return _isDisplayForImage;
		}
		set
		{
			if (_isDisplayForImage != value)
			{
				_isDisplayForImage = value;
				if (value)
				{
					DynamicObjectManager.AddDisplayObject(IoDataSource);
				}
				else
				{
					DynamicObjectManager.RemoveDisplayObject(IoDataSource);
				}
				ButtonSelecter_Shell buttonSelecter_Shell = GetParentNode<ButtonSelecter_Shell>();
				if (buttonSelecter_Shell != null)
				{
					buttonSelecter_Shell.ShellDisplayContainerA.UpdateDisplayItems();
				}
				OnPropertyChanged("IsDisplayForImage");
			}
		}
	}

	public ObservableCollection<string> ShellInputVarA
	{
		get
		{
			if (_shellInputVar.Count == 0)
			{
				_shellInputVar.Add(AppLocalizationService.GetLang("Lang_RstEpt"));
			}
			return _shellInputVar;
		}
		set
		{
			if (_shellInputVar != value)
			{
				_shellInputVar = value;
				OnPropertyChanged("ShellInputVarA");
			}
		}
	}

	public ObservableCollection<string> ShellOutputVarA
	{
		get
		{
			if (_shellOutputVar.Count == 0)
			{
				_shellOutputVar.Add(AppLocalizationService.GetLang("Lang_RstEpt"));
			}
			return _shellOutputVar;
		}
		set
		{
			if (_shellOutputVar != value)
			{
				_shellOutputVar = value;
				OnPropertyChanged("ShellOutputVarA");
			}
		}
	}
}
