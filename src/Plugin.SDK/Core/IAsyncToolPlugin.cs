using System.Threading.Tasks;
using SunEyeVision.Plugin.SDK.Execution.Results;

namespace SunEyeVision.Plugin.SDK.Core;

/// <summary>
/// 支持真正异步执行的工具契约。迁移期与 <see cref="IToolPlugin"/> 同时实现；
/// 执行器会优先调用此接口，旧工具继续使用同步 Run。
/// </summary>
public interface IAsyncToolPlugin : IToolPlugin
{
    ValueTask<ToolResults> ExecuteAsync(ToolExecutionRequest request);
}
