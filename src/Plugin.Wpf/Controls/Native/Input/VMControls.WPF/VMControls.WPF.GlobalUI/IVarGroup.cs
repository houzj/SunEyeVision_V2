namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.GlobalUI;

/// <summary>
/// 变量分组
/// </summary>
public interface IVarGroup
{
	/// <summary>
	/// 重命名
	/// </summary>
	/// <returns></returns>
	bool ReName(string oldName, string newName);
}
