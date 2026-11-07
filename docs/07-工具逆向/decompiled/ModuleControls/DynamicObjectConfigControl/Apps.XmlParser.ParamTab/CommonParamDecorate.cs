using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class CommonParamDecorate : IDecorate
{
	private NameValueItem _DecorateSource;

	private IVmModule _ModuleSource;

	object IDecorate.DecorateSource
	{
		get
		{
			return DecorateSource;
		}
		set
		{
			DecorateSource = value as NameValueItem;
		}
	}

	public NameValueItem DecorateSource
	{
		get
		{
			return _DecorateSource;
		}
		set
		{
			if (value != _DecorateSource)
			{
				_DecorateSource = value;
			}
		}
	}

	public virtual IVmModule ModuleSource
	{
		get
		{
			if (_ModuleSource == null)
			{
				VmModuleSet obj = (DecorateSource.GetTopBaseNode() as AlgorithmTabRoot)?.Owner as VmModuleSet;
				_ModuleSource = ((obj != null) ? obj.GetFirstParamRelationModule(DecorateSource.Name) : null);
			}
			return _ModuleSource;
		}
	}
}
