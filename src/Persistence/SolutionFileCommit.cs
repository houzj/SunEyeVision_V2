using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SunEyeVision.Persistence;

public sealed record SolutionRecoveryFile(string Role, string Path, bool Exists);

/// <summary>The two-file operation could not compensate a detected failure. This is deliberately
/// not an ordinary false save result: callers must retain the previous document baseline and recover explicitly.</summary>
public sealed class SolutionSaveRecoveryRequiredException : IOException
{
    internal SolutionSaveRecoveryRequiredException(string target, string localPrevious, string? centralPrevious,
        string pending, string observedTargetState, bool targetRestored, bool centralRestored,
        IReadOnlyList<SolutionRecoveryFile> retainedFiles, Exception cause)
        : base("保存失败且补偿未完成；目标可能已写入。保留恢复文件：" + localPrevious
            + "；目标已恢复=" + targetRestored + "，中央已恢复=" + centralRestored + "。保留位置："
            + string.Join("；", retainedFiles.Select(file => file.Role + "=" + file.Path + "（存在=" + file.Exists + "）")), cause)
    { TargetPath = target; LocalPreviousTargetPath = localPrevious; CentralPreviousPath = centralPrevious; PendingPath = pending; ObservedTargetState = observedTargetState; TargetRestored = targetRestored; CentralRestored = centralRestored; RetainedFiles = retainedFiles; }
    public string TargetPath { get; }
    public string LocalPreviousTargetPath { get; }
    public string? CentralPreviousPath { get; }
    public string PendingPath { get; }
    public string ObservedTargetState { get; }
    public bool TargetRestored { get; }
    public bool CentralRestored { get; }
    public IReadOnlyList<SolutionRecoveryFile> RetainedFiles { get; }
    public string RecoveryInstructions => "停止继续保存并解除外部锁；仅使用存在的暂存文件，先保留当前内容。目标已恢复时不要再找已消耗的本地前版；中央未恢复时核对中央前版暂存文件。双盘均恢复原SHA/长度/mtime后显式ConfirmSaveRecovery，不推进未确认的已保存基线。";
    public FrozenSolutionDocument? AttemptedSnapshot { get; internal set; }
}

