using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls;
using SunEyeVision.Plugin.Wpf.Native.Numeric.VM.Controls.PropertyGrid.Editors;
#define TRACE
using System;
using System.Collections.Generic;
using System.Diagnostics;
using SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid.Definitions;

namespace SunEyeVision.Plugin.Wpf.Native.Display.VMControls.PropertyGrid;

public class PropertyDefinition : PropertyDefinitionBase
{
	private string _name;

	private bool? _isBrowsable = true;

	private bool? _isExpandable;

	private string _displayName;

	private string _description;

	private string _category;

	private int? _displayOrder;

	[Obsolete("Use 'TargetProperties' instead of 'Name'")]
	public string Name
	{
		get
		{
			return _name;
		}
		set
		{
			Trace.TraceWarning("{0}: 'Name' property is obsolete. Instead use 'TargetProperties'. (XAML example: <t:PropertyDefinition TargetProperties=\"FirstName,LastName\" />)", typeof(PropertyDefinition));
			_name = value;
		}
	}

	public string Category
	{
		get
		{
			return _category;
		}
		set
		{
			ThrowIfLocked(() => Category);
			_category = value;
		}
	}

	public string DisplayName
	{
		get
		{
			return _displayName;
		}
		set
		{
			ThrowIfLocked(() => DisplayName);
			_displayName = value;
		}
	}

	public string Description
	{
		get
		{
			return _description;
		}
		set
		{
			ThrowIfLocked(() => Description);
			_description = value;
		}
	}

	public int? DisplayOrder
	{
		get
		{
			return _displayOrder;
		}
		set
		{
			ThrowIfLocked(() => DisplayOrder);
			_displayOrder = value;
		}
	}

	public bool? IsBrowsable
	{
		get
		{
			return _isBrowsable;
		}
		set
		{
			ThrowIfLocked(() => IsBrowsable);
			_isBrowsable = value;
		}
	}

	public bool? IsExpandable
	{
		get
		{
			return _isExpandable;
		}
		set
		{
			ThrowIfLocked(() => IsExpandable);
			_isExpandable = value;
		}
	}

	internal override void Lock()
	{
		if (_name != null && TargetProperties != null && TargetProperties.Count > 0)
		{
			throw new InvalidOperationException($"{typeof(PropertyDefinition)}: When using 'TargetProperties' property, do not use 'Name' property.");
		}
		if (_name != null)
		{
			TargetProperties = new List<object> { _name };
		}
		base.Lock();
	}
}
