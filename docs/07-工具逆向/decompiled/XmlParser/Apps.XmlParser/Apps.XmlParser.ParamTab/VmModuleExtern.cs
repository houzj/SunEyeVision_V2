using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using Apps.Data;
using Apps.ErrorCode;
using Apps.Localization;
using Apps.Log;
using Apps.XmlParser.Variable;
using Hik.Xml;
using VM.Core;
using VM.PlatformSDKCS;
using VMControls.BaseInterface;
using VMControls.Interface;

namespace Apps.XmlParser.ParamTab;

public static class VmModuleExtern
{
	public static string GroupParamsName = "Tab_Run Params";

	private static readonly string[] StrParamNames = new string[6] { "基本参数", "运行参数", "结果显示", "Tab_Basic Params", "Tab_Run Params", "ResultShow" };

	private static readonly string DynamicParamsKey = "DynamicParams";

	public static AlgorithmTabRoot GetParamRoot(this ModuleParam param)
	{
		if (param.Data == null)
		{
			param.Data = new AlgorithmTabRoot(param);
		}
		return param.Data as AlgorithmTabRoot;
	}

	public static void GetFrontOutputItems(this VmIO vmIO, IList allInputDiagramItem)
	{
		if (vmIO.Root.Inputs.Contains(vmIO))
		{
			vmIO.DeGetFrontOutputItems(allInputDiagramItem);
			if (vmIO.Root.Parent != null)
			{
				allInputDiagramItem.Add(vmIO.Root.Parent);
			}
			return;
		}
		foreach (object child in (vmIO.Root as IVmModuleSet).Children)
		{
			allInputDiagramItem.Add(child);
		}
	}

	public static void GetFrontOutputItemsByOutputIO(this VmIO vmIO, IList allInputDiagramItem)
	{
		vmIO.DeGetFrontOutputItems(allInputDiagramItem);
		if (vmIO.Root.Parent != null)
		{
			allInputDiagramItem.Add(vmIO.Root.Parent);
		}
	}

	public static void DeGetFrontOutputItems(this VmIO vmIO, IList allInputDiagramItem)
	{
		IList preModules = vmIO.Root.GetPreModules();
		foreach (VmModule item in preModules)
		{
			if (!allInputDiagramItem.Contains(item))
			{
				allInputDiagramItem.Add(item);
				item.DeGetFrontOutputItems(allInputDiagramItem);
			}
		}
	}

	public static List<VmModule> GetFrontParamItems(this VmIO vmIO)
	{
		List<VmModule> list = new List<VmModule>();
		vmIO.GetFrontOutputItems(list);
		return list;
	}

	public static List<VmModule> GetFrontParamItemsByOutputIO(this VmIO vmIO)
	{
		List<VmModule> list = new List<VmModule>();
		vmIO.GetFrontOutputItemsByOutputIO(list);
		return list;
	}

