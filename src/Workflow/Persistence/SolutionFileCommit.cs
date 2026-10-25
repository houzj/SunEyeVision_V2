using System;
using System.Collections.Concurrent;
using System.IO;
using System.Security.Cryptography;
using System.Text;

namespace SunEyeVision.Workflow.Persistence;

/// <summary>The two-file operation could not compensate a detected failure. This is deliberately
/// not an ordinary false save result: callers must retain the previous document baseline and recover explicitly.</summary>
public sealed class SolutionSaveRecoveryRequiredException : IOException
{
    internal SolutionSaveRecoveryRequiredException(string target, string localPrevious, string? centralPrevious,
        string pending, string observedTargetState, Exception cause)
        : base("保存失败且补偿未完成；目标可能已写入。保留恢复文件：" + localPrevious
            + "；中央前版：" + centralPrevious + "；暂存文件：" + pending + "。请核对后恢复。", cause)
    { TargetPath = target; LocalPreviousTargetPath = localPrevious; CentralPreviousPath = centralPrevious; PendingPath = pending; ObservedTargetState = observedTargetState; }
    public string TargetPath { get; }
    public string LocalPreviousTargetPath { get; }
    public string? CentralPreviousPath { get; }
    public string PendingPath { get; }
    public string ObservedTargetState { get; }
    public string RecoveryInstructions => "停止对该目标的继续保存；解除外部锁后核对目标与暂存文件身份。若本地前版存在，先保留当前目标，再以该前版恢复；不要推进文档已保存基线。";
    public SolutionSnapshot? AttemptedSnapshot { get; internal set; }
}

/// <summary>Serializes in-process writers to one solution. Each replacement is on its own volume.
/// A recovery commit failure compensates the target; this is not crash-atomic across two files.</summary>
internal static class SolutionFileCommit
{
    private static readonly ConcurrentDictionary<string, object> Gates = new(StringComparer.OrdinalIgnoreCase);
    private sealed record Stamp(long Length, DateTime Modified, string Hash)
    {
        internal static Stamp Read(string path) => new(new FileInfo(path).Length, File.GetLastWriteTimeUtc(path),
            Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))));
        internal bool Matches(string path) => File.Exists(path) && this == Read(path);
    }

    internal static string? Write(string destination, string recovery, string json, bool overwrite)
    {
        destination = Path.GetFullPath(destination);
        recovery = Path.GetFullPath(recovery);
        if (AtomicJsonFile.SamePath(destination, recovery)) throw new IOException("Target and recovery must differ.");
        lock (Gates.GetOrAdd(destination, _ => new object()))
            return WriteLocked(destination, recovery, json, overwrite);
    }

    private static string? WriteLocked(string destination, string recovery, string json, bool overwrite)
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
            if (!File.Exists(destination))
            {
                File.Move(pending, destination);
                committed = true;
                return null;
            }
            if (!overwrite) throw new IOException("目标方案已存在，不会覆盖：" + destination);
            previousTarget = Stamp.Read(destination);
            previousRecovery = File.Exists(recovery) ? Stamp.Read(recovery) : null;
            // Prepare central-volume storage before changing either visible file.
            Directory.CreateDirectory(Path.GetDirectoryName(recovery)!);
            pendingRecovery = Temporary(recovery);
            oldRecovery = Temporary(recovery);
            File.Copy(destination, pendingRecovery);
            if (!previousTarget.Matches(destination) || !previousTarget.Matches(pendingRecovery))
                throw new IOException("方案在准备保存时被外部修改，未提交。");
            File.Replace(pending, destination, oldTarget, ignoreMetadataErrors: true);
            targetReplaced = true;
            installedTarget = Stamp.Read(destination);
            // The same-volume backup is the actual replaced file, not a later live read.
            if (!previousTarget.Matches(oldTarget)) throw new IOException("目标在替换时被外部修改。");
            if (previousRecovery is null ? File.Exists(recovery) : !previousRecovery.Matches(recovery))
                throw new IOException("恢复备份在保存时被外部修改。");
            if (previousRecovery is null) File.Move(pendingRecovery, recovery);
            else File.Replace(pendingRecovery, recovery, oldRecovery, ignoreMetadataErrors: true);
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
                    throw new SolutionSaveRecoveryRequiredException(destination, oldTarget, oldRecovery, pending, observed,
                        new AggregateException(failure, rollback));
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
