using System;
using System.Collections.Generic;
using Hik.Xml;

namespace IMVSColorRecognitionModuControl;

public class CompressMaskData : BaseNode
{
	public byte[] compressMask;

	protected byte[] CompressToNormalMask(byte[] compressMask, int FlagNum)
	{
		int[] array = new int[compressMask.Length / 4];
		Buffer.BlockCopy(compressMask, 0, array, 0, compressMask.Length);
		List<int[]> list = new List<int[]>();
		int num = 0;
		for (int i = 0; i < array.Length; i++)
		{
			num += array[i];
		}
		num /= FlagNum;
		byte[] array2 = new byte[num];
		bool flag = false;
		int num2 = 0;
		byte b = 1;
		for (int j = 0; j < array.Length; j++)
		{
			if (flag)
			{
				for (int k = 0; k < array[j]; k++)
				{
					array2[k + num2] = b;
				}
			}
			if (num2 + array[j] == num)
			{
				flag = false;
				num2 = 0;
				b++;
			}
			else
			{
				num2 += array[j];
				flag = !flag;
			}
		}
		return array2;
	}

	protected byte[] NormalToCompressMask(byte[] otherMask, int FlagNum)
	{
		List<int> list = new List<int>();
		for (int i = 1; i <= FlagNum; i++)
		{
			bool flag = false;
			int num = 0;
			for (int j = 0; j < otherMask.Length; j++)
			{
				int num2 = ((otherMask[j] > 10) ? (otherMask[j] - 10) : otherMask[j]);
				if (num2 == i != flag)
				{
					flag = !flag;
					list.Add(num);
					num = 0;
				}
				num++;
			}
			list.Add(num);
		}
		int[] array = list.ToArray();
		byte[] array2 = new byte[array.Length * 4];
		Buffer.BlockCopy(array, 0, array2, 0, array2.Length);
		return array2;
	}
}
