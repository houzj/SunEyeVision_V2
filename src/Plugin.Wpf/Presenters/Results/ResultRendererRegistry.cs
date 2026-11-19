using System;
using System.Collections.Generic;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;

namespace SunEyeVision.Plugin.Wpf.Presenters.Results;

/// <summary>
/// 结果渲染器注册表：<c>rendererKey → IToolResultView 工厂</c>。
/// <para>
/// 对齐原厂 <c>VMControls.WPF.ModuleResultView</c> 的可插拔思路：工具只声明
/// 用哪种呈现（Display 声明里的 rendererKey），平台按 key 找到渲染器。
/// 内置 <c>standard-table</c>；复合形态（如"直方图+表格"）由后续注册补充。
/// </para>
/// <para>
/// 命中不到 key 时抛异常并列出已注册项——<b>禁止静默兜底</b>。
/// </para>
/// </summary>
public static class ResultRendererRegistry
{
    private static readonly Dictionary<string, Func<ToolSchema, ResultProjectionDescriptor, IToolResultView>> _renderers
        = new(StringComparer.Ordinal);

    static ResultRendererRegistry()
        => Register("standard-table", (schema, projection) => new DeclaredResultTable(schema, projection));

    /// <summary>注册渲染器；同 key 重复注册以后者为准。</summary>
    public static void Register(string key, Func<ToolSchema, ResultProjectionDescriptor, IToolResultView> factory)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("渲染器 key 不能为空。", nameof(key));
        _renderers[key] = factory ?? throw new ArgumentNullException(nameof(factory));
    }

    /// <summary>按 key 创建结果视图；未注册则抛出并列出全部已注册 key。</summary>
    public static IToolResultView Create(string key, ToolSchema schema, ResultProjectionDescriptor projection)
    {
        if (!_renderers.TryGetValue(key, out var factory))
            throw new NotSupportedException(
                $"没有注册名为 '{key}' 的结果渲染器。已注册：{string.Join(", ", _renderers.Keys)}");
        return factory(schema, projection);
    }

    /// <summary>当前已注册的 key（供诊断）。</summary>
    public static IReadOnlyCollection<string> Keys => _renderers.Keys;
}
