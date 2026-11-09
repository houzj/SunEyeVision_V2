using System.Text.Json;
using System.Security.Cryptography;
using SunEyeVision.Domain.Persistence;
using SunEyeVision.Domain.Solutions;
using SunEyeVision.Domain.Workflows;
using SunEyeVision.Persistence;
using SunEyeVision.Persistence.Variables;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.Application;

public sealed partial class DocumentSessionManager
{
    /// <summary>
    /// Background-safe candidate produced before the owner thread publishes a session.
    /// The candidate owns its decode scope until OpenPrepared transfers it or Dispose releases it.
    /// </summary>
    public sealed class PreparedDocumentOpen : IDisposable
    {
        private int _disposed;
        internal PreparedDocumentOpen(DocumentSessionManager manager, string path, byte[] bytes,
            IReadOnlyDictionary<string, FrozenToolDeclarations> declarations, SolutionReadCandidate candidate)
            => (Manager, Path, Bytes, Declarations, Candidate) = (manager, path, bytes, declarations, candidate);
        internal DocumentSessionManager Manager { get; }
        internal string Path { get; }
        internal byte[] Bytes { get; }
        internal IReadOnlyDictionary<string, FrozenToolDeclarations> Declarations { get; }
        internal SolutionReadCandidate Candidate { get; }
        internal void MarkConsumed() => Interlocked.Exchange(ref _disposed, 1);
        public void Dispose()
        {
            if (Interlocked.Exchange(ref _disposed, 1) == 0)
                Candidate.Dispose();
        }
    }

    /// <summary>Prepare the expensive file and resource work without touching the owner-thread session.</summary>
    public PreparedDocumentOpen PrepareOpen(ReadOnlyMemory<byte> bytes, string path,
        Func<ReadOnlyMemory<byte>, FrozenVariableTypeCatalog> captureTypes,
        Func<DecodedResourcesScope, IVariableValueExtensionCodec?> createCodec,
        JsonSerializerOptions options)
    {
        ArgumentNullException.ThrowIfNull(captureTypes);
        ArgumentNullException.ThrowIfNull(createCodec);
        ArgumentNullException.ThrowIfNull(options);
        if (!_directory.IsActive)
            throw new InvalidOperationException("运行目录已退役，不能准备方案候选。");
        path = System.IO.Path.GetFullPath(path);
        var raw = bytes.ToArray();
        var declarations = FreezeDeclarations(raw, _directory);
        var types = captureTypes(raw);
        var scope = new DecodedResourcesScope();
        try
        {
            var codec = createCodec(scope);
            var candidate = SolutionDocumentReader.Read(raw, declarations, types, options, scope, codec,
                System.IO.Path.GetDirectoryName(path));
            return new PreparedDocumentOpen(this, path, raw, declarations, candidate);
        }
        catch
        {
            scope.Dispose();
            throw;
        }
    }

    /// <summary>Publish a prepared candidate on the manager's owner thread.</summary>
    public DocumentSession OpenPrepared(PreparedDocumentOpen prepared, bool discardPendingEdits = false)
    {
        ArgumentNullException.ThrowIfNull(prepared);
        EnsureAvailable();
        if (!ReferenceEquals(prepared.Manager, this))
            throw new InvalidOperationException("候选方案不属于当前方案管理器或运行目录。");

        _busy = true;
        DocumentSession? session = null;
        try
        {
            session = DocumentSession.FromPersistenceCandidate(prepared.Candidate, _directory, prepared.Declarations);
            try
            {
                var expectedHash = Convert.ToHexString(SHA256.HashData(prepared.Bytes));
                session.SetOpenedPersistenceBaseline(prepared.Candidate.PrepareSave(
                    prepared.Path, TargetExpectation.Match(expectedHash)));
            }
            catch
            {
                session.Dispose();
                throw;
            }

            Install(session, discardPendingEdits);
            prepared.MarkConsumed();
            return session;
        }
        catch
        {
            if (session is not null && !ReferenceEquals(session, CurrentSession))
            {
                try { session.Dispose(); }
                catch { /* preserve the admission failure */ }
            }
            throw;
        }
        finally { _busy = false; prepared.Dispose(); }
    }

