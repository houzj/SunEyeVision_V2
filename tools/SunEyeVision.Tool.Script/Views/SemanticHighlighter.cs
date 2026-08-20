using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using SunEyeVision.Plugin.SDK.Logging;
// TextDocument 在 Microsoft.CodeAnalysis（Roslyn 文档）与 AvalonEdit（编辑器文档）中同名冲突，用别名消歧
using AvalonTextDocument = ICSharpCode.AvalonEdit.Document.TextDocument;

namespace SunEyeVision.Tool.Script.Views;

/// <summary>
/// Roslyn 语义高亮器（IHighlighter）：按需逐行渲染 + 防闪烁，对齐 RoslynPad-avalonedit RoslynSemanticHighlighter。
///
/// 核心机制：
///  1. AvalonEdit 只对可视行调 HighlightLine，命中行缓存（CompareAge==0 且长度相同）→ 直接复用旧 HighlightedLine（不闪烁）。
///  2. 未命中 → DoHighlightLine 先用旧 sections + offsetShift 平移上色（对齐参考：编辑后旧色保留对准），再入队异步重算。
///  3. 单 worker 串行队列（ConcurrentQueue + SemaphoreSlim）逐行 Classifier 分析，回 UI 线程更新 line.Sections。
/// 打字/Enter/删除全程无"颜色消失→恢复"空档。
///
/// AvalonEdit 6.3.1.120 适配：IHighlighter 较 5.0.2 新增 Document/DefaultTextColor/GetNamedColor/GetColorStack/
/// BeginHighlighting/EndHighlighting/UpdateHighlightingState 成员（参考项目未实现，此处补全）。
/// </summary>
internal sealed class RoslynSemanticHighlighter : IHighlighter
{
    private const int CacheSize = 512;
    private const int WarmupLineCount = 50;

    private readonly AvalonTextDocument _document;
    private readonly Func<Document?> _getUserDocument;
    private readonly ClassificationHighlightColors _highlightColors;
    private readonly List<CachedLine> _cachedLines;
    private readonly SemaphoreSlim _semaphore;
    private readonly ConcurrentQueue<HighlightedLine> _queue;
    private readonly SynchronizationContext? _syncContext;
    private CancellationTokenSource _cts;

    private bool _inHighlightingGroup;

    public RoslynSemanticHighlighter(AvalonTextDocument document, Func<Document?> getUserDocument, ClassificationHighlightColors highlightColors)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _getUserDocument = getUserDocument;
        _highlightColors = highlightColors;
        _semaphore = new SemaphoreSlim(0);
        _queue = new ConcurrentQueue<HighlightedLine>();
        _cachedLines = new List<CachedLine>();

        _syncContext = SynchronizationContext.Current;
        _cts = new CancellationTokenSource();
        var cancellationToken = _cts.Token;
        _ = Task.Run(() => Worker(cancellationToken), cancellationToken);

