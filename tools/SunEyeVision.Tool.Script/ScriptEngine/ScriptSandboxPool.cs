using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Runtime.CompilerServices;
using SunEyeVision.Plugin.SDK.Core;

namespace SunEyeVision.Tool.Script.ScriptEngine;

/// <summary>运行域串行持租约；活跃域计入16项容量且不可淘汰。</summary>
internal static class ScriptSandboxPool
{
    private const int MaxResident = 16;
    private static readonly object Gate = new();
    private static readonly Dictionary<(string Domain, string Key), Slot> Pool = new();
    private static readonly HashSet<string> ActiveDomains = new(StringComparer.Ordinal);
    private static readonly HashSet<string> RetiredDomains = new(StringComparer.Ordinal);
    private static bool _clearing;
    internal static (int Resident, int Active, int Retiring) Inspect()
    {
        lock (Gate) return (Pool.Count, Pool.Values.Count(slot => slot.Active), Pool.Values.Count(slot => slot.Retiring));
    }
    internal static WeakReference[] GetContextReferences(string domain)
    {
        lock (Gate) return Pool.Values.Where(slot => slot.Domain == domain).Select(slot => slot.Sandbox.ContextReference).ToArray();
    }
    private static readonly ConditionalWeakTable<ToolExecutionOwner, object> Owners = new();
    internal static WeakReference[] GetLifetimeReferences(string domain)
    {
        lock (Gate)
            return Pool.Values.Where(slot => slot.Domain == domain)
                .SelectMany(slot => new[] { new WeakReference(slot), new WeakReference(slot.Sandbox), new WeakReference(slot.Sandbox.LoadedAssembly) }).ToArray();
    }
    internal static void Attach(ToolExecutionOwner owner)
    {
        lock (Gate)
            if (!Owners.TryGetValue(owner, out _))
            {
                owner.DomainRetired += RetireDomain;
                Owners.Add(owner, new object());
            }
    }
    internal sealed class Slot(string domain, string key, ScriptSandbox sandbox)
    {
        internal readonly string Domain = domain;
        internal readonly string Key = key;
        internal readonly ScriptSandbox Sandbox = sandbox;
        internal bool Active;
        internal bool Retiring;
        internal long LastUsed = Environment.TickCount64;
    }

    internal sealed class Lease : IDisposable
    {
        private Slot? _slot;
        internal Lease(Slot slot) => _slot = slot;
        internal ScriptSandbox Sandbox => _slot?.Sandbox ?? throw new ObjectDisposedException(nameof(Lease));
        public void Dispose()
        {
            var slot = Interlocked.Exchange(ref _slot, null);
            if (slot == null) return;
            ScriptSandbox? retired = null;
            lock (Gate)
            {
                slot.Active = false;
                slot.LastUsed = Environment.TickCount64;
                ActiveDomains.Remove(slot.Domain);
                if (slot.Retiring)
                {
                    Pool.Remove((slot.Domain, slot.Key));
                    retired = slot.Sandbox;
                }
                if (_clearing && Pool.Count == 0) _clearing = false;
                Monitor.PulseAll(Gate);
            }
            retired?.Dispose();
        }
    }

    public static Lease Acquire(string cacheKey, string domain, string? refDir,
        ScriptReferenceSnapshot? references, CancellationToken cancellationToken)
    {
        lock (Gate)
        {
            while (ActiveDomains.Contains(domain))
            {
                if (RetiredDomains.Contains(domain)) throw new InvalidOperationException("脚本运行域已退役。");
                cancellationToken.ThrowIfCancellationRequested();
                if (_clearing) throw new InvalidOperationException("脚本池正在排空，拒绝新执行。");
                Monitor.Wait(Gate, 25);
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (RetiredDomains.Contains(domain)) throw new InvalidOperationException("脚本运行域已退役。");
            if (_clearing) throw new InvalidOperationException("脚本池正在排空，拒绝新执行。");
            foreach (var old in Pool.Values.Where(slot => slot.Domain == domain && slot.Key != cacheKey).ToArray())
            {
                Pool.Remove((old.Domain, old.Key));
                old.Sandbox.Dispose();
            }
            var identity = (domain, cacheKey);
            if (!Pool.TryGetValue(identity, out var slot))
            {
                if (Pool.Count >= MaxResident)
                {
                    var victim = Pool.Values.Where(candidate => !candidate.Active).OrderBy(candidate => candidate.LastUsed).FirstOrDefault();
                    if (victim == null) throw new InvalidOperationException("脚本运行域容量16已全部占用，请等待在途运行完成。");
                    Pool.Remove((victim.Domain, victim.Key));
                    victim.Sandbox.Dispose();
                }
                slot = new Slot(domain, cacheKey, new ScriptSandbox("Script_" + Guid.NewGuid().ToString("N"), refDir, references));
                Pool.Add(identity, slot);
            }
            if (slot.Retiring) throw new InvalidOperationException("脚本运行域已退役。");
            slot.Active = true;
            ActiveDomains.Add(domain);
            return new Lease(slot);
        }
    }

    public static void RetireDomain(string domain)
    {
        lock (Gate)
        {
            RetiredDomains.Add(domain);
            foreach (var slot in Pool.Values.Where(slot => slot.Domain == domain).ToArray())
            {
                slot.Retiring = true;
                if (!slot.Active) { Pool.Remove((slot.Domain, slot.Key)); slot.Sandbox.Dispose(); }
            }
            Monitor.PulseAll(Gate);
        }
    }

    public static void Invalidate(string cacheKey)
    {
        lock (Gate)
            foreach (var slot in Pool.Values.Where(slot => slot.Key == cacheKey).ToArray())
            {
                slot.Retiring = true;
                if (!slot.Active) { Pool.Remove((slot.Domain, slot.Key)); slot.Sandbox.Dispose(); }
            }
    }

    public static void Clear()
    {
        lock (Gate)
        {
            _clearing = true;
            foreach (var slot in Pool.Values.ToArray())
            {
                slot.Retiring = true;
                if (!slot.Active) { Pool.Remove((slot.Domain, slot.Key)); slot.Sandbox.Dispose(); }
            }
            if (Pool.Count == 0) _clearing = false;
            Monitor.PulseAll(Gate);
        }
    }
}
