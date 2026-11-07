using System;

namespace HikPcUI.Controls;

[Flags]
public enum ArrowEnds
{
	None = 0,
	Start = 1,
	End = 2,
	Both = Start | End
}
