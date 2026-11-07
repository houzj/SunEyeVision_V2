using Apps.UIHelper.Extern;
using Apps.XmlParser.ParamTab;
using VM.Core;

namespace FormatModuleControl;

public class ButtonSelecter_FormatIndex : ButtonSelecter
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
			string modulePathName = vmIO.ModulePathName;
			selectedFilterType = vmIO.TypeName;
			CurValueA = "<" + modulePathName + ">";
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
					string modulePathName2 = subFilteFromGlobalVariable.ModulePathName;
					if (subFilteFromGlobalVariable != null)
					{
						selectedFilterType = subFilteFromGlobalVariable.TypeName;
					}
					CurValueA = "<" + modulePathName2 + ">";
				}
			}
		}
		IsDropDownOpen = false;
	}
}
