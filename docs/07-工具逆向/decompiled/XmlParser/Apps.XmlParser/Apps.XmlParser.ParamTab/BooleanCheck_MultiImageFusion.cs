using System;
using System.Collections.ObjectModel;
using System.Windows.Input;
using Apps.Interface;
using Apps.Localization;
using Mvvm.Commands;
using VM.Controls;

namespace Apps.XmlParser.ParamTab;

public class BooleanCheck_MultiImageFusion : Boolean
{
	private DelegateCommand _ValueChangedCommand = null;

	public new ICommand ValueChangedCommand
	{
		get
		{
			//IL_001b: Unknown result type (might be due to invalid IL or missing references)
			//IL_0025: Expected Obj, but got Unknown
			if (_ValueChangedCommand == null)
			{
				_ValueChangedCommand = new DelegateCommand((Action)ValueChangedCmdExecute);
			}
			return (ICommand)_ValueChangedCommand;
		}
	}

	protected override bool IsControlByParent()
	{
		if (parentNode is IContainerRun { isControlChildRun: not false })
		{
			return true;
		}
		return false;
	}

	private new void ValueChangedCmdExecute()
	{
		if (IsUnConnect() || !(OldValue != Value))
		{
			return;
		}
		ObservableCollection<object> enumEntrysA = (parentNode as UniformGridNoCheck).EnumEntrysA;
		int count = enumEntrysA.Count;
		int num = 0;
		foreach (BooleanCheck_MultiImageFusion item in enumEntrysA)
		{
			if (!item.CurValueA)
			{
				num++;
			}
		}
		if (num == count)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("Lang_MultiImageFusionOutputImageTypeError"));
			CurValueA = true;
		}
		else if (SetParamUnit() == 0)
		{
			OldValue = Value;
			if (!IsControlByParent())
			{
				PreExecuteRun();
			}
		}
	}
}
