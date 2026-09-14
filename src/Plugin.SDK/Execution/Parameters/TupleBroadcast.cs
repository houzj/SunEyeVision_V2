using System;

namespace SunEyeVision.Plugin.SDK.Execution.Parameters;

/// <summary>
/// 元素配对广播（对齐 Halcon tuple 规则）—— 多元素测量的**唯一配对语义源**（方案 §6 向量化核心契约 / 6d）。
/// </summary>
/// <remarks>
/// <para>规则：</para>
/// <list type="bullet">
/// <item><b>N × N</b> → 逐对，得 N 个结果；</item>
/// <item><b>1 × N</b>（或 <b>N × 1</b>）→ 广播，得 N 个结果；</item>
/// <item><b>N × M</b>（N≠M 且都≠1）→ **报错**"长度不匹配（N vs M）"（rule-015，不静默按短的一方截断）。</item>
/// </list>
/// <para><b>为什么单独成类</b>：配对规则若散落在各工具里，必然出现"这个工具广播、那个工具截断"的语义漂移
/// （rule-008 单一真相源）。6 个测量工具一律调用本类，规则只在此处定义、只在此处被测。</para>
/// <para>0 元素视为"无输入" → 同样报错（不产生空结果，避免"静默零结果"这种不可观测的失败）。</para>
/// </remarks>
public static class TupleBroadcast
{
    /// <summary>
    /// 计算配对结果个数（同时完成合法性校验）。
    /// </summary>
    /// <param name="length1">元素1 的元素个数</param>
    /// <param name="length2">元素2 的元素个数</param>
    /// <param name="count">配对结果个数（成功时有效）</param>
    /// <param name="error">失败原因（失败时非空）</param>
    /// <returns>是否可配对</returns>
    public static bool TryGetPairCount(int length1, int length2, out int count, out string? error)
    {
        count = 0;
        error = null;

        if (length1 <= 0 || length2 <= 0)
        {
            error = $"测量元素不能为空（元素1 {length1} 个、元素2 {length2} 个）：请先绑定上游结果。";
            return false;
        }

        if (length1 == length2)
        {
            count = length1;
            return true;
        }

        if (length1 == 1)
        {
            count = length2;   // 元素1 单值广播
            return true;
        }

        if (length2 == 1)
        {
            count = length1;   // 元素2 单值广播
            return true;
        }

        error = $"元素长度不匹配（{length1} vs {length2}）：只允许等长或其中一个为 1（广播），"
            + "请检查上游绑定元素个数。";
        return false;
    }

    /// <summary>
    /// 取第 <paramref name="i"/> 个结果所用到的某侧下标（长度 1 = 广播，恒取 0）。
    /// </summary>
    /// <param name="length">该侧元素个数</param>
    /// <param name="i">结果序号</param>
    /// <returns>该侧下标</returns>
    public static int IndexFor(int length, int i)
    {
        if (length <= 0)
            throw new ArgumentOutOfRangeException(nameof(length), length, "元素个数必须大于 0（应先用 TryGetPairCount 校验）。");

        return length == 1 ? 0 : i;
    }
}
