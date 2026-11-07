using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using Apps.Interface;
using Apps.Localization;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser;

public class DynamicObject : Object, IObjectCollection
{
	public const string PrefixKeyName = "DynamicObject_";

	public const string KeyName = "DynamicObject";

	public const string SuffixKeyName = "_[Child]";

	private VmIO relateIO;

	private bool isLoadedRelateIO = false;

	private const string ExternInfoKeyName = "ObjectExternInfos";

	public override string NameA
	{
		get
		{
			if (!string.IsNullOrEmpty(Name) && Name.First() != '%')
			{
				Name = AppLocalizationService.UnDoTranslate(Name);
			}
			return Name;
		}
		set
		{
			if (Name != AppLocalizationService.UnDoTranslate(value))
			{
				string moduleName = ((IVmFullPath)this)?.FullPath;
				string nameA = NameA;
				Name = AppLocalizationService.UnDoTranslate(value);
				OnPropertyChanged("NameA");
				OnNameChanged(nameA);
				if (!string.IsNullOrWhiteSpace(moduleName) && !string.IsNullOrWhiteSpace(((IVmFullPath)this)?.FullPath))
				{
					((IVmSolution)VmSolution.Instance).OnModuleActionChanged("ReName", moduleName, ((IVmFullPath)this)?.FullPath);
				}
			}
		}
	}

	private IList CollectionParent { get; set; }

	internal bool IsLoading { get; set; }

	public VmIO RelateIO
	{
		get
		{
			if (!isLoadedRelateIO)
			{
				isLoadedRelateIO = true;
				string text = LoadExternInfo("RelateIO");
				if (text != null)
				{
					relateIO = (Owner as VmModule).GetIO(text);
					PropertyChangedEventManager.AddHandler(relateIO, RelateIO_PropertyChanged, "Name");
				}
			}
			return relateIO;
		}
		set
		{
			if (value != relateIO)
			{
				PropertyChangedEventManager.RemoveHandler(relateIO, RelateIO_PropertyChanged, "Name");
				relateIO = value;
				SaveExternInfo("RelateIO", relateIO.Name);
				PropertyChangedEventManager.AddHandler(relateIO, RelateIO_PropertyChanged, "Name");
			}
		}
	}

	public event EventHandler MappingChanged;

	protected override void OnNameChanged(string oldName)
	{
		base.OnNameChanged(oldName);
		if (Owner is VmModule vmModule)
		{
			ReNameInfo(vmModule, oldName, NameA);
		}
	}

