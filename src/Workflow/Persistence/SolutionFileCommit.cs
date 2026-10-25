using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SunEyeVision.Workflow.Persistence;

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
    public SolutionSnapshot? AttemptedSnapshot { get; internal set; }
}

/// <summary>Serializes in-process writers to one solution. Each replacement is on its own volume.
/// A recovery commit failure compensates the target; this is not crash-atomic across two files.</summary>
internal static class SolutionFileCommit
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

    internal static string? Write(string destination, string recovery, string json, bool overwrite, Action<string>? boundary = null)
    {
        destination = Path.GetFullPath(destination);
        recovery = Path.GetFullPath(recovery);
        if (AtomicJsonFile.SamePath(destination, recovery)) throw new IOException("Target and recovery must differ.");
        lock (Gates.GetOrAdd(destination, _ => new object()))
        {
            ThrowIfRecoveryRequired(destination);
            return WriteLocked(destination, recovery, json, overwrite, boundary);
        }
    }

    internal static void ThrowIfRecoveryRequired(string destination)
    {
        if (Blocked.TryGetValue(Path.GetFullPath(destination), out var pending)) throw pending.Signal;
    }

    /// <summary>Explicit acknowledgement after external recovery. Never guesses which version to retain,
    /// changes files or releases the gate unless both visible files have the exact pre-save identity.</summary>
    internal static void ConfirmRecovery(string destination)
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

    private static string? WriteLocked(string destination, string recovery, string json, bool overwrite, Action<string>? boundary)
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
            File.WriteAllText(pending, json, new UTF8Encoding(false));
            // Ownership is fixed from our own closed staging file BEFORE replacement. A later
            // destination read may prove equality, but can never redefine what this operation wrote.
            installedTarget = Stamp.Read(pending);
            void RequireOwnPayload(string point)
            {
                if (!installedTarget.Matches(destination))
                    throw new IOException("保存目标在" + point + "被外部替换；不覆盖外部现物。");
            }
            if (!File.Exists(destination))
            {
                File.Move(pending, destination);
                committed = true;
                return null;
            }
            if (!overwrite) throw new IOException("目标方案已存在，不会覆盖：" + destination);
            previousTarget = Stamp.Read(destination);
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
            File.Replace(pending, destination, oldTarget, ignoreMetadataErrors: true);
            targetReplaced = true;
            previousTarget = Stamp.Read(oldTarget);
            boundary?.Invoke("target-replaced");
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
