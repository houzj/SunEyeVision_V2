using System;

namespace VM.Controls;

[Flags]
public enum AllowedSpecialValues
{
	None = 0,
	NaN = 1,
	PositiveInfinity = 2,
	NegativeInfinity = 4,
	AnyInfinity = PositiveInfinity | NegativeInfinity,
	Any = AnyInfinity | NaN
}