	public static IList GetFrontFilterTree(this VmIO vmIO, Func<VmModule, bool> predicate, string CategoryName, bool IsIncludeSelf = false, bool IsIncludeInside = false, bool IsNeedNull = true, bool IsGlobalVar = true, string SearchFilter = "", bool IsNeedTs = true)
	{
		List<VmModule> list = vmIO.GetFrontParamItems().Where(predicate).ToList();
		List<Holder> list2 = new List<Holder>();
		string[] ValueType = new string[1] { vmIO.TypeName };
		if (!vmIO.IsOutput && ValueType.Contains("string"))
		{
			Holder holder = new Holder
			{
				DisplayNameA = "外部通讯"
			};
			holder.EnumEntrysA.Add(VmSolution.ExternIO);
			list2.Add(holder);
		}
		foreach (VmModule item2 in list)
		{
			item2.AddOutputTreeNode(vmIO.Root, list2, ValueType, SearchFilter);
		}
		List<Holder> list3 = list2.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (IsGlobalVar && !vmIO.IsOutput)
		{
			VmModule globalVarModule = GetGlobalVarModule();
			bool flag = globalVarModule != null && globalVarModule.Outputs.FirstOrDefault((VmIO x) => ValueType.Contains(x.TypeName)) != null;
			bool flag2 = false;
			if (!(vmIO.Root is IVmProcedure))
			{
				IVmModule localVarModule = vmIO.Root.GetLocalVarModule(!vmIO.IsOutput);
				flag2 = localVarModule != null && (localVarModule as VmModule).Outputs.FirstOrDefault((VmIO x) => ValueType.Contains(x.TypeName)) != null;
			}
			if (flag | flag2)
			{
				Holder item = new Holder
				{
					DisplayNameA = "Lang_VariableManager",
					HolderTarget = new List<object>
					{
						ValueType,
						!vmIO.IsOutput
					}
				};
				list3.Add(item);
			}
		}
		if (IsNeedNull)
		{
			list3.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list3;
	}

	public static IList GetFrontFilterTreeByOuputIO(this VmIO vmIO, Func<VmModule, bool> predicate, string CategoryName, bool IsIncludeSelf = false, bool IsIncludeInside = false, bool IsNeedNull = true, bool IsGlobalVar = true, string SearchFilter = "", bool IsNeedTs = true)
	{
		List<VmModule> list = vmIO.GetFrontParamItemsByOutputIO().Where(predicate).ToList();
		List<Holder> list2 = new List<Holder>();
		string[] ValueType = new string[1] { vmIO.TypeName };
		if (!vmIO.IsOutput && ValueType.Contains("string"))
		{
			Holder holder = new Holder
			{
				DisplayNameA = "外部通讯"
			};
			holder.EnumEntrysA.Add(VmSolution.ExternIO);
			list2.Add(holder);
		}
		foreach (VmModule item2 in list)
		{
			item2.AddOutputTreeNode(vmIO.Root, list2, ValueType, SearchFilter);
		}
		List<Holder> list3 = list2.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (IsGlobalVar && !vmIO.IsOutput)
		{
			VmModule globalVarModule = GetGlobalVarModule();
			bool flag = globalVarModule != null && globalVarModule.Outputs.FirstOrDefault((VmIO x) => ValueType.Contains(x.TypeName)) != null;
			IVmModule localVarModule = vmIO.Root.GetLocalVarModule();
			bool flag2 = localVarModule != null && (localVarModule as VmModule).Outputs.FirstOrDefault((VmIO x) => ValueType.Contains(x.TypeName)) != null;
			if (flag | flag2)
			{
				Holder item = new Holder
				{
					DisplayNameA = "Lang_VariableManager",
					HolderTarget = ValueType
				};
				list3.Add(item);
			}
		}
		if (IsNeedNull)
		{
			list3.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list3;
	}

	public static IList GetFrontFilterTree(this VmModule vmModule, Func<VmModule, bool> predicate, string CategoryName, string[] ValueType, bool IsIncludeSelf = false, bool IsIncludeInside = false, bool IsNeedNull = true, bool IsGlobalVar = true, string SearchFilter = "", bool isNeedTS = true, object customObj = null)
	{
		List<VmModule> moduleInfos = vmModule.GetFrontParamItems().Where(predicate).ToList();
		List<Holder> list = new List<Holder>();
		if (isNeedTS && ValueType.Contains("string"))
		{
			Holder holder = new Holder
			{
				DisplayNameA = "外部通讯"
			};
			holder.EnumEntrysA.Add(VmSolution.ExternIO);
			list.Add(holder);
		}
		if (IsIncludeInside && vmModule is IVmModuleSet vmModuleSet)
		{
			vmModuleSet.Children.OfType<VmModule>().ToList().ForEach((VmModule p) =>
			{
				moduleInfos.Add(p);
			});
		}
		if (IsIncludeSelf)
		{
			moduleInfos.Add(vmModule);
		}
		foreach (VmModule item2 in moduleInfos)
		{
			item2.AddOutputTreeNode(vmModule, list, ValueType, SearchFilter);
		}
		List<Holder> list2 = list.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (IsGlobalVar)
		{
			VmModule globalVarModule = GetGlobalVarModule();
			bool flag = globalVarModule != null && globalVarModule.Outputs.FirstOrDefault((VmIO x) => ValueType.Contains(x.TypeName)) != null;
			IVmModule localVarModule = vmModule.GetLocalVarModule(CategoryName == "Output");
			bool flag2 = localVarModule != null && (localVarModule as VmModule).Outputs.FirstOrDefault((VmIO x) => ValueType.Contains(x.TypeName)) != null;
			object obj = null;
			obj = ((customObj == null) ? new List<object>
			{
				ValueType,
				CategoryName == "Output"
			} : new List<object> { ValueType, customObj });
			if (flag | flag2)
			{
				Holder item = new Holder
				{
					DisplayNameA = "Lang_VariableManager",
					HolderTarget = obj
				};
				list2.Add(item);
			}
		}
		if (IsNeedNull)
		{
			list2.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		if (IsIncludeSelf)
		{
			foreach (Holder item3 in list2)
			{
				if (item3.HolderTarget is VmModule vmModule2 && vmModule2.Name.Equals(vmModule.Name))
				{
					VmIO vmIO = item3.EnumEntrysA.OfType<VmIO>().FirstOrDefault((VmIO p) => p.Name.Equals("ModuRunTime"));
					if (vmIO != null)
					{
						item3.EnumEntrysA.Remove(vmIO);
						break;
					}
				}
			}
		}
		return list2;
	}

	public static IList GetChildren(this IVmIO vmIO)
	{
		return (vmIO as VmIO).Childs;
	}

	public static IList<object> GetDisplayList(this IVmModule module)
	{
		string text = null;
		string moduleTypeName = module.ModuleTypeName;
		DirectoryInfo directoryInfo = new DirectoryInfo(Path.GetFullPath("./Module(sp)") + "\\x64");
		DirectoryInfo[] directories = directoryInfo.GetDirectories();
		foreach (DirectoryInfo directoryInfo2 in directories)
		{
			if ((from p in directoryInfo2.GetDirectories()
				select p.Name).Contains(moduleTypeName))
			{
				text = directoryInfo2.FullName;
				break;
			}
		}
		string fileName = text + "\\" + moduleTypeName + "\\" + moduleTypeName + "Display.xml";
		ParamRoot paramRoot = new ParamRoot();
		paramRoot.loadXml(fileName);
		paramRoot.Owner = module;
		return paramRoot.GetDisplayList();
	}

	public static IList GetInternalModuleSetObjectTree(this IVmModuleSet vmModule, Func<VmModule, bool> predicate, string CategoryName, Func<Object, bool> typePredicate, bool isIncludeInside = false, bool isNeedNull = true)
	{
		List<VmModule> list = vmModule.Children.OfType<IMVSGroup>().Where(predicate).ToList();
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item in list)
		{
			item.AddObjectNode(list2, typePredicate);
		}
		List<Holder> list3 = list2.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (isNeedNull)
		{
			list3.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list3;
	}

	public static IList GetInternalCombinationTree(this IVmModuleSet vmModule, Func<VmModule, bool> predicate, string CategoryName, string CategoryStyle, bool isIncludeInside = false, bool isNeedNull = true)
	{
		List<VmModule> list = vmModule.Children.OfType<VmModule>().Where(predicate).ToList();
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item in list)
		{
			item.AddCombinationNode(vmModule as VmModule, list2, CategoryStyle);
		}
		List<Holder> list3 = list2.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (isNeedNull)
		{
			list3.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list3;
	}

	public static IList GetParamsOfEqualTypeTree(this VmModuleSet vmModuleSet, string[] ValueType)
	{
		List<Holder> list = new List<Holder>();
		vmModuleSet.AddParamsNode(list, ValueType);
		return list.OrderBy((Holder P) => P.DisplayNameA).ToList();
	}

	public static void AddParamsNode(this VmModuleSet vmModuleSet, List<Holder> holderList, string[] valueType)
	{
		Holder paramsNode = vmModuleSet.GetParamsNode(valueType);
		if (paramsNode != null)
		{
			holderList.Add(paramsNode);
		}
	}

	public static Holder GetParamsNode(this VmModuleSet vmModuleSet, string[] styleType)
	{
		string moduleStrName = styleType[0];
		string text = styleType[1];
		string text2 = styleType[2];
		List<NameValueItem> list = new List<NameValueItem>();
		IEnumerable<VmModule> enumerable = vmModuleSet.Modules.Where((VmModule p) => !(p is IVmModuleSet) && moduleStrName == p.StrModuleName);
		foreach (VmModule item2 in enumerable)
		{
			if (item2.Params.GetParamRoot().FindBaseNodeByName(text) is NameValueItem item)
			{
				list.Add(item);
			}
		}
		foreach (VmModuleSet item3 in vmModuleSet.Modules.OfType<VmModuleSet>())
		{
			IEnumerable<NameValueItem> enumerable2 = (item3.Params.GetParamRoot().FindBaseNodeByName(GroupParamsName) as IChildren).Children.OfType<NameValueItem>();
			foreach (NameValueItem item4 in enumerable2)
			{
				string key = VmModuleSet.HeadPathPre + item4.Name;
				string[] array = StringUtil.UTF8GetString(item3.UserData[key]).Split('.');
				string text3 = array[0];
				string text4 = array[1];
				if (text3 == moduleStrName && text4 == text)
				{
					list.Add(item4);
				}
			}
		}
		List<Holder> list2 = new List<Holder>();
		if (list != null && list.Count > 0)
		{
			Holder holder = new Holder();
			holder.DisplayNameA = "%" + vmModuleSet.ID + " " + vmModuleSet.Name + "%";
			holder.HolderTarget = vmModuleSet;
			Holder holder2 = holder;
			Stack stack = new Stack();
			foreach (NameValueItem item5 in list)
			{
				Holder holder3 = holder2;
				while (stack.Count > 0)
				{
					Holder holder4 = stack.Pop() as Holder;
					if (!holder3.EnumEntrysA.Contains(holder4))
					{
						holder3.EnumEntrysA.Add(holder4);
					}
					holder3 = holder4;
				}
				string text5 = null;
				text5 = ((!(item5.ParamsSource is IVmModuleSet)) ? $"%{(item5.ParamsSource as IVmModule).ID} {(item5.ParamsSource as IVmModule).Name}.{AppLocalizationService.GetLang(item5.DisplayName)}%" : $"%{(item5.ParamsSource as IVmModule).ID} {(item5.ParamsSource as IVmModule).Name}.{AppLocalizationService.GetLang(item5.Name)}%");
				Holder holder5 = new Holder
				{
					DisplayNameA = text5,
					HolderTarget = item5
				};
				holder5.ParentTree = holder2;
				holder3.EnumEntrysA.Add(holder5);
			}
			return holder2;
		}
		return null;
	}

	public static IList GetInsideDisplayTree(this IVmModuleSet vmModule, string[] ValueType)
	{
		return (vmModule as VmModuleSet).GetInternalObjectTree(ValueType, isNeedNull: false);
	}

	public static IList GetInsideVmIOTree(this VmModule vmModule, Func<VmModule, bool> predicate, string CategoryName, string[] ValueType, bool IsIncludeSelf = false, bool IsIncludeInside = false, bool IsNeedNull = true, bool IsGlobalVar = true, string SearchFilter = "")
	{
		List<VmModule> list = (vmModule as IVmModuleSet).Children.OfType<VmModule>().Where(predicate).ToList();
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item in list)
		{
			item.AddOutputVmIOTreeNode(list2, ValueType, SearchFilter);
		}
		List<Holder> list3 = list2.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (IsNeedNull)
		{
			list3.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list3;
	}

	public static void BuildBaseNodeTreePath(this BaseNode item, List<Holder> forShownList, Stack stack)
	{
		BaseNode parentObj = item.GetParentNode() as BaseNode;
		if (parentObj != null && parentObj is IVmModule)
		{
			Holder holder = forShownList.Find((Holder p) => p.HolderTarget == parentObj);
			if (holder == null)
			{
				Holder holder2 = new Holder();
				holder2.DisplayNameA = (parentObj as IName).Name;
				holder2.HolderTarget = parentObj;
				holder = holder2;
				forShownList.Add(holder);
			}
			stack.Push(holder);
			parentObj.BuildBaseNodeTreePath(forShownList, stack);
		}
	}

	public static Holder GetRunParamTreeNode(this VmModule vmModule, List<string> filterTypes)
	{
		List<NameValueItem> list = null;
		if (vmModule is VmModuleSet vmModuleSet)
		{
			list = new List<NameValueItem>();
			IEnumerable<NameValueItem> enumerable = (vmModuleSet.Params.GetParamRoot().FindBaseNodeByName(GroupParamsName) as IChildren).Children.OfType<NameValueItem>();
			foreach (NameValueItem item in enumerable)
			{
				if (filterTypes.Contains(item.GetPropertyValueByName("CloneSource").GetType().Name))
				{
					list.Add(item);
				}
			}
		}
		else
		{
			list = vmModule.Params.GetParamRoot().GetAllItems().Where((NameValueItem p) =>
			{
				NameValueItem nameValueItem;
				return p is IGroupRunParam groupRunParam && (nameValueItem = p) != null && nameValueItem.CustomVisibleA && nameValueItem.GroupRunParamEnable && filterTypes.Contains(p.GetType().Name) && (nameValueItem.AccessMode?.ToLower() == "rw" || !string.IsNullOrEmpty(groupRunParam.DisplayForGroupType));
			})
				.ToList();
		}
		List<Holder> forShownList = new List<Holder>();
		if (list != null && list.Count > 0)
		{
			Holder holder = new Holder
			{
				DisplayNameA = "%" + vmModule.Name + "%",
				HolderTarget = vmModule
			};
			Stack stack = new Stack();
			foreach (NameValueItem item2 in list)
			{
				Holder holder2 = holder;
				item2.BuildBaseNodeTreePath(forShownList, stack);
				while (stack.Count > 0)
				{
					Holder holder3 = stack.Pop() as Holder;
					if (!holder2.EnumEntrysA.Contains(holder3))
					{
						holder3.ParentTree = holder2;
						holder2.EnumEntrysA.Add(holder3);
					}
					holder2 = holder3;
				}
				Holder holder4 = new Holder
				{
					DisplayNameA = item2.DisplayNameA,
					HolderTarget = item2
				};
				holder4.ParentTree = holder2;
				holder2.EnumEntrysA.Add(holder4);
			}
			return holder;
		}
		return null;
	}

	public static void AddRunParamTreeNode(this VmModule vmModule, List<Holder> holderList, List<string> filterTypes)
	{
		Holder runParamTreeNode = vmModule.GetRunParamTreeNode(filterTypes);
		if (runParamTreeNode != null)
		{
			holderList.Add(runParamTreeNode);
		}
	}

	public static IList GetInsideRunParamTree(this VmModule vmModule, List<string> filterTypes)
	{
		List<VmModule> list = (vmModule as IVmModuleSet).Children.OfType<VmModule>().ToList();
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item in list)
		{
			item.AddRunParamTreeNode(list2, filterTypes);
		}
		return list2;
	}

	public static ImageData GetFirstData(this IVmModule module)
	{
		if (module != null)
		{
			foreach (VmIO item in from p in module.Inputs.OfType<VmIO>()
				where p.TypeName == "IMAGE"
				select p)
			{
				if (item.RelateIO != null)
				{
					object combinationValueByType = item.RelateIO.GetCombinationValueByType(uint.MaxValue);
					return combinationValueByType as ImageData;
				}
			}
		}
		return null;
	}

	public static IVmModule GetFirstParamRelationModule(this IVmModuleSet vmModuleSet, string paramName)
	{
		try
		{
			ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION_LIST? dynamicParamRelationByKey = ServerSDKManager.serverSDKManager.mModuleManager.GetDynamicParamRelationByKey((vmModuleSet as IVmModule).ID, paramName);
			if (dynamicParamRelationByKey.HasValue)
			{
				ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION_LIST value = dynamicParamRelationByKey.Value;
				if (value.nNum != 0)
				{
					for (int i = 0; i < value.nNum; i++)
					{
						ImvsSdkDefine.IMVS_DYNAMIC_PARAM_RELATION iMVS_DYNAMIC_PARAM_RELATION = value.stRelationList[i];
						uint id = iMVS_DYNAMIC_PARAM_RELATION.nObjectID;
						string paramName2 = StringUtil.UTF8GetString(iMVS_DYNAMIC_PARAM_RELATION.strName);
						IVmModule vmModule = vmModuleSet.Children.OfType<IVmModule>().FirstOrDefault((IVmModule p) => p.ID == id);
						if (vmModule is VmModuleSet vmModuleSet2)
						{
							IVmModule firstParamRelationModule = vmModuleSet2.GetFirstParamRelationModule(paramName2);
							if (firstParamRelationModule != null)
							{
								return firstParamRelationModule;
							}
							continue;
						}
						return vmModule;
					}
				}
			}
		}
		catch (VmException ex)
		{
			int errorCode = ex.errorCode;
			string message = "GetFirstParamRelationModule error, " + AppLocalizationService.GetLang("errCode:") + errorCode.ToString("X") + Environment.NewLine + AppLocalizationService.GetLang("errInfo:") + ErrorCodeInfo.GetErrorInfo(errorCode);
			LogHelper.Error(message);
			return null;
		}
		return null;
	}

	public static IList GetFrontVmIOTree(this VmModule vmModule, Func<VmModule, bool> predicate, string CategoryName, string[] ValueType, bool IsIncludeSelf = false, bool IsIncludeInside = false, bool IsNeedNull = true, bool IsGlobalVar = true, string SearchFilter = "")
	{
		List<VmModule> list = vmModule.GetFrontParamItems().Where(predicate).ToList();
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item in list)
		{
			if (vmModule.Parent == item)
			{
				item.AddInputVmIOTreeNode(list2, ValueType, SearchFilter);
			}
			else
			{
				item.AddOutputVmIOTreeNode(list2, ValueType, SearchFilter);
			}
		}
		List<Holder> list3 = list2.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (IsNeedNull)
		{
			list3.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list3;
	}

	public static void AddObjectNode(this VmModule vmModule, List<Holder> holderList, string[] styleType)
	{
		Holder objectNode = vmModule.GetObjectNode(styleType);
		if (objectNode != null)
		{
			holderList.Add(objectNode);
		}
	}

	public static Holder GetObjectNode(this VmModule vmModule, string[] styleType)
	{
		if (vmModule["[Display]"] is ParamRoot paramRoot)
		{
			List<Object> list = (from p in paramRoot.GetDisplayList().OfType<Object>()
				where p.IsDisplayA && styleType.Contains(p.TypeA) && (p.TypeA == "text" || vmModule.Outputs.GetAllIO().Contains(p.GetAllTopMappingIO().FirstOrDefault()))
				select p).ToList();
			List<Holder> list2 = new List<Holder>();
			if (list != null && list.Count > 0)
			{
				Holder holder = new Holder();
				holder.DisplayNameA = "%" + vmModule.ID + " " + vmModule.Name + "%";
				holder.HolderTarget = vmModule;
				Holder holder2 = holder;
				Stack stack = new Stack();
				foreach (Object item in list)
				{
					Holder holder3 = holder2;
					while (stack.Count > 0)
					{
						Holder holder4 = stack.Pop() as Holder;
						if (!holder3.EnumEntrysA.Contains(holder4))
						{
							holder3.EnumEntrysA.Add(holder4);
						}
						holder3 = holder4;
					}
					Holder holder5 = new Holder
					{
						DisplayNameA = item.NameA,
						HolderTarget = item
					};
					holder5.ParentTree = holder2;
					holder3.EnumEntrysA.Add(holder5);
				}
				return holder2;
			}
		}
		return null;
	}

	public static IList GetInternalObjectTree(this VmModuleSet vmModuleSet, string styleType)
	{
		return vmModuleSet.GetInternalObjectTree(new string[1] { styleType });
	}

	public static IList GetInternalObjectTree(this VmModuleSet vmModuleSet, string[] styleType, bool isNeedNull = true)
	{
		VmModuleCollection<VmModule> modules = vmModuleSet.Modules;
		List<Holder> list = new List<Holder>();
		foreach (VmModule item in modules)
		{
			item.AddObjectNode(list, styleType);
		}
		List<Holder> list2 = list.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (isNeedNull)
		{
			list2.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list2;
	}

	public static IList GetSelfCombinationTree(this VmModule vmModule, Func<VmModule, bool> predicate, string CategoryStyle, bool isIncludeInside = false, bool isNeedNull = true)
	{
		List<VmModule> list = new List<VmModule> { vmModule };
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item in list)
		{
			item.AddCombinationNode(vmModule, list2, CategoryStyle, includeInput: true);
		}
		List<Holder> list3 = list2.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (isNeedNull)
		{
			list3.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list3;
	}

	public static IList GetSelfOutputCombinationTree(this VmModule vmModule, Func<VmModule, bool> predicate, string CategoryStyle, bool isIncludeInside = false, bool isNeedNull = true)
	{
		List<VmModule> list = new List<VmModule> { vmModule };
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item in list)
		{
			item.AddCombinationNode(vmModule, list2, CategoryStyle);
		}
		List<Holder> list3 = list2.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (isNeedNull)
		{
			list3.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list3;
	}

	public static IList GetFrontCombinationTree(this VmModule vmModule, Func<VmModule, bool> predicate, string CategoryName, string CategoryStyle, bool isIncludeInside = false, bool isNeedNull = true, bool IsGlobalVar = true)
	{
		List<VmModule> list = vmModule.GetFrontParamItems().Where(predicate).ToList();
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item2 in list)
		{
			item2.AddCombinationNode(vmModule, list2, CategoryStyle);
		}
		List<Holder> list3 = list2.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (IsGlobalVar)
		{
			VmModule globalVarModule = GetGlobalVarModule();
			bool flag = globalVarModule != null && globalVarModule.Outputs.FirstOrDefault((VmIO x) => CategoryStyle == x.TypeName) != null;
			bool flag2 = false;
			if (!(vmModule is IVmProcedure))
			{
				IVmModule localVarModule = vmModule.GetLocalVarModule(CategoryName == "Output");
				flag2 = localVarModule != null && (localVarModule as VmModule).Outputs.FirstOrDefault((VmIO x) => CategoryStyle == x.TypeName) != null;
			}
			if (flag | flag2)
			{
				Holder holder = new Holder();
				holder.DisplayNameA = "Lang_VariableManager";
				holder.HolderTarget = new List<object>
				{
					new string[1] { CategoryStyle },
					CategoryName == "Output"
				};
				Holder item = holder;
				list3.Add(item);
			}
		}
		if (isNeedNull)
		{
			list3.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list3;
	}

	public static IList GetInternalCombinationTree(this VmModule vmModule, Func<VmModule, bool> predicate, string CategoryName, string CategoryStyle, bool isIncludeInside = false, bool isNeedNull = true)
	{
		List<VmModule> list = (vmModule as IVmModuleSet).Children.OfType<VmModule>().Where(predicate).ToList();
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item in list)
		{
			item.AddCombinationNode(vmModule, list2, CategoryStyle);
		}
		List<Holder> list3 = list2.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (isNeedNull)
		{
			list3.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list3;
	}

	public static IList GetInternalObjectTree(this IVmModuleSet vmModule, Func<VmModule, bool> predicate, string CategoryName, Func<Object, bool> typePredicate, bool isIncludeInside = false, bool isNeedNull = true, bool isIncludeSelf = false)
	{
		List<VmModule> list = vmModule.Children.OfType<VmModule>().Where(predicate).ToList();
		if (isIncludeSelf)
		{
			list.Insert(0, vmModule as VmModule);
		}
		List<Holder> list2 = new List<Holder>();
		foreach (VmModule item in list)
		{
			item.AddObjectNode(list2, typePredicate);
		}
		List<Holder> list3 = list2.OrderBy((Holder P) => P.DisplayNameA).ToList();
		if (isNeedNull)
		{
			list3.Insert(0, new Holder
			{
				DisplayNameA = Holder.NullName,
				HolderTarget = "null"
			});
		}
		return list3;
	}

	public static void AddObjectNode(this VmModule vmModule, List<Holder> holderList, Func<Object, bool> typePredicate)
	{
		Holder objectNode = vmModule.GetObjectNode(typePredicate);
		if (objectNode != null)
		{
			holderList.Add(objectNode);
		}
	}

	public static void AddCombinationNode(this VmModule vmModule, VmModule targetModule, List<Holder> holderList, string CategoryStyle, bool includeInput = false)
	{
		Holder combinationNode = vmModule.GetCombinationNode(targetModule, CategoryStyle, includeInput);
		if (combinationNode != null)
		{
			holderList.Add(combinationNode);
		}
	}

	public static Object GetObject(this VmModule vmModule, string name)
	{
		ParamRoot paramRoot = vmModule["[Display]"] as ParamRoot;
		return paramRoot.GetDisplayList().OfType<Object>().First((Object p) => p.NameA == name);
	}

	public static Holder GetObjectNode(this VmModule vmModule, Func<Object, bool> typePredicate)
	{
		object obj = vmModule["[Display]"];
		if (obj is ParamRoot paramRoot)
		{
			List<Object> list = paramRoot.GetDisplayList().OfType<Object>().ToList();
			if (vmModule is IMVSGroup)
			{
				byte[] array = vmModule.UserData["ImageSourceObjectPath"];
				if (array != null)
				{
					string sub = StringUtil.UTF8GetString(array);
					if (VmSolution.Instance[sub] is Object item)
					{
						list.Add(item);
					}
				}
				array = vmModule.UserData["ShapeObjectPath"];
				if (array != null)
				{
					string[] source = StringUtil.UTF8GetString(array)?.Split(';');
					List<Object> list2 = source.Select((string p) => VmSolution.Instance[p]).OfType<Object>().ToList();
					foreach (Object item2 in list2)
					{
						list.Add(item2);
					}
				}
			}
			list = list.Where(typePredicate).ToList();
			List<Holder> list3 = new List<Holder>();
			if (list != null && list.Count > 0)
			{
				Holder holder = new Holder
				{
					DisplayNameA = "%" + vmModule.Name + "%",
					HolderTarget = vmModule
				};
				Stack stack = new Stack();
				foreach (Object item3 in list)
				{
					Holder holder2 = new Holder
					{
						DisplayNameA = item3.NameA,
						HolderTarget = item3
					};
					holder2.ParentTree = holder;
					holder.EnumEntrysA.Add(holder2);
				}
				return holder;
			}
		}
		return null;
	}

	public static Holder GetCombinationNode(this VmModule vmModule, VmModule targetModule, string CategoryStyle, bool includeInput = false)
	{
		VmIOCollection vmIOCollection = null;
		if (targetModule.Parent == vmModule)
		{
			vmIOCollection = vmModule.Inputs;
		}
		else
		{
			vmIOCollection = ((!includeInput) ? vmModule.Outputs : new VmIOCollection(vmModule.Inputs.Concat(vmModule.Outputs)));
		}
		List<VmIO> list = (from p in vmIOCollection.GetAllTrunkIO()
			where p.TypeName == CategoryStyle && p.Visible
			select p).ToList();
		List<Holder> forShownList = new List<Holder>();
		if (list != null && list.Count > 0)
		{
			Holder holder = new Holder();
			holder.DisplayNameA = "%" + vmModule.ID + " " + vmModule.Name + "%";
			holder.HolderTarget = vmModule;
			Holder holder2 = holder;
			Stack stack = new Stack();
			foreach (VmIO item in list)
			{
				Holder holder3 = holder2;
				BuildCombinationTreePath(item, forShownList, stack);
				while (stack.Count > 0)
				{
					Holder holder4 = stack.Pop() as Holder;
					if (!holder3.EnumEntrysA.Contains(holder4))
					{
						holder3.EnumEntrysA.Add(holder4);
					}
					holder3 = holder4;
				}
				holder3.EnumEntrysA.Add(item);
			}
			return holder2;
		}
		return null;
	}

	public static void BuildCombinationTreePath(VmIO combination, List<Holder> forShownList, Stack stack)
	{
		VmIO parentObj = combination.Parent as VmIO;
		if (parentObj != null)
		{
			Holder holder = forShownList.Find((Holder p) => p.HolderTarget == parentObj);
			if (holder == null)
			{
				holder = new Holder
				{
					DisplayNameA = parentObj.Name,
					HolderTarget = parentObj
				};
				forShownList.Add(holder);
			}
			stack.Push(holder);
			BuildCombinationTreePath(parentObj, forShownList, stack);
		}
	}

	public static void AddOutputVmIOTreeNode(this VmModule vmModule, List<Holder> holderList, string[] ValueType, string SearchFilter = "")
	{
		Holder outputVmIOTreeNode = vmModule.GetOutputVmIOTreeNode(ValueType, SearchFilter);
		if (outputVmIOTreeNode != null)
		{
			holderList.Add(outputVmIOTreeNode);
		}
	}

	public static void AddInputVmIOTreeNode(this VmModule vmModule, List<Holder> holderList, string[] ValueType, string SearchFilter = "")
	{
		Holder inputVmIOTreeNode = vmModule.GetInputVmIOTreeNode(ValueType, SearchFilter);
		if (inputVmIOTreeNode != null)
		{
			holderList.Add(inputVmIOTreeNode);
		}
	}

	public static void AddOutputTreeNode(this VmModule vmModule, VmModule targetModule, List<Holder> holderList, string[] ValueType, string SearchFilter = "")
	{
		Holder outputTreeNode = vmModule.GetOutputTreeNode(targetModule, ValueType, SearchFilter);
		if (outputTreeNode != null)
		{
			holderList.Add(outputTreeNode);
		}
	}

	public static void FindAllSubNode(this VmIO filter, List<VmIO> forShownList, string[] ValueType, string SearchFilter = "")
	{
		if (ValueType.Contains(filter.TypeName) && filter.Name.Contains(SearchFilter))
		{
			forShownList.Add(filter);
		}
		foreach (VmIO child in filter.Childs)
		{
			child.FindAllSubNode(forShownList, ValueType, SearchFilter);
		}
	}

	public static void FindAllSubNodeA(this VmIO filter, List<DynamicIO> forShownList, string[] ValueType, string SearchFilter = "")
	{
		if (ValueType.Contains(filter.TypeName) && AppLocalizationService.GetLang(filter.Name).ToLower().Contains(SearchFilter.ToLower()) && !VmModule.fTimeStamp.Contains(filter.Name))
		{
			DynamicIO item = new DynamicIO
			{
				IoDataSource = filter,
				DisplayNameA = filter.Name
			};
			forShownList.Add(item);
		}
		foreach (VmIO child in filter.Childs)
		{
			child.FindAllSubNodeA(forShownList, ValueType, SearchFilter);
		}
	}

	public static Holder GetInputVmIOTreeNode(this VmModule vmModule, string[] ValueType, string SearchFilter = "")
	{
		if (vmModule.Inputs.Count > 0)
		{
			List<VmIO> list = (from p in vmModule.Inputs.GetAllLeafIO()
				where ValueType.Contains(p.TypeName) && p.CustomVisibleA && p.Visible
				select p).ToList();
			List<Holder> forShownList = new List<Holder>();
			if (list != null && list.Count > 0)
			{
				Holder holder = new Holder
				{
					DisplayNameA = "%" + vmModule.Name + "%",
					HolderTarget = vmModule
				};
				Stack stack = new Stack();
				foreach (VmIO item in list)
				{
					Holder holder2 = holder;
					item.BuildFilterTreePath(forShownList, stack);
					while (stack.Count > 0)
					{
						Holder holder3 = stack.Pop() as Holder;
						if (!holder2.EnumEntrysA.Contains(holder3))
						{
							holder3.ParentTree = holder2;
							holder2.EnumEntrysA.Add(holder3);
						}
						holder2 = holder3;
					}
					Holder holder4 = new Holder
					{
						DisplayNameA = item.Name,
						HolderTarget = item
					};
					holder4.ParentTree = holder;
					holder2.EnumEntrysA.Add(holder4);
				}
				return holder;
			}
		}
		return null;
	}

	public static Holder GetOutputVmIOTreeNode(this VmModule vmModule, string[] ValueType, string SearchFilter = "")
	{
		if (vmModule.Outputs.Count > 0)
		{
			List<VmIO> list = (from p in vmModule.Outputs.GetAllLeafIO()
				where ValueType.Contains(p.TypeName) && p.CanSubscribeA && p.Visible && !p.IsPrivate && !VmModule.fTimeStamp.Contains(p.Name)
				select p).ToList();
			List<Holder> forShownList = new List<Holder>();
			if (list != null && list.Count > 0)
			{
				Holder holder = new Holder
				{
					DisplayNameA = "%" + vmModule.Name + "%",
					HolderTarget = vmModule
				};
				Stack stack = new Stack();
				foreach (VmIO item in list)
				{
					Holder holder2 = holder;
					item.BuildFilterTreePath(forShownList, stack);
					while (stack.Count > 0)
					{
						Holder holder3 = stack.Pop() as Holder;
						if (!holder2.EnumEntrysA.Contains(holder3))
						{
							if (holder3.HolderTarget is VmIO { TypeName: "ACQUIMAGE" })
							{
								continue;
							}
							holder3.ParentTree = holder2;
							holder2.EnumEntrysA.Add(holder3);
						}
						holder2 = holder3;
					}
					Holder holder4 = new Holder
					{
						DisplayNameA = item.Name,
						HolderTarget = item
					};
					holder4.ParentTree = holder2;
					holder2.EnumEntrysA.Add(holder4);
				}
				return holder;
			}
		}
		return null;
	}

	public static Holder GetOutputTreeNode(this VmModule vmModule, VmModule targetModule, string[] ValueType, string SearchFilter = "")
	{
		if (vmModule.Outputs.Count > 0)
		{
			bool flag = targetModule.Parent == vmModule;
			VmIOCollection vmIOCollection = (flag ? vmModule.Inputs : vmModule.Outputs);
			List<VmIO> list = (from p in vmIOCollection.GetAllLeafIO()
				where ValueType.Contains(p.TypeName) && p.CanSubscribeA && p.Visible && !VmModule.fTimeStamp.Contains(p.Name)
				select p).ToList();
			if (!flag)
			{
				list = list.Where((VmIO p) => !p.IsPrivate).ToList();
			}
			if (targetModule.Parent == vmModule && vmModule is IMVSGroup iMVSGroup)
			{
				VmIO leafIO = iMVSGroup.Outputs.GetLeafIO("LoopIndex");
				if (ValueType.Contains("int"))
				{
					list.Add(leafIO);
				}
			}
			List<Holder> forShownList = new List<Holder>();
			if (list != null && list.Count > 0)
			{
				Holder holder = new Holder
				{
					DisplayNameA = AppLocalizationService.UnDoTranslate(vmModule.ID + " " + vmModule.Name),
					HolderTarget = vmModule
				};
				Stack stack = new Stack();
				foreach (VmIO item in list)
				{
					Holder holder2 = holder;
					item.BuildFilterTreePath(forShownList, stack);
					while (stack.Count > 0)
					{
						Holder holder3 = stack.Pop() as Holder;
						if (!holder2.EnumEntrysA.Contains(holder3))
						{
							holder3.ParentTree = holder2;
							holder2.EnumEntrysA.Add(holder3);
						}
						holder2 = holder3;
					}
					holder2.EnumEntrysA.Add(item);
				}
				return holder;
			}
		}
		return null;
	}

	public static Holder GetOutputTreeNodeA(this VmModule vmModule, List<DynamicIO> dylist, string[] ValueType, string SearchFilter = "")
	{
		if (vmModule.Outputs != null && vmModule.Outputs.Count > 0)
		{
			List<DynamicIO> list = new List<DynamicIO>();
			List<Holder> forShownList = new List<Holder>();
			foreach (VmIO output in vmModule.Outputs)
			{
				output.FindAllSubNodeA(dylist, ValueType, SearchFilter);
			}
			if (dylist.Count > 0)
			{
				Holder holder = new Holder
				{
					DisplayNameA = vmModule.Name,
					HolderTarget = vmModule
				};
				Stack stack = new Stack();
				foreach (DynamicIO item in dylist)
				{
					if (VmModule.fTimeStamp.Contains(item.NameA))
					{
						continue;
					}
					Holder holder2 = holder;
					item.IoDataSource.BuildFilterTreePath(forShownList, stack);
					while (stack.Count > 0)
					{
						Holder holder3 = stack.Pop() as Holder;
						if (!holder2.EnumEntrysA.Contains(holder3))
						{
							holder3.ParentTree = holder2;
							holder3.DisplayNameA = AppLocalizationService.GetLang(holder3.DisplayNameA);
							holder2.EnumEntrysA.Add(holder3);
						}
						holder2 = holder3;
					}
					item.ParentTree = holder2;
					holder2.EnumEntrysA.Add(item);
				}
				return holder;
			}
		}
		return null;
	}

	public static void BuildFilterTreePath(this VmIO filter, List<Holder> forShownList, Stack stack)
	{
		VmIO parentObj = filter.Parent as VmIO;
		if (parentObj != null)
		{
			Holder holder = forShownList.Find((Holder p) => p.HolderTarget == parentObj);
			if (holder == null)
			{
				holder = new Holder
				{
					DisplayNameA = parentObj.Name,
					HolderTarget = parentObj
				};
				forShownList.Add(holder);
			}
			stack.Push(holder);
			parentObj.BuildFilterTreePath(forShownList, stack);
		}
	}

	public static object GetInOutputByName(this VmModule vmModule, string name)
	{
		return new DynamicIOContainer<DynamicIO>
		{
			IsOutput = (name == "Output"),
			DataSource = vmModule
		};
	}

	public static DynamicVmIO AddIOByNameType(this VmModule vmModule, string intoutName, string ioName, string ioType)
	{
		DynamicVmIO dynamicVmIO = new DynamicVmIO
		{
			Name = ioName,
			TypeName = ioType,
			Root = vmModule,
			Parent = vmModule
		};
		if (intoutName == "Input")
		{
			vmModule.Inputs.Add(dynamicVmIO);
		}
		else if (intoutName == "Output")
		{
			vmModule.Outputs.Add(dynamicVmIO);
		}
		return dynamicVmIO;
	}

	public static IList<IVmIO> GetParamsDataEditSources(this VmModule module, string ValueType = "", string findByNodeName = "")
	{
		List<IVmIO> list = new List<IVmIO>();
		if (module == null || module.Params == null)
		{
			return list;
		}
		AlgorithmTabRoot paramRoot = module.Params.GetParamRoot();
		if (paramRoot == null)
		{
			return list;
		}
		ObservableCollection<object> tabs = paramRoot.Tabs;
		if (tabs == null)
		{
			return list;
		}
		string[] strParamNames = StrParamNames;
		foreach (string name in strParamNames)
		{
			Tab tab = tabs.OfType<Tab>().FirstOrDefault((Tab p) => p.Name == name);
			if (tab == null || tab.Categorys == null)
			{
				continue;
			}
			IEnumerable<Category> enumerable = tab.Categorys.OfType<Category>();
			if (enumerable == null || enumerable.Count() == 0)
			{
				return list;
			}
			if (!string.IsNullOrWhiteSpace(findByNodeName))
			{
				foreach (Category item in enumerable)
				{
					if (item.Items != null && item.Items.FirstOrDefault((object f) => (f as NameValueItem).NameA.Equals(findByNodeName)) is NameValueItem nameValueItem)
					{
						list.Add(new VmIO
						{
							Name = nameValueItem.Name,
							Root = module,
							TypeName = ValueType,
							CustomName = nameValueItem.DisplayName,
							Tag = nameValueItem
						});
					}
				}
				return list;
			}
			foreach (Category item2 in enumerable)
			{
				if (item2.Items == null)
				{
					continue;
				}
				foreach (object item3 in item2.Items)
				{
					if (!(item3 is NameValueItem nameValueItem2))
					{
						continue;
					}
					string text = AppLocalizationService.GetLang(name) + "_" + AppLocalizationService.GetLang(item2.Name) + "_" + (string.IsNullOrEmpty(nameValueItem2.DisplayName) ? nameValueItem2.Name : AppLocalizationService.GetLang(nameValueItem2.DisplayName));
					if (nameValueItem2 is IntegerBettween || nameValueItem2 is FloatBettween)
					{
						if (nameValueItem2 is IntegerBettween)
						{
							if (!string.IsNullOrWhiteSpace(ValueType) && "int" != ValueType)
							{
								continue;
							}
							IntegerBettween integerBettween = nameValueItem2 as IntegerBettween;
							if (integerBettween.IntegersA == null)
							{
								continue;
							}
							Integer[] array = (from b in integerBettween.IntegersA.OfType<Integer>()
								orderby b.NameA
								select b).ToArray();
							Integer[] array2 = array;
							foreach (Integer integer in array2)
							{
								string text2 = "[Params]." + integer.Name;
								string text3 = "int";
								if (!string.IsNullOrWhiteSpace(text2) && !string.IsNullOrWhiteSpace(text3))
								{
									VmIO vmIO = new VmIO();
									vmIO.Name = text2;
									vmIO.Root = module;
									vmIO.TypeName = text3;
									vmIO.CustomName = text + "_" + (string.IsNullOrEmpty(integer.DisplayName) ? integer.Name : AppLocalizationService.GetLang(integer.DisplayName));
									vmIO.Tag = nameValueItem2;
									list.Add(vmIO);
								}
							}
						}
						else
						{
							if (!(nameValueItem2 is FloatBettween) || (!string.IsNullOrWhiteSpace(ValueType) && "float" != ValueType))
							{
								continue;
							}
							FloatBettween floatBettween = nameValueItem2 as FloatBettween;
							if (floatBettween.FloatsA == null)
							{
								continue;
							}
							Float[] array3 = (from b in floatBettween.FloatsA.OfType<Float>()
								orderby b.NameA
								select b).ToArray();
							Float[] array4 = array3;
							foreach (Float obj in array4)
							{
								string text4 = "[Params]." + obj.Name;
								string text5 = "float";
								if (!string.IsNullOrWhiteSpace(text4) && !string.IsNullOrWhiteSpace(text5))
								{
									VmIO vmIO2 = new VmIO();
									vmIO2.Name = text4;
									vmIO2.Root = module;
									vmIO2.TypeName = text5;
									vmIO2.CustomName = text + "_" + (string.IsNullOrEmpty(obj.DisplayName) ? obj.Name : AppLocalizationService.GetLang(obj.DisplayName));
									vmIO2.Tag = nameValueItem2;
									list.Add(vmIO2);
								}
							}
						}
						continue;
					}
					VmIO vmIO3 = new VmIO();
					if (nameValueItem2 is Integer)
					{
						vmIO3.Name = "[Params]." + nameValueItem2.Name;
						vmIO3.Root = module;
						vmIO3.TypeName = "int";
						vmIO3.CustomName = text;
					}
					else if (nameValueItem2 is IntegerBettween)
					{
						IntegerBettween integerBettween2 = nameValueItem2 as IntegerBettween;
						if (integerBettween2.IntegersA == null)
						{
							continue;
						}
						Integer[] array5 = integerBettween2.IntegersA.OfType<Integer>().ToArray();
						Integer[] array6 = array5;
						foreach (Integer integer2 in array6)
						{
							Integer[] array7 = new Integer[array5.Length];
							Array.Copy(array5, array7, array5.Length);
							array7[array7.Length - 1] = integer2;
							vmIO3.Name = "[Params]." + nameValueItem2.Name;
							vmIO3.Root = module;
							vmIO3.TypeName = "int";
							vmIO3.CustomName = text + "_" + (string.IsNullOrEmpty(integer2.DisplayName) ? integer2.Name : AppLocalizationService.GetLang(integer2.DisplayName));
						}
					}
					else if (nameValueItem2 is Float)
					{
						vmIO3.Name = "[Params]." + nameValueItem2.Name;
						vmIO3.Root = module;
						vmIO3.TypeName = "float";
						vmIO3.CustomName = text;
					}
					else if (nameValueItem2 is FloatBettween)
					{
						FloatBettween floatBettween2 = nameValueItem2 as FloatBettween;
						if (floatBettween2.FloatsA == null)
						{
							continue;
						}
						Float[] array8 = floatBettween2.FloatsA.OfType<Float>().ToArray();
						Float[] array9 = array8;
						foreach (Float obj2 in array9)
						{
							Float[] array10 = new Float[array8.Length];
							Array.Copy(array8, array10, array8.Length);
							array10[array10.Length - 1] = obj2;
							vmIO3.Name = "[Params]." + nameValueItem2.Name;
							vmIO3.Root = module;
							vmIO3.TypeName = "float";
							vmIO3.CustomName = text + "_" + (string.IsNullOrEmpty(obj2.DisplayName) ? obj2.Name : AppLocalizationService.GetLang(obj2.DisplayName));
						}
					}
					else if (nameValueItem2 is Boolean)
					{
						if (nameValueItem2.Name == "Assist0")
						{
							continue;
						}
						vmIO3.Name = "[Params]." + nameValueItem2.Name;
						vmIO3.Root = module;
						vmIO3.TypeName = "bool";
						vmIO3.CustomName = text;
					}
					else if (nameValueItem2 is String)
					{
						if (nameValueItem2.Name == "FontFilterInfo")
						{
							continue;
						}
						vmIO3.Name = "[Params]." + nameValueItem2.Name;
						vmIO3.Root = module;
						vmIO3.TypeName = "string";
						vmIO3.CustomName = text;
					}
					if (string.IsNullOrWhiteSpace(vmIO3.Name) || string.IsNullOrWhiteSpace(vmIO3.TypeName))
					{
						vmIO3 = null;
					}
					else if (string.IsNullOrWhiteSpace(ValueType) || !(vmIO3.TypeName != ValueType))
					{
						vmIO3.Tag = nameValueItem2;
						list.Add(vmIO3);
					}
				}
			}
		}
		return list;
	}

	public static IEnumerable<VmIO> GetDisplaySources(this VmModule module, string ValueType = "")
	{
		ParamRoot paramRoot = VmSolution.Instance[((IVmFullPath)module).FullPath + ".[Display]"] as ParamRoot;
		IEnumerable<Object> source = from p in paramRoot.GetDisplayList().OfType<Object>()
			where p.TypeA == "image" || p.Type == "pixelinfoimage"
			select p;
		List<Object> source2 = source.Where((Object p) => (module.Parent != p.Owner && (p.Owner as IVmModule).GetVmIOByType("Outputs", "IMAGE", "PIXELIMAGE").Contains(p.GetDirectMappingIO())) || (module == p.Owner && module is IVmModuleSet && module.GetVmIOByType("Inputs", "IMAGE", "PIXELIMAGE").Contains(p.GetDirectMappingIO()))).ToList();
		return source2.Select((Object s) => new VmIO
		{
			Name = "[Display]." + s.NameA,
			Root = module,
			TypeName = ObjectTypeEnum.image.ToString(),
			CustomName = AppLocalizationService.GetLang(s.NameA)
		});
	}

	public static IList GetVmIOByType(this IVmModule iVmModule, string intoutName, params string[] ioTypes)
	{
		List<VmIO> list = new List<VmIO>();
		if (iVmModule is VmModule vmModule && vmModule[intoutName] is VmIOCollection vmIOCollection)
		{
			foreach (VmIO item in vmIOCollection)
			{
				if (ioTypes.Contains(item.TypeName))
				{
					list.Add(item);
				}
				else
				{
					if (!(item.GetChildren() is VmIOCollection vmIOCollection2))
					{
						continue;
					}
					foreach (VmIO item2 in vmIOCollection2)
					{
						if (ioTypes.Contains(item2.TypeName))
						{
							list.Add(item2);
						}
					}
				}
			}
		}
		return list;
	}

	public static object GetCombinationValueByType(this IVmIO iVmIO, uint excuteCount)
	{
		if (iVmIO is VmIO { TypeName: var typeName } vmIO && typeName == "IMAGE")
		{
			return GetImageValue(vmIO, excuteCount);
		}
		return null;
	}

	private static object GetImageValue(VmIO io, uint excuteCount)
	{
		byte[] array = null;
		string text = null;
		int num = 0;
		int num2 = 0;
		int num3 = 0;
		if (io.Childs != null)
		{
			object obj = io.Childs.FirstOrDefault((VmIO p) => p.TypeName == "image")?.GetValue(excuteCount, 0);
			if (obj is ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data)
			{
				ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data iMVS_MODULE_CHUNK_VALUE_EX_Data = (ImvsSdkDefine.IMVS_MODULE_CHUNK_VALUE_EX_Data)obj;
				if (iMVS_MODULE_CHUNK_VALUE_EX_Data.IsMemory)
				{
					text = iMVS_MODULE_CHUNK_VALUE_EX_Data.GetRealMemoryAddress();
				}
				else
				{
					array = iMVS_MODULE_CHUNK_VALUE_EX_Data.GetData();
				}
			}
			num = (int)(io.Childs.FirstOrDefault((VmIO p) => p.Name.ToLower().Contains("height"))?.GetValue(excuteCount, 0) ?? ((object)0));
			num2 = (int)(io.Childs.FirstOrDefault((VmIO p) => p.Name.ToLower().Contains("width"))?.GetValue(excuteCount, 0) ?? ((object)0));
			num3 = (int)(io.Childs.FirstOrDefault((VmIO p) => p.Name.ToLower().Contains("format"))?.GetValue(excuteCount, 0) ?? ((object)0));
			if (num == 0 || num2 == 0 || (array == null && text == null))
			{
				return null;
			}
			ImageData imageData = new ImageData();
			imageData.ImageBuffer = array;
			imageData.MemoryAddress = text;
			imageData.Width = num2;
			imageData.Height = num;
			imageData.PixelFormat = ImageData.GetPixelFormatByProtocol((uint)num3);
			imageData.DetailedPixelFormat = (uint)num3;
			return imageData;
		}
		return null;
	}

	public static void FindSubNodeList(this VmIO filter, List<DynamicIO> forShownList, string[] ValueType, string[] exceptValuetype, string SearchFilter = "")
	{
		if (ValueType.Contains(filter.TypeName) && filter.Name.Contains(SearchFilter) && !VmModule.fTimeStamp.Contains(filter.Name) && !exceptValuetype.Contains(filter.TypeName))
		{
			DynamicIO item = new DynamicIO
			{
				IoDataSource = filter,
				DisplayNameA = filter.Name
			};
			forShownList.Add(item);
		}
		if (exceptValuetype.Contains(filter.TypeName))
		{
			return;
		}
		foreach (VmIO child in filter.Childs)
		{
			child.FindSubNodeList(forShownList, ValueType, exceptValuetype, SearchFilter);
		}
	}

	public static IList GetInputTreeNode(this VmModule vmModule, string[] ValueType, string[] exceptValuetype, string SearchFilter = "")
	{
		if (vmModule.Inputs != null && vmModule.Inputs.Count > 0)
		{
			List<DynamicIO> list = new List<DynamicIO>();
			List<Holder> list2 = new List<Holder>();
			foreach (VmIO input in vmModule.Inputs)
			{
				input.FindSubNodeList(list, ValueType, exceptValuetype, SearchFilter);
			}
			foreach (DynamicIO item in list)
			{
				list2.Add(new Holder
				{
					DisplayNameA = item.NameA,
					HolderTarget = item
				});
			}
			return list2;
		}
		return null;
	}

	public static ButtonSelecter GetButtonSelecter(this VmIO vmIO, object owner, bool isInput)
	{
		ButtonSelecter buttonSelecter = null;
		BaseNode baseNode = null;
		if (isInput)
		{
			baseNode = ((!vmIO.IsLeafNode) ? GroupsRoot.GetInstance()["SubscriptionCombinationGroup"] : GroupsRoot.GetInstance()["SubscriptionFilterGroup"]);
		}
		else
		{
			baseNode = ((!vmIO.IsLeafNode) ? GroupsRoot.GetInstance()["SubscriptionCombinationGroupOfOut"] : GroupsRoot.GetInstance()["SubscriptionFilterGroup"]);
		}
		if (baseNode is GroupInfoItem)
		{
			GroupInfoItem groupInfoItem = (baseNode as GroupInfoItem).Clone<GroupInfoItem>();
			if (groupInfoItem != null && groupInfoItem.Items != null)
			{
				using IEnumerator<object> enumerator = groupInfoItem.Items.GetEnumerator();
				if (enumerator.MoveNext())
				{
					BaseNode baseNode2 = (BaseNode)enumerator.Current;
					buttonSelecter = baseNode2 as ButtonSelecter;
				}
			}
		}
		if (buttonSelecter != null)
		{
			buttonSelecter.SetParentNode(owner);
			buttonSelecter.DropOpenSetter.OperationParams = vmIO.TypeName;
			if (vmIO.IsLeafNode)
			{
				buttonSelecter.DropOpenSetter.OperationParams = vmIO.Name;
			}
			buttonSelecter.Triggers.OfType<Trigger>().First().Setters.OfType<Setter>().First().OperationParams = vmIO.Name;
			buttonSelecter.Initers.OfType<Setter>().First().OperationParams = vmIO.Name;
			buttonSelecter.InniterBusiness();
			return buttonSelecter;
		}
		return null;
	}

	public static ButtonSelecter GetButtonSelecterFromGroup(this Object displayObj, object owner)
	{
		ButtonSelecter buttonSelecter = null;
		BaseNode baseNode = GroupsRoot.GetInstance()["ObjectSubscriptionCombinationGroup"];
		if (baseNode is GroupInfoItem)
		{
			GroupInfoItem groupInfoItem = (baseNode as GroupInfoItem).Clone<GroupInfoItem>();
			if (groupInfoItem != null && groupInfoItem.Items != null)
			{
				using IEnumerator<object> enumerator = groupInfoItem.Items.GetEnumerator();
				if (enumerator.MoveNext())
				{
					BaseNode baseNode2 = (BaseNode)enumerator.Current;
					buttonSelecter = baseNode2 as ButtonSelecter;
				}
			}
		}
		string typeA = displayObj.TypeA;
		if (buttonSelecter != null)
		{
			buttonSelecter.SetParentNode(owner);
			buttonSelecter.DropOpenSetter.OperationParams = typeA;
			buttonSelecter.Triggers.OfType<Trigger>().First().Setters.OfType<Setter>().First().OperationParams = displayObj.Name;
			buttonSelecter.Initers.OfType<Setter>().First().OperationParams = displayObj.Name;
			buttonSelecter.InniterBusiness();
			return buttonSelecter;
		}
		return null;
	}

	public static string RealName(this VmIO vmIO)
	{
		try
		{
			if (vmIO == null)
			{
				return "";
			}
			if (string.IsNullOrEmpty(vmIO.Name))
			{
				return null;
			}
			string name = vmIO.Name;
			if (name.First() == '%' && name.Last() == '%' && name.Length > 1)
			{
				return name.Substring(1, name.Length - 2);
			}
			return name;
		}
		catch
		{
			return "";
		}
	}

	public static void BindingGlobalVariable(this IVmIO io, string strGlobalVariableName, object varModule = null)
	{
		LogHelper.objLog.Info("输出参数" + io.FullName + " try Binding GlobalVariable " + strGlobalVariableName);
		ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO_LIST stGlobalVarInfo = ConvertToBindRelationStruct(io, strGlobalVariableName, varModule);
		if (stGlobalVarInfo.nNum != 0)
		{
			try
			{
				ServerSDKManager.serverSDKManager.mGlobalModuleManager.BindGlobalVarParam(stGlobalVarInfo);
			}
			catch (Exception ex)
			{
				if (!(ex is VmException ex2))
				{
					throw ex;
				}
				if (ex2.errorCode != -536870089)
				{
					throw ex;
				}
			}
			io.Tag = new VarItem
			{
				Owner = varModule,
				Name = strGlobalVariableName
			};
			(io as ModelBase).OnPropertyChanged("Tag");
			return;
		}
		throw new Exception(AppLocalizationService.GetLang("Lang_GlobalVariableGetModuleListERR"));
	}

	private static ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO? ConvertToVarBindRelationStruct(IVmIO subIO, IVmIO pubIo)
	{
		if (subIO == null || pubIo == null)
		{
			return null;
		}
		ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO value = new ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO
		{
			nModuleId = pubIo.Owner.ID,
			strParamName = StringUtil.UTF8GetFixLenBytes(pubIo.Name, 128),
			stBindValue = new ImvsSdkDefine.IMVS_SUBSCRIBE_MODU_PARAM_INFO
			{
				eVarType = ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_VAR_TYPE.SUBSCRIBE_VAR_VARIABLE,
				stVariableValue = new ImvsSdkDefine.IMVS_VARIABLE_VALUE_INFO
				{
					eIndexType = ImvsSdkDefine.IMVS_ENUM_SUBSCRIBE_INDEX_TYPE.SUBSCRIBE_INDEX_ALL,
					strIndexConstant = StringUtil.UTF8GetFixLenBytes("All", 256),
					stModuleParam = new ImvsSdkDefine.IMVS_SUBSCRIBE_MODULE_PARAM_INFO
					{
						nModuleId = (int)subIO.Owner.ID,
						byParamName = StringUtil.UTF8GetFixLenBytes(subIO.Name, 128)
					}
				}
			}
		};
		switch (subIO.TypeName)
		{
		case "int":
			value.eGlobalVarType = ImvsSdkDefine.IMVS_ENUM_MODULE_RESULT_TYPE.IMVS_MODURES_TYPE_INT;
			break;
		case "float":
			value.eGlobalVarType = ImvsSdkDefine.IMVS_ENUM_MODULE_RESULT_TYPE.IMVS_MODURES_TYPE_FLOAT;
			break;
		case "string":
			value.eGlobalVarType = ImvsSdkDefine.IMVS_ENUM_MODULE_RESULT_TYPE.IMVS_MODURES_TYPE_STRING;
			break;
		case "double":
			value.eGlobalVarType = ImvsSdkDefine.IMVS_ENUM_MODULE_RESULT_TYPE.IMVS_MODURES_TYPE_DOUBLE;
			break;
		case "pointset":
			value.eGlobalVarType = ImvsSdkDefine.IMVS_ENUM_MODULE_RESULT_TYPE.IMVS_MODURES_TYPE_CHUNK;
			break;
		case "image":
			value.eGlobalVarType = ImvsSdkDefine.IMVS_ENUM_MODULE_RESULT_TYPE.IMVS_MODURES_TYPE_IMAGE;
			break;
		case "byte":
			value.eGlobalVarType = ImvsSdkDefine.IMVS_ENUM_MODULE_RESULT_TYPE.IMVS_MODURES_TYPE_BYTEARRAY;
			break;
		}
		return value;
	}

	private static void ConvertToVarBindRelationStructList(IVmIO subIO, IVmIO pubIo, ref List<ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO> bindinfoList)
	{
		if ((subIO as VmIO).IsLeafNode && (subIO as VmIO).IsLeafNode)
		{
			ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO? iMVS_BIND_GLOBALVAR_PARAM_INFO = ConvertToVarBindRelationStruct(subIO, pubIo);
			if (iMVS_BIND_GLOBALVAR_PARAM_INFO.HasValue)
			{
				bindinfoList.Add(iMVS_BIND_GLOBALVAR_PARAM_INFO.Value);
			}
		}
		else if ((subIO as VmIO).Childs.Count == (pubIo as VmIO).Childs.Count)
		{
			for (int i = 0; i < (subIO as VmIO).Childs.Count; i++)
			{
				ConvertToVarBindRelationStructList((subIO as VmIO).Childs[i], (pubIo as VmIO).Childs[i], ref bindinfoList);
			}
		}
	}

	private static ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO_LIST ConvertToBindRelationStruct(IVmIO subio, string GlobalVarName, object varModule)
	{
		ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO_LIST result = default;
		if (varModule is VmModule vmModule && subio.Owner != null)
		{
			int iD = (int)vmModule.ID;
			VmIO vmIO = vmModule.Outputs.FirstOrDefault((VmIO x) => x.Name == GlobalVarName);
			if (vmIO == null)
			{
				LogHelper.Error("ConvertToBindRelationStruct is error:io name is not exit:" + GlobalVarName);
				return result;
			}
			List<ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO> bindinfoList = new List<ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO>();
			ConvertToVarBindRelationStructList(subio, vmIO, ref bindinfoList);
			result.astBindGlobalVarList = new ImvsSdkDefine.IMVS_BIND_GLOBALVAR_PARAM_INFO[128];
			for (int num = 0; num < bindinfoList.Count; num++)
			{
				result.astBindGlobalVarList[num] = bindinfoList[num];
			}
			result.nNum = (uint)bindinfoList.Count;
		}
		else
		{
			result.nNum = 0u;
		}
		return result;
	}

	public static DynamicVmIO AddIO(this VmModule module, object obj, object parent, IList parentList)
	{
		if (obj is Combination combination)
		{
			DynamicVmIO dynamicVmIO = new DynamicVmIO
			{
				Root = module,
				Parent = parent,
				Name = combination.Name,
				TypeName = combination.Style
			};
			parentList.Add(dynamicVmIO);
			foreach (object filter2 in combination.Filters)
			{
				module.AddIO(filter2, dynamicVmIO, dynamicVmIO.Childs);
			}
			return dynamicVmIO;
		}
		if (obj is Filter filter)
		{
			AddLeafIO(module, filter.ValueTypeA, parent, parentList, (obj as Filter).Name);
		}
		return null;
	}

	public static DynamicVmIO AddLeafIO(VmModule module, string typeName, object parent, IList parentList, string ioName)
	{
		DynamicVmIO dynamicVmIO = new DynamicVmIO
		{
			Root = module,
			Parent = parent,
			Name = ioName,
			TypeName = typeName
		};
		parentList.Add(dynamicVmIO);
		return dynamicVmIO;
	}

	public static VmModule GetFrontModuleByFullName(this VmModule owner, string fullName)
	{
		List<VmModule> frontParamItems = owner.GetFrontParamItems();
		return frontParamItems.FirstOrDefault((VmModule p) => p.FullName == fullName);
	}

	public static VmModule GetModuleByPath(this VmModuleSet set, string str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return null;
		}
		int num = str.IndexOf(' ', 0);
		if (num < 0)
		{
			return null;
		}
		string s = str.Substring(0, num);
		int id = -1;
		if (!int.TryParse(s, out id))
		{
			return null;
		}
		List<VmModule> list = new List<VmModule>();
		set.GetAllModule(list);
		return list.FirstOrDefault((VmModule p) => p.ID == id);
	}

	public static VmIO GetIoByPath(this VmModuleSet set, string str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return null;
		}
		if (!str.Contains('.'))
		{
			return null;
		}
		VmModule moduleByPath = set.GetModuleByPath(str);
		if (moduleByPath == null)
		{
			return null;
		}
		string[] source = str.Split('.');
		string sub = source.Last();
		return moduleByPath[sub] as VmIO;
	}

	private static VmModule GetVarIo(this VmModule node, int id)
	{
		if (id == 13000)
		{
			return GetGlobalVarModule();
		}
		if (node.Parent is VmModuleSet vmModuleSet)
		{
			if (!(((IVmModuleSet)vmModuleSet).LocalVariablModule is VmModule vmModule))
			{
				return null;
			}
			return (vmModule.ID == id) ? vmModule : null;
		}
		return null;
	}

	public static VmModule GetAroundModuleById(this VmModule node, int id)
	{
		if (node.ID == id)
		{
			return node;
		}
		VmModule varIo = node.GetVarIo(id);
		if (varIo != null)
		{
			return varIo;
		}
		if (node.Parent is VmModuleSet vmModuleSet)
		{
			VmModule vmModule = vmModuleSet.Modules.FirstOrDefault((VmModule p) => p.ID == id);
			if (vmModule != null)
			{
				return vmModule;
			}
			if (vmModuleSet.ID == id)
			{
				return vmModuleSet;
			}
		}
		if (node is VmModuleSet vmModuleSet2)
		{
			VmModule vmModule2 = vmModuleSet2.Modules.FirstOrDefault((VmModule p) => p.ID == id);
			if (vmModule2 != null)
			{
				return vmModule2;
			}
		}
		return null;
	}

	public static VmModule GetModuleByPathFromAround(this VmModule node, string str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return null;
		}
		int num = str.IndexOf(' ', 0);
		if (num < 0)
		{
			return null;
		}
		string s = str.Substring(0, num);
		int result = -1;
		if (!int.TryParse(s, out result))
		{
			return null;
		}
		return node.GetAroundModuleById(result);
	}