        // 预热不再在构造函数自动触发：此时 Roslyn workspace（含文档/引用）可能尚未就绪，
        // 语义分类会因 document 为 null 直接跳过而毫无效果。改为由外部在 RebuildWorkspace
        // 完成后显式调用 TriggerWarmup()（文档就绪时才能构建 SemanticModel）。
    }

    /// <summary>
    /// 触发语义模型预热：文档就绪后，后台对前 N 行预分类写 _cachedLines，并触发一次完整
    /// SemanticModel 构建。行进入可视区时 HighlightLine 命中缓存直接复用，避免"打开窗口后
    /// 逐行白→青渐变"。可重入：已预热/正在进行时调用直接返回。
    /// </summary>
    public void TriggerWarmup()
    {
        var cts = _cts;
        if (cts == null) return;
        if (Interlocked.Exchange(ref _warmupRequested, 1) != 0) return; // 幂等：进程内只预热一次
        _ = Task.Run(() => WarmupAsync(maxLines: 50, cts.Token), cts.Token);
    }

    private int _warmupRequested;

    public IDocument Document => _document;

    public event HighlightingStateChangedEventHandler HighlightingStateChanged;

    public HighlightedLine HighlightLine(int lineNumber)
    {
        var documentLine = _document.GetLineByNumber(lineNumber);
        var newVersion = _document.Version;
        CachedLine? cachedLine = null;
        if (_cachedLines.Count > 0)
        {
            for (var i = 0; i < _cachedLines.Count; i++)
            {
                var line = _cachedLines[i];
                if (line.DocumentLine != documentLine) continue;
                if (newVersion == null || !newVersion.BelongsToSameDocumentAs(line.OldVersion))
                {
                    // cannot list changes from old to new: remove cache
                    _cachedLines.RemoveAt(i);
                }
                else
                {
                    cachedLine = line;
                }
            }

            if (cachedLine != null && cachedLine.IsValid && newVersion.CompareAge(cachedLine.OldVersion) == 0 &&
                cachedLine.DocumentLine.Length == documentLine.Length)
            {
                // file hasn't changed since cache: reuse old highlighted line (no flicker)
                return cachedLine.HighlightedLine;
            }
        }

        var wasInHighlightingGroup = _inHighlightingGroup;
        if (!_inHighlightingGroup)
        {
            BeginHighlighting();
        }
        try
        {
            return DoHighlightLine(documentLine, cachedLine);
        }
        finally
        {
            if (!wasInHighlightingGroup)
                EndHighlighting();
        }
    }

    private HighlightedLine DoHighlightLine(IDocumentLine documentLine, CachedLine? previousCachedLine)
    {
        var line = new HighlightedLine(_document, documentLine);

        // 防闪烁：复用旧 sections + offsetShift 平移（对齐参考 DoHighlightLine:161-183）
        var previousHighlight = previousCachedLine?.HighlightedLine;
        if (previousHighlight != null && previousHighlight.Sections.Count > 0)
        {
            var offsetShift = documentLine.Offset - previousCachedLine!.Offset;

            foreach (var section in previousHighlight.Sections)
            {
                var offset = section.Offset + offsetShift;

                // stop if section starts after end of line
                if (offset >= documentLine.EndOffset)
                    break;

                // clamp section to not be longer than line
                line.Sections.Add(new HighlightedSection
                {
                    Color = section.Color,
                    Offset = offset,
                    Length = Math.Min(section.Length, documentLine.EndOffset - offset),
                });
            }
        }

        // since we don't want to block the UI thread, enqueue and process asynchronously
        EnqueueLine(line);

        CacheLine(line);
        return line;
    }

    private void EnqueueLine(HighlightedLine line)
    {
        _queue.Enqueue(line);
        _semaphore.Release();
    }

    /// <summary>
    /// 预热：后台对前 N 行预分类，结果写 _cachedLines。
    /// 这样字段/方法声明行在可视区外时已被缓存，行进入可视区时 HighlightLine 命中 CompareAge==0
    /// 直接返回已分类行，避免"初始打开时字段显示白色、等用户编辑触发补全后才变金色"问题。
    /// </summary>
    private async Task WarmupAsync(int maxLines, CancellationToken cancellationToken)
    {
        try
        {
            var document = _getUserDocument();
            if (document == null) return;

            // 先触发一次完整 SemanticModel 构建（首次几百 ms），确保后续逐行分类拿到语义结果
            // （否则前几行可能因模型未就绪只回语法分类）。语义分类用 document.GetSemanticModelAsync。
            try
            {
                await document.GetSemanticModelAsync(cancellationToken).ConfigureAwait(false);
            }
            catch { /* 构建失败则预热退化为普通分类，不阻塞 */ }
            if (cancellationToken.IsCancellationRequested) return;

            var lineCount = Math.Min(maxLines, _document.LineCount);
            for (int lineNumber = 1; lineNumber <= lineCount; lineNumber++)
            {
                if (cancellationToken.IsCancellationRequested) return;
                var documentLine = _document.GetLineByNumber(lineNumber);
                if (documentLine.TotalLength == 0) continue;

                IEnumerable<ClassifiedSpan> spans;
                try
                {
                    spans = await GetClassifiedSpansAsync(document, documentLine).ConfigureAwait(false);
                }
                catch { continue; }

                var highlightedLine = new HighlightedLine(_document, documentLine);
                foreach (var classifiedSpan in spans)
                {
                    if (IsOutsideLine(classifiedSpan, documentLine)) continue;
                    highlightedLine.Sections.Add(new HighlightedSection
                    {
                        Color = _highlightColors.GetBrush(classifiedSpan.ClassificationType),
                        Offset = classifiedSpan.TextSpan.Start,
                        Length = classifiedSpan.TextSpan.Length,
                    });
                }

                // 写 _cachedLines（行进入可视区时 HighlightLine 命中直接复用）
                if (_document.Version != null)
                {
                    _cachedLines.Add(new CachedLine(highlightedLine, _document.Version));
                    if (_cachedLines.Count > CacheSize)
                        _cachedLines.RemoveRange(0, CacheSize / 2);
                }
            }

            // 预热完成触发重绘（AvalonEdit 对受影响行重画，命中缓存显示分类色）
            HighlightingStateChanged?.Invoke(1, lineCount);
        }
        catch { /* 预热失败不阻塞主流程 */ }
    }

    private async Task Worker(CancellationToken cancellationToken)
    {
        while (true)
        {
            if (cancellationToken.IsCancellationRequested) return;
            try
            {
                await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) { return; }

            if (!_queue.TryDequeue(out var line)) continue;

            var document = _getUserDocument();
            // ★ 设计迁移（对齐参考项目时序）：colorizer 在 RebuildWorkspace 完成后才挂载，
            //   worker 处理行时 document 必已就绪，此分支按参考项目保持纯防御（正常永不触发）。
            if (document == null) continue;

            var documentLine = line.DocumentLine;
            IEnumerable<ClassifiedSpan> spans;
            try
            {
                spans = await GetClassifiedSpansAsync(document, documentLine).ConfigureAwait(false);
            }
            catch
            {
                continue;
            }

            // rebuild sections
            var sections = new List<HighlightedSection>();
            foreach (var classifiedSpan in spans)
            {
                if (IsOutsideLine(classifiedSpan, documentLine))
                    continue;
                sections.Add(new HighlightedSection
                {
                    Color = _highlightColors.GetBrush(classifiedSpan.ClassificationType),
                    Offset = classifiedSpan.TextSpan.Start,
                    Length = classifiedSpan.TextSpan.Length
                });
            }

            // post update on UI thread
            UpdateHighlightingSections(line, sections);
        }
    }

    private static bool IsOutsideLine(ClassifiedSpan classifiedSpan, IDocumentLine documentLine)
    {
        return classifiedSpan.TextSpan.Start < documentLine.Offset ||
               classifiedSpan.TextSpan.Start > documentLine.EndOffset ||
               classifiedSpan.TextSpan.End > documentLine.EndOffset;
    }

    private void UpdateHighlightingSections(HighlightedLine line, List<HighlightedSection> sections)
    {
        var lineNumber = line.DocumentLine.LineNumber;
        var sync = _syncContext;
        if (sync != null)
        {
            sync.Post(_ =>
            {
                line.Sections.Clear();
                foreach (var section in sections)
                    line.Sections.Add(section);
                HighlightingStateChanged?.Invoke(lineNumber, lineNumber);
            }, null);
        }
        else
        {
            line.Sections.Clear();
            foreach (var section in sections)
                line.Sections.Add(section);
            HighlightingStateChanged?.Invoke(lineNumber, lineNumber);
        }
    }

    private void CacheLine(HighlightedLine line)
    {
        if (_document.Version != null)
        {
            _cachedLines.Add(new CachedLine(line, _document.Version));

            // Clean cache once it gets too big
            if (_cachedLines.Count > CacheSize)
            {
                _cachedLines.RemoveRange(0, CacheSize / 2);
            }
        }
    }

    private async Task<IEnumerable<ClassifiedSpan>> GetClassifiedSpansAsync(Document document, IDocumentLine documentLine)
    {
        var text = await document.GetTextAsync().ConfigureAwait(false);
        if (text.Length >= documentLine.Offset + documentLine.TotalLength)
        {
            return await Classifier.GetClassifiedSpansAsync(document,
                new TextSpan(documentLine.Offset, documentLine.TotalLength), CancellationToken.None)
                .ConfigureAwait(false);
        }
        return Array.Empty<ClassifiedSpan>();
    }

    public void Dispose()
    {
        if (_cts != null)
        {
            _cts.Cancel();
            _cts.Dispose();
            _cts = null!;
        }
    }

    public HighlightingColor DefaultTextColor => _highlightColors.DefaultBrush;

    public void BeginHighlighting()
    {
        if (_inHighlightingGroup)
            throw new InvalidOperationException();
        _inHighlightingGroup = true;
    }

    public void EndHighlighting()
    {
        _inHighlightingGroup = false;
        // AvalonEdit 6.x：缓存清理可在文档版本变化时由 UpdateHighlightingState 驱动
    }

    public HighlightingColor GetNamedColor(string name) => null!;

    public IEnumerable<HighlightingColor> GetColorStack(int lineNumber) => Array.Empty<HighlightingColor>();

    public void UpdateHighlightingState(int lineNumber)
    {
        // 无状态高亮（基于 Roslyn 分类），无需维护状态栈
    }

    private class CachedLine
    {
        public readonly HighlightedLine HighlightedLine;
        public readonly ITextSourceVersion OldVersion;
        public readonly int Offset;
        public readonly bool IsValid;

        public IDocumentLine DocumentLine => HighlightedLine.DocumentLine;

        public CachedLine(HighlightedLine highlightedLine, ITextSourceVersion fileVersion)
        {
            HighlightedLine = highlightedLine ?? throw new ArgumentNullException(nameof(highlightedLine));
            OldVersion = fileVersion ?? throw new ArgumentNullException(nameof(fileVersion));
            IsValid = true;
            Offset = HighlightedLine.DocumentLine.Offset;
        }
    }
}

