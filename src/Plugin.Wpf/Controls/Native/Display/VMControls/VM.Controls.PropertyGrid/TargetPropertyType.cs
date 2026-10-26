using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
using System;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

public sealed class TargetPropertyType
{
	private Type _type;

	private bool _sealed;

	public Type Type
	{
		get
		{
			return _type;
		}
		set
		{
			if (_sealed)
			{
				throw new InvalidOperationException($"{typeof(TargetPropertyType)}.Type property cannot be modified once the instance is used");
			}
			_type = value;
		}
	}

	internal void Seal()
	{
		if (_type == null)
		{
			throw new InvalidOperationException($"{typeof(TargetPropertyType)}.Type property must be initialized");
		}
		_sealed = true;
	}
}
