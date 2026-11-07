using Apps.Interface;
using Apps.UIHelper.Extern;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ButtonSelecter_DisplayObject : ButtonSelecter, IDelete
{
	public override bool ReadOnlyA => true;

	public override string DisplayType => "ButtonSelecter_sub";

	public int ModuleId { get; set; } = -1;

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
				string idName = ((e.Param as Holder).HolderTarget as VmModule).GetIdName();
				CurValueA = idName;
			}
		}
		IsDropDownOpen = false;
	}

	public void Delete()
	{
		CurValueA = null;
	}
}
