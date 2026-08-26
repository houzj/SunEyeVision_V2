using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

namespace SunEyeVision.Plugin.SDK.Core
{
    /// <summary>
    /// 可预编译工具接口 — 支持在"代码就绪"时提前编译并预热缓存，
    /// 保证后续执行命中缓存零编译（首次与后续执行时间一致）。
    ///
    /// 供 UI/执行层通过 <see cref="ToolRegistry"/> 拿到工具实例后，按
    /// <c>tool is IPrecompilableTool</c> 判断并调用，不依赖具体工具类型
    /// （工具是运行时插件，UI 层不直接引用工具程序集）。
    ///
    /// 非所有工具需要实现；仅"编译型"工具（如脚本工具）实现此接口。
    /// </summary>
    public interface IPrecompilableTool
    {
        /// <summary>
        /// 预编译并预热执行环境：编译结果写入工具自身进程级缓存（命中则跳过编译），
        /// 编译成功后由工具按需预热运行所需资源（如沙箱常驻池、程序集加载、JIT）。
        /// 不执行工具逻辑、不产生副作用。保证后续执行命中缓存零编译 + 零冷启动。
        /// </summary>
        /// <param name="parameters">工具参数（节点参数运行时类型由各工具自行转换）</param>
        /// <returns>编译诊断列表；空 = 编译成功（仅用于日志，不阻塞）</returns>
        IReadOnlyList<string> PreCompile(ToolParameters parameters);
    }
}
