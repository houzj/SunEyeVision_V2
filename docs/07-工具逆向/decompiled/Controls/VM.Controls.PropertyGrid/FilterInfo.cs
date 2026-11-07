using System;

namespace VM.Controls.PropertyGrid;

internal struct FilterInfo
{
	public string InputString;

	public Predicate<object> Predicate;
}