    /// <summary>Restore a fully admitted backup only to a target that is not the active document.</summary>
    public FrozenSaveReceipt RestoreInactive(string targetPath,string backupPath,string expectedTargetHash,string expectedBackupHash,
        Func<ReadOnlyMemory<byte>,FrozenVariableTypeCatalog> captureTypes,
        Func<DecodedResourcesScope,IVariableValueExtensionCodec?> createCodec,JsonSerializerOptions options,
        CancellationToken cancellationToken=default)
    {
        EnsureAvailable(); ArgumentNullException.ThrowIfNull(captureTypes); ArgumentNullException.ThrowIfNull(createCodec); ArgumentNullException.ThrowIfNull(options);
        _busy=true;
        try
        {
            cancellationToken.ThrowIfCancellationRequested(); targetPath=Path.GetFullPath(targetPath); backupPath=Path.GetFullPath(backupPath);
            var activePath=CurrentSession?.LastWrittenSnapshot?.TargetPath;
            if(activePath is not null && string.Equals(targetPath,activePath,StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("活动方案不能原地恢复，请先关闭，再恢复并重新打开。");
            var bytes=File.ReadAllBytes(backupPath); cancellationToken.ThrowIfCancellationRequested();
            if(!string.Equals(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes)),expectedBackupHash,StringComparison.OrdinalIgnoreCase))
                throw new IOException("备份在验证后发生变化，不执行恢复。");
            var declarations=FreezeDeclarations(bytes,_directory); var types=captureTypes(bytes); var scope=new DecodedResourcesScope();
            IVariableValueExtensionCodec? codec;
            try { codec=createCodec(scope); }
            catch(Exception failure) { try { scope.Dispose(); } catch(Exception cleanup) { throw new AggregateException("创建解码器和释放候选资源均失败。",failure,cleanup); } throw; }
            using var candidate=SolutionDocumentReader.Read(bytes,declarations,types,options,scope,codec,Path.GetDirectoryName(targetPath));
            cancellationToken.ThrowIfCancellationRequested();
            using(var admitted=DocumentSession.FromPersistenceCandidate(candidate,_directory,declarations)) { }
            cancellationToken.ThrowIfCancellationRequested();
            using var lease=_directory.AcquireInvocation();
            var frozen=candidate.PrepareSave(targetPath,TargetExpectation.Match(expectedTargetHash));
            return SolutionFileCommit.Restore(frozen,backupPath,expectedTargetHash.ToUpperInvariant(),expectedBackupHash.ToUpperInvariant(),cancellationToken);
        }
        finally { _busy=false; }
    }

    /// <summary>Prepare a complete candidate before replacing the current session.</summary>
    public DocumentSession Open(string path, Func<ReadOnlyMemory<byte>,FrozenVariableTypeCatalog> captureTypes,
        Func<DecodedResourcesScope,IVariableValueExtensionCodec?> createCodec, JsonSerializerOptions options,
        bool discardPendingEdits=false, CancellationToken cancellationToken=default)
    {
        EnsureAvailable(); ArgumentNullException.ThrowIfNull(captureTypes); ArgumentNullException.ThrowIfNull(createCodec); ArgumentNullException.ThrowIfNull(options);
        _busy=true;
        try
        {
            cancellationToken.ThrowIfCancellationRequested();
            path=Path.GetFullPath(path); var bytes=File.ReadAllBytes(path); cancellationToken.ThrowIfCancellationRequested();
            var declarations=FreezeDeclarations(bytes,_directory);
            var types=captureTypes(bytes); var scope=new DecodedResourcesScope();
            IVariableValueExtensionCodec? codec;
            try { codec=createCodec(scope); }
            catch(Exception failure)
            {
                try { scope.Dispose(); } catch(Exception cleanup) { throw new AggregateException("创建解码器和释放候选资源均失败。",failure,cleanup); }
                throw;
            }
            using var candidate=SolutionDocumentReader.Read(bytes,declarations,types,options,scope,codec,Path.GetDirectoryName(path));
            cancellationToken.ThrowIfCancellationRequested();
            var session=DocumentSession.FromPersistenceCandidate(candidate,_directory,declarations);
            try { session.SetOpenedPersistenceBaseline(candidate.PrepareSave(path,TargetExpectation.Match(Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes))))); cancellationToken.ThrowIfCancellationRequested(); }
            catch(Exception failure)
            {
                try { session.Dispose(); } catch(Exception cleanup) { throw new AggregateException("准备打开方案和释放候选均失败。",failure,cleanup); }
                throw;
            }
            Install(session,discardPendingEdits);
            return session;
        }
        finally { _busy=false; }
    }
    private static IReadOnlyDictionary<string,FrozenToolDeclarations> FreezeDeclarations(byte[] bytes,RuntimeDirectorySnapshot directory)
    {
        using var json=JsonDocument.Parse(bytes); SolutionFormatPolicy.ValidateReadable(json.RootElement);
        var ids=new HashSet<string>(StringComparer.Ordinal);
        if(json.RootElement.TryGetProperty("Workflows",out var flows) && flows.ValueKind==JsonValueKind.Array)
            foreach(var flow in flows.EnumerateArray())
                if(flow.TryGetProperty("Nodes",out var nodes) && nodes.ValueKind==JsonValueKind.Array)
                    foreach(var node in nodes.EnumerateArray())
                        if(node.TryGetProperty("ToolType",out var tool) && tool.ValueKind==JsonValueKind.String) ids.Add(tool.GetString()!);
        var declarations=new Dictionary<string,FrozenToolDeclarations>(StringComparer.Ordinal);
        using var lease=directory.AcquireInvocation();
        foreach(var id in ids)
        {
            var definition=directory.CreateDefinition(id);
            var settings=definition as IToolSettingsFields ?? throw new InvalidOperationException("工具未声明正式设置字段："+id);
            var manual=(definition as IToolManualReferenceSources)?.ManualReferenceSources ?? Array.Empty<ManualReferenceSource>();
            declarations.Add(id,new(id,definition.Describe(),settings.SettingsSchema,ResultDisplayFields.Schema(definition),
                ParameterReferenceRules.Freeze(definition),ToolHostDeclarationRules.Capture(definition),manual));
        }
        if(!directory.IsActive) throw new InvalidOperationException("运行目录已退役。");
        return declarations;
    }
}

