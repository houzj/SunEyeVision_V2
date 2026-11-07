using System;
using System.Windows;
using System.Xml;
using Apps.Localization;
using Hik.Util;
using Hik.Xml;

namespace Apps.XmlParser.ParamTab;

public class ExternPanal : NameValueItem
{
	private object CurValue;

	public string ExternPanalFullName;

	public double PanalHeight = double.NaN;

	public object CurValueA
	{
		get
		{
			return CurValue;
		}
		set
		{
			CurValue = value;
			OnPropertyChanged("CurValueA");
		}
	}

	public double PanalHeightA
	{
		get
		{
			return PanalHeight;
		}
		set
		{
			PanalHeight = value;
			OnPropertyChanged("PanalHeightA");
		}
	}

	public override BaseNode Init(XmlNode node)
	{
		base.Init(node);
		if (!string.IsNullOrEmpty(ExternPanalFullName))
		{
			try
			{
				if (Application.Current != null)
				{
					Application.Current.Dispatcher.Invoke(() =>
					{
						CurValueA = ActivatorWrap.CreateInstance(ExternPanalFullName);
						if (CurValueA is IParentNode)
						{
							(CurValueA as IParentNode).SetParentNode(this);
						}
					});
				}
			}
			catch
			{
				throw new Exception(AppLocalizationService.GetLang(Name) + AppLocalizationService.GetLang("目标生成失败") + " !");
			}
		}
		return this;
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
