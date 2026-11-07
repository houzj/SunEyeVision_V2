using System.Collections;
using System.Collections.ObjectModel;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public interface IGlobalData : IParamList
{
	ObservableCollection<DynamicNameValueItemOfGroup> AllParamCollection { get; }

	ObservableCollection<GlobalParamConfigItem> GlobalParamItems { get; }

	IList EnumEntrys { get; }

	AlgorithmTabRoot SolutionTabRoot { get; }

	ObservableCollection<DynamicNameValueItemOfGroup> GetProcedureParams(VmProcedure procedure);

	void OnUpdateDecorateEvent(IDecorate decorate);
}
