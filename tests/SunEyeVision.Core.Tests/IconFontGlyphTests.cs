using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Xunit;

namespace SunEyeVision.Core.Tests;

/// <summary>
/// 图标字体字形契约测试。
/// 防御：共享图标字体 IconFontFamily（Segoe UI Symbol）必须包含
/// ⚙(U+2699 BLACK GEAR) 和 ↩(U+21A9 LEFTWARDS ARROW WITH HOOK) 两个字形，
/// 用于子程序节点 ⚙ 设置按钮、↩ 返回按钮、顶部工具栏 ⚙ 按钮。
/// 若未来有人把 IconFontFamily 换成不含这些字形的字体，
/// 此处立即报错，避免"⚙ 不可见"类问题静默回归。
/// </summary>
public class IconFontGlyphTests
{
    private const string IconFontFamilyName = "Segoe UI Symbol";

    // 与 UI 层 IconFontFamily 资源保持一致（见 src/UI/Resources/Styles/CommonStyleResources.xaml）
    private const char GearGlyph = '\u2699';
    private const char BackArrowGlyph = '\u21A9';

    /// <summary>
    /// 契约：IconFontFamily 首选字体必须能解析为 GlyphTypeface（字体可用）。
    /// </summary>
    [Fact]
    public void IconFontFamily_Must_Resolve_To_Typeface()
    {
        var typeface = new Typeface(new FontFamily(IconFontFamilyName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        Assert.True(typeface.TryGetGlyphTypeface(out _),
            $"图标字体 '{IconFontFamilyName}' 无法解析为 GlyphTypeface，字体不可用。");
    }

    /// <summary>
    /// 契约：⚙(U+2699) 字形必须存在，否则子程序设置按钮渲染为空白。
    /// </summary>
    [Fact]
    public void IconFontFamily_Must_Contain_GearGlyph()
    {
        var typeface = new Typeface(new FontFamily(IconFontFamilyName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        Assert.True(typeface.TryGetGlyphTypeface(out var glyph),
            $"图标字体 '{IconFontFamilyName}' 无法解析为 GlyphTypeface。");

        bool hasGear = glyph.CharacterToGlyphMap.TryGetValue(GearGlyph, out ushort gearIdx) && gearIdx > 0;

        Assert.True(hasGear,
            $"图标字体 '{IconFontFamilyName}' 不含 ⚙(U+2699) 字形，子程序设置按钮将不可见。请改用含该字形的字体资源。");
    }

    /// <summary>
    /// 契约：↩(U+21A9) 字形必须存在，否则子程序返回按钮渲染为空白。
    /// </summary>
    [Fact]
    public void IconFontFamily_Must_Contain_BackArrowGlyph()
    {
        var typeface = new Typeface(new FontFamily(IconFontFamilyName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        Assert.True(typeface.TryGetGlyphTypeface(out var glyph),
            $"图标字体 '{IconFontFamilyName}' 无法解析为 GlyphTypeface。");

        bool hasBackArrow = glyph.CharacterToGlyphMap.TryGetValue(BackArrowGlyph, out ushort arrowIdx) && arrowIdx > 0;

        Assert.True(hasBackArrow,
            $"图标字体 '{IconFontFamilyName}' 不含 ↩(U+21A9) 字形，子程序返回按钮将不可见。请改用含该字形的字体资源。");
    }

    /// <summary>
    /// 决定性契约：⚙ 在 IconFontFamily 下必须能测量出非零渲染宽度。
    /// 即使 CharacterToGlyphMap 含字形，若 WPF 实际测量宽度为 0，
    /// 按钮内容宽度仍为 0 → ActualWidth=0 → 按钮不可见。
    /// 这直接对应线上 bug 探针报错 pixelWidth=0 的根因。
    /// </summary>
    [Fact]
    public void IconFontFamily_GearGlyph_Must_Have_NonZero_Measured_Width()
    {
        var typeface = new Typeface(new FontFamily(IconFontFamilyName), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

        var ftGear = new FormattedText("\u2699", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeface, 14, Brushes.Black, 96);
        var ftA = new FormattedText("A", CultureInfo.InvariantCulture, FlowDirection.LeftToRight,
            typeface, 14, Brushes.Black, 96);

        Assert.True(ftGear.WidthIncludingTrailingWhitespace > 0,
            $"图标字体 '{IconFontFamilyName}' 下 ⚙(U+2699) 测量宽度=0（参考 'A' 宽度={ftA.WidthIncludingTrailingWhitespace:F2}）。" +
            "说明该字体即使含字形映射，WPF 也无法渲染出内容宽度，按钮尺寸将为 0 而不可见。请换用能渲染该字形的字体。");
    }
}
