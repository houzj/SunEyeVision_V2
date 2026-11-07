using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ModuleDecorate : CommonParamDecorate
{
	private IVmModule _ModuleSource;

	public override IVmModule ModuleSource
	{
		get
		{
			if (_ModuleSource == null)
			{
				_ModuleSource = (DecorateSource?.GetTopBaseNode() as AlgorithmTabRoot)?.Owner as IVmModule;
			}
			return _ModuleSource;
		}
	}
}
