using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class FloatParam : IOutput
{
	public string NameA { get; set; }

	public float ValueA { get; set; }

	public string DisplayType => "StaticParam";

	public void AddOutput(IVmModule vmModule)
	{
		vmModule.Outputs.Add(new DynamicVmIO
		{
			TypeName = "float",
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
