using System;
using System.Collections.Generic;
using Apps.Localization;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class ReadDataDynamicIO : DynamicIO
{
	public static int SelectQueueIndex = 0;

	public string filterID;

	public List<ReadDataModuleIO> saveData_ReadDataModule = new List<ReadDataModuleIO>();

	private ReadDataModuleIO _ReadDataModuleFilter;

	private ReadDataModuleIO cacheModuleIo = null;

	private string oldName = "";

	public bool IsTypeChange { get; set; } = false;

	public string ImageNo
	{
		get
		{
			if (IoDataSource.TypeName == "IMAGE" && IoDataSource.Children.Count > 0)
			{
				string name = IoDataSource.Children[0].Name;
				string text = "%IMAGE";
				return name.Substring(text.Length, name.Length - text.Length - 1);
			}
			return "0";
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
			if (value != IoDataSource.TypeName)
			{
				if (value == "IMAGE" || IoDataSource.TypeName == "IMAGE")
				{
					int num = (GetParentNode() as ReadDatasModuleDynamicIO).ItemsA.IndexOf(this);
					(GetParentNode() as ReadDatasModuleDynamicIO).IsChangeType = false;
					ReadDataModuleIO readDataModuleFilterA = ReadDataModuleFilterA;
					(GetParentNode() as IDynamic).RemoveIO(this);
					(GetParentNode() as ReadDatasModuleDynamicIO).IsChangeType = true;
					object obj = (GetParentNode() as IDynamic).AddIO(NameA, value, num + 1);
					readDataModuleFilterA?.SetParentNode(obj);
					(obj as ReadDataDynamicIO).SetReadDataModuleIO(readDataModuleFilterA);
					(obj as ReadDataDynamicIO).SetParamUnit();
				}
				else
				{
					IoDataSource.TypeName = value;
				}
				OnPropertyChanged("ValueTypeA");
				OnPropertyChanged("FilterSelecter");
			}
		}
	}

	public string FilterID
	{
		get
		{
			if (string.IsNullOrWhiteSpace(filterID))
			{
				filterID = ReadDataModuleFilterA?.GetUIParams("FilterID");
				if (string.IsNullOrEmpty(filterID))
				{
					filterID = Guid.NewGuid().ToString("N");
					ReadDataModuleFilterA?.SetUIParams("FilterID", filterID);
				}
			}
			return filterID;
		}
	}

	public string FilterInfor => FilterID + "#" + NameA + "#" + NameA + "#" + ValueTypeA + "#" + Convert.ToInt32(value: true) + "#" + Convert.ToInt32(value: false) + "#RW#" + Convert.ToInt32(CustomVisibleA) + "#" + Convert.ToInt32(IsAddedA);

	public ReadDataModuleIO ReadDataModuleFilterA
	{
		get
		{
			if (!CustomVisibleA)
			{
				saveData_ReadDataModule.Clear();
				return null;
			}
			if (null == _ReadDataModuleFilter)
			{
				EnumerationDataCenter enumDataCenter = null;
				EnumerationCommunication enumerationCommunication = null;
				RadioSelecterEx3 radSelecter = null;
				object algorithmItems = (GetParentNode() as ReadDatasModuleDynamicIO).AlgorithmItems;
				if (algorithmItems != null)
				{
					enumDataCenter = (algorithmItems as AlgorithmTabRoot).FindBaseNodeByName("DataCenter") as EnumerationDataCenter;
					enumerationCommunication = (algorithmItems as AlgorithmTabRoot).FindBaseNodeByName("CommunicationModuleID") as EnumerationCommunication;
					radSelecter = (algorithmItems as AlgorithmTabRoot).FindBaseNodeByName("OutputType") as RadioSelecterEx3;
				}
				if (cacheModuleIo != null)
				{
					_ReadDataModuleFilter = cacheModuleIo;
					_ReadDataModuleFilter.SetParentNode(this);
					_ReadDataModuleFilter.Init(enumDataCenter, enumerationCommunication, radSelecter, CustomVisibleA);
				}
				else if (saveData_ReadDataModule != null && saveData_ReadDataModule.Count > 0)
				{
					_ReadDataModuleFilter = saveData_ReadDataModule[0];
					_ReadDataModuleFilter.SetParentNode(this);
					_ReadDataModuleFilter.Init(enumDataCenter, enumerationCommunication, radSelecter, CustomVisibleA);
				}
				else
				{
					_ReadDataModuleFilter = new ReadDataModuleIO();
					_ReadDataModuleFilter.SetParentNode(this);
					_ReadDataModuleFilter.Init(enumDataCenter, enumerationCommunication, radSelecter, CustomVisibleA);
					if (enumerationCommunication == null)
					{
						_ReadDataModuleFilter.SetGlobalVarIndex(0);
						_ReadDataModuleFilter.SetCommDeviceIndex(0);
						_ReadDataModuleFilter.SetQueueIndex(0);
					}
					if (CustomVisibleA && enumerationCommunication != null)
					{
						_ReadDataModuleFilter.SetParamUnit();
					}
					if (saveData_ReadDataModule != null && !saveData_ReadDataModule.Contains(_ReadDataModuleFilter))
					{
						saveData_ReadDataModule.Add(_ReadDataModuleFilter);
					}
				}
				_ReadDataModuleFilter.OutTypeChangeEventHandler += _ReadDataModuleFilter_OutTypeChangeEventHandler;
				return _ReadDataModuleFilter;
			}
			return _ReadDataModuleFilter;
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
			if (value.Contains("#"))
			{
				HikMessageBox.Show(AppLocalizationService.GetLang("Lang_NameContainError") + " #", MessageIcons.Error, isTranslate: true);
				OnPropertyChanged("NameA");
				return;
			}
			ReadDataModuleFilterA?.SetOldIndex();
			ReadDataModuleFilterA?.SetUIParams("FilterID", "", oldName);
			oldName = NameA;
			base.NameA = value;
			if (oldName != base.NameA && CustomVisibleA)
			{
				UpdateUseIndex(NameA);
				ReadDataModuleFilterA?.SetUIParams("FilterID", filterID, NameA);
			}
			if (oldName == base.NameA && CustomVisibleA)
			{
				ReadDataModuleFilterA?.SetUIParams("FilterID", filterID, NameA);
			}
		}
	}

	private bool _ReadDataModuleFilter_OutTypeChangeEventHandler(int obj)
	{
		cacheModuleIo = null;
		bool flag = (GetParentNode() as ReadDatasModuleDynamicIO).CheckImageType();
		if (flag)
		{
			_ReadDataModuleFilter.Dispose();
			_ReadDataModuleFilter.OutTypeChangeEventHandler -= _ReadDataModuleFilter_OutTypeChangeEventHandler;
		}
		return flag;
	}

	public void SetReadDataModuleIO(ReadDataModuleIO data)
	{
		cacheModuleIo = data;
	}

	public override int SetParamUnit()
	{
		if (_ReadDataModuleFilter == null && cacheModuleIo != null)
		{
			ReadDataModuleIO readDataModuleFilterA = ReadDataModuleFilterA;
		}
		if (_ReadDataModuleFilter != null)
		{
			return _ReadDataModuleFilter.SetParamUnit();
		}
		return -1;
	}

	public void UpdateUseIndex(string newName = null)
	{
		_ReadDataModuleFilter?.UpdateUIParams(newName);
	}

	public void GetCommAddress()
	{
		ReadDataModuleFilterA?.GetCommAddress();
	}
}
