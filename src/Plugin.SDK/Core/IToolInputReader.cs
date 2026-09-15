using System.Collections.Generic;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Plugin.SDK.Core;

/// <summary>
/// 工具运行期输入的只读视图。新工具通过此接口读取输入，避免持久化参数持有运行对象。
/// </summary>
public interface IToolInputReader
{
    IVisionImage? Image { get; }

    bool TryGetNodeResult(string nodeId, out ToolResults? result);

    bool TryGetGlobalVariable<T>(string name, out T? value);
}

internal sealed class ToolInputReader : IToolInputReader
{
    private readonly IDictionary<string, ToolResults>? _nodeResults;
    private readonly IDictionary<string, object?>? _globalVariables;

    public ToolInputReader(
        IVisionImage? image,
        IDictionary<string, ToolResults>? nodeResults,
        IDictionary<string, object?>? globalVariables)
    {
        Image = image;
        _nodeResults = nodeResults;
        _globalVariables = globalVariables;
    }

    public IVisionImage? Image { get; }

    public bool TryGetNodeResult(string nodeId, out ToolResults? result)
    {
        if (_nodeResults != null && _nodeResults.TryGetValue(nodeId, out var found))
        {
            result = found;
            return true;
        }

        result = null;
        return false;
    }

    public bool TryGetGlobalVariable<T>(string name, out T? value)
    {
        if (_globalVariables != null
            && _globalVariables.TryGetValue(name, out var rawValue)
            && rawValue is T typedValue)
        {
            value = typedValue;
            return true;
        }

        value = default;
        return false;
    }
}
