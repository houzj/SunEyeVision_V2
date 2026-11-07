using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;
using VM.Core;

namespace FormatModuleControl;

public class ButtonSelecter_Format : ButtonSelecter
{
	protected override void ClickCommandExecute(ObjectEventArgs e)
	{
		if (e.Param == null)
		{
			return;
		}
		if (e.Param is VmIO)
		{
			VmIO vmIO = e.Param as VmIO;
			string text = vmIO.ModulePathName;
			selectedFilterType = vmIO.TypeName;
			switch (selectedFilterType)
			{
			case "string":
				text += "(%s)";
				break;
			case "float":
				text += "(%1.3f)";
				break;
			case "int":
				text += "(%0d)";
				break;
			}
			CurValueA = text;
		}
		else if (e.Param is Holder && (e.Param as Holder).HolderTarget != null && (e.Param as Holder).HolderTarget.ToString() == "null")
		{
			CurValueA = null;
		}
		else if (e.Param is Holder && (e.Param as Holder).HolderTarget != null && (e.Param as Holder).HolderTarget.ToString() != "null")
		{
			string displayNameA = (e.Param as Holder).DisplayNameA;
			if (displayNameA.Equals("Lang_VariableManager"))
			{
				VmIO subFilteFromGlobalVariable = GetSubFilteFromGlobalVariable(e.Param);
				if (subFilteFromGlobalVariable != null)
				{
					string text2 = subFilteFromGlobalVariable.ModulePathName;
					if (subFilteFromGlobalVariable != null)
					{
						selectedFilterType = subFilteFromGlobalVariable.TypeName;
					}
					switch (selectedFilterType)
					{
					case "string":
						text2 += "(%s)";
						break;
					case "float":
						text2 += "(%1.3f)";
						break;
					case "int":
						text2 += "(%0d)";
						break;
					}
					CurValueA = text2;
				}
			}
		}
		IsDropDownOpen = false;
	}
}
