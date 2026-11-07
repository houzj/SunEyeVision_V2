using System.Collections.ObjectModel;
using System.Linq;
using Apps.Interface;

namespace Apps.XmlParser.ParamTab;

public class ReadDatasModuleDynamicIO : DynamicIOContainer<ReadDataDynamicIO>
{
	private ObservableCollection<object> communicationItems;

	public bool IsChangeType = true;

	public override bool IsInitAdd => true;

	public override string DefaultTypeName => "string";

	public ObservableCollection<object> CommunicationItemsA
	{
		get
		{
			if (communicationItems == null)
			{
				communicationItems = new ObservableCollection<object>();
			}
			communicationItems.Clear();
			if (ItemsA == null)
			{
				return communicationItems;
			}
			object obj = ItemsA.FirstOrDefault((object p) => p is ReadDataDynamicIO && (p as ReadDataDynamicIO).CustomVisibleA);
			if (obj != null)
			{
				communicationItems.Add(obj);
			}
			return communicationItems;
		}
	}

	public ReadDatasModuleDynamicIO()
	{
	}

	public ReadDatasModuleDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	public override object AddIO(string name = null, string type = null, int index = -1)
	{
		object obj = base.AddIO(name, type, index);
		if (obj != null)
		{
			if (index > -1)
			{
				(obj as ReadDataDynamicIO).IsTypeChange = true;
				(obj as ReadDataDynamicIO).IndexA = (index - ItemsA.Where((object x) => !(x as ReadDataDynamicIO).CustomVisibleA).Count()).ToString();
			}
			(obj as DynamicIO).AddImageObject();
		}
		return obj;
	}

	public override void RemoveIO(object dymaicIO)
	{
		if (dymaicIO is ReadDataDynamicIO readDataDynamicIO)
		{
			readDataDynamicIO.UpdateUseIndex();
			readDataDynamicIO.IsAddedA = false;
			readDataDynamicIO.RemoveImageObject(readDataDynamicIO.IoDataSource);
			base.RemoveIO(dymaicIO);
			if (IsChangeType)
			{
				readDataDynamicIO.ReadDataModuleFilterA?.SetUIParams("FilterID", "");
				readDataDynamicIO.ReadDataModuleFilterA?.RecordGlobalQueueIndex();
			}
		}
	}

	public void GetCommAddress()
	{
		object obj = ItemsA.First((object p) => (p as ReadDataDynamicIO).ReadDataModuleFilterA != null);
		if (obj != null && obj is ReadDataDynamicIO && (obj as ReadDataDynamicIO).ReadDataModuleFilterA != null)
		{
			(obj as ReadDataDynamicIO).GetCommAddress();
		}
	}

	public bool CheckImageType()
	{
		object obj = ItemsA.FirstOrDefault((object x) => (x as DynamicIO).ValueTypeA == "IMAGE");
		if (obj != null)
		{
			string nameA = (obj as DynamicIO).NameA;
			int num = ItemsA.IndexOf(obj);
			IsChangeType = false;
			ReadDataModuleIO readDataModuleIO = (obj as ReadDataDynamicIO)?.ReadDataModuleFilterA;
			if (readDataModuleIO != null)
			{
				RemoveIO(obj);
				IsChangeType = true;
				object obj2 = AddIO(nameA, "int", num + 1);
				readDataModuleIO.SetParentNode(obj2);
				(obj2 as ReadDataDynamicIO).SetReadDataModuleIO(readDataModuleIO);
				return true;
			}
		}
		return false;
	}
}
