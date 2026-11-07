using System;

namespace Apps.XmlParser.ParamTab;

public class OpenFewShotFileDialog : OpenFileDialog
{
	public event EventHandler BeforeClickCommandCmdExecuted;

	protected override void ClickCommandCmdExecute()
	{
		try
		{
			BeforeClickCommandCmdExecuted?.Invoke(this, null);
			base.ClickCommandCmdExecute();
		}
		catch (Exception)
		{
		}
	}

	protected override void SetCurValue(string value)
	{
		CurValueA = value;
	}
}
