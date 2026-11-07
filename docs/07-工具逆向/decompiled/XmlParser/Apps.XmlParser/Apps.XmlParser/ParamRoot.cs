using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Xml;
using Apps.Interface;
using Apps.Log;
using Apps.UIHelper.Extern;
using Hik.Xml;
using VM.PlatformSDKCS;
using VMControls.Interface;
using VMControls.Interface.CustomAttribute;

namespace Apps.XmlParser;

[VmExternPath("[Display]")]
public class ParamRoot : BaseNode, IOwner, IRoot, INotifyPropertyChanged, IVmExternPath, IReload
{
	internal const string SavePreName = "[ParamRoot]_";

	private object owner;

	public ObservableCollection<object> Categorys;

	private Category _DisplayCategory;

	public string _strDisplayImageName = "";

	private IList<Object> _displayObjects;

	public object Owner
	{
		get
		{
			return owner;
		}
		set
		{
			if (owner != value)
			{
				owner = value;
				OnPropertyChanged("Owner");
			}
		}
	}

	public ObservableCollection<object> CategorysA
	{
		get
		{
			return Categorys;
		}
		set
		{
			Categorys = value;
			OnPropertyChanged("CategorysA");
		}
	}

	private Category DisplayCategory
	{
		get
		{
			if (_DisplayCategory == null)
			{
				_DisplayCategory = GetParamCategoryByName("Display");
			}
			return _DisplayCategory;
		}
	}

	public string strDisplayImageName
	{
		get
		{
			return _strDisplayImageName;
		}
		set
		{
			if (_strDisplayImageName != value)
			{
				_strDisplayImageName = value;
			}
		}
	}

	public event PropertyChangedEventHandler PropertyChanged;

	public event EventHandler ParamRootReloadEnd;

	protected virtual void OnPropertyChanged(string proName)
	{
		if (PropertyChanged != null)
		{
			PropertyChanged(this, new PropertyChangedEventArgs(proName));
		}
	}

	internal void OnParamRootReloadEnd()
	{
		ParamRootReloadEnd?.Invoke(this, null);
	}

	public override BaseNode Init(XmlNode node)
	{
		string text = BaseNode.defaultNameSpace;
		BaseNode.defaultNameSpace = "Apps.XmlParser";
		base.Init(node);
		BaseNode.defaultNameSpace = text;
		return this;
	}

	public ParamRoot()
	{
	}

	public ParamRoot(string path)
		: this()
	{
		try
		{
			loadXml(path);
		}
		catch (Exception ex)
		{
			throw ex;
		}
	}

	public void Load(byte[] fileData)
	{
		loadXml(fileData);
	}

	internal Category GetParamCategoryByName(string groupName)
	{
		if (Categorys != null)
		{
			try
			{
				return Categorys.SingleOrDefault((object p) => (p as Category).Name == groupName) as Category;
			}
			catch (Exception)
			{
			}
			return null;
		}
		return null;
	}

	object IVmExternPath.GetExternNode(string sub)
	{
		if (sub == null || sub.Length == 0)
		{
			return this;
		}
		string[] subSplit = sub.Split('.');
		object obj = null;
		return GetDisplayList().OfType<Object>().Single((Object p) => p.NameA == subSplit[0]);
	}

	void IVmExternPath.PasteNode(object datas)
	{
		PasteNode(datas);
	}

	private void PasteNode(object datas)
	{
		if (!(Owner is IVmModule) || !(datas is Dictionary<object, object> dictionary))
		{
			return;
		}
		try
		{
			Dictionary<object, object> dictionary2 = new Dictionary<object, object>(dictionary);
			List<Object> list = GetDisplayList().OfType<Object>().ToList();
			IVmModuleSet moduleSet;
			if ((moduleSet = Owner as IVmModuleSet) != null && dictionary2.Where((KeyValuePair<object, object> p) => p.Value == moduleSet).FirstOrDefault().Key is IVmModuleSet { Children: var children })
			{
				IList children2 = moduleSet.Children;
				if (children != null && children2 != null && children.Count == children2.Count)
				{
					for (int num = 0; num < children.Count; num++)
					{
						IVmModule oldChild = children[num] as IVmModule;
						IEnumerable<IVmModule> obj = children2 as IEnumerable<IVmModule>;
						IVmModule value = ((obj != null) ? obj.FirstOrDefault((IVmModule p) => p.Name == oldChild?.Name) : null);
						dictionary2.Add(oldChild, value);
					}
				}
			}
			foreach (Object item in list)
			{
				if (item.FeaturesB == null || item.FeaturesB.Count <= 0)
				{
					continue;
				}
				foreach (FeatureBase item2 in item.FeaturesB)
				{
					(item2 as Feature)?.PasteFeature(dictionary2);
				}
			}
		}
		catch (Exception ex)
		{
			string message = ex.Message;
			if (ex is VmException ex2)
			{
				message = "ErrorCode = " + ex2.errorCode + ".ErrorMessage = " + ex2.errorMessage;
			}
			LogHelper.Error(message);
		}
	}

	void IVmExternPath.InitNode(object datas)
	{
		InitNode(datas);
	}

