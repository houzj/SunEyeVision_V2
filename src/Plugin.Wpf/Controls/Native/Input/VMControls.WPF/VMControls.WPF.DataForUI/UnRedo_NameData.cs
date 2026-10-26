namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.DataForUI;

public class UnRedo_NameData
{
	/// <summary>
	/// 模块对象名称
	/// </summary>
	public string ModuleFullName;

	/// <summary>
	/// 旧名称
	/// </summary>
	public string OldName;

	/// <summary>
	/// 新名称
	/// </summary>
	public string NewName;

	public UnRedo_NameData(string fullName, string oldName, string newName)
	{
		ModuleFullName = fullName;
		OldName = oldName;
		NewName = newName;
	}
}
