using System;
using System.Collections.Immutable;
using System.Windows.Media;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis.Classification;

namespace SunEyeVision.Tool.Script.Views
{
    /// <summary>
    /// Roslyn 语义高亮着色器 — 从 _currentClassifications（RunDiagnosticAsync 更新）
    /// 获取精准的类型/方法/属性/字段/关键字等分类，覆盖 xshd 正则猜测的不足。
    ///
    /// Layer 2b（在 ErrorMarkerColorizer 之前 / 在 xshd 基础高亮之后）：
    /// 仅覆盖前景色，不改变背景色，与 ErrorMarkerColorizer 红色背景不冲突。
    ///
    /// 设计要点：
    /// - 只修改 VisualLineElement 的前景色（不涉及背景），与 ErrorMarker 互不干扰
    /// - 通过闭包延迟获取最新分类快照
    /// </summary>
    internal sealed class RoslynSemanticColorizer : DocumentColorizingTransformer
    {
        private readonly Func<ImmutableArray<ClassifiedSpan>> _getClassifications;

        // 缓存语义分类 → 前景色
        private static readonly SolidColorBrush ClassBrush = FrozenBrush("#4EC9B0");    // 类名 青
        private static readonly SolidColorBrush StructBrush = FrozenBrush("#4EC9B0");   // 结构 青
        private static readonly SolidColorBrush InterfaceBrush = FrozenBrush("#B8D7A3");// 接口 浅绿
        private static readonly SolidColorBrush EnumBrush = FrozenBrush("#B8D7A3");     // 枚举 浅绿
        private static readonly SolidColorBrush DelegateBrush = FrozenBrush("#4EC9B0"); // 委托 青
        private static readonly SolidColorBrush MethodBrush = FrozenBrush("#DCDCAA");   // 方法 米黄
        private static readonly SolidColorBrush PropertyBrush = FrozenBrush("#DCDCAA"); // 属性 米黄
        private static readonly SolidColorBrush FieldBrush = FrozenBrush("#D7BA7D");    // 字段 暗金
        private static readonly SolidColorBrush KeywordBrush = FrozenBrush("#569CD6");  // 关键字 蓝
        private static readonly SolidColorBrush StringBrush = FrozenBrush("#CE9178");   // 字符串 橙
        private static readonly SolidColorBrush NumberBrush = FrozenBrush("#B5CEA8");   // 数字 绿
        private static readonly SolidColorBrush IdentifierBrush = FrozenBrush("#D4D4D4");// 标识符 浅灰

        public RoslynSemanticColorizer(Func<ImmutableArray<ClassifiedSpan>> getClassifications)
        {
            _getClassifications = getClassifications;
        }

        protected override void ColorizeLine(DocumentLine line)
        {
            // 获取最新的分类快照（由 RunDiagnosticAsync 更新 _currentClassifications）
            var spans = _getClassifications();
            if (spans.IsDefaultOrEmpty) return;

            var lineStart = line.Offset;
            var lineEnd = line.EndOffset;

            foreach (var span in spans)
            {
                if (span.TextSpan.Start >= lineEnd) continue;
                if (span.TextSpan.End <= lineStart) continue;
                if (span.TextSpan.Length <= 0) continue;

                var start = Math.Max(span.TextSpan.Start, lineStart);
                var end = Math.Min(span.TextSpan.End, lineEnd);
                if (start >= end) continue;

                var brush = GetBrushForClassification(span.ClassificationType);
                if (brush == null) continue;

                ChangeLinePart(start, end, e =>
                {
                    if (e.TextRunProperties is VisualLineElementTextRunProperties tp)
                    {
                        // ★ 仅覆盖前景色，保留 xshd 背景色（ErrorMarker 红色背景）
                        tp.SetForegroundBrush(brush);
                    }
                });
            }
        }

        /// <summary>
        /// 根据 Roslyn 分类类型返回对应前景色
        /// </summary>
        private static SolidColorBrush? GetBrushForClassification(string classificationType)
        {
            return classificationType switch
            {
                ClassificationTypeNames.ClassName => ClassBrush,
                ClassificationTypeNames.StructName => StructBrush,
                ClassificationTypeNames.InterfaceName => InterfaceBrush,
                ClassificationTypeNames.EnumName => EnumBrush,
                ClassificationTypeNames.DelegateName => DelegateBrush,
                ClassificationTypeNames.MethodName => MethodBrush,
                ClassificationTypeNames.PropertyName => PropertyBrush,
                ClassificationTypeNames.FieldName => FieldBrush,
                ClassificationTypeNames.Keyword => KeywordBrush,
                ClassificationTypeNames.StringLiteral => StringBrush,
                ClassificationTypeNames.NumericLiteral => NumberBrush,
                ClassificationTypeNames.Identifier => IdentifierBrush,
                _ => null,  // 未覆盖的分类类型让 xshd 基础高亮处理
            };
        }

        private static SolidColorBrush FrozenBrush(string hexColor)
        {
            var color = (Color)ColorConverter.ConvertFromString(hexColor);
            var brush = new SolidColorBrush(color);
            if (brush.CanFreeze) brush.Freeze();
            return brush;
        }
    }
}
