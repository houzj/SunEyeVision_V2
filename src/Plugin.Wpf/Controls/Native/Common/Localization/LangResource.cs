using System;
using System.Windows;

namespace SunEyeVision.Plugin.Wpf.Native.Common.Localization;

public class LangResource : DynamicResourceExtension
{
	public LangResource()
	{
	}

	public LangResource(object resourceKey)
		: base(resourceKey)
	{
	}

	public override object ProvideValue(IServiceProvider serviceProvider)
	{
		return AppLocalizationService.GetLang(ResourceKey as string);
	}
}
