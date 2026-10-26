using System;
using System.Collections.Generic;

namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.Base.Astar;

public class AStar
{
	/// <summary>
	/// 查找最优路径
	/// </summary>
	/// <param name="start"></param>
	/// <param name="end"></param>
	public static List<APoint> FindPath(APoint start, APoint end, APoint[,] map)
	{
		if (start.X == end.X && start.Y == end.Y)
		{
			return new List<APoint> { start, end };
		}
		APoint aPoint = map[start.X, start.Y];
		APoint aPoint2 = map[end.X, end.Y];
		if (aPoint.IsWall || aPoint2.IsWall)
		{
			return new List<APoint> { start, end };
		}
		Dictionary<POINT, APoint> dictionary = new Dictionary<POINT, APoint>();
		Dictionary<POINT, APoint> dictionary2 = new Dictionary<POINT, APoint>();
		dictionary.Add(new POINT
		{
			X = start.X,
			Y = start.Y
		}, start);
		CalcF(start, start, end);
		APoint aPoint3 = start;
		bool flag = true;
		POINT key = new POINT
		{
			X = end.X,
			Y = end.Y
		};
		int num = Math.Abs(end.X - start.X) + Math.Abs(end.Y - start.Y);
		while (dictionary.Count > 0)
		{
			APoint aPoint4 = (flag ? aPoint3 : (aPoint3 = FindMinFOfPoint(dictionary, dictionary2)));
			POINT key2 = new POINT
			{
				X = aPoint4.X,
				Y = aPoint4.Y
			};
			dictionary.Remove(key2);
			flag = false;
			dictionary2.Add(key2, aPoint4);
			List<APoint> surroundPoints = GetSurroundPoints(aPoint4, map, start, end);
			PointsFilter(surroundPoints, dictionary2);
			foreach (APoint item in surroundPoints)
			{
				POINT key3 = new POINT
				{
					X = item.X,
					Y = item.Y
				};
				if (dictionary.ContainsKey(key3))
				{
					float num2 = CalcG(item, item.Parent);
					if (num2 < item.G)
					{
						item.UpdateParent(aPoint4, num2);
					}
				}
				else
				{
					item.Parent = aPoint4;
					CalcF(item, start, end);
					dictionary.Add(key3, item);
				}
				if (dictionary[key3].F <= aPoint3.F)
				{
					flag = true;
					aPoint3 = dictionary[key3];
				}
			}
			if (dictionary.ContainsKey(key))
			{
				end.Parent = aPoint4;
				break;
			}
			int num3 = Math.Abs(aPoint4.X - start.X) + Math.Abs(aPoint4.Y - start.Y);
			if ((num3 > 100 && num3 > (Math.Abs(end.X - start.X) + Math.Abs(end.Y - start.Y)) * 3 / 2) || (num > 0 && dictionary2.Count / num > 30))
			{
				return new List<APoint> { start, end };
			}
		}
		List<APoint> list = new List<APoint>();
		APoint aPoint5 = end;
		list.Add(aPoint5);
		while (aPoint5.Parent != null)
		{
			aPoint5 = aPoint5.Parent;
			list.Add(aPoint5);
		}
		list.Reverse();
		return list;
	}

	private static void PointsFilter(List<APoint> src, Dictionary<POINT, APoint> closeList)
	{
		src.RemoveAll((APoint p) => closeList.ContainsKey(new POINT
		{
			X = p.X,
			Y = p.Y
		}));
	}

	private static List<APoint> GetSurroundPoints(APoint point, APoint[,] map, APoint start, APoint end)
	{
		APoint aPoint = null;
		APoint aPoint2 = null;
		APoint aPoint3 = null;
		APoint aPoint4 = null;
		if (point.Y < map.GetLength(1) - 1)
		{
			aPoint = map[point.X, point.Y + 1];
		}
		if (point.Y > 0)
		{
			aPoint2 = map[point.X, point.Y - 1];
		}
		if (point.X > 0)
		{
			aPoint3 = map[point.X - 1, point.Y];
		}
		if (point.X < map.GetLength(0) - 1)
		{
			aPoint4 = map[point.X + 1, point.Y];
		}
		List<APoint> list = new List<APoint>();
		if (aPoint2 != null && !aPoint2.IsWall)
		{
			list.Add(aPoint2);
		}
		if (aPoint != null && !aPoint.IsWall)
		{
			list.Add(aPoint);
		}
		if (aPoint3 != null && !aPoint3.IsWall)
		{
			list.Add(aPoint3);
		}
		if (aPoint4 != null && !aPoint4.IsWall)
		{
			list.Add(aPoint4);
		}
		return list;
	}

	private static APoint FindMinFOfPoint(Dictionary<POINT, APoint> openList, Dictionary<POINT, APoint> closeList)
	{
		float num = float.MaxValue;
		APoint result = null;
		foreach (APoint value in openList.Values)
		{
			if (value.F < num)
			{
				result = value;
				num = value.F;
			}
		}
		return result;
	}

	private static float CalcG(APoint now, APoint parent)
	{
		return (float)(Math.Abs(now.X - parent.X) + Math.Abs(now.Y - parent.Y)) + parent.G;
	}

	private static void CalcF(APoint now, APoint start, APoint end)
	{
		float num = Math.Abs(end.X - now.X) + Math.Abs(end.Y - now.Y);
		float num2 = 0f;
		num2 = ((now.Parent != null) ? CalcG(now, now.Parent) : 0f);
		float num3 = 0f;
		APoint aPoint = now.Parent?.Parent;
		if (aPoint != null && now.X != aPoint.X && now.Y != aPoint.Y)
		{
			num3 = 0.5f;
		}
		float f = num2 + num + num3;
		now.F = f;
		now.G = num2;
		now.H = num;
	}
}
