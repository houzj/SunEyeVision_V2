namespace SunEyeVision.Infrastructure.Persistence.ApplicationData;

/// <summary>Owned application preferences, logs and default save directory, independent of solution execution.</summary>
/// <remarks>
/// 数据根来自程序目录 <c>App.ini</c> 的 <c>[Data] Root</c>；未配置时使用按用户缺省根。
/// 配置存在但读不出、或值非法时直接抛明确异常，不退回缺省根。
/// </remarks>
public sealed class ApplicationDataPaths
{
    /// <summary>程序目录中承载数据根配置的文件名。</summary>
    public const string ConfigurationFileName = "App.ini";
    public const string ConfigurationSection = "Data";
    public const string ConfigurationKey = "Root";

    public ApplicationDataPaths(string root) : this(root, "调用方指定") { }
    private ApplicationDataPaths(string root, string rootSource)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(root);
        Root = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root));
        RootSource = rootSource;
    }

    public string Root { get; }
    /// <summary>数据根的来源说明，用于启动日志如实记录。</summary>
    public string RootSource { get; }
    public string ConfigFile => Path.Combine(Root, "Config", "start-page.json");
    public string SolutionsDirectory => Path.Combine(Root, "Solutions");
    public string LogDirectory => Path.Combine(Root, "Logs");

    /// <summary>按用户缺省数据根：<c>%LocalAppData%\SunEyeVision</c>。</summary>
    public static string DefaultRoot
    {
        get
        {
            var local = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            if (string.IsNullOrWhiteSpace(local))
                throw new InvalidOperationException("当前用户没有可用的本地应用数据目录，无法确定缺省数据根。");
            return Path.Combine(local, "SunEyeVision");
        }
    }

    /// <summary>打开调用方明确指定的数据根（隔离启动、测试探针），并建立必要子目录。</summary>
    public static ApplicationDataPaths Open(string root, string source = "调用方指定")
    {
        var paths = new ApplicationDataPaths(root, source);
        paths.EnsureDirectories();
        return paths;
    }

    /// <summary>
    /// 读取程序目录 <c>App.ini</c> 的 <c>[Data] Root</c> 并打开该数据根。
    /// 文件缺失或未配置该键时使用缺省根并在来源中说明；读不出或值非法直接抛异常。
    /// </summary>
    public static ApplicationDataPaths OpenConfigured()
        => OpenConfigured(Path.Combine(AppContext.BaseDirectory, ConfigurationFileName), DefaultRoot);

    /// <summary>
    /// 读取指定配置文件并打开数据根。语义与无参版本一致：文件缺失或未配置该键用 <paramref name="defaultRoot"/>，
    /// 读不出或值非法直接抛异常，不回退。
    /// </summary>
    public static ApplicationDataPaths OpenConfigured(string configurationFile, string defaultRoot)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(configurationFile);
        var fileName = Path.GetFileName(configurationFile);
        if (!File.Exists(configurationFile))
            return Open(defaultRoot, $"缺省根（程序目录没有 {fileName}）");
        string[] lines;
        try { lines = File.ReadAllLines(configurationFile); }
        catch (Exception failure) when (failure is IOException or UnauthorizedAccessException)
        { throw new IOException($"无法读取程序目录配置：{configurationFile}", failure); }
        var configured = ReadConfiguredRoot(lines, configurationFile);
        return configured is null
            ? Open(defaultRoot, $"缺省根（{fileName} 未配置 [{ConfigurationSection}] {ConfigurationKey}）")
            : Open(ValidateRoot(configured, configurationFile), $"{fileName} [{ConfigurationSection}] {ConfigurationKey}");
    }

    /// <summary>建立数据根及其子目录，并立即验证根可写。</summary>
    public void EnsureDirectories()
    {
        Directory.CreateDirectory(Root);
        Directory.CreateDirectory(Path.GetDirectoryName(ConfigFile)!);
        Directory.CreateDirectory(SolutionsDirectory);
        Directory.CreateDirectory(LogDirectory);
        // 只读根在启动时如实失败，不能等到首次保存或写日志才暴露。
        var probe = Path.Combine(Root, ".suneyevision-write-" + Guid.NewGuid().ToString("N"));
        using var stream = new FileStream(probe, FileMode.CreateNew, FileAccess.Write, FileShare.None, 1, FileOptions.DeleteOnClose);
    }

    private static string? ReadConfiguredRoot(string[] lines, string file)
    {
        string? value = null;
        var inSection = false;
        for (var index = 0; index < lines.Length; index++)
        {
            var line = lines[index].Trim();
            if (line.Length == 0 || line[0] is ';' or '#') continue;
            if (line[0] == '[')
            {
                if (!line.EndsWith(']'))
                    throw new InvalidDataException($"{file} 第 {index + 1} 行段名格式无效：{line}");
                inSection = string.Equals(line[1..^1].Trim(), ConfigurationSection, StringComparison.OrdinalIgnoreCase);
                continue;
            }
            var separator = line.IndexOf('=');
            if (separator <= 0)
                throw new InvalidDataException($"{file} 第 {index + 1} 行不是 key=value：{line}");
            if (!inSection || !string.Equals(line[..separator].Trim(), ConfigurationKey, StringComparison.OrdinalIgnoreCase)) continue;
            if (value is not null)
                throw new InvalidDataException($"{file} 第 {index + 1} 行重复配置 [{ConfigurationSection}] {ConfigurationKey}。");
            value = line[(separator + 1)..].Trim();
        }
        return value;
    }

    private static string ValidateRoot(string value, string file)
    {
        if (value.Length == 0)
            throw new InvalidDataException($"{file} 的 [{ConfigurationSection}] {ConfigurationKey} 为空。");
        if (!Path.IsPathFullyQualified(value))
            throw new InvalidDataException($"{file} 的 [{ConfigurationSection}] {ConfigurationKey} 必须是绝对路径：{value}");
        var volume = Path.GetPathRoot(Path.GetFullPath(value));
        if (string.IsNullOrEmpty(volume) || !Directory.Exists(volume))
            throw new InvalidDataException($"{file} 的 [{ConfigurationSection}] {ConfigurationKey} 所在盘符或共享不存在：{value}");
        return value;
    }
}
