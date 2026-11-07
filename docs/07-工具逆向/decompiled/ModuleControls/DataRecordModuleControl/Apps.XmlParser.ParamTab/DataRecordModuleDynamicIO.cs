using System.Linq;
using Apps.Interface;
using Apps.Localization;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

/// <summary>
/// 数据集合模块动态IO
/// </summary>
public class DataRecordModuleDynamicIO : DynamicIOContainer<DataRecordDynamicIO>
{
	public override string DefaultName => "var";

	public DataRecordModuleDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	/// <summary>
	/// 添加数据项
	/// </summary>
	public override object AddIO(string name = null, string type = null, int index = -1)
	{
		if (DataSource == null)
		{
			return null;
		}
		if (ItemsA.Where((object x) => (x as DataRecordDynamicIO).CustomVisibleA).Count() >= MaxItemCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " :" + MaxItemCount, MessageButtons.Normal, MessageIcons.Warning);
			return null;
		}
		string typeName = ((type == null) ? DefaultTypeName : type);
		VmIO vmIO = null;
		vmIO = AddIoByType(typeName, name);
		vmIO.IsReturnRelateValue = false;
		DataRecordDynamicIO dataRecordDynamicIO = new DataRecordDynamicIO
		{
			IoDataSource = vmIO,
			CustomVisibleA = true
		};
		if (index > -1)
		{
			dataRecordDynamicIO.IndexA = index.ToString();
			dataRecordDynamicIO.SetParentNode(this);
			ItemsA.Insert(index - 1, dataRecordDynamicIO);
		}
		else
		{
			dataRecordDynamicIO.IndexA = (ItemsA.Where((object x) => (x as DataRecordDynamicIO).CustomVisibleA).Count() + 1).ToString();
			dataRecordDynamicIO.SetParentNode(this);
			ItemsA.Add(dataRecordDynamicIO);
		}
		UpdateCanDelete();
		dataRecordDynamicIO.SetParamUnit();
		return dataRecordDynamicIO;
	}
}
