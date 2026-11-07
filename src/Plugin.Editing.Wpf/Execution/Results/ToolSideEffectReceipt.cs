using SunEyeVision.Plugin.Contracts;
using System;
using System.Collections.Generic;
using System.Linq;
using SunEyeVision.Plugin.Editing.Wpf.Models.Communication;

namespace SunEyeVision.Plugin.Editing.Wpf.Execution.Results;

/// <summary>取消或身份失效后仅保留已发生的发送事实，不携带运行输出、所有权或可执行回调。</summary>
public sealed class ToolSideEffectReceipt
{
    public long Submitted { get; }
    public long Skipped { get; }
    public long Failed { get; }
    public IReadOnlyList<string> SentData { get; }
    public IReadOnlyList<BinaryValue> Payloads { get; }
    public ToolSideEffectReceipt(long submitted, long skipped, long failed,
        IEnumerable<string> sentData, IEnumerable<BinaryValue> payloads)
    {
        if (submitted < 0 || skipped < 0 || failed < 0) throw new ArgumentOutOfRangeException(nameof(submitted));
        ArgumentNullException.ThrowIfNull(sentData);
        ArgumentNullException.ThrowIfNull(payloads);
        Submitted = submitted; Skipped = skipped; Failed = failed;
        SentData = Array.AsReadOnly(sentData.Select(value => value ?? throw new ArgumentException("发送事实不能为空。", nameof(sentData))).ToArray());
        Payloads = Array.AsReadOnly(payloads.Select(value => new BinaryValue(value.ToArray())).ToArray());
    }
}

/// <summary>只有发生外部发送副作用的结果显式提供收据。执行器不会反射或复制其他业务状态。</summary>
public interface IToolSideEffectReceiptSource
{
    ToolSideEffectReceipt CaptureSideEffectReceipt();
}