/// <summary>Serializes in-process writers to one solution. Each replacement is on its own volume.
/// A recovery commit failure compensates the target; this is not crash-atomic across two files.</summary>
public static class SolutionFileCommit
{
    private static readonly ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);
    private static readonly ConcurrentDictionary<string, RecoveryState> Blocked = new(StringComparer.OrdinalIgnoreCase);
    private sealed record RecoveryState(SolutionSaveRecoveryRequiredException Signal, Stamp Target, Stamp? Recovery, string RecoveryPath);
    private sealed record Stamp(long Length, DateTime Modified, string Hash)
    {
        internal static Stamp Read(string path) => new(new FileInfo(path).Length, File.GetLastWriteTimeUtc(path),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        internal bool Matches(string path) => File.Exists(path) && this == Read(path);
    }

    internal static string? Write(string destination, string recovery, byte[] bytes, bool overwrite, Action<string>? boundary = null, TargetExpectation? targetExpectation = null, CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        destination = Path.GetFullPath(destination);
        recovery = Path.GetFullPath(recovery);
        if (AtomicJsonFile.SamePath(destination, recovery)) throw new IOException("Target and recovery must differ.");
        lock (Gates.GetOrAdd(destination, _ => new object()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfRecoveryRequired(destination);
            Stamp? expectedTarget = null;
            if (targetExpectation?.Kind == TargetExpectationKind.Missing)
            {
                if (File.Exists(destination)) throw new IOException("目标方案已存在，不会覆盖。");
                overwrite=false;
            }
            if (targetExpectation?.Kind == TargetExpectationKind.MatchSha256)
            {
                if (!File.Exists(destination)) throw new IOException("方案在验证后消失，不会覆盖。");
                expectedTarget = Stamp.Read(destination);
                if (!string.Equals(expectedTarget.Hash, targetExpectation!.Sha256, StringComparison.Ordinal))
                    throw new IOException("方案在验证后发生变化，不会覆盖。");
            }
            return WriteLocked(destination, recovery, bytes, overwrite, boundary, expectedTarget, cancellationToken: cancellationToken);
        }
    }

    public static FrozenSaveReceipt Write(FrozenSolutionDocument document, string recovery, bool overwrite = true, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document);
        try { var warning=Write(document.TargetPath,recovery,document.CopyBytes(),overwrite,targetExpectation:document.TargetExpectation,cancellationToken:cancellationToken); return new(document,warning); }
        catch(SolutionSaveRecoveryRequiredException error) { error.AttemptedSnapshot ??= document; throw; }
    }

    public static FrozenSaveReceipt Restore(FrozenSolutionDocument document, string sourceRecovery, string expectedTargetHash, string expectedRecoveryHash, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(document); cancellationToken.ThrowIfCancellationRequested();
        var bytes=document.CopyBytes();
        if (!string.Equals(Convert.ToHexString(SHA256.HashData(bytes)),expectedRecoveryHash,StringComparison.Ordinal)) throw new IOException("恢复候选字节与已校验备份不一致。");
        try { var warning=Restore(document.TargetPath,sourceRecovery,bytes,expectedTargetHash,expectedRecoveryHash,cancellationToken:cancellationToken); return new(document,warning); }
        catch(SolutionSaveRecoveryRequiredException error) { error.AttemptedSnapshot ??= document; throw; }
    }
    /// <summary>Restore a previously validated snapshot without mutating its source backup.
    /// Uses the same target gate and compensation logic as ordinary saves.</summary>
    internal static string? Restore(string destination, string sourceRecovery, byte[] bytes,
        string expectedDestinationHash, string expectedRecoveryHash, Action<string>? boundary = null, CancellationToken cancellationToken = default)
    {
        destination=Path.GetFullPath(destination); sourceRecovery=Path.GetFullPath(sourceRecovery);
        if (AtomicJsonFile.SamePath(destination,sourceRecovery)) throw new IOException("Target and recovery must differ.");
        lock (Gates.GetOrAdd(destination,_=>new object()))
        {
            cancellationToken.ThrowIfCancellationRequested();
            ThrowIfRecoveryRequired(destination);
            if (!File.Exists(destination) || !File.Exists(sourceRecovery)) throw new IOException("方案或恢复文件不存在，不会覆盖。");
            var target=Stamp.Read(destination); var source=Stamp.Read(sourceRecovery);
            if (!string.Equals(target.Hash,expectedDestinationHash,StringComparison.Ordinal) ||
                !string.Equals(source.Hash,expectedRecoveryHash,StringComparison.Ordinal))
                throw new IOException("方案或恢复文件在验证后发生变化，不会覆盖。");
            var privateRecovery=Path.Combine(Path.GetDirectoryName(destination)!,
                "."+Path.GetFileName(destination)+"."+Guid.NewGuid().ToString("N")+".restore-backup");
            var preserve=false; string? warning=null, cleanupWarning=null;
            try { warning=WriteLocked(destination,privateRecovery,bytes,true,boundary,target,sourceRecovery,source,cancellationToken); }
            catch (SolutionSaveRecoveryRequiredException) { preserve=true; throw; }
            finally
            {
                if (!preserve) try { if (File.Exists(privateRecovery)) File.Delete(privateRecovery); }
                catch (Exception error) when (error is IOException or UnauthorizedAccessException)
                { cleanupWarning="恢复成功，临时备份待清理："+privateRecovery+"；"+error.Message; }
            }
            return warning is null ? cleanupWarning : cleanupWarning is null ? warning : warning+"；"+cleanupWarning;
        }
    }

    internal static void ThrowIfRecoveryRequired(string destination)
    {
        if (Blocked.TryGetValue(Path.GetFullPath(destination), out var pending)) throw pending.Signal;
    }

    /// <summary>Explicit acknowledgement after external recovery. Never guesses which version to retain,
    /// changes files or releases the gate unless both visible files have the exact pre-save identity.</summary>
    public static void ConfirmRecovery(string destination)
    {
        destination = Path.GetFullPath(destination);
        lock (Gates.GetOrAdd(destination, _ => new object()))
        {
            if (!Blocked.TryGetValue(destination, out var pending)) return;
            if (!pending.Target.Matches(destination) || (pending.Recovery is null
                ? File.Exists(pending.RecoveryPath) : !pending.Recovery.Matches(pending.RecoveryPath)))
                throw pending.Signal;
            Blocked.TryRemove(destination, out _);
        }
    }

    private static string? WriteLocked(string destination, string recovery, byte[] bytes, bool overwrite, Action<string>? boundary,
        Stamp? expectedTarget=null,string? expectedSourcePath=null,Stamp? expectedSource=null,CancellationToken cancellationToken=default)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
        string Temporary(string path) => Path.Combine(Path.GetDirectoryName(path)!,
            "." + Path.GetFileName(path) + "." + Guid.NewGuid().ToString("N") + ".tmp");
        var pending = Temporary(destination);
        var oldTarget = Temporary(destination);
        string? pendingRecovery = null, oldRecovery = null;
        bool targetReplaced = false, committed = false, preserveRollback = false;
        Stamp? previousTarget = null, previousRecovery = null, installedTarget = null;
        string? warning = null;
        try
        {
            File.WriteAllBytes(pending, bytes);
            // Ownership is fixed from our own closed staging file BEFORE replacement. A later
            // destination read may prove equality, but can never redefine what this operation wrote.
            installedTarget = Stamp.Read(pending);
            void RequireOwnPayload(string point)
            {
                if (!installedTarget.Matches(destination))
                    throw new IOException("保存目标在" + point + "被外部替换；不覆盖外部现物。");
            }
            cancellationToken.ThrowIfCancellationRequested();
            if (!File.Exists(destination))
            {
                if (expectedTarget is not null) throw new IOException("方案在恢复前已消失，不会覆盖。");
                File.Move(pending, destination);
                committed = true;
                return null;
            }
            if (!overwrite) throw new IOException("目标方案已存在，不会覆盖：" + destination);
            previousTarget = Stamp.Read(destination);
            if (expectedTarget is not null && previousTarget != expectedTarget)
                throw new IOException("方案在恢复前发生变化，不会覆盖。");
            var preflightTarget = previousTarget;
            previousRecovery = File.Exists(recovery) ? Stamp.Read(recovery) : null;
            // Prepare central-volume storage before changing either visible file.
            Directory.CreateDirectory(Path.GetDirectoryName(recovery)!);
            pendingRecovery = Temporary(recovery);
            oldRecovery = Temporary(recovery);
            File.Copy(destination, pendingRecovery);
            if (!previousTarget.Matches(destination) || !previousTarget.Matches(pendingRecovery))
                throw new IOException("方案在准备保存时被外部修改，未提交。");
            boundary?.Invoke("before-target-replace");
            if (expectedTarget is not null && !expectedTarget.Matches(destination))
                throw new IOException("方案在恢复提交前发生变化，不会覆盖。");
            if (expectedSource is not null && !expectedSource.Matches(expectedSourcePath!))
                throw new IOException("恢复文件在提交前发生变化，不会覆盖。");
            cancellationToken.ThrowIfCancellationRequested();
            File.Replace(pending, destination, oldTarget, ignoreMetadataErrors: true);
            targetReplaced = true;
            previousTarget = Stamp.Read(oldTarget);
            boundary?.Invoke("target-replaced");
            if (expectedSource is not null && !expectedSource.Matches(expectedSourcePath!))
                throw new IOException("恢复文件在提交期间发生变化，恢复原方案。");
            RequireOwnPayload("目标替换后");
            // The same-volume backup is the actual replaced file, not a later live read.
            if (previousTarget != preflightTarget) throw new IOException("目标在替换时被外部修改；恢复实际被替换的盘前态。");
            boundary?.Invoke("before-central-commit");
            RequireOwnPayload("中央提交前");
            if (previousRecovery is null ? File.Exists(recovery) : !previousRecovery.Matches(recovery))
                throw new IOException("恢复备份在保存时被外部修改。");
            if (previousRecovery is null) File.Move(pendingRecovery, recovery);
            else File.Replace(pendingRecovery, recovery, oldRecovery, ignoreMetadataErrors: true);
            boundary?.Invoke("central-committed");
            RequireOwnPayload("中央提交后");
            committed = true;
        }
        catch (Exception failure) when (!committed)
        {
            if (targetReplaced)
            {
                try
                {
                    if (installedTarget is null || !installedTarget.Matches(destination))
                        throw new IOException("目标已被外部修改，不覆盖外部写入。");
                    // Windows replacement preserves the timestamp of the actual original file.
                    File.Replace(oldTarget, destination, pending, ignoreMetadataErrors: true);
                    if (!installedTarget.Matches(pending))
                        throw new IOException("补偿期间目标被外部修改，已保留被替换内容而不清理。");
                    if (previousTarget is null || !previousTarget.Matches(destination))
                        throw new IOException("目标恢复后的字节或时间戳不符。");
                    if (previousRecovery is null ? File.Exists(recovery) : !previousRecovery.Matches(recovery))
                    {
                        // A partially completed central replace may have moved the old file.
                        if (previousRecovery is not null && previousRecovery.Matches(oldRecovery!))
                        {
                            if (File.Exists(recovery)) File.Replace(oldRecovery!, recovery, null, true);
                            else File.Move(oldRecovery!, recovery);
                        }
                        if (previousRecovery is null ? File.Exists(recovery) : !previousRecovery.Matches(recovery))
                            throw new IOException("恢复备份有外部改动或未能恢复。");
                    }
                }
                catch (Exception rollback)
                {
                    preserveRollback = true;
                    string observed;
                    try { observed = File.Exists(destination) ? Stamp.Read(destination).ToString()! : "Target missing"; }
                    catch (Exception read) { observed = "Target state unavailable: " + read.Message; }
                    bool SafeMatches(Stamp? stamp, string path) { try { return stamp is null ? !File.Exists(path) : stamp.Matches(path); } catch { return false; } }
                    var files = new[]
                    {
                        (Role: "target", Path: destination), (Role: "local-previous-target", Path: oldTarget),
                        (Role: "replaced-content", Path: pending), (Role: "pending-central-copy", Path: pendingRecovery),
                        (Role: "central-previous", Path: oldRecovery), (Role: "central", Path: recovery)
                    }.Where(file => file.Path is not null).Select(file => new SolutionRecoveryFile(file.Role, file.Path!, File.Exists(file.Path))).ToArray();
                    var signal = new SolutionSaveRecoveryRequiredException(destination, oldTarget, oldRecovery, pending, observed,
                        SafeMatches(previousTarget, destination), SafeMatches(previousRecovery, recovery), Array.AsReadOnly(files),
                        new AggregateException(failure, rollback));
                    Blocked[destination] = new(signal, previousTarget!, previousRecovery, recovery);
                    throw signal;
                }
            }
            throw;
        }
        finally
        {
            if (!preserveRollback)
                foreach (var path in new[] { pending, oldTarget, pendingRecovery, oldRecovery })
                {
                    if (path is null) continue;
                    try { if (File.Exists(path)) File.Delete(path); }
                    catch (Exception cleanup) when (cleanup is IOException or UnauthorizedAccessException)
                    { if (committed) warning = "保存成功，临时文件待清理：" + path + "；" + cleanup.Message; }
                }
        }
        return warning;
    }
}