/// <summary>
/// HighlightingColorizer 派生：接入 RoslynSemanticHighlighter（对齐 RoslynPad RoslynHighlightingColorizer）。
/// CreateHighlighter(TextView, TextDocument) 签名与 AvalonEdit 5.0.2 一致，6.3.1.120 已验证。
/// </summary>
internal sealed class RoslynHighlightingColorizer : HighlightingColorizer
{
    private readonly AvalonTextDocument _document;
    private readonly Func<Document?> _getUserDocument;
    private readonly ClassificationHighlightColors _highlightColors;
    private RoslynSemanticHighlighter? _highlighter;

    public RoslynHighlightingColorizer(AvalonTextDocument document, Func<Document?> getUserDocument, ClassificationHighlightColors highlightColors)
    {
        _document = document;
        _getUserDocument = getUserDocument;
        _highlightColors = highlightColors;
    }

    protected override IHighlighter CreateHighlighter(TextView textView, AvalonTextDocument document)
    {
        var highlighter = new RoslynSemanticHighlighter(document, _getUserDocument, _highlightColors);
        _highlighter = highlighter;
        // ★ 设计迁移（对齐参考时序）：highlighter 由 AvalonEdit 在首次渲染时创建，此刻 workspace/文档
        //   必已就绪（colorizer 是 RebuildWorkspace 后才挂载的）。highlighter 一出生即触发语义模型预热
        //   （TriggerWarmup 幂等，文档就绪才有效），让可见行进入视野即命中缓存，无"白→青"渐变。
        highlighter.TriggerWarmup();
        return highlighter;
    }

