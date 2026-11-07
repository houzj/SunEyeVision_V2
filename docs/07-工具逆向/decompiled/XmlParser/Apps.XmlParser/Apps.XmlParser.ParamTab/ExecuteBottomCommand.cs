using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ExecuteBottomCommand : BottomCommand
{
	public object Solution => VmSolution.Instance;

	public object Procedure
	{
		get
		{
			VmModule vmModule = (GetTopBaseNode() as AlgorithmTabRoot)?.Owner as VmModule;
			return (vmModule is VmProcedure) ? vmModule : vmModule?.Root;
		}
	}

	protected override bool IsBusy => (Procedure as IVMRun)?.IsRunning ?? false;
}
