using Apps.Interface;

namespace Apps.XmlParser.ParamTab;

public class DataAssembleModuleDynamicIO : DynamicIOContainer<DataAssembleDynamicIO>
{
	public override string DefaultTypeName => "string";

	public DataAssembleModuleDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}
}
