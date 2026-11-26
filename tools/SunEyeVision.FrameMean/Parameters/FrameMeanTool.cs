using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Manifest;

namespace SunEyeVision.Tool.FrameMean.Runtime;

/// <summary>
/// 帧均值 Runtime 入口（原厂 <c>IMVSFrameMeanModuVA</c>）。
/// 声明与契约生成全部由 <see cref="ToolDefinition{TSettings,TOutput}"/> 承担，
/// 本类只保留：声明来源、算法会话与运行期初始化。
/// </summary>
public sealed class FrameMeanTool : ToolDefinition<FrameMeanSettings, FrameMeanOutput>, IToolRuntimeInitialization
{
    public const string Id = "FrameMean";
    public const string ImageInput = "input.image";

    protected override ToolManifest Manifest => FrameMeanManifest.Value;

    /// <summary>执行 Schema：描述 = 输入 + 参数，供宿主装配。</summary>
    public static ToolSchema Schema => new FrameMeanTool().Describe();

    public override IToolSession<FrameMeanSettings, FrameMeanOutput> CreateSession() => new FrameMeanSession();

    public override IReadOnlyList<Diagnostic> Validate(FrameMeanSettings settings)
    {
        ArgumentNullException.ThrowIfNull(settings);
        return settings.Validate();
    }

    public override ToolCapabilities Capabilities() => new(new[] { "halcon" });

    public void InitializeRuntime() => HalconPlatform.Initialise();
}
