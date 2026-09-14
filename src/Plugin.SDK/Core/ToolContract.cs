using System;

namespace SunEyeVision.Plugin.SDK.Core
{
    /// <summary>
    /// 工具插件契约版本 —— 宿主与插件之间的兼容线（多人协作的"证件"）。
    /// </summary>
    /// <remarks>
    /// <b>为什么需要</b>：工具支持在**没有主程序源码**的机器上独立开发（只给 SDK 包 + 主程序目录），
    /// 宿主与工具的版本必然会错位。没有显式兼容线时，错位表现为"插件加载了但行为诡异/静默不生效"，
    /// 属最难排查的一类问题。本类把兼容线显式化，并要求宿主在**不兼容时明确拒绝**该插件（rule-008 报错优先）。
    ///
    /// <b>兼容规则</b>：主版本（<see cref="Major"/>）相同即兼容；次版本（<see cref="Minor"/>）递增表示
    /// 向后兼容的新增能力 —— 旧插件不需要重新编译。
    ///
    /// <b>判定依据</b>：插件编译时引用的 SDK 程序集版本由**程序集元数据**给出
    /// （见 <see cref="GetPluginContractVersion"/>），不要求插件自行声明：声明可能写错或过期，元数据不会说谎。
    /// </remarks>
    public static class ToolContract
    {
        /// <summary>契约主版本：变化表示**不兼容** —— 宿主必须拒绝旧插件并说明原因。</summary>
        public const int Major = 1;

        /// <summary>契约次版本：变化表示**向后兼容的功能新增** —— 旧插件仍可直接使用。</summary>
        public const int Minor = 0;

        /// <summary>契约版本字符串（形如 <c>"1.0"</c>）。</summary>
        public static string Version => $"{Major}.{Minor}";

        /// <summary>契约程序集名（插件编译时引用的版本即此程序集的版本）。</summary>
        public const string AssemblyName = "SunEyeVision.Plugin.SDK";

        /// <summary>
        /// 判断插件引用的 SDK 版本是否与本契约兼容（主版本相同）。
        /// </summary>
        /// <param name="pluginSdkVersion">插件引用的 SDK 程序集版本；<c>null</c> = 该插件未引用 SDK（视为不兼容）。</param>
        public static bool IsCompatible(Version? pluginSdkVersion)
            => pluginSdkVersion != null && pluginSdkVersion.Major == Major;

        /// <summary>
        /// 读取插件程序集引用的 SDK 版本（即该插件编译时的契约版本）。
        /// </summary>
        /// <param name="pluginAssembly">插件程序集（宿主加载后传入）</param>
        /// <returns>引用的 SDK 版本；该程序集未引用 SDK 时返回 <c>null</c>。</returns>
        public static Version? GetPluginContractVersion(System.Reflection.Assembly pluginAssembly)
        {
            if (pluginAssembly == null) throw new ArgumentNullException(nameof(pluginAssembly));

            foreach (var reference in pluginAssembly.GetReferencedAssemblies())
            {
                if (string.Equals(reference.Name, AssemblyName, StringComparison.Ordinal))
                    return reference.Version;
            }

            return null;
        }

        /// <summary>
        /// 生成**可读**的兼容性说明（宿主拒绝插件时用它报错，避免出现"插件没生效却不知为何"）。
        /// </summary>
        /// <param name="pluginSdkVersion">插件引用的 SDK 版本（可为 <c>null</c>）</param>
        public static string Describe(Version? pluginSdkVersion)
        {
            var pluginText = pluginSdkVersion?.ToString() ?? "未引用 SDK";

            return IsCompatible(pluginSdkVersion)
                ? $"契约兼容：插件 {pluginText} / 宿主 {Version}"
                : $"契约不兼容：插件依赖 {pluginText}，宿主提供 {Version}（要求 SDK 主版本 {Major}）。"
                  + "请用与宿主契约主版本一致的开发包重新编译该工具。";
        }
    }
}
