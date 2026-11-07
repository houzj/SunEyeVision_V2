using System.Globalization;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Plugin.Wpf.Adapters.References;

/// <summary>
/// 引用文本投影。对齐原件 <c>VmIO.ModulePathName</c>：<c>{模块编号} {模块名}.{IO名}</c>，
/// 索引下标由字段自己追加 <c>[]</c>/<c>[n]</c>。全仓只有这一处生成引用文本，禁止各处再拼字符串。
/// </summary>
public static class ReferenceText
{
    /// <summary>
    /// 记录型输出把资源本体放在固定成员上（图像、二值图等记录均如此）。
    /// 原件把这份资源单独建模为一个 IO，因此投影到该成员时折叠为端口名。
    /// </summary>
    public const string ResourceMemberId = "data";

    /// <summary>模块编号按原件从 0 开始：节点全局序号从 1 起，投影时左移一位。</summary>
    public static int ModuleNumber(int globalIndex) => globalIndex - 1;

    /// <summary>IO 名取端口名或最深成员名；成员路径只指向资源本体时折叠为端口名。</summary>
    public static string IoName(string portCaption, IReadOnlyList<string> memberIds, IReadOnlyList<string> memberNames)
    {
        ArgumentNullException.ThrowIfNull(portCaption);
        ArgumentNullException.ThrowIfNull(memberIds);
        ArgumentNullException.ThrowIfNull(memberNames);
        if (memberIds.Count == 0) return portCaption;
        if (memberIds[^1] == ResourceMemberId) return portCaption;
        if (memberNames.Count != 0) return memberNames[^1];
        return memberIds[^1];
    }

    /// <summary>完整引用文本：<c>编号 模块名.IO名</c>。</summary>
    public static string Format(int moduleNumber, string moduleName, string ioName)
    {
        ArgumentNullException.ThrowIfNull(moduleName);
        ArgumentNullException.ThrowIfNull(ioName);
        return moduleNumber.ToString(CultureInfo.InvariantCulture) + " " + moduleName + "." + ioName;
    }

    /// <summary>带成员路径的引用文本，成员按原件规则取叶子名。</summary>
    public static string Format(int moduleNumber, string moduleName, string portCaption,
        IReadOnlyList<string> memberIds, IReadOnlyList<string> memberNames)
        => Format(moduleNumber, moduleName, IoName(portCaption, memberIds, memberNames));

    /// <summary>
    /// 索引标记。原件选中任意模块 IO 都写成 <c>模块.IO[]</c>（订阅全部索引），图像引用与全局变量不带索引标记；
    /// 指定索引时写成 <c>[n]</c>/<c>[索引来源]</c>。见原件 ButtonSelecter.ClickCommandExecute。
    /// </summary>
    public static string IndexSuffix(TypeRef? projected, BindingSelection selection, bool fromModuleIo,
        Func<OutputReference, string> formatIndex, Func<VariableReference, string> formatVariable)
    {
        ArgumentNullException.ThrowIfNull(formatIndex);
        ArgumentNullException.ThrowIfNull(formatVariable);
        if (!fromModuleIo) return string.Empty;
        return selection switch
        {
            BindingSelection.AtConstant constant => "[" + constant.Index.ToString(CultureInfo.InvariantCulture) + "]",
            BindingSelection.AtField field => "[" + formatIndex(field.Reference) + "]",
            BindingSelection.AtVariable variable => "[" + formatVariable(variable.Reference) + "]",
            _ => projected?.Shape == ValueShape.Resource ? string.Empty : "[]"
        };
    }
}
