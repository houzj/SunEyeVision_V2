using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apps.Log;
using DynamicObjectConfigControl;
using GraphicsSetModuleControl;
using VM.Core;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class GroupDisplayParamForGraphicsSet : GroupDisplayParam
{
	public static List<ObjectTypeEnum> AllowType = new List<ObjectTypeEnum>
	{
		ObjectTypeEnum.point,
		ObjectTypeEnum.line,
		ObjectTypeEnum.rect,
		ObjectTypeEnum.annularsector,
		ObjectTypeEnum.text,
		ObjectTypeEnum.ellipse
	};

	public override IList DisplayTypes
	{
		get
		{
			if (base._DisplayTypes == null)
			{
				IEnumerable<Tuple<string, object>> source = AllowType.Select((ObjectTypeEnum p) => new Tuple<string, object>(GroupDisplayParam.GetDefaultObjectName(p), p));
				base._DisplayTypes = source.ToList();
			}
			return base._DisplayTypes;
		}
	}

	public GroupDisplayParamForGraphicsSet()
	{
		((GroupDisplayParam)this).SelectedType = AllowType.First();
	}

	protected override DisplayObjectDecorate CreatNewDecorate(DynamicObject newObject)
	{
		DisplayObjectForGraphicsSetDecorate displayObjectForGraphicsSetDecorate = new DisplayObjectForGraphicsSetDecorate();
		((DisplayObjectDecorate)displayObjectForGraphicsSetDecorate).DecorateSource = newObject;
		((DisplayObjectDecorate)displayObjectForGraphicsSetDecorate).Owner = (GroupDisplayParam)(object)this;
		return (DisplayObjectDecorate)(object)displayObjectForGraphicsSetDecorate;
	}

	protected override void AddExcutedAfter(DisplayObjectDecorate displayItem)
	{
		if (!(displayItem is DisplayObjectForGraphicsSetDecorate displayObjectForGraphicsSetDecorate))
		{
			return;
		}
		((IVmModule)((GroupDisplayParam)this).Owner)?.RebuildLeafIODictionary();
		if (((DisplayObjectDecorate)displayObjectForGraphicsSetDecorate).DecorateSource.okcolorA != "#66ff00")
		{
			((DisplayObjectDecorate)displayObjectForGraphicsSetDecorate).DecorateSource.okcolorA = "#66ff00";
		}
		IoCombinationRoot instance = IoCombinationRoot.GetInstance();
		string type = GetCombinationType(((DisplayObjectDecorate)displayObjectForGraphicsSetDecorate).DecorateSource.TypeA) ?? throw new Exception("类型异常");
		Combination combination = instance.Items.OfType<Combination>().FirstOrDefault((Combination p) => p.StyleA == type) ?? throw new Exception("类型异常");
		Combination combination2 = combination.Clone<Combination>();
		ChangeParamName(combination2, ((DisplayObjectDecorate)displayObjectForGraphicsSetDecorate).NoSignName);
		if (type == "TEXT")
		{
			foreach (object item in combination2.FiltersA)
			{
				((GroupDisplayParam)this).Owner.AddIO(item, ((GroupDisplayParam)this).Owner, ((GroupDisplayParam)this).Owner.Outputs);
			}
		}
		else
		{
			((GroupDisplayParam)this).Owner.AddIO(combination2, ((GroupDisplayParam)this).Owner, ((GroupDisplayParam)this).Owner.Outputs);
		}
		string ioName = "Status_" + ((DisplayObjectDecorate)displayObjectForGraphicsSetDecorate).NoSignName;
		DynamicVmIO dynamicVmIO = ((GroupDisplayParam)this).Owner.AddIOByNameType("Output", ioName, "int");
		displayObjectForGraphicsSetDecorate.ChangeMapping();
		SetIOInfoToModule();
	}

	internal void SetIOInfoToModule()
	{
		try
		{
			List<string> list = new List<string>();
			if (((GroupDisplayParam)this).DisplayItemsA == null)
			{
				LogHelper.objLog.Error("DisplayItemsA==null");
				return;
			}
			foreach (DisplayObjectForGraphicsSetDecorate item2 in ((GroupDisplayParam)this).DisplayItemsA)
			{
				if (item2 == null)
				{
					continue;
				}
				if (item2.OutputIO.Count() > 1)
				{
					IEnumerable<VmIO> source = from p in item2.GetAllIO()
						where p.IsLeafNode
						select p;
					IEnumerable<string> values = source.Select((VmIO p) => p.Name + "/" + p.TypeName);
					string arg = string.Join(",", values);
					string text = item2.StatusIO.Name + "/" + item2.StatusIO.TypeName;
					string text2 = $"TEXT,{arg}";
					if (!text2.Contains(text))
					{
						text2 += $",{text}";
					}
					list.Add(text2);
				}
				else
				{
					VmIO vmIO = item2.OutputIO.First();
					IEnumerable<string> values2 = from p in vmIO.GetAllLeafIO()
						select p.Name + "/" + p.TypeName;
					string item = string.Format("{0},{1},{2}", vmIO.TypeName, string.Join(",", values2), item2.StatusIO.Name + "/" + item2.StatusIO.TypeName);
					list.Add(item);
				}
			}
			string text3 = string.Join(";", list);
			if (string.IsNullOrEmpty(text3))
			{
				text3 = "Clear";
			}
			LogHelper.objLog.Info(text3);
			((IUserBytesData)((GroupDisplayParam)this).Owner.Params)["UpdateIOData"] = Encoding.UTF8.GetBytes(text3);
		}
		catch (Exception ex)
		{
			LogHelper.objLog.Error(ex.Message);
		}
	}

	private string GetCombinationType(string valueType)
	{
		return valueType switch
		{
			"rect" => "ROIBOX", 
			"line" => "LINE", 
			"text" => "TEXT", 
			"LineAngle" => "", 
			"coordinate" => "", 
			"point" => "POINT", 
			"circle" => "CIRCLE", 
			"annularsector" => "ROIANNULUS", 
			"ellipse" => "ELLIPSE", 
			_ => null, 
		};
	}

	private void ChangeParamName(object obj, string hashCode)
	{
		if (obj is Combination combination)
		{
			combination.Name = combination.Name + "_" + hashCode;
			{
				foreach (object filter2 in combination.Filters)
				{
					ChangeParamName(filter2, hashCode);
				}
				return;
			}
		}
		if (obj is Filter filter)
		{
			filter.Name = filter.Name + "_" + hashCode;
		}
	}
}
