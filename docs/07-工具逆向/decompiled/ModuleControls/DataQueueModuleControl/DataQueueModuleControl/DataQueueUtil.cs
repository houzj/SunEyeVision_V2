using System;
using System.Text;
using Apps.XmlParser.ParamTab;
using Hik.Util;

namespace DataQueueModuleControl;

public class DataQueueUtil
{
	public const int MaxLen = 256;

	public static bool bNameChanged;

	public const int MaxImageCount = 4;

	public const int UnitDataSize = 512;

	public static int StringToByteCount(string input)
	{
		if (!string.IsNullOrEmpty(input))
		{
			return Encoding.UTF8.GetByteCount(input) + 1;
		}
		return 0;
	}

	public static byte[] GetBytes_UTF8(uint bytesLen, string strValue)
	{
		byte[] array = new byte[bytesLen];
		try
		{
			if (!string.IsNullOrEmpty(strValue))
			{
				byte[] bytes = Encoding.UTF8.GetBytes(strValue);
				Array.Copy(bytes, 0, array, 0, bytes.Length);
			}
		}
		catch
		{
		}
		return array;
	}

	public static DataQueueItem StructToItem(IMVS_DATAQUEUE value)
	{
		return new DataQueueItem
		{
			Name = StringUtil.UTF8GetString(value.chszQueueName),
			Value = value.chszValue,
			Type = value.chszDataType
		};
	}

	public static string[] GetData(IMVS_DATAQUEUE value)
	{
		string[] array = new string[value.nCount];
		byte[] array2 = new byte[512];
		for (int i = 0; i < value.nCount; i++)
		{
			Array.Copy(value.chszValue, i * 512, array2, 0, 512);
			array[i] = StringUtil.UTF8GetString(array2);
		}
		return array;
	}
}
