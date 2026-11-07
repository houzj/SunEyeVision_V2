using Apps.Interface;
using Apps.UIHelper.Extern;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ButtonSelecter_Edit_DisplayObject : ButtonSelecter, IDelete
{
	protected override void ClickCommandExecute(ObjectEventArgs e)
	{
		if (e.Param == null)
		{
			return;
		}
		if (e.Param is BaseParamNode)
		{
			string curValueA = (e.Param as BaseParamNode).GetPropertyValueByName("DisplayFullNameA").ToString();
			CurValueA = curValueA;
		}
		else if (e.Param is Holder && (e.Param as Holder).HolderTarget != null)
		{
			if ((e.Param as Holder).HolderTarget.ToString() == "null")
			{
				CurValueA = null;
			}
			else if ((e.Param as Holder).HolderTarget is VmModule)
			{
				string fullName = ((e.Param as Holder).HolderTarget as VmModule).FullName;
				CurValueA = fullName;
			}
		}
		IsDropDownOpen = false;
	}

	public void Delete()
	{
		CurValueA = null;
	}
}
