using System;
using System.Collections.Concurrent;
using System.Linq;

namespace SunEyeVision.Tool.Script.ScriptEngine
{
    /// <summary>
    /// 脚本沙箱常驻池 — 按编译缓存键分组复用沙箱（ALC + 已加载脚本程序集），
    /// 避免每次执行重建沙箱/重新加载程序集/重新 JIT 用户代码，实现"每次执行时间一致"。
    ///
    /// 设计要点：
    /// - key = 编译缓存键（代码哈希|宿主指纹|引用指纹），相同键 = 相同编译产物 + 引用环境
    /// - 程序集常驻（不每次卸载），实例由调用方每次新建（状态干净）
    /// - 代码变化 → 缓存键变化 → 建新沙箱；旧沙箱留池，LRU 超限淘汰（卸载 ALC 防泄漏）
    /// - 宿主 DLL 仍走默认 ALC fallback（类型一致，Script 基类校验不受影响）
    /// </summary>
    internal static class ScriptSandboxPool
    {
        /// <summary>常驻沙箱上限（LRU 淘汰，防止代码频繁变化导致 ALC 累积泄漏）</summary>
        private const int MaxResident = 16;

        private sealed class SandboxSlot
        {
            public ScriptSandbox Sandbox { get; }
            public DateTime LastUsed { get; set; }

            public SandboxSlot(ScriptSandbox sandbox)
            {
                Sandbox = sandbox;
                LastUsed = DateTime.UtcNow;
            }
        }

        private static readonly ConcurrentDictionary<string, SandboxSlot> _pool = new(StringComparer.Ordinal);

        /// <summary>
        /// 获取或创建指定缓存键的常驻沙箱。线程安全；同键并发 GetOrAdd 只会创建一个沙箱。
        /// </summary>
        /// <param name="cacheKey">编译缓存键（决定程序集与引用环境）</param>
        /// <param name="refDir">用户 DLL 引用目录（ScriptRefs），供新沙箱预加载</param>
        /// <returns>常驻沙箱（调用方不 Dispose，归还池复用）</returns>
        public static ScriptSandbox GetOrCreate(string cacheKey, string? refDir, ScriptReferenceSnapshot? references = null)
        {
            var slot = _pool.GetOrAdd(cacheKey, k => new SandboxSlot(new ScriptSandbox($"ScriptSandboxPool_{k[..8]}", refDir, references)));
            slot.LastUsed = DateTime.UtcNow;
            EvictIfNeeded();
            return slot.Sandbox;
        }

        /// <summary>
        /// 使指定缓存键的沙箱失效并卸载（代码/引用配置变化需重建时显式调用）。
        /// </summary>
        /// <param name="cacheKey">要失效的缓存键</param>
        public static void Invalidate(string cacheKey)
        {
            if (_pool.TryRemove(cacheKey, out var slot))
            {
                slot.Sandbox.Dispose();
            }
        }

        /// <summary>
        /// 清空全部常驻沙箱（卸载所有 ALC），供应用关闭/方案重载时调用。
        /// </summary>
        public static void Clear()
        {
            foreach (var slot in _pool.Values)
            {
                try { slot.Sandbox.Dispose(); }
                catch { /* 卸载失败不阻塞清空 */ }
            }
            _pool.Clear();
        }

        private static void EvictIfNeeded()
        {
            if (_pool.Count <= MaxResident) return;

            // 淘汰最久未使用的沙箱（卸载 ALC，防止累积泄漏）
            var victim = _pool.OrderBy(kv => kv.Value.LastUsed).First();
            if (_pool.TryRemove(victim.Key, out var slot))
            {
                try { slot.Sandbox.Dispose(); }
                catch { /* 卸载失败不阻塞 */ }
            }
        }
    }
}
