using System.Linq;
using Apps.Interface;
using Apps.Localization;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class DataSetModuleDynamicIO : DynamicIOContainer<DataSetDynamicIO>
{
	public override string DefaultName => "var";

	public DataSetModuleDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	public override object AddIO(string name = null, string type = null, int index = -1)
	{
		if (DataSource == null)
		{
			return null;
		}
		if (ItemsA.Where((object x) => (x as DataSetDynamicIO).CustomVisibleA).Count() >= MaxItemCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " :" + MaxItemCount, MessageButtons.Normal, MessageIcons.Warning);
			return null;
		}
		string typeName = ((type == null) ? DefaultTypeName : type);
		VmIO vmIO = null;
		vmIO = AddIoByType(typeName, name);
		vmIO.IsReturnRelateValue = false;
		DataSetDynamicIO dataSetDynamicIO = new DataSetDynamicIO
		{
			IoDataSource = vmIO,
			CustomVisibleA = true
		};
		if (index > -1)
		{
			dataSetDynamicIO.IndexA = index.ToString();
			dataSetDynamicIO.SetParentNode(this);
			ItemsA.Insert(index - 1, dataSetDynamicIO);
		}
		else
		{
			dataSetDynamicIO.IndexA = (ItemsA.Where((object x) => (x as DataSetDynamicIO).CustomVisibleA).Count() + 1).ToString();
			dataSetDynamicIO.SetParentNode(this);
			ItemsA.Add(dataSetDynamicIO);
		}
		UpdateCanDelete();
		dataSetDynamicIO.SetParamUnit();
		return dataSetDynamicIO;
	}
}
