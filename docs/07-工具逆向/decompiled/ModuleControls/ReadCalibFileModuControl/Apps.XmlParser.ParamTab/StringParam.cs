using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class StringParam : IOutput
{
	public string NameA { get; set; }

	public string ValueA { get; set; }

	public string DisplayType => "StaticParam";

	public void AddOutput(IVmModule vmModule)
	{
		vmModule.Outputs.Add(new DynamicVmIO
		{
			TypeName = "string",
			Name = NameA,
			Root = (vmModule as VmModule),
			Parent = vmModule
		});
	}

	public override string ToString()
	{
		return $"{NameA}:{ValueA}";
	}
}
