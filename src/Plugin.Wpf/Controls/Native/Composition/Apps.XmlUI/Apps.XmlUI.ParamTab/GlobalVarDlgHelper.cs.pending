namespace SunEyeVision.Plugin.Wpf.Native.Composition.Apps.XmlUI.ParamTab;

public class GlobalVarDlgHelper
{
	public object ShowGlobalVarSelectDlg(bool isAllowRepeatedSelection, object moduleSet, string[] valueTypes = null)
	{
		GlobalVariableSelectDlg globalVariableSelectDlg = new GlobalVariableSelectDlg(isAllowRepeatedSelection);
		globalVariableSelectDlg.Type = valueTypes;
		globalVariableSelectDlg.DecorateSource = moduleSet;
		globalVariableSelectDlg.ShowDialog();
		if (globalVariableSelectDlg.IsConfirm)
		{
			return globalVariableSelectDlg.SelectedGlobalVar;
		}
		return null;
	}

	public object ShowVarSelectDlg(bool isAllowRepeatedSelection, object moduleSet, string[] valueTypes = null)
	{
		GlobalVariableSelectDlg globalVariableSelectDlg = new GlobalVariableSelectDlg(isAllowRepeatedSelection);
		globalVariableSelectDlg.Type = valueTypes;
		globalVariableSelectDlg.DecorateSource = moduleSet;
		globalVariableSelectDlg.ShowDialog();
		if (globalVariableSelectDlg.IsConfirm)
		{
			return globalVariableSelectDlg.SelectedVarItem;
		}
		return null;
	}
}