	private void RelateIO_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		SaveExternInfo("RelateIO", (sender as VmIO).Name);
	}

	void IObjectCollection.OnAddCollection(IList ls)
	{
		foreach (FeatureBase item in FeaturesB)
		{
			item.PropertyChanged += Feature_PropertyChanged;
		}
		CollectionParent = ls;
		if (IsLoading)
		{
			IsLoading = false;
		}
		else if (Owner is VmModule vmModule)
		{
			string key = "DynamicObject";
			vmModule.UserData[key] = StringUtil.UTF8GetBytesPadZero(string.Join(";", from p in ls.OfType<DynamicObject>()
				select p.NameA));
			vmModule.UserData["DynamicObject_" + NameA + "_[Child]"] = StringUtil.UTF8GetBytesPadZero(string.Join(";", FeaturesB.Select((FeatureBase p) => p.NameA)));
		}
	}

	public void RemoveSelf()
	{
		FeaturesB.ToList().ForEach((FeatureBase p) =>
		{
			p.DeInitVmIOs();
		});
		CollectionParent.Remove(this);
		ClearPropertyInfo();
		ClearExternInfo();
		((IVmSolution)VmSolution.Instance).OnModuleActionChanged("DeleteObject", ((IVmFullPath)this)?.FullPath, string.Empty);
	}

	private void ClearPropertyInfo()
	{
		foreach (FeatureBase item in FeaturesB)
		{
			item.ClearPropertyInfo();
		}
		okcolorA = null;
		ngcolorA = null;
		SaveOpacity(null, NameA);
		SaveIsDisplay(null, NameA);
		SaveIsFill(null, NameA);
		SaveStrokeThickness(null, NameA);
		TypeA = null;
	}

	private void Feature_PropertyChanged(object sender, PropertyChangedEventArgs e)
	{
		Feature feature = sender as Feature;
		if (Owner is VmModule && e.PropertyName == "MappingA")
		{
			MappingChanged?.Invoke(this, null);
		}
	}

	void IObjectCollection.OnRemoveCollection()
	{
		IList collectionParent = CollectionParent;
		if (!(Owner is VmModule vmModule))
		{
			return;
		}
		vmModule.UserData["DynamicObject_" + NameA] = null;
		string key = "DynamicObject";
		vmModule.UserData[key] = StringUtil.UTF8GetBytesPadZero(string.Join(";", from p in CollectionParent.OfType<DynamicObject>()
			select p.Name));
		foreach (FeatureBase item in FeaturesB)
		{
			vmModule.UserData["DynamicObject_" + NameA + "_" + item.NameA] = null;
			item.RemoveOutputFilter();
		}
		vmModule.UserData["DynamicObject_" + NameA + "_[Child]"] = null;
	}

	private void ReNameInfo(VmModule vmModule, string oldName, string newName)
	{
		byte[] array = vmModule.UserData["DynamicObject_" + oldName];
		vmModule.UserData["DynamicObject_" + oldName] = null;
		vmModule.UserData["DynamicObject_" + newName] = array;
		string key = "DynamicObject";
		vmModule.UserData[key] = StringUtil.UTF8GetBytesPadZero(string.Join(";", from p in CollectionParent.OfType<DynamicObject>()
			select p.Name));
		foreach (FeatureBase item in FeaturesB)
		{
			item.ReNameInfo(oldName, newName);
		}
		vmModule.UserData["DynamicObject_" + NameA + "_[Child]"] = StringUtil.UTF8GetBytesPadZero(string.Join(";", FeaturesB.Select((FeatureBase p) => p.NameA)));
		List<string> list = new List<string>();
		string key2 = "ObjectExternInfos_" + oldName;
		string key3 = "ObjectExternInfos_" + newName;
		byte[] array2 = vmModule.UserData[key2];
		vmModule.UserData[key3] = array2;
		if (array2 != null)
		{
			list = StringUtil.UTF8GetString(array2).Split(';').ToList();
			list.ForEach((string p) =>
			{
				vmModule.UserData[p + "_" + newName] = vmModule.UserData[p + "_" + oldName];
				vmModule.UserData[p + "_" + oldName] = null;
			});
		}
		vmModule.InvokeOnModuleDisplayParamNameChanged(oldName, newName);
	}

	private void ClearExternInfo()
	{
		VmModule vmModule;
		if ((vmModule = Owner as VmModule) == null)
		{
			return;
		}
		List<string> list = new List<string>();
		string key = "ObjectExternInfos_" + Name;
		byte[] array = vmModule.UserData[key];
		if (array != null)
		{
			list = (from p in StringUtil.UTF8GetString(array).Split(';')
				where !string.IsNullOrEmpty(p)
				select p).ToList();
			list.ForEach((string p) =>
			{
				vmModule.UserData[p + "_" + Name] = null;
			});
			vmModule.UserData[key] = null;
		}
	}

	public void SaveExternInfo(string key, string value)
	{
		if (!(Owner is VmModule vmModule))
		{
			return;
		}
		string key2 = key + "_" + Name;
		List<string> list = new List<string>();
		string key3 = "ObjectExternInfos_" + Name;
		byte[] array = vmModule.UserData[key3];
		if (array != null)
		{
			list = StringUtil.UTF8GetString(array).Split(';').ToList();
		}
		vmModule.UserData[key2] = ((value == null) ? null : StringUtil.UTF8GetBytesPadZero(value));
		if (value == null)
		{
			if (list.Contains(key))
			{
				list.Remove(key);
				vmModule.UserData[key3] = StringUtil.UTF8GetBytesPadZero(string.Join(";", list));
			}
		}
		else if (!list.Contains(key))
		{
			list.Add(key);
			vmModule.UserData[key3] = StringUtil.UTF8GetBytesPadZero(string.Join(";", list));
		}
	}

	public string LoadExternInfo(string key)
	{
		if (Owner is VmModule vmModule)
		{
			string key2 = key + "_" + Name;
			byte[] array = vmModule.UserData[key2];
			if (array != null)
			{
				return StringUtil.UTF8GetString(array);
			}
		}
		return null;
	}
}
