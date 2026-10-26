using System;
using System.Collections.Generic;
using System.Linq;
// Original TypeDef token: 0x020000D6
// Ported from VMControls.WPF.dll SHA256 99FFA401C8784472913AD43813D68A8A5577890C00E149506E3E5664FA61C97D; ILSpy 11.1.0.9782. Algorithm statements retained; namespace/geometry adapter changed.
#nullable disable
using System;
using System.Collections.Generic;

namespace SunEyeVision.UI.Services.PathCalculators.VmRouting.Original;

internal class APoint : IComparable<float>
{
	public class FComparer : IComparer<APoint>
	{
		public int Compare(APoint x, APoint y)
		{
			if (x == null)
			{
				return -1;
			}
			if (y == null)
			{
				return 1;
			}
			if (x.F == y.F)
			{
				return 0;
			}
			return (x.F > y.F) ? 1 : (-1);
		}
	}

	public APoint Parent;

	public float H;

	public int X;

	public int Y;

	public float G;

	public float F;

	private bool isWall;

	public string TimeEnableFlag;

	public static string CurTimeEnableFlag { get; set; }

	public bool IsWall
	{
		get
		{
			CheckTime();
			return isWall;
		}
		set
		{
			CheckTime();
			isWall = value;
		}
	}

	private void CheckTime()
	{
		if (CurTimeEnableFlag != TimeEnableFlag)
		{
			Init();
			TimeEnableFlag = CurTimeEnableFlag;
		}
	}

	public APoint(int x, int y)
	{
		X = x;
		Y = y;
	}

	public void UpdateParent(APoint parent, float g)
	{
		Parent = parent;
		G = g;
		F = G + H;
	}

	public override string ToString()
	{
		return X + "," + Y + " " + F + " " + IsWall.ToString();
	}

	internal void Init()
	{
		isWall = false;
		Parent = null;
		H = 0f;
		G = 0f;
		F = 0f;
	}

	public int CompareTo(float other)
	{
		return F.CompareTo(other);
	}
}


