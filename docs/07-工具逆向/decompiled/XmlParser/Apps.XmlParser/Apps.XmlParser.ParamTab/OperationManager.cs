using System;
using System.Reflection;
using Hik.Util;

namespace Apps.XmlParser.ParamTab;

public class OperationManager
{
	public static void CreatInstanceBySetter(Setter setter)
	{
		if (!string.IsNullOrEmpty(setter.OperationName))
		{
			object obj = null;
			string text = MethodBase.GetCurrentMethod().DeclaringType.Namespace + "." + setter.OperationName;
			if (ActivatorWrap.TypeTable.Contains(text))
			{
				obj = ActivatorWrap.CreateInstance(text, new object[1] { setter.TargetName });
				setter.Operation = obj as IOperation;
				setter.Operation.Executor = setter;
			}
			else if (!ActivatorWrap.IgnoreUnknown)
			{
				throw new Exception(text + "is unKnown");
			}
		}
	}
}
