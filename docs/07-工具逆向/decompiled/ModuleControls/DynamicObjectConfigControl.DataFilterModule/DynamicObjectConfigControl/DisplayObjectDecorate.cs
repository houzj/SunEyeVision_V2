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

public class DisplayObjectDecorate : NotifyPropertyChangedBase, IButtonSelecter
{
	private DynamicObject _DecorateSource;

	protected ButtonSelecter _imageFilterSelecter;

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

	public string RelateIOPre => string.Format("({0})", AppLocalizationService.GetLang(DecorateSource.RelateIO.Root.Inputs.Contains(DecorateSource.RelateIO) ? "输入" : "Output"));

	public object RelateIO => DecorateSource.RelateIO;

	public string ValueType => DecorateSource.TypeA;

	public virtual ButtonSelecter ImageFilterSelecter
	{
		get
		{
			if (_imageFilterSelecter == null)
			{
				_imageFilterSelecter = DecorateSource.GetButtonSelecterFromGroup(Owner);
			}
			return _imageFilterSelecter;
		}
	}

	ButtonSelecter IButtonSelecter.Selecter => ImageFilterSelecter;

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
		shapePropertyConfigWin.ShowDialog();
	}

	protected virtual void SetTabItemProperty(Tab cloneTab)
	{
	}
}
