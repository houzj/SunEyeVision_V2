using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Threading;
using Microsoft.CodeAnalysis;

namespace CommonControl;

/// <summary>公共编辑器消费引用内容身份，不依赖工具程序集。</summary>
public sealed record ScriptAnalysisReferences(string Identity, ImmutableArray<MetadataReference> References)
{
    public ScriptAnalysisReferences(string identity, IEnumerable<MetadataReference> references)
        : this(identity, references.ToImmutableArray()) { }
}

/// <summary>编辑器独占的文档/引用版本及任务生命周期；共享服务不能持有此可变状态。</summary>
public sealed class ScriptAnalysisSession : IDisposable
{
    private readonly object _gate = new();
    private long _version;
    private string _references = "";
    private string _generatedCode = "";
    private readonly CancellationTokenSource _lifetime = new();
    private bool _closed;
    public Guid Id { get; } = Guid.NewGuid();
    public readonly record struct Stamp(Guid SessionId, long Version, string ReferenceIdentity);
    public CancellationToken LifetimeToken { get { lock (_gate) return _lifetime.Token; } }
    public void AdvanceDocument() { lock (_gate) { if (!_closed) _version++; } }
    public void SetEnvironment(string referenceIdentity, string generatedCode)
    {
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_closed, this);
            if (_references == referenceIdentity && _generatedCode == generatedCode) return;
            _references = referenceIdentity;
            _generatedCode = generatedCode;
            _version++;
        }
    }
    public Stamp Capture() { lock (_gate) return new(Id, _version, _references); }
    public bool IsCurrent(Stamp stamp)
    {
        lock (_gate) return !_closed && stamp == new Stamp(Id, _version, _references);
    }
    public void Dispose()
    {
        lock (_gate)
        {
            if (_closed) return;
            _closed = true;
            _version++;
            _lifetime.Cancel();
            _lifetime.Dispose();
        }
    }
}
