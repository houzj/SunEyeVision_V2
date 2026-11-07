using VMControls.WPF;

namespace Apps.XmlParser.ParamTab;

public class EnumerationLightForAcq : EnumerationLight
{
	public override string DisplayType
	{
		get
		{
			return "EnumerationLight";
		}
		set
		{
		}
	}

	public override int CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			if (CurValue != value)
			{
				CurValue = value;
				OnPropertyChanged("CurValueA");
			}
		}
	}

	public override bool OpenGlobalLight()
	{
		if (!base.OpenGlobalLight())
		{
			ToolControl.LightSet();
		}
		return true;
	}

	public override int SetParamUnit()
	{
		return 0;
	}

	public override int GetParamUnit()
	{
		return 0;
	}
}