    /// <summary>
    /// 转发语义模型预热调用（幂等）。仅当 highlighter 已创建时生效；若尚未创建（首次渲染前），
    /// 则 CreateHighlighter 已自动触发预热，此处调用为冗余但无害。
    /// </summary>
    public void Warmup() => _highlighter?.TriggerWarmup();
}

/// <summary>
/// 分类颜色映射：Roslyn ClassificationTypeNames → HighlightingColor/Brush（对齐参考 IClassificationHighlightColors）。
/// 语义着色：类型=青、方法/属性=紫；字段=基础色（对齐 Roslyn 语法分类的固有行为——
/// 声明处由语法分类直接给色、引用处依赖语义模型解析，两处行为不一致会导致"有的金有的白"，
/// 故字段不映射、统一落基础色，从根上消除变色感知）。
/// </summary>
internal sealed class ClassificationHighlightColors
{
    private readonly System.Windows.Media.Color _typeColor;
    private readonly System.Windows.Media.Color _methodColor;
    private readonly System.Windows.Media.Color _propertyColor;

    public ClassificationHighlightColors()
    {
        _typeColor = MakeColor("#267F99");       // 青（类型）
        _methodColor = MakeColor("#9B4F96");     // 紫（方法）
        _propertyColor = MakeColor("#9B4F96");   // 紫（属性）
    }

    public HighlightingColor DefaultBrush => null!;

    public HighlightingColor GetBrush(string classificationType)
    {
        var color = new HighlightingColor();
        switch (classificationType)
        {
            case ClassificationTypeNames.ClassName:
            case ClassificationTypeNames.StructName:
            case ClassificationTypeNames.InterfaceName:
            case ClassificationTypeNames.EnumName:
            case ClassificationTypeNames.DelegateName:
                color.Foreground = new SimpleHighlightingBrush(_typeColor);
                break;
            case ClassificationTypeNames.MethodName:
            case ClassificationTypeNames.ExtensionMethodName:
                color.Foreground = new SimpleHighlightingBrush(_methodColor);
                break;
            case ClassificationTypeNames.PropertyName:
                color.Foreground = new SimpleHighlightingBrush(_propertyColor);
                break;
        }
        return color;
    }

    private static System.Windows.Media.Color MakeColor(string hex)
        => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
}
