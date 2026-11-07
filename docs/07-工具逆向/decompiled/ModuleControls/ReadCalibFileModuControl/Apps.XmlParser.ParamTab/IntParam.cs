using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class IntParam : IOutput
{
	public string DisplayType => "StaticParam";

	public string NameA { get; set; }

	public int ValueA { get; set; } = -999;

	public void AddOutput(IVmModule vmModule)
	{
		vmModule.Outputs.Add(new DynamicVmIO
		{
			TypeName = "int",
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
