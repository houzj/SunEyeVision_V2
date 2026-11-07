using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Input;
using Apps.BaseData;
using Apps.Interface;
using Apps.Localization;
using Apps.UIHelper.Extern;
using Apps.XmlParser.DateForDisplayInGroup;
using DynamicObjectConfigControl;
using DynamicObjectConfigControl.XmlData;
using DynamicObjectConfigControl.XmlData.Extern;
using Mvvm.Commands;
using VM.Controls;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.Interface;
using VMControls.RenderInterface;

namespace Apps.XmlParser.ParamTab;

public class DynamicNameValueItemOfGroup : NameValueItem, IDynamicItemOfGroup
{
	private bool isROIVisible = false;

	private DynamicNameValueItemOfGroupCategory _Group;

	private ICommand _OpenDialogCommand = null;

	public NameValueItem CloneSource { get; set; }

	public bool IsRelationEnable
	{
		get
		{
			try
			{
				VmModuleSet obj = ParamsSource as VmModuleSet;
				return ((obj != null) ? obj.GetFirstParamRelationModule(Name) : null) != null;
			}
			catch (VmException vmException)
			{
				ShowMessage.ShowVmExceptionMessage(vmException);
			}
			return false;
		}
	}

	public bool IsROIVisible
	{
		get
		{
			return isROIVisible;
		}
		set
		{
			if (value == isROIVisible)
			{
				return;
			}
			IModuleResultView moduleResultView = Ioc.Get<IModuleResultView>();
			if (value)
			{
				bool flag = false;
				if (moduleResultView?.GetDisplayImageIO() is VmIO vmIO && CloneSource is IGroupRunParam param && param.GetSupportDisplayForGroupData() is ROISelecterForGroupDecorate rOISelecterForGroupDecorate)
				{
					VmIO vmIO2 = rOISelecterForGroupDecorate.ModuleSource.Inputs.OfType<VmIO>().FirstOrDefault((VmIO p) => p.TypeName == "IMAGE");
					if (vmIO2 != null && vmIO2.RelateIO != null && vmIO.RelateIO != null)
					{
						flag = vmIO2.RelateIO.FullName.Equals(vmIO.RelateIO.FullName);
					}
				}
				if (!flag)
				{
					HikMessageBox.Show(AppLocalizationService.GetLang("Lang_ROIVisibleWarning"), MessageIcons.Warning);
					return;
				}
			}
			isROIVisible = value;
			moduleResultView?.SetRoiVisible((object)CloneSource, value);
			OnPropertyChanged("IsROIVisible");
		}
	}

	public override string DisplayNameA
	{
		get
		{
			return Name;
		}
		set
		{
		}
	}

	public override string DescriptionA
	{
		get
		{
			return CloneSource?.DescriptionA ?? "";
		}
		set
		{
			if (CloneSource != null && CloneSource.Description != value)
			{
				CloneSource.DescriptionA = value;
				OnPropertyChanged("DescriptionA");
			}
		}
	}

	public override string NameA
	{
		get
		{
			return Name;
		}
		set
		{
			if (Name != value)
			{
				Name = value;
				CloneSource.Name = value;
				OnPropertyChanged("NameA");
			}
		}
	}

	public DynamicNameValueItemOfGroupCategory Group
	{
		get
		{
			return _Group;
		}
		set
		{
			if (_Group != value)
			{
				_Group = value;
				OnPropertyChanged("Group");
			}
		}
	}

	public ICommand OpenDialogCommand => _OpenDialogCommand ?? (_OpenDialogCommand = (ICommand)new DelegateCommand<DialogHandler>((Action<DialogHandler>)OpenDialogCommandExecute));

	private bool CheckName(string value)
	{
		return true;
	}

	protected void OpenDialogCommandExecute(DialogHandler method)
	{
		if (!(CloneSource is IGroupRunParam groupRunParam))
		{
			return;
		}
		IDecorate decorate = groupRunParam.SupportDisplayForGroupData();
		if (decorate == null)
		{
			decorate = groupRunParam.GetSupportDisplayForGroupData();
		}
		if (CloneSource is IGroupRunParamWithMultiParams groupRunParamWithMultiParams)
		{
			RunParamConfigDecorate runParamConfigDecorate = new RunParamConfigDecorate();
			runParamConfigDecorate.Owner = ParamsSource as VmModuleSet;
			runParamConfigDecorate.DecorateSource = this;
			RunParamConfigDecorate runParamConfigDecorate2 = runParamConfigDecorate;
			runParamConfigDecorate2.ClearRelationLeftFirst(out var isOnlyOne, out var relation);
			method.Invoke((object)decorate);
			if (!isOnlyOne)
			{
				object allData = groupRunParamWithMultiParams.GetAllData();
				runParamConfigDecorate2.DeleteFirstRelationConnectAfter(relation);
				groupRunParamWithMultiParams.Clear();
				groupRunParamWithMultiParams.SetAllData(allData);
				runParamConfigDecorate2.RecoverRelation(relation);
			}
		}
		else
		{
			method.Invoke((object)decorate);
		}
	}

	internal int GetGroupID()
	{
		string key = $"{VmModuleSet.GroupIDPre}{Name}";
		byte[] array = (ParamsSource as VmModuleSet).UserData[key];
		return (array == null) ? (-1) : int.Parse(StringUtil.UTF8GetString((ParamsSource as VmModuleSet).UserData[key]));
	}

	internal void SetGroupID(int id)
	{
		string key = $"{VmModuleSet.GroupIDPre}{Name}";
		(ParamsSource as VmModuleSet).UserData[key] = StringUtil.UTF8GetBytesPadZero(id.ToString());
	}

	public static void SyncGroupRunParamWithMultiParams(IGroupRunParamWithMultiParams multiParams, VmModuleSet vmModuleSet, DynamicNameValueItemOfGroup dynamicNameValueItemOfGroup)
	{
		RunParamConfigDecorate runParamConfigDecorate = new RunParamConfigDecorate
		{
			Owner = vmModuleSet,
			DecorateSource = dynamicNameValueItemOfGroup
		};
		Dictionary<uint, string> relationDic = runParamConfigDecorate.GetRelationDic();
		if (relationDic != null)
		{
			object allData = multiParams.GetAllData();
			runParamConfigDecorate.DeleteFirstRelationConnectAfter(relationDic);
			multiParams.Clear();
			multiParams.SetAllData(allData);
			runParamConfigDecorate.RecoverRelation(relationDic);
		}
	}
}
