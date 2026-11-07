using Hik.Util;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class ExternPanalF : NameValueItem
{
	private object CurValue;

	public string ExternPanalFullName;

	public double PanalHeight = double.NaN;

	public override string DisplayType => "ExternPanal";

	public object CurValueA
	{
		get
		{
			if (CurValue == null)
			{
				CurValue = ActivatorWrap.CreateInstance(ExternPanalFullName);
				if (CurValue is IParentNode)
				{
					(CurValue as IParentNode).SetParentNode(this);
				}
			}
			return CurValue;
		}
	}

	public double PanalHeightA
	{
		get
		{
			if (PanalHeight == 0.0)
			{
				PanalHeight = double.NaN;
			}
			return PanalHeight;
		}
		set
		{
			if (PanalHeight != value)
			{
				PanalHeight = value;
				OnPropertyChanged("PanalHeightA");
			}
		}
	}

	public override void OnShownFinish()
	{
		base.OnShownFinish();
		CurValue = null;
	}

	public override int GetParamUnit()
	{
		return 0;
	}

	public override int SetParamUnit()
	{
		return 0;
	}
}
