using System.Text.Json;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Persistence;
using SunEyeVision.Persistence.Variables;
using SunEyeVision.Plugin.Contracts;

namespace SunEyeVision.Application;

public sealed partial class DocumentSession
{
    private IDisposable? _decodedResources;
    private FrozenSolutionDocument? _lastWrittenSnapshot;
    private long? _lastWrittenRevision;
    public FrozenSolutionDocument? LastWrittenSnapshot { get { lock(_gate) return _lastWrittenSnapshot; } }
    public long? LastWrittenRevision { get { lock(_gate) return _lastWrittenRevision; } }

    /// <summary>Captures and encodes the current document inside the session and value ownership gates.</summary>
    public EncodedSolutionDocument CaptureForSave(FrozenVariableTypeCatalog variableTypes, JsonSerializerOptions options,
        IVariableValueExtensionCodec? codec = null, string? sourceDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(variableTypes); ArgumentNullException.ThrowIfNull(options);
        AcceptEditorsForSave();
        return CaptureAcceptedDocument(variableTypes, options, codec, sourceDirectory);
    }

    // Saving accepts the same private editor batch as Apply. Acknowledgements run before
    // any persistence lock, so rebase/cleanup cannot execute inside the snapshot gate.
    private void AcceptEditorsForSave()
    {
        EnsureOwnerCanEdit();
        if (Editing.HasPendingChanges) Editing.Apply();
        EnsureOwnerCanEdit();
    }

    private EncodedSolutionDocument CaptureAcceptedDocument(FrozenVariableTypeCatalog variableTypes,
        JsonSerializerOptions options, IVariableValueExtensionCodec? codec, string? sourceDirectory)
    {
        EnsureOwnerCanEdit();
        using var mutation=_owner.BeginDocumentMutation();
        lock (_gate)
        {
            EnsureOwnerCanEdit(); _preparing=true;
            try
            {
                lock(GlobalVariable.ValueGate)
                {
                    var declarations=CapturePersistenceDeclarations();
                    var encoded=SolutionDocumentWriter.Capture(Document,declarations,variableTypes,options,codec,sourceDirectory);
                    EnsureOpen();
                    // Re-admission catches changes in declaration identities and directory generation during encoding.
                    VerifyPersistenceDeclarations(declarations);
                    return encoded;
                }
            }
            finally { _preparing=false; }
        }
    }

