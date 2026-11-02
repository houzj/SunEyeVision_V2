using System;
using System.Collections.Generic;
using System.Linq;

namespace SunEyeVision.UI.Shell.Controls.Shell;

/// <summary>One declaration drives both the menu hierarchy and its toolbar projection.</summary>
internal sealed record ShellMenuEntry(string Id, string Label, string? Command = null, string? Gesture = null,
    string? DisabledReason = null, string? Action = null, string? ToolGroup = null, int Priority = 0,
    string? ToolTip = null, string? Icon = null, string? DisabledIcon = null, string? ToolName = null,
    string? Special = null, bool ShowInMenu = true, bool SeparatorBefore = false,
    IReadOnlyList<ShellMenuEntry>? Children = null);

internal static class ShellMenuCatalog
{
    private const string Native = "/SunEyeVision.Plugin.Wpf;component/Themes/Assets/Native/Input/VMControls.WPF/resources/";
    private const string Workflow = "/SunEyeVision.UI;component/Resources/Workflow/";
    internal static IReadOnlyList<ShellMenuEntry> Create() => new[]
    {
        new ShellMenuEntry("file","文件(_F)",Children:new[] {
            new ShellMenuEntry("new","新建方案(_N)","NewSolutionCommand","Ctrl+N"),
            new ShellMenuEntry("open","打开方案(_O)","OpenSolutionCommand","Ctrl+O",ToolGroup:"group0",Priority:1,ToolTip:"打开方案",Icon:Native+"toolbaropen_normal.png",DisabledIcon:Native+"toolbaropen_disable.png"),
            new ShellMenuEntry("recent","打开最近方案",DisabledReason:"最近方案菜单尚未接入；可通过帮助中的欢迎页打开最近方案"),
            new ShellMenuEntry("sample","打开示例方案",DisabledReason:"示例方案入口尚未接入"),
            new ShellMenuEntry("save","保存方案(_S)","SaveCurrentSolutionCommand","Ctrl+S",ToolGroup:"group0",Priority:0,ToolTip:"保存 (Ctrl+S)",Icon:Native+"toolbarsave_normal.png",DisabledIcon:Native+"toolbarsave_disable.png",SeparatorBefore:true),
            new ShellMenuEntry("save-as","另存方案",DisabledReason:"方案另存入口尚未接入"),
            new ShellMenuEntry("export","导出算子代码",DisabledReason:"导出功能尚未接入",SeparatorBefore:true),
            new ShellMenuEntry("import","导入流程",DisabledReason:"流程导入尚未接入"),
            new ShellMenuEntry("exit","退出(_X)",Gesture:"Alt+F4",Action:"exit",SeparatorBefore:true) }),
        new ShellMenuEntry("settings","设置",Children:new[] {
            Disabled("permissions","权限设置"),Disabled("software","软件设置"),Disabled("solution","方案设置"),Disabled("run-policy","运行策略"),Disabled("capture-policy","采集策略"),
            new ShellMenuEntry("path-points","显示路径拐点(_P)","TogglePathPointsCommand","Ctrl+P",SeparatorBefore:true),
            new ShellMenuEntry("fullscreen","全屏(_F)",Gesture:"F11",Action:"fullscreen") }),
        new ShellMenuEntry("tools","工具(_T)",Children:new[]{Disabled("calibrate","一键标定引导")}),
        new ShellMenuEntry("system","系统",Children:new[] {
            Disabled("log","日志"),
            new ShellMenuEntry("communication","通信设置","ShowCommunicationManagerCommand",ToolGroup:"group2",Priority:2,ToolTip:"通讯管理",Icon:Native+"ic_通信管理_normal.png",DisabledIcon:Native+"ic_通信管理_disable.png"),
            Disabled("light","光源设置"),
            new ShellMenuEntry("camera","相机设置","ShowCameraManagerCommand",ToolGroup:"group1",Priority:0,ToolTip:"相机管理",Icon:Native+"ic_topbar_camera_normal.png",DisabledIcon:Native+"ic_topbar_camera_disable.png") }),
        new ShellMenuEntry("help","帮助(_H)",Children:new[] {
            Disabled("language","语言"),new ShellMenuEntry("manual","用户手册(_M)","ShowHelpCommand","F1"),Disabled("online","在线帮助"),
            new ShellMenuEntry("about","版本信息","ShowAboutCommand"),Disabled("more","更多信息"),Disabled("community","社区"),
            new ShellMenuEntry("welcome","欢迎页","ShowStartPageCommand",SeparatorBefore:true) }),
        new ShellMenuEntry("toolbar-only","",ShowInMenu:false,Children:new[] {
            new ShellMenuEntry("undo","撤销","UndoCommand",ToolGroup:"group0",Priority:2,ToolTip:"撤销 (Ctrl+Z)",Icon:Workflow+"toolbar_ic_topbar_undo_normal.png",DisabledIcon:Workflow+"toolbar_ic_topbar_undo_disable.png",ToolName:"UndoEditButton",Special:"undo"),
            new ShellMenuEntry("redo","重做","RedoCommand",ToolGroup:"group0",Priority:3,ToolTip:"重做 (Ctrl+Y)",Icon:Workflow+"toolbar_ic_topbar_redo_normal.png",DisabledIcon:Workflow+"toolbar_ic_topbar_redo_disable.png",ToolName:"RedoEditButton"),
            new ShellMenuEntry("lock","锁定",DisabledReason:"锁定：功能尚未接入",ToolGroup:"group0",Priority:4),
            new ShellMenuEntry("variable","全局变量管理","ShowGlobalVariableManagerCommand",ToolGroup:"group2",Priority:1,ToolTip:"全局变量管理",Icon:Native+"ic_全局变量_normal.png",DisabledIcon:Native+"ic_全局变量_disable.png"),
            new ShellMenuEntry("trigger","触发",DisabledReason:"触发：功能尚未接入",ToolGroup:"group2",Priority:3),
            new ShellMenuEntry("script","脚本",DisabledReason:"脚本：功能尚未接入",ToolGroup:"group2",Priority:4),
            new ShellMenuEntry("run","运行当前工作流","RunWorkflowCommand",ToolGroup:"group3",Priority:0,ToolTip:"运行当前工作流 (F5)",Icon:Native+"toolbarstart_normal.png",DisabledIcon:Native+"toolbarstart_disable.png"),
            new ShellMenuEntry("continuous","连续运行",DisabledReason:"全流程连续运行尚未接入：缺少可区分连续与单次运行的正式状态",ToolGroup:"group3",Priority:1,Icon:Native+"toolbarrunrepeat_disable.png",ToolName:"ContinuousRunToggle"),
            new ShellMenuEntry("config","综合配置",DisabledReason:"综合配置：功能尚未接入",ToolGroup:"group3",Priority:2),
            new ShellMenuEntry("frontend","运行界面",DisabledReason:"运行界面：功能尚未接入",ToolGroup:"group3",Priority:3),
            new ShellMenuEntry("theme","主题","ToggleThemeCommand",ToolGroup:"group4",Priority:0,ToolName:"ThemeToggleButton",Special:"theme") })
    };

    internal static IEnumerable<ShellMenuEntry> Flatten(IEnumerable<ShellMenuEntry> roots)
    { foreach (var node in roots) { yield return node; if (node.Children is not null) foreach (var child in Flatten(node.Children)) yield return child; } }

    internal static IEnumerable<ShellMenuEntry> Toolbar(IEnumerable<ShellMenuEntry> roots) => Flatten(roots)
        .Where(item => item.ToolGroup is not null).OrderBy(item => item.ToolGroup, StringComparer.Ordinal).ThenBy(item => item.Priority);

    private static ShellMenuEntry Disabled(string id,string label) => new(id,label,DisabledReason:label+"尚未接入");
}