	private void InitNode(object datas)
	{
		if (datas is IVmModule { ModuleTypeName: var moduleTypeName } vmModule)
		{
			string fileName = vmModule.ModuleDir + "\\" + moduleTypeName + "Display.xml";
			loadXml(fileName, ignoreUnknown: true);
			owner = vmModule;
			InitDynamicObject(vmModule);
			ReloadProperty();
			InitVmIOs();
		}
	}

	public void InitVmIOs()
	{
		IList<object> displayList = GetDisplayList();
		if (displayList == null)
		{
			return;
		}
		foreach (object item in displayList)
		{
			(item as Object).InitVmIOs();
		}
	}

	private void DeInitVmIOs()
	{
		IList<object> displayList = GetDisplayList();
		if (displayList == null)
		{
			return;
		}
		foreach (object item in displayList)
		{
			(item as Object).FeaturesB.ToList().ForEach((FeatureBase p) =>
			{
				p.DeInitVmIOs();
			});
		}
	}

	private void ReloadProperty()
	{
		try
		{
			ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO_LIST iMVS_UI_OBJECT_PARAM_INFO_LIST = null;
			if (owner is IVmModule vmModule)
			{
				iMVS_UI_OBJECT_PARAM_INFO_LIST = ServerSDKManager.serverSDKManager.mModuleManager.GetUIParamValueByObject(vmModule.ID);
			}
			List<Object> list = GetDisplayList().OfType<Object>().ToList();
			if (list == null)
			{
				return;
			}
			ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO[] array = iMVS_UI_OBJECT_PARAM_INFO_LIST?.pUIObjParamInfo();
			foreach (Object item in list)
			{
				List<ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO> infos = ((array != null) ? array.Where((ImvsSdkDefine.IMVS_UI_OBJECT_PARAM_INFO p) => StringUtil.UTF8GetString(p.strParamName).Contains(item.NameA)).ToList() : null);
				item.UpdateResultShowParam(infos);
			}
		}
		catch (Exception ex)
		{
			string message = ex.Message;
			if (ex is VmException ex2)
			{
				message = "ErrorCode = " + ex2.errorCode + ".ErrorMessage = " + ex2.errorMessage;
			}
			LogHelper.Error(message);
		}
	}

	void IReload.Reload()
	{
		DeInitVmIOs();
		_DisplayCategory = null;
		Categorys = new ObservableCollection<object>();
		InitNode(owner);
		OnParamRootReloadEnd();
	}

	private void InitDynamicObject(IVmModule module)
	{
		try
		{
			InitDynamicObject(module, module.UserData["DynamicObject"], GetDisplayList() as ObjectCollection);
		}
		catch (Exception ex)
		{
			string message = ex.Message;
			if (ex is VmException ex2)
			{
				message = "ErrorCode = " + ex2.errorCode + ".ErrorMessage = " + ex2.errorMessage;
			}
			LogHelper.Error(message);
		}
	}

	private void InitDynamicObject(IVmModule module, byte[] Data, ObjectCollection objCollection)
	{
		if (Data == null)
		{
			return;
		}
		string text = StringUtil.UTF8GetString(Data);
		IEnumerable<string> enumerable = from p in text.Split(';')
			where !string.IsNullOrEmpty(p)
			select "DynamicObject_" + p;
		foreach (string item in enumerable)
		{
			string objName = item.Remove(0, "DynamicObject_".Length);
			if (objCollection.OfType<Object>().Any((Object p) => p.Name == objName))
			{
				continue;
			}
			DynamicObject dynamicObject = new DynamicObject
			{
				IsLoading = true
			};
			dynamicObject.SetParentNode(objCollection.Parent);
			dynamicObject.Name = objName;
			dynamicObject.Features = new ObservableCollection<FeatureBase>();
			byte[] array = module.UserData["DynamicObject_" + dynamicObject.Name + "_[Child]"];
			if (array != null)
			{
				string text2 = StringUtil.UTF8GetString(array);
				if (text2 != null)
				{
					enumerable = from p in text2.Split(';')
						where !string.IsNullOrEmpty(p)
						select "DynamicObject_" + dynamicObject.Name + "_" + p;
					foreach (string item2 in enumerable)
					{
						string name = item2.Remove(0, "DynamicObject_".Length + dynamicObject.Name.Length + 1);
						Feature feature = new Feature
						{
							Name = name
						};
						feature.SetParentNode(dynamicObject);
						dynamicObject.Features.Add(feature);
					}
				}
			}
			objCollection.Add(dynamicObject);
		}
	}

	public IList<object> GetDisplayList()
	{
		if (DisplayCategory != null)
		{
			return DisplayCategory.Items;
		}
		return null;
	}

	internal Object GetDisplayObject(int index)
	{
		if (_displayObjects == null)
		{
			_displayObjects = ((DisplayCategory != null) ? DisplayCategory.Items.OfType<Object>().ToList() : new List<Object>());
		}
		if (index < 0)
		{
			return null;
		}
		return (index > _displayObjects.Count - 1) ? null : _displayObjects[index];
	}

	internal Object GetDisplayObject(string name)
	{
		if (_displayObjects == null)
		{
			_displayObjects = ((DisplayCategory != null) ? DisplayCategory.Items.OfType<Object>().ToList() : new List<Object>());
		}
		return _displayObjects.FirstOrDefault((Object p) => p.NameA == name);
	}
}
