using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using SunEyeVision.Workflow;

namespace SunEyeVision.UI.Services.Startup;

/// <summary>显式隔离根仅用于受控启动验收；不重定向用户配置、不修改进程环境变量。</summary>
public sealed class StartupLaunchContext
{
    public string? SolutionPath { get; private init; }
    public string? IsolatedRoot { get; private init; }
    public bool IsIsolated => IsolatedRoot != null;
    public static StartupLaunchContext Current { get; internal set; } = new();
    public static StartupLaunchContext Parse(string[] args)
    {
        string? root = null;
        int at = Array.IndexOf(args, "--startup-test-root");
        if (at >= 0)
        {
            if (at + 1 >= args.Length) throw new ArgumentException("缺少隔离验收目录。");
            root = System.IO.Path.GetFullPath(args[at + 1]);
            if (!File.Exists(System.IO.Path.Combine(root, ".suneye-startup-test")))
                throw new InvalidDataException("隔离验收目录必须有 .suneye-startup-test 标记。");
        }
        var files = args.Where((s,i) => i != at + 1 || at < 0)
            .Where(x => x.EndsWith(".solution", StringComparison.OrdinalIgnoreCase)).ToArray();
        if (files.Length > 1) throw new ArgumentException("一次只能打开一个方案文件。");
        var result = new StartupLaunchContext { SolutionPath = files.FirstOrDefault(), IsolatedRoot = root };
        if (result.SolutionPath != null) result.ValidatePath(result.SolutionPath);
        return result;
    }
    public string? InstanceIdentity => IsIsolated ? SingleInstanceCoordinator.DefaultIdentity + ".StartupTest." +
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(IsolatedRoot!.ToUpperInvariant())))[..16] : null;
    public void ValidatePath(string path)
    {
        if (!IsIsolated) return;
        var full = System.IO.Path.GetFullPath(path);
        if (!full.StartsWith(IsolatedRoot!.TrimEnd(System.IO.Path.DirectorySeparatorChar) + System.IO.Path.DirectorySeparatorChar, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException("隔离验收只能打开专属目录内的方案。");
    }
    public void ValidateCandidate(Solution solution)
    {
        if (IsIsolated && (solution.Devices.Count != 0 || solution.Communications.Count != 0))
            throw new InvalidDataException("隔离启动验收拒绝包含设备或通信配置的方案。");
    }
}
