using System;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Text;
using TextChangeEventArgs = Microsoft.CodeAnalysis.Text.TextChangeEventArgs;

namespace SunEyeVision.Tool.Script.Views
{
    /// <summary>
    /// AvalonEdit 文档 ↔ Roslyn SourceText 增量桥接（对齐 RoslynPad AvalonEditTextContainer）。
    /// 实现 SourceTextContainer，经 ScriptWorkspace.OpenDocument 注册为 workspace 文档的增量文本通道。
    /// 监听 AvalonEdit Document.Changed，用增量 TextChangeRange 更新 _currentText 并触发 TextChanged，
    /// Roslyn 据此只重解析受影响语法树区域（复用未变节点/编译/语义模型），消除连续打字卡顿。
    /// </summary>
    public sealed class AvalonEditTextContainer : SourceTextContainer, IDisposable
    {
        private SourceText _currentText;

        public ICSharpCode.AvalonEdit.Document.TextDocument Document { get; }

        public override SourceText CurrentText => _currentText;

        public AvalonEditTextContainer(ICSharpCode.AvalonEdit.Document.TextDocument document)
        {
            Document = document ?? throw new ArgumentNullException(nameof(document));
            _currentText = SourceText.From(Document.Text);
            Document.Changed += DocumentOnChanged;
        }

        public void Dispose()
        {
            Document.Changed -= DocumentOnChanged;
        }

        /// <summary>
        /// AvalonEdit 文本增量变化 → 更新 _currentText + 触发 TextChanged（workspace 自动增量同步）。
        /// </summary>
        private void DocumentOnChanged(object sender, DocumentChangeEventArgs e)
        {
            var oldText = _currentText;
            var textSpan = new TextSpan(e.Offset, e.RemovalLength);
            var textChangeRange = new TextChangeRange(textSpan, e.InsertionLength);
            _currentText = _currentText.WithChanges(
                new TextChange(textSpan, e.InsertedText?.Text ?? string.Empty));

            TextChanged?.Invoke(this, new TextChangeEventArgs(oldText, _currentText, textChangeRange));
        }

        public override event EventHandler<TextChangeEventArgs> TextChanged;
    }
}
