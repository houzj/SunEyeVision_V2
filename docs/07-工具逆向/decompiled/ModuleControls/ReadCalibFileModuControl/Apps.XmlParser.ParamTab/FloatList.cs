using System.Collections.ObjectModel;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class FloatList : IOutput
{
	public string DisplayType => "FloatList";

	public string NameA { get; set; }

	public ObservableCollection<FloatParam> EnumEntrysA { get; set; } = new ObservableCollection<FloatParam>();

	public void AddOutput(IVmModule vmModule)
	{
		DynamicVmIO dynamicVmIO = new DynamicVmIO();
		dynamicVmIO.TypeName = "float";
		dynamicVmIO.Name = NameA;
		dynamicVmIO.Root = vmModule as VmModule;
		dynamicVmIO.Parent = vmModule;
		VmIO value = dynamicVmIO;
		vmModule.Outputs.Add(value);
	}
}
