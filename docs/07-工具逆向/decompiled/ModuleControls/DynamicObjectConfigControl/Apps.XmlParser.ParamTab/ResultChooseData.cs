using System.Collections;
using System.Collections.Generic;
using System.Linq;
using VM.Core;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ResultChooseData
{
	private string[] allType = new string[4] { "int", "float", "string", "byte" };

	private IList _EnumEntrysA;

	public IVmModule ModuleSource { get; set; }

	public IList EnumEntrysA
	{
		get
		{
			if (_EnumEntrysA == null)
			{
				List<Holder> list = new List<Holder>();
				if (ModuleSource is IVmSolution vmSolution)
				{
					IEnumerable<IVmModule> enumerable = from p in vmSolution.Children.OfType<IVmModule>()
						where p is IVmProcedure
						select p;
					foreach (IVmModule item in enumerable)
					{
						(item as VmModule).AddOutputVmIOTreeNode(list, allType);
					}
				}
				else
				{
					IVmModule moduleSource = ModuleSource;
					if (moduleSource != null)
					{
						Holder outputVmIOTreeNode = (moduleSource as VmModule).GetOutputVmIOTreeNode(allType);
						if (outputVmIOTreeNode != null)
						{
							list = outputVmIOTreeNode.EnumEntrysA.OfType<Holder>().ToList();
						}
					}
				}
				_EnumEntrysA = list;
			}
			return _EnumEntrysA;
		}
	}
}
