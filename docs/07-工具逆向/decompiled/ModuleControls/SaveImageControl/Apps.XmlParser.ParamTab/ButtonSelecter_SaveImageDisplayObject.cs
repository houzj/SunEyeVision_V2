using Apps.Localization;
using SaveImageCs;
using VM.Controls;
using VM.Core;

namespace Apps.XmlParser.ParamTab;

public class ButtonSelecter_SaveImageDisplayObject : ButtonSelecter_DisplayObject
{
	public override void OnPropertyChanged(string proName)
	{
		base.OnPropertyChanged(proName);
		if (proName != "CurValueA")
		{
			return;
		}
		VmModule module = GetModule();
		if (module == null)
		{
			return;
		}
		VmModule moduleByPath = module.Root.GetModuleByPath(OldCurValue);
		if (moduleByPath != null)
		{
			SaveImageHelper.Remove(module, moduleByPath);
		}
		AddList_SaveImage addList_SaveImage = GetParentNode<AddList_SaveImage>();
		if (addList_SaveImage != null)
		{
			foreach (int moduleId in addList_SaveImage.ModuleIds)
			{
				SaveImageHelper.Remove(module, (uint)moduleId);
			}
			addList_SaveImage.ModuleIds.Clear();
		}
		VmModule moduleByPath2 = module.Root.GetModuleByPath(CurValueA);
		if (moduleByPath2 != null && !SaveImageHelper.Add(module, moduleByPath2))
		{
			CurValueA = "";
			HikMessageBox.Show(AppLocalizationService.GetLang("该模块未配置显示项"), MessageIcons.Warning);
		}
	}

	private VmModule GetModule()
	{
		if (GetTopBaseNode() is AlgorithmTabRoot { Owner: VmModule owner })
		{
			return owner;
		}
		return null;
	}
}