public sealed partial class DocumentSession
{
    internal void SetOpenedPersistenceBaseline(FrozenSolutionDocument opened)
    { lock(_gate) { EnsureOpen(); _lastWrittenSnapshot=opened; _lastWrittenRevision=ChangeRevision; _writtenState=CapturePersistenceState(); _resourceBaseDirectory=NormalizeResourceDirectory(Path.GetDirectoryName(opened.TargetPath)!); } }
    internal static DocumentSession FromPersistenceCandidate(SolutionReadCandidate candidate,RuntimeDirectorySnapshot directory,
        IReadOnlyDictionary<string,FrozenToolDeclarations> declarations)
    {
        var document=new SolutionDefinition(candidate.Id,candidate.Name)
        {
            Description=candidate.Description,Version=candidate.Version,AdditionalData=candidate.AdditionalData,
            GlobalVariables=candidate.GlobalVariables,Devices=candidate.Devices,Communications=candidate.Communications,
            CommunicationEvents=candidate.CommunicationEvents,CommunicationSendEvents=candidate.CommunicationSendEvents,
            CommunicationHeartbeats=candidate.CommunicationHeartbeats,CommunicationTriggers=candidate.CommunicationTriggers,
            CommunicationResponses=candidate.CommunicationResponses,DatabaseConfiguration=candidate.DatabaseConfiguration,
            ExecutionStrategy=candidate.ExecutionStrategy,VersionHistory=candidate.VersionHistory
        };
        foreach(var item in candidate.Workflows)
        {
            var flow=new WorkflowDefinition(item.Id,item.Name){ IsSubroutine=item.IsSubroutine, RootConfiguration=item.RootConfiguration, WorkspaceSettings=item.WorkspaceSettings,AdditionalData=item.AdditionalData };
            foreach(var entry in item.Nodes)
                flow.MutableNodes.Add(new NodeDefinition(entry.Id,entry.Name,entry.ToolType,entry.Configuration)
                { DispName=entry.DispName,GlobalIndex=entry.GlobalIndex,LocalIndex=entry.LocalIndex,IsEnabled=entry.IsEnabled,
                  SubroutineId=entry.SubroutineId,SelectedDisplayReference=entry.SelectedDisplayReference,PositionX=entry.PositionX,PositionY=entry.PositionY,
                  Width=entry.Width,Height=entry.Height,AdditionalData=entry.AdditionalData });
            foreach(var edge in item.Connections)
                flow.MutableConnections.Add(new ConnectionDefinition(edge.Id,edge.SourceNodeId,edge.TargetNodeId,edge.SourcePort,edge.TargetPort){ManualPathPoints=edge.ManualPathPoints});
            document.MutableWorkflows.Add(flow);
        }
        var session=new DocumentSession(document,directory);
        try
        {
            foreach(var variable in document.GlobalVariables)
            { var result=variable.Validate(); if(!result.IsValid)throw new InvalidOperationException(string.Join("；",result.Errors)); }
            session.VerifyPersistenceDeclarations(declarations);
            var states=document.Workflows.ToDictionary(flow=>flow,GraphState.Capture,(IEqualityComparer<WorkflowDefinition>)ReferenceEqualityComparer.Instance);
            _=GraphDependencySnapshot.Create(document,states,document.GlobalVariables,session.AdmitConfiguration);
            foreach(var variable in document.GlobalVariables)
            {
                if(variable.ProducerBinding is null || !string.IsNullOrEmpty(variable.RebindingReason) || !string.IsNullOrEmpty(variable.BindingValidationPending)) continue;
                var result=GlobalProducerBindingRules.Validate(variable.ProducerBinding,variable,
                    id=>document.Workflows.SingleOrDefault(flow=>flow.Id==id)?.Nodes,
                    node=>session.AdmitConfiguration(node,node.Configuration).Schema,
                    ()=>directory.IsActive && !session._owner.IsRetired);
                if(result.State is BindingCompatibility.Incompatible or BindingCompatibility.Unresolved)
                    throw new InvalidOperationException("全局变量生产来源未通过正式声明校验："+result.Reason);
            }
            session._decodedResources=candidate.TransferResources();
            return session;
        }
        catch(Exception error)
        {
            try { session.Dispose(); }
            catch(Exception cleanup) { throw new AggregateException("候选方案准入和清理均失败。",error,cleanup); }
            throw;
        }
    }
}
