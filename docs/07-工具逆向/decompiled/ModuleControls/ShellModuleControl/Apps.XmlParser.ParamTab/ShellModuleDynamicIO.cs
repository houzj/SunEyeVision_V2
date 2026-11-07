using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Apps.Interface;
using Apps.Localization;
using Apps.Log;
using DynamicObjectConfigControl.Common;
using ShellCommonControl;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public class ShellModuleDynamicIO : DynamicIOContainer<ShellDynamicIO>
{
	private IList<object> targetObjects;

	public int HiddenCount => ItemsA.Where((object x) => !(x as ShellDynamicIO).CustomVisibleA).Count();

	public IList<object> TargetObjects
	{
		get
		{
			if (targetObjects == null)
			{
				targetObjects = ((Owner as VmModule)["[Display]"] as ParamRoot).GetDisplayList();
			}
			return targetObjects;
		}
	}

	public override bool IsInitAdd => true;

	public override int MaxItemCount => 65535;

	public ShellModuleDynamicIO(string name, IOwner dataSorce)
		: base(name, dataSorce)
	{
	}

	public ShellModuleDynamicIO()
	{
	}

	private object BaseAddIO(string name = null, string type = null, int index = -1)
	{
		if (DataSource == null)
		{
			return null;
		}
		if (ItemsA.Where((object x) => (x as ShellDynamicIO).CustomVisibleA).Count() >= MaxItemCount)
		{
			HikMessageBox.Show(AppLocalizationService.GetLang("最多添加") + " :" + MaxItemCount, MessageButtons.Normal, MessageIcons.Warning);
			return null;
		}
		string typeName = ((type == null) ? DefaultTypeName : type);
		VmIO vmIO = null;
		vmIO = AddIoByType(typeName, name, (index > -1) ? (ItemsA.Where((object x) => !(x as ShellDynamicIO).CustomVisibleA).Count() + index - 1) : (-1));
		ShellDynamicIO shellDynamicIO = new ShellDynamicIO
		{
			IoDataSource = vmIO,
			CustomVisibleA = true
		};
		if (index > -1)
		{
			shellDynamicIO.IndexA = index.ToString();
			shellDynamicIO.SetParentNode(this);
			int num = ItemsA.Where((object x) => !(x as ShellDynamicIO).CustomVisibleA).Count();
			ItemsA.Insert(index - 1 + num, shellDynamicIO);
		}
		else
		{
			shellDynamicIO.IndexA = (ItemsA.Where((object x) => (x as ShellDynamicIO).CustomVisibleA).Count() + 1).ToString();
			shellDynamicIO.SetParentNode(this);
			ItemsA.Add(shellDynamicIO);
		}
		UpdateCanDelete();
		shellDynamicIO.SetParamUnit();
		return shellDynamicIO;
	}

	public void AddIO(string name, string type, int index, string dispalyValue)
	{
		object obj = AddIO(name, type, index);
		if (obj is ShellDynamicIO shellDynamicIO && dispalyValue.Contains("[]"))
		{
			shellDynamicIO.DisplayValueTypeA = dispalyValue;
		}
	}

	public override object AddIO(string name = null, string type = null, int index = -1)
	{
		object obj = BaseAddIO(name, type, index);
		ShellDynamicIO shellDynamicIO = obj as ShellDynamicIO;
		if (shellDynamicIO != null && shellDynamicIO.IoDataSource.TypeName == "IMAGE" && IsOutput)
		{
			shellDynamicIO.IsDisplayForImage = true;
		}
		if (IsOutput)
		{
			ShellHelper.ChangeRecordObject(Owner as VmModule, (shellDynamicIO?.GetParentNode() as ShellModuleDynamicIO)?.ItemsA.ToList());
		}
		return obj;
	}

	public override void RemoveIO(object dymaicIO)
	{
		base.RemoveIO(dymaicIO);
		ShellDynamicIO shellDynamicIO = dymaicIO as ShellDynamicIO;
		if (shellDynamicIO != null && shellDynamicIO.IsDisplayForImage)
		{
			DynamicObjectManager.RemoveDisplayObject(shellDynamicIO.IoDataSource);
			ButtonSelecter_Shell buttonSelecter_Shell = GetParentNode<ButtonSelecter_Shell>();
			if (buttonSelecter_Shell != null)
			{
				buttonSelecter_Shell.ShellDisplayContainerA.UpdateDisplayItems();
			}
		}
		if (IsOutput)
		{
			ShellHelper.ChangeRecordObject(Owner as VmModule, (shellDynamicIO?.GetParentNode() as ShellModuleDynamicIO)?.ItemsA.ToList());
		}
	}

	public void ChangeRemoveIO(object dymaicIO)
	{
		ShellDynamicIO shellDynamicIO = dymaicIO as ShellDynamicIO;
		shellDynamicIO.IoDataSource.RemoveSelf();
		ItemsA.Remove(dymaicIO);
		UpdateCanDelete();
		try
		{
			((IVmIO)shellDynamicIO.IoDataSource).UnBindingGlobalVariable();
		}
		catch (Exception ex)
		{
			LogHelper.Error("解除全局变量绑定关系失败：" + ex.Message);
		}
		shellDynamicIO.SetParamUnit();
		if (shellDynamicIO.IsDisplayForImage)
		{
			DynamicObjectManager.RemoveDisplayObject(shellDynamicIO.IoDataSource);
			ButtonSelecter_Shell buttonSelecter_Shell = GetParentNode<ButtonSelecter_Shell>();
			if (buttonSelecter_Shell != null)
			{
				buttonSelecter_Shell.ShellDisplayContainerA.UpdateDisplayItems();
			}
		}
	}

	public void UpdateDisplayObjectName(VmIO iO, string oldName, string NewName)
	{
		if (IsOutput)
		{
			IList<object> list = TargetObjects;
			DynamicObject dynamicObject = ((list != null) ? list.OfType<DynamicObject>().FirstOrDefault((DynamicObject x) => x.NameA == oldName) : null);
			if (dynamicObject != null)
			{
				dynamicObject.NameA = ChangeName(NewName);
			}
		}
	}

	private string ChangeName(string tmpName)
	{
		if (string.IsNullOrEmpty(tmpName))
		{
			return tmpName;
		}
		if (tmpName.First() == '%' && tmpName.Last() == '%' && tmpName.Length > 1)
		{
			return tmpName.Substring(1, tmpName.Length - 2);
		}
		return tmpName;
	}

	public void ChangeItemsAOrder(string type, int index, string orderType)
	{
		int index2 = -1;
		if (!(orderType == "Up"))
		{
			if (orderType == "Down")
			{
				index2 = index + 1;
			}
		}
		else
		{
			index2 = index - 1;
		}
		if (ItemsA[index2] is ShellDynamicIO shellDynamicIO && ItemsA[index] is ShellDynamicIO shellDynamicIO2)
		{
			string indexA = shellDynamicIO.IndexA;
			shellDynamicIO.IndexA = shellDynamicIO2.IndexA;
			shellDynamicIO2.IndexA = indexA;
		}
		object value = ItemsA[index];
		ItemsA[index] = ItemsA[index2];
		ItemsA[index2] = value;
		StringBuilder stringBuilder = new StringBuilder();
		foreach (object item in ItemsA)
		{
			if (item is ShellDynamicIO shellDynamicIO3)
			{
				stringBuilder.Append($"{shellDynamicIO3.NameA}:{shellDynamicIO3.IndexA},");
			}
		}
		string text = stringBuilder.ToString();
		text = text.Substring(0, text.LastIndexOf(","));
		DataSource.UserData[type] = StringUtil.UTF8GetBytesPadZero(text);
	}

	public void LoadChangedItemsOrder(string type)
	{
		byte[] array = DataSource.UserData[type];
		if (array == null || array.Length == 0)
		{
			return;
		}
		string[] array2 = StringUtil.UTF8GetString(array).Split(',');
		string[] array3 = array2;
		foreach (string text in array3)
		{
			string[] nameList = text.Split(':');
			if (nameList.Length == 2 && ItemsA.FirstOrDefault((object t) => (t as ShellDynamicIO).NameA == nameList[0]) is ShellDynamicIO shellDynamicIO)
			{
				shellDynamicIO.IndexA = nameList[1];
			}
		}
	}

	public void SaveDisplayValueType(string type)
	{
		StringBuilder stringBuilder = new StringBuilder();
		foreach (object item in ItemsA)
		{
			if (item is ShellDynamicIO shellDynamicIO && shellDynamicIO.DisplayValueTypeA.Contains("[]"))
			{
				stringBuilder.Append($"{shellDynamicIO.NameA}:{shellDynamicIO.DisplayValueTypeA},");
			}
		}
		string text = stringBuilder.ToString();
		if (text.Length > 0)
		{
			text = text.Substring(0, text.LastIndexOf(","));
		}
		DataSource.UserData[type] = StringUtil.UTF8GetBytesPadZero(text);
	}

	public void LoadDisplayValueType(string type)
	{
		byte[] array = DataSource.UserData[type];
		if (array == null || array.Length == 0)
		{
			return;
		}
		string[] array2 = StringUtil.UTF8GetString(array).Split(',');
		string[] array3 = array2;
		foreach (string text in array3)
		{
			string[] nameList = text.Split(':');
			if (nameList.Length == 2 && ItemsA.FirstOrDefault((object t) => (t as ShellDynamicIO).NameA == nameList[0]) is ShellDynamicIO shellDynamicIO)
			{
				shellDynamicIO.DisplayValueTypeA = nameList[1];
			}
		}
	}
}
