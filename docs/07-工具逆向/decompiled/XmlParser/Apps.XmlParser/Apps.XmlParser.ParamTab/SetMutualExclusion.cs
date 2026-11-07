using Apps.Interface;
using Hik.Xml;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class SetMutualExclusion : IOperation
{
	public string TargetName = "";

	public Setter Executor { get; set; }

	public bool Working => false;

	public SetMutualExclusion(string TName)
	{
		TargetName = TName;
	}

	public void GetOriginalValue()
	{
	}

	public void Operate(object[] Params, string[] ExtraParams)
	{
		Integer parentNode = Executor.GetParentNode<Integer>();
		if (parentNode != null && parentNode.GetTopBaseNode() is BaseNode baseNode && (baseNode as IOwner).Owner != null && (baseNode as IOwner).Owner is VmModule && (Executor.GetTopBaseNode() as AlgorithmTabRoot).FindBaseNodeByName(TargetName) is Integer integer)
		{
			integer.MinValueA = ((parentNode.CurValue == 0) ? 1 : 0);
		}
	}

	public void ReOperate()
	{
	}
}
