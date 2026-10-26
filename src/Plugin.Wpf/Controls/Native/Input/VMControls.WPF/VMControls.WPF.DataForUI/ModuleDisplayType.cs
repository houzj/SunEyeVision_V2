namespace SunEyeVision.Plugin.Wpf.Native.Input.VMControls.WPF.DataForUI;

public enum ModuleDisplayType
{
	DEFAULT,
	/// <summary>
	/// 不显示的全局模块
	/// </summary>
	NONE,
	/// <summary>
	/// 普通模块
	/// </summary>
	NORMAL,
	NORMALADORNER,
	/// <summary>
	/// 图像源模块
	/// </summary>
	IMAGE,
	IMAGEADORNER,
	/// <summary>
	/// 流程模块
	/// </summary>
	PROCESS,
	PROCESSADORNER,
	/// <summary>
	/// 组模块
	/// </summary>
	GROUP,
	GROUPADORNER,
	/// <summary>
	/// 分支模块
	/// </summary>
	IFBRANCH,
	IFBRANCHADORNER,
	/// <summary>
	/// 运算模块
	/// </summary>
	FUNCTION,
	FUNCTIONADORNER,
	/// <summary>
	/// 脚本模块
	/// </summary>
	SHELL,
	SHELLADORNER,
	/// <summary>
	/// 发送数据
	/// </summary>
	COMMUNCATION,
	COMMUNCATIONADORNER,
	/// <summary>
	/// 耗时模块
	/// </summary>
	TIMER,
	TIMERADORNER
}
