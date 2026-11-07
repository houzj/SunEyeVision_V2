using Apps.Interface;

namespace Apps.XmlParser.ParamTab;

public class BooleanCheck : Boolean
{
	protected override bool IsControlByParent()
	{
		if (parentNode is IContainerRun { isControlChildRun: not false })
		{
			return true;
		}
		return false;
	}
}
