using System.Windows.Media;
using SunEyeVision.Tool.Script.Resources;

namespace SunEyeVision.Tool.Script.Completion
{
    /// <summary>
    /// 把 Glyph 枚举渲染为 ImageSource（从 Glyphs.xaml 资源字典取 DrawingImage）。
    /// 对齐 RoslynPad GlyphExtensions，用单例资源服务缓存。
    /// </summary>
    internal static class GlyphExtensions
    {
        private static readonly GlyphService _service = new GlyphService();

        public static ImageSource ToImageSource(this Glyph glyph) => _service.GetGlyphImage(glyph) as ImageSource;

        private class GlyphService
        {
            private readonly Glyphs _glyphs;

            public GlyphService()
            {
                _glyphs = new Glyphs();
            }

            public object GetGlyphImage(Glyph glyph) => _glyphs[glyph] as ImageSource;
        }
    }
}
