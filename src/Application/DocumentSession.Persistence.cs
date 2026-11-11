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
    private EmbeddedFileCatalog? _embeddedFiles;
    private FrozenSolutionDocument? _lastWrittenSnapshot;
    private long? _lastWrittenRevision;
    public FrozenSolutionDocument? LastWrittenSnapshot { get { lock(_gate) return _lastWrittenSnapshot; } }
    public long? LastWrittenRevision { get { lock(_gate) return _lastWrittenRevision; } }

    // Relative fields describe resources in this document's live coordinate system.
    // Changing the output filename does not silently mutate that system or private drafts.
    private string? _resourceBaseDirectory;
    public string? ResourceBaseDirectory { get { lock (_gate) return _resourceBaseDirectory; } }
    public IResourcePathResolver CreateResourcePathResolver()
    { lock (_gate) { EnsureOpen(); return new SolutionResourceResolver(_resourceBaseDirectory,_embeddedFiles); } }

    /// <summary>Accept private editors and encode this document for its current file location.</summary>
    public EncodedSolutionDocument CaptureForSave(FrozenVariableTypeCatalog variableTypes, JsonSerializerOptions options,
        Func<DecodedResourcesScope, IVariableValueExtensionCodec?>? createCodec = null, string? sourceDirectory = null)
    {
        ArgumentNullException.ThrowIfNull(variableTypes); ArgumentNullException.ThrowIfNull(options);
        AcceptEditorsForSave();
        EnsureOwnerCanEdit();
        using var mutation = _owner.BeginDocumentMutation();
        lock (_gate)
        {
            var source = ResolveResourceBaseDirectory(sourceDirectory);
            var target = _lastWrittenSnapshot is null ? source : Path.GetDirectoryName(_lastWrittenSnapshot.TargetPath);
            return CaptureAcceptedDocument(variableTypes, options, createCodec, source, target);
        }
    }

    // Call before persistence locks: participant acknowledgements can rebase or release views.
    private void AcceptEditorsForSave()
    {
        EnsureOwnerCanEdit();
        if (Editing.HasPendingChanges) Editing.Apply();
        EnsureOwnerCanEdit();
    }

    private static string NormalizeResourceDirectory(string directory)
        => Path.TrimEndingDirectorySeparator(Path.GetFullPath(directory));

    private string? ResolveResourceBaseDirectory(string? requested)
    {
        var normalized = requested is null ? null : NormalizeResourceDirectory(requested);
        if (_resourceBaseDirectory is not null && normalized is not null &&
            !string.Equals(_resourceBaseDirectory, normalized, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException("提供的资源目录与当前会话实际资源基址不一致。");
        return _resourceBaseDirectory ?? normalized;
    }

    // The caller owns both the execution mutation lease and the session gate.
    private EncodedSolutionDocument CaptureAcceptedDocument(FrozenVariableTypeCatalog variableTypes,
        JsonSerializerOptions options, Func<DecodedResourcesScope, IVariableValueExtensionCodec?>? createCodec,
        string? sourceDirectory, string? targetDirectory)
    {
        EnsureOwnerCanEdit(); _preparing = true;
        try
        {
            lock (GlobalVariable.ValueGate)
            {
                var declarations = CapturePersistenceDeclarations();
                using var captureScope = new DecodedResourcesScope();
                var encoded = SolutionDocumentWriter.Capture(Document, declarations, variableTypes, options,
                    createCodec?.Invoke(captureScope), sourceDirectory,_embeddedFiles);
                if (targetDirectory is not null && sourceDirectory is not null &&
                    !string.Equals(NormalizeResourceDirectory(sourceDirectory), NormalizeResourceDirectory(targetDirectory), StringComparison.OrdinalIgnoreCase))
                {
                    using var rebaseScope = new DecodedResourcesScope();
                    encoded = encoded.Rebase(targetDirectory, declarations, variableTypes, options, createCodec?.Invoke(rebaseScope));
                }
                EnsureOpen(); VerifyPersistenceDeclarations(declarations);
                return encoded;
            }
        }
        finally { _preparing = false; }
    }

    public FrozenSaveReceipt Save(string destination, string recoveryPath, FrozenVariableTypeCatalog variableTypes,
        JsonSerializerOptions options, Func<DecodedResourcesScope, IVariableValueExtensionCodec?>? createCodec = null,
        string? sourceDirectory = null, TargetExpectation? expected = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (expected?.Kind == TargetExpectationKind.Unchecked) throw new ArgumentException("方案会话保存必须校验目标身份。", nameof(expected));
        ArgumentNullException.ThrowIfNull(variableTypes); ArgumentNullException.ThrowIfNull(options);
        AcceptEditorsForSave();
        using var mutation = _owner.BeginDocumentMutation();
        long revision; PersistenceStateStamp savedState; EncodedSolutionDocument encoded;
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            if (expected is null)
            {
                if (_lastWrittenSnapshot is null || !string.Equals(Path.GetFullPath(destination), _lastWrittenSnapshot.TargetPath, StringComparison.OrdinalIgnoreCase))
                    throw new InvalidOperationException("该目标没有已打开或已保存的基线，请使用另存为或明确指定目标身份。");
                expected = TargetExpectation.Match(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(_lastWrittenSnapshot.CopyBytes())));
            }
            sourceDirectory = ResolveResourceBaseDirectory(sourceDirectory);
            revision = ChangeRevision; savedState = CapturePersistenceState();
            encoded = CaptureAcceptedDocument(variableTypes, options, createCodec, sourceDirectory, Path.GetDirectoryName(Path.GetFullPath(destination)));
        }
        mutation.Dispose();
        var receipt = WithEmbeddedResourceWarnings(SolutionFileCommit.Write(encoded.BindTarget(destination, expected ?? TargetExpectation.Missing), recoveryPath, cancellationToken: cancellationToken));
        lock (_gate)
        {
            if (!_closed && !_closing && !_owner.IsRetired && _directory.IsActive)
            { _resourceBaseDirectory ??= sourceDirectory; _lastWrittenSnapshot = receipt.WrittenSnapshot; _lastWrittenRevision = revision; _writtenState = savedState; }
        }
        return receipt;
    }

    public FrozenSaveReceipt SaveAs(string destination, string recoveryPath, string sourceDirectory,
        FrozenVariableTypeCatalog variableTypes, JsonSerializerOptions options,
        Func<DecodedResourcesScope, IVariableValueExtensionCodec?> createCodec,
        TargetExpectation? expected = null, CancellationToken cancellationToken = default)
        => SaveAsCore(destination, recoveryPath, sourceDirectory, variableTypes, options, createCodec, expected, cancellationToken, true);

    public FrozenSaveReceipt SaveCopy(string destination, string recoveryPath, string sourceDirectory,
        FrozenVariableTypeCatalog variableTypes, JsonSerializerOptions options,
        Func<DecodedResourcesScope, IVariableValueExtensionCodec?> createCodec,
        TargetExpectation? expected = null, CancellationToken cancellationToken = default)
        => SaveAsCore(destination, recoveryPath, sourceDirectory, variableTypes, options, createCodec, expected, cancellationToken, false);

    private FrozenSaveReceipt SaveAsCore(string destination, string recoveryPath, string sourceDirectory,
        FrozenVariableTypeCatalog variableTypes, JsonSerializerOptions options,
        Func<DecodedResourcesScope, IVariableValueExtensionCodec?> createCodec,
        TargetExpectation? expected, CancellationToken cancellationToken, bool updateBaseline)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceDirectory); ArgumentNullException.ThrowIfNull(createCodec);
        if (expected?.Kind == TargetExpectationKind.Unchecked) throw new ArgumentException("方案会话另存为必须校验目标身份。", nameof(expected));
        cancellationToken.ThrowIfCancellationRequested();
        ArgumentNullException.ThrowIfNull(variableTypes); ArgumentNullException.ThrowIfNull(options);
        AcceptEditorsForSave();
        using var mutation = _owner.BeginDocumentMutation();
        long revision; PersistenceStateStamp savedState; EncodedSolutionDocument encoded;
        lock (_gate)
        {
            EnsureOwnerCanEdit();
            sourceDirectory = ResolveResourceBaseDirectory(sourceDirectory)!;
            if (!updateBaseline && _lastWrittenSnapshot is not null && string.Equals(Path.GetFullPath(destination), _lastWrittenSnapshot.TargetPath, StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("保存副本不能覆盖当前方案。");
            revision = ChangeRevision; savedState = CapturePersistenceState();
            encoded = CaptureAcceptedDocument(variableTypes, options, createCodec, sourceDirectory, Path.GetDirectoryName(Path.GetFullPath(destination)));
        }
        mutation.Dispose();
        var receipt = WithEmbeddedResourceWarnings(SolutionFileCommit.Write(encoded.BindTarget(destination, expected ?? TargetExpectation.Missing), recoveryPath, cancellationToken: cancellationToken));
        lock (_gate)
        {
            if (updateBaseline && !_closed && !_closing && !_owner.IsRetired && _directory.IsActive)
            { _resourceBaseDirectory ??= sourceDirectory; _lastWrittenSnapshot = receipt.WrittenSnapshot; _lastWrittenRevision = revision; _writtenState = savedState; }
        }
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
    private static FrozenSaveReceipt WithEmbeddedResourceWarnings(FrozenSaveReceipt receipt)
    {
        var warnings=receipt.WrittenSnapshot.Diagnostics.Where(item=>item.Severity=="Warning"&&item.Code.StartsWith("RESOURCE_EMBED_",StringComparison.Ordinal))
            .Select(item=>item.Message).Distinct(StringComparer.Ordinal).ToList();
        if(!string.IsNullOrEmpty(receipt.Warning))warnings.Insert(0,receipt.Warning);
        return warnings.Count==0?receipt:receipt with{Warning=string.Join(Environment.NewLine,warnings)};
    }
    private void ReleaseDecodedResources() => Interlocked.Exchange(ref _decodedResources,null)?.Dispose();
}