	public static VmIO GetIoByPathFromAround(this VmModule node, string str)
	{
		if (string.IsNullOrEmpty(str))
		{
			return null;
		}
		if (!str.Contains('.'))
		{
			return null;
		}
		VmModule moduleByPathFromAround = node.GetModuleByPathFromAround(str);
		if (moduleByPathFromAround == null)
		{
			return null;
		}
		string[] source = str.Split('.');
		string paramName = source.Last();
		VmIO vmIO = (from p in moduleByPathFromAround.Outputs.GetAllIO()
			where (p.Name == paramName || AppLocalizationService.GetLang(p.Name) == paramName) && p.Visible
			select p).FirstOrDefault();
		if (vmIO == null)
		{
			vmIO = (from p in moduleByPathFromAround.Inputs.GetAllIO()
				where (p.Name == paramName || AppLocalizationService.GetLang(p.Name) == paramName) && p.Visible
				select p).FirstOrDefault();
		}
		return vmIO;
	}

	public static string GetIdName(this VmModule m)
	{
		return m.ID + " " + m.Name;
	}

	public static List<string> GetDynamicIOParamsByTypeEx(this VmModule module, string ValueType = "", string findByNodeName = "")
	{
		List<string> list = new List<string>();
		if (module == null || module.Params == null)
		{
			return list;
		}
		try
		{
			byte[] bt = ((IUserBytesData)module.Params)[DynamicParamsKey];
			string text = StringUtil.UTF8GetString(bt);
			if (!string.IsNullOrWhiteSpace(text))
			{
				string[] array = text.Trim(';').Split(';');
				string[] array2 = array;
				foreach (string text2 in array2)
				{
					string[] array3 = text2.Split(',');
					if (array3.Count() >= 2 && (string.IsNullOrWhiteSpace(ValueType) || !(array3[1] != ValueType)))
					{
						list.Add(array3[0]);
					}
				}
			}
			return list;
		}
		catch (Exception message)
		{
			LogHelper.Error(message);
			return list;
		}
	}

