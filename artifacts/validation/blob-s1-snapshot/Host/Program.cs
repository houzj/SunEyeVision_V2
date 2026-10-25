using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using SunEyeVision.Workflow;
using SunEyeVision.Workflow.Persistence;
using SunEyeVision.Plugin.Infrastructure.Managers.Tool;
using SunEyeVision.Plugin.Infrastructure.Managers.Plugin;
using SunEyeVision.Tool.BlobAnalysis;

internal static class Program
{
    private static readonly List<object> Cases = new();
    private static string Baseline = "", Root = "", Filter = "", CrossRecoveryRoot = "";
    private static int Failures;
    private static string Stamp(string path) => File.Exists(path) ? JsonSerializer.Serialize(new
    {
        sha = Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path))),
        length = new FileInfo(path).Length, mtime = File.GetLastWriteTimeUtc(path)
    }) : "missing";
    private static void Require(bool condition, string detail) { if (!condition) throw new Exception(detail); }
    private static void Case(string name, Action action)
    {
        if (Filter.Length > 0 && !name.Contains(Filter, StringComparison.Ordinal)) return;
        try { action(); Cases.Add(new { name, status = "PASS" }); Console.WriteLine("PASS " + name); }
        catch (Exception fault) { Failures++; Cases.Add(new { name, status = "FAIL", error = fault.ToString() }); Console.WriteLine("FAIL " + name + ": " + fault); }
    }
    private static (SolutionRepository Repository, Solution Candidate, string Target, string Backup) Setup(string name, bool crossVolume = false)
    {
        var folder = Path.Combine(Root, name); Directory.CreateDirectory(folder);
        var target = Path.Combine(folder, "document.solution"); File.WriteAllText(target, Baseline);
        var recovery = crossVolume ? Path.Combine(CrossRecoveryRoot, "SunEyeVision-snapshot-fix-" + Guid.NewGuid().ToString("N")) : Path.Combine(folder, "recovery");
        var repository = new SolutionRepository(recovery);
        var loaded = repository.LoadCandidate(target);
        var candidate = loaded.Candidate ?? throw new Exception(string.Join(";", loaded.Diagnostics.Select(d => d.Message)));
        candidate.Name = "existing-version";
        Require(repository.Save(candidate, target), "setup save: " + repository.LastError);
        return (repository, candidate, target, repository.GetRecoveryPath(target));
    }
    [STAThread] private static int Main(string[] args)
    {
        ToolRegistry.RegisterTool(typeof(BlobAnalysisTool)); PluginLoader.MarkAsLoaded();
        Filter = args.Length > 1 ? args[1] : "";
        CrossRecoveryRoot = args.Length > 2 ? args[2] : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Temp");
        Baseline = File.ReadAllText(args[0]); Root = Path.Combine(AppContext.BaseDirectory, "fixtures-" + Guid.NewGuid().ToString("N")); Directory.CreateDirectory(Root);
        foreach (var mode in new[] { "missing-node", "wrong-owner", "changed-schema" }) Case(mode, () =>
        {
            var s = Setup(mode); var targetBefore = Stamp(s.Target); var backupBefore = Stamp(s.Backup);
            var setting = s.Candidate.Workflows[0].Nodes.Single(n => n.Id == "filter-consumer").ParamSettings.GetSetting("FilterAreaMin")!;
            var original = setting.ReferenceExpression!; var expression = JsonNode.Parse(original)!;
            if (mode == "missing-node") expression["Reference"]!["nodeId"] = "missing-source";
            else if (mode == "wrong-owner") expression["Reference"]!["scopeId"] = "absent-scope";
            else expression["Reference"]!["expectedType"]!["maximum"] = 0;
            setting.ReferenceExpression = expression.ToJsonString();
            bool rejected = false; try { _ = SolutionSnapshot.Prepare(s.Candidate, s.Target); } catch { rejected = true; }
            Require(rejected, "snapshot accepted bad owner/schema"); Require(!s.Repository.Save(s.Candidate, s.Target), "Save accepted bad owner/schema");
            Require(targetBefore == Stamp(s.Target) && backupBefore == Stamp(s.Backup), "rejected save changed target/backup");
            Require(setting.ReferenceExpression == expression.ToJsonString(), "validation modified source document");
        });
        Case("readable-target-deny-delete", () =>
        {
            var s = Setup("target-locked"); var before = Stamp(s.Target); var backup = Stamp(s.Backup); s.Candidate.Name = "must-not-commit";
            using (var held = new FileStream(s.Target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            { Require(File.ReadAllText(s.Target).Length > 0, "target must be readable"); Require(!s.Repository.Save(s.Candidate, s.Target), "locked target save succeeded"); }
            Require(before == Stamp(s.Target) && backup == Stamp(s.Backup), "target failure changed visible files");
        });
        Case("central-commit-failure-compensates-target", () =>
        {
            var s = Setup("backup-locked"); var before = Stamp(s.Target); var backup = Stamp(s.Backup); s.Candidate.Name = "must-rollback";
            using (var held = new FileStream(s.Backup, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                Require(!s.Repository.Save(s.Candidate, s.Target), "locked backup save succeeded");
            Require(before == Stamp(s.Target), "target rollback changed bytes/length/mtime"); Require(backup == Stamp(s.Backup), "failed backup changed bytes/length/mtime");
            Require(s.Repository.LoadCandidate(s.Target).Candidate!.Name == "existing-version", "rollback target rejected");
        });
        Case("cross-volume-success-and-central-lock", () =>
        {
            var s = Setup("cross-volume", true);
            Require(Path.GetPathRoot(s.Target) != Path.GetPathRoot(s.Backup), "fixture is not cross-volume");
            var previous = File.ReadAllBytes(s.Target); s.Candidate.Name = "cross-volume-success";
            Require(s.Repository.Save(s.Candidate, s.Target), s.Repository.LastError ?? "cross-volume failed");
            Require(previous.SequenceEqual(File.ReadAllBytes(s.Backup)), "cross-volume backup not actual prior target");
            var before = Stamp(s.Target); var backup = Stamp(s.Backup); s.Candidate.Name = "cross-volume-rollback";
            using (var held = new FileStream(s.Backup, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
                Require(!s.Repository.Save(s.Candidate, s.Target), "cross-volume locked backup accepted");
            Require(before == Stamp(s.Target) && backup == Stamp(s.Backup), "cross-volume compensation changed visible files");
        });
        Case("public-write-snapshot-frozen-json-and-bytes", () =>
        {
            var s = Setup("frozen"); s.Candidate.Name = "captured"; var captured = SolutionSnapshot.Prepare(s.Candidate, s.Target);
            s.Candidate.Name = "later-live-edit";
            var setting = s.Candidate.Workflows[0].Nodes.Single(n => n.Id == "filter-consumer").ParamSettings.GetSetting("FilterAreaMin")!;
            setting.ReferenceExpression = "invalid-live-edit";
            var result = s.Repository.WriteSnapshot(captured); Require(result.FileWritten, result.Error ?? "WriteSnapshot failed");
            Require(File.ReadAllText(s.Target) == result.WrittenSnapshot!.Json, "written bytes differ from WrittenSnapshot.Json");
            Require(result.WrittenSnapshot.CanonicalContent == result.WrittenSnapshot.Json, "canonical snapshot differs");
            Require(s.Repository.LoadCandidate(s.Target).Candidate!.Name == "captured", "read live document after capture");
            Require(s.Candidate.Name == "later-live-edit" && setting.ReferenceExpression == "invalid-live-edit", "source mutated");
        });
        Case("new-target-and-no-overwrite", () =>
        {
            var s = Setup("new-target"); var fresh = Path.Combine(Path.GetDirectoryName(s.Target)!, "new.solution");
            Require(s.Repository.Save(s.Candidate, fresh), "new target rejected"); var before = Stamp(fresh);
            Require(!s.Repository.Save(s.Candidate, fresh, false), "overwrite=false accepted"); Require(before == Stamp(fresh), "overwrite=false changed target");
        });
        Case("rollback-denied-explicit-recovery-signal", () =>
        {
            var s = Setup("rollback-denied");
            // A real large existing backup makes the central identity check observable to the
            // competing reader. No production hook or injected filesystem is used.
            File.WriteAllBytes(s.Backup, new byte[64 * 1024 * 1024]);
            var before = Stamp(s.Target); var backup = Stamp(s.Backup);
            const string marker = "compensation-lock-probe"; s.Candidate.Name = marker;
            using var heldSignal = new ManualResetEventSlim(); using var release = new ManualResetEventSlim();
            var competitor = Task.Run(() =>
            {
                var deadline = DateTime.UtcNow.AddSeconds(8);
                while (DateTime.UtcNow < deadline)
                {
                    try
                    {
                        if (File.ReadAllText(s.Target).Contains(marker, StringComparison.Ordinal))
                        {
                            using var targetLock = new FileStream(s.Target, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                            heldSignal.Set(); release.Wait(TimeSpan.FromSeconds(8)); return;
                        }
                    }
                    catch (IOException) { }
                    Thread.Sleep(1);
                }
            });
            SolutionSaveRecoveryRequiredException? signal = null;
            try
            {
                using var backupLock = new FileStream(s.Backup, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                try { _ = s.Repository.Save(s.Candidate, s.Target); }
                catch (SolutionSaveRecoveryRequiredException recovery) { signal = recovery; }
            }
            finally { release.Set(); competitor.GetAwaiter().GetResult(); }
            Require(heldSignal.IsSet && signal is not null, "real competing lock did not enter the compensation window");
            Require(File.Exists(signal!.LocalPreviousTargetPath), "only rollback file was deleted");
            Require(signal.AttemptedSnapshot is not null && signal.AttemptedSnapshot.Json.Contains(marker), "attempted immutable snapshot missing");
            Require(before != Stamp(s.Target) && backup == Stamp(s.Backup), "partial-write fixture state differs");
            File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "partial-recovery.json"), JsonSerializer.Serialize(new
            {
                signal.TargetPath, signal.LocalPreviousTargetPath, signal.CentralPreviousPath, signal.PendingPath,
                signal.ObservedTargetState, signal.RecoveryInstructions, before, after = Stamp(s.Target), backupBefore = backup, backupAfter = Stamp(s.Backup)
            }, new JsonSerializerOptions { WriteIndented = true }));
        });
        Case("format1-direct-save-owner-migration", () =>
        {
            var s = Setup("legacy-direct");
            var legacy = JsonNode.Parse(Baseline)!; legacy["SchemaVersion"] = 1;
            foreach (var workflow in legacy["Workflows"]!.AsArray()) foreach (var node in workflow!["Nodes"]!.AsArray())
            foreach (var setting in node!["ParamSettings"]!["Settings"]!.AsArray()) if (setting!["ReferenceExpression"] is JsonValue expression)
            { var reference = JsonNode.Parse(expression.GetValue<string>())!; reference["ContractMajor"] = 4; setting["ReferenceExpression"] = reference.ToJsonString(); }
            var source = Path.Combine(Root, "legacy-input.solution"); File.WriteAllText(source, legacy.ToJsonString()); var before = Stamp(source);
            var loaded = s.Repository.LoadCandidate(source).Candidate; Require(loaded is not null, "legacy load rejected");
            Require(s.Repository.Save(loaded!, s.Target), "migrated save failed: " + s.Repository.LastError);
            Require(before == Stamp(source), "legacy source changed"); Require(s.Repository.LoadCandidate(s.Target).Candidate!.SchemaVersion == 2, "format2 reopen failed");
        });
        File.WriteAllText(Path.Combine(AppContext.BaseDirectory, "report.json"), JsonSerializer.Serialize(new
        {
            processId = Environment.ProcessId, finishedUtc = DateTime.UtcNow, args,
            baselineSha256 = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(Baseline))),
            passed = Cases.Count - Failures, failed = Failures, root = Root, cases = Cases
        }, new JsonSerializerOptions { WriteIndented = true }));
        Console.WriteLine($"SUMMARY {Cases.Count - Failures} PASS / {Failures} FAIL"); return Failures == 0 ? 0 : 1;
    }
}