    public FrozenSaveReceipt Save(string destination,string recoveryPath, FrozenVariableTypeCatalog variableTypes,
        JsonSerializerOptions options, IVariableValueExtensionCodec? codec = null, string? sourceDirectory = null,
        TargetExpectation? expected = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if(expected?.Kind==TargetExpectationKind.Unchecked) throw new ArgumentException("方案会话保存必须校验目标身份。",nameof(expected));
        ArgumentNullException.ThrowIfNull(variableTypes); ArgumentNullException.ThrowIfNull(options);
        AcceptEditorsForSave();
        EnsureOwnerCanEdit();
        using var mutation=_owner.BeginDocumentMutation();
        long revision; EncodedSolutionDocument encoded;
        lock(_gate)
        {
            EnsureOwnerCanEdit();
            if(expected is null)
            {
                if(_lastWrittenSnapshot is null || !string.Equals(Path.GetFullPath(destination),_lastWrittenSnapshot.TargetPath,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("该目标没有已打开或已保存的基线，请使用另存为或明确指定目标身份。");
                expected=TargetExpectation.Match(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(_lastWrittenSnapshot.CopyBytes())));
            }
            sourceDirectory ??= _lastWrittenSnapshot is null ? null : Path.GetDirectoryName(_lastWrittenSnapshot.TargetPath);
            revision=ChangeRevision; encoded=CaptureAcceptedDocument(variableTypes,options,codec,sourceDirectory);
        }
        mutation.Dispose();
        var receipt=SolutionFileCommit.Write(encoded.BindTarget(destination,expected ?? TargetExpectation.Missing),recoveryPath,cancellationToken:cancellationToken);
        lock(_gate) { if (!_closed && !_closing && !_owner.IsRetired && _directory.IsActive) { _lastWrittenSnapshot=receipt.WrittenSnapshot; _lastWrittenRevision=revision; } }
        return receipt;
    }

    public FrozenSaveReceipt SaveAs(string destination,string recoveryPath,string sourceDirectory,
        FrozenVariableTypeCatalog variableTypes,JsonSerializerOptions options,
        Func<DecodedResourcesScope,IVariableValueExtensionCodec?> createCodec,
        TargetExpectation? expected=null,CancellationToken cancellationToken=default)
        => SaveAsCore(destination,recoveryPath,sourceDirectory,variableTypes,options,createCodec,expected,cancellationToken,true);

    public FrozenSaveReceipt SaveCopy(string destination,string recoveryPath,string sourceDirectory,
        FrozenVariableTypeCatalog variableTypes,JsonSerializerOptions options,
        Func<DecodedResourcesScope,IVariableValueExtensionCodec?> createCodec,
        TargetExpectation? expected=null,CancellationToken cancellationToken=default)
        => SaveAsCore(destination,recoveryPath,sourceDirectory,variableTypes,options,createCodec,expected,cancellationToken,false);

    private FrozenSaveReceipt SaveAsCore(string destination,string recoveryPath,string sourceDirectory,
        FrozenVariableTypeCatalog variableTypes,JsonSerializerOptions options,
        Func<DecodedResourcesScope,IVariableValueExtensionCodec?> createCodec,
        TargetExpectation? expected,CancellationToken cancellationToken,bool updateBaseline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory); ArgumentNullException.ThrowIfNull(createCodec);
        if(expected?.Kind==TargetExpectationKind.Unchecked) throw new ArgumentException("方案会话另存为必须校验目标身份。",nameof(expected));
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(variableTypes); ArgumentNullException.ThrowIfNull(options);
        AcceptEditorsForSave();
        EnsureOwnerCanEdit();
        using var mutation=_owner.BeginDocumentMutation();
        long revision; EncodedSolutionDocument encoded;
        lock(_gate)
        {
            EnsureOwnerCanEdit(); _preparing=true;
            try
            {
                if(_lastWrittenSnapshot is not null && !string.Equals(
                    Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory)),
                    Path.TrimEndingDirectorySeparator(Path.GetDirectoryName(_lastWrittenSnapshot.TargetPath)!),StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("源资源目录与当前打开或已保存的方案位置不一致。");
                if(!updateBaseline && _lastWrittenSnapshot is not null && string.Equals(Path.GetFullPath(destination),_lastWrittenSnapshot.TargetPath,StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("保存副本不能覆盖当前方案。");
                lock(GlobalVariable.ValueGate)
                {
                    revision=ChangeRevision;
                    var declarations=CapturePersistenceDeclarations();
                    using var captureScope=new DecodedResourcesScope();
                    var captureCodec=createCodec(captureScope);
                    encoded=SolutionDocumentWriter.Capture(Document,declarations,variableTypes,options,captureCodec,sourceDirectory);
                    var targetDirectory=Path.GetDirectoryName(Path.GetFullPath(destination))!;
                    if(!string.Equals(Path.TrimEndingDirectorySeparator(Path.GetFullPath(sourceDirectory)),Path.TrimEndingDirectorySeparator(targetDirectory),StringComparison.OrdinalIgnoreCase))
                    {
                        using var rebaseScope=new DecodedResourcesScope();
                        encoded=encoded.Rebase(targetDirectory,declarations,variableTypes,options,createCodec(rebaseScope));
                    }
                    EnsureOpen(); VerifyPersistenceDeclarations(declarations);
                }
            }
            finally { _preparing=false; }
        }
        mutation.Dispose();
        var receipt=SolutionFileCommit.Write(encoded.BindTarget(destination,expected ?? TargetExpectation.Missing),recoveryPath,cancellationToken:cancellationToken);
        lock(_gate) { if (updateBaseline && !_closed && !_closing && !_owner.IsRetired && _directory.IsActive) { _lastWrittenSnapshot=receipt.WrittenSnapshot; _lastWrittenRevision=revision; } }
        return receipt;
    }

    private Dictionary<string,FrozenToolDeclarations> CapturePersistenceDeclarations()
    {
        var declarations=new Dictionary<string,FrozenToolDeclarations>(StringComparer.Ordinal);
        foreach(var node in Document.Workflows.SelectMany(flow=>flow.Nodes))
        {
            var admitted=AdmitConfiguration(node,node.Configuration);
            var current=new FrozenToolDeclarations(node.ToolType,admitted.Schema,admitted.SettingsSchema,admitted.DisplaySchema,
                admitted.ReferenceBehaviors,admitted.HostDeclarations,admitted.ManualReferenceSources);
            if(declarations.TryGetValue(node.ToolType,out var previous))
                VerifyDeclaration(previous,current);
            else declarations.Add(node.ToolType,current);
        }
        return declarations;
    }
    private void VerifyPersistenceDeclarations(IReadOnlyDictionary<string,FrozenToolDeclarations> declarations)
    {
        var again=CapturePersistenceDeclarations();
        if(again.Count!=declarations.Count)throw new InvalidOperationException("工具声明集合在保存期间变化。");
        foreach(var (id,previous) in declarations)
        { if(!again.TryGetValue(id,out var current))throw new InvalidOperationException("工具声明在保存期间消失。"); VerifyDeclaration(previous,current); }
    }
    private static void VerifyDeclaration(FrozenToolDeclarations a,FrozenToolDeclarations b)
    {
        if(a.ToolId!=b.ToolId || a.Schema.Fingerprint!=b.Schema.Fingerprint || a.SettingsSchema.Fingerprint!=b.SettingsSchema.Fingerprint ||
           a.DisplaySchema.Fingerprint!=b.DisplaySchema.Fingerprint || a.HostDeclarations.Fingerprint!=b.HostDeclarations.Fingerprint ||
           a.ReferenceBehaviors.Count!=b.ReferenceBehaviors.Count || a.ReferenceBehaviors.Any(p=>!b.ReferenceBehaviors.TryGetValue(p.Key,out var x)||x!=p.Value) ||
           a.ManualReferenceSources.Count!=b.ManualReferenceSources.Count || a.ManualReferenceSources.Zip(b.ManualReferenceSources).Any(p=>p.First.PortId!=p.Second.PortId || !p.First.MemberPath.SequenceEqual(p.Second.MemberPath,StringComparer.Ordinal)))
            throw new InvalidOperationException("工具声明在持久化期间变化。");
    }
    private void ReleaseDecodedResources() => Interlocked.Exchange(ref _decodedResources,null)?.Dispose();
}
