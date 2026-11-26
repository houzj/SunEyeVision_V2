namespace SunEyeVision.Tool.ColorExtract.Runtime;

/// <summary>
/// 原厂 <c>IMVSColorExtractModuDisplay.xml</c> 的四个 <c>ImageViewConfig</c> 分组身份。
/// 显示行（可见/颜色/透明度/线宽）由 <see cref="ColorExtractManifest"/> 声明并生成，
/// 这里只保留被编辑期显示装配引用的组 id。
/// </summary>
public static class ColorExtractResultDisplay
{
    public const string CheckZone = "display.check-zone";
    public const string FixtureInitPoint = "display.fixture-init-point";
    public const string FixtureRunPoint = "display.fixture-run-point";
    public const string CheckZoneAnnular = "display.check-zone-annular";
}