	public static IVmModule GetRelatedImageSourceModule(this IVmModule module)
	{
		List<VmIO> obj = module?.GetVmIOByType("Inputs", "IMAGE") as List<VmIO>;
		VmModule vmModule = ((obj != null) ? obj.FirstOrDefault() : null)?.RelateIO?.Root;
		bool flag = ((vmModule is ISubjectModel subjectModel) ? (subjectModel.DepositModule is IImageSourceNode) : (vmModule is IImageSourceNode));
		while (!flag)
		{
			if (vmModule == null || vmModule is IVmModuleSet)
			{
				return null;
			}
			List<VmIO> obj2 = vmModule.GetVmIOByType("Inputs", "IMAGE") as List<VmIO>;
			vmModule = ((obj2 != null) ? obj2.FirstOrDefault() : null)?.RelateIO?.Root;
			flag = ((vmModule is ISubjectModel subjectModel2) ? (subjectModel2.DepositModule is IImageSourceNode) : (vmModule is IImageSourceNode));
		}
		return vmModule;
	}

	public static bool RunCheck(this VmProcedure procedure, ref string workStateInfo)
	{
		if (procedure.IsReady != true)
		{
			if (!procedure.IsReady.HasValue)
			{
				workStateInfo = AppLocalizationService.GetLang("等待添加组件");
			}
			if (procedure.IsReady == false)
			{
				workStateInfo = ((IVmModuleSet)procedure).UnReadyModuleID + " " + ((IVmModuleSet)procedure).UnReadyModuleName + " " + AppLocalizationService.GetLang("Param not Complete");
			}
			return false;
		}
		return true;
	}

	public static ExternModule GetCustomModule(this VmSolution solution)
	{
		if (!(((IVmSolution)solution).CustomModule is ExternModule))
		{
			((IVmSolution)solution).CustomModule = new ExternModule();
		}
		return ((IVmSolution)solution).CustomModule as ExternModule;
	}

	public static void UpdateROI(this IVmModule vmModule)
	{
		AlgorithmTabRoot paramRoot = (vmModule as VmModule).Params.GetParamRoot();
		paramRoot.UpdateROI();
	}

	public static VmModule GetGlobalVarModule()
	{
		return VmSolution.Instance.Modules.FirstOrDefault((VmModule x) => x.ID == 13000);
	}

	public static IVmModule GetLocalVarModule(this IVmModule vmModule, bool isInput = false)
	{
		if (vmModule == null)
		{
			return null;
		}
		if (isInput && vmModule is IVMGroup)
		{
			return (vmModule.Parent as IVmModuleSet)?.LocalVariablModule;
		}
		return (vmModule is IVmModuleSet) ? (vmModule as IVmModuleSet).LocalVariablModule : (vmModule.Parent as IVmModuleSet)?.LocalVariablModule;
	}

	public static void SetLinkWithVarNull(this VmModule node, string bindValue, int paramModueId, string paramName, string paramType)
	{
		if (string.IsNullOrEmpty(bindValue))
		{
			return;
		}
		string text = "";
		int num = bindValue.IndexOf('[', 0);
		if (num > 0)
		{
			text = bindValue.Substring(num);
			text = text.Replace("[", "").Replace("]", "");
			bindValue = bindValue.Remove(bindValue.IndexOf('[', 0));
		}
		VmIO ioByPathFromAround = node.GetIoByPathFromAround(bindValue);
		if (ioByPathFromAround != null)
		{
			IVmModule root = ioByPathFromAround.Root;
			if (root.ModuleType == 5 || root.ModuleType == 25)
			{
				VariableManager variableManager = new VariableManager(ioByPathFromAround.Root);
				variableManager.ModifyLinkModuleByParamBind(ioByPathFromAround.Name, new VarRunParamInfo
				{
					Name = paramName,
					Type = paramType,
					nModuleID = paramModueId
				}, isBind: false);
			}
		}
	}

	public static void SetLinkWithVar(this VmModule node, string paramName, string paramType, VmIO vario)
	{
		if (!string.IsNullOrEmpty(paramName) && !string.IsNullOrEmpty(paramType))
		{
			IVmModule root = vario.Root;
			if (root.ModuleType == 5 || root.ModuleType == 25)
			{
				VariableManager variableManager = new VariableManager(vario.Root);
				variableManager.ModifyLinkModuleByParamBind(vario.Name, new VarRunParamInfo
				{
					Name = paramName,
					Type = paramType,
					nModuleID = (int)node.ID
				}, isBind: true);
			}
		}
	}
}
