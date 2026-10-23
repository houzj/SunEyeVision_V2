using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Classification;
using Microsoft.CodeAnalysis.Text;
using AvalonTextDocument = ICSharpCode.AvalonEdit.Document.TextDocument;

namespace CommonControl;

/// <summary>UI捕获行/版本，后台只读Roslyn快照；有限队列，首屏优先，不预填全文。</summary>
internal sealed class RoslynSemanticHighlighter : IHighlighter
{
    private const int CacheSize = 512;
    private const int WarmupLineCount = 50;
    private const int QueueCapacity = 128;
    private readonly AvalonTextDocument _document;
    private readonly Func<Document?> _getUserDocument;
    private readonly ClassificationHighlightColors _highlightColors;
    private readonly ScriptAnalysisScheduler? _scheduler;
    private readonly Dispatcher _dispatcher = Dispatcher.CurrentDispatcher;
    private readonly Dictionary<int, CachedLine> _cachedLines = new();
    private readonly ConcurrentQueue<Work> _queue = new();
    private readonly SemaphoreSlim _signal = new(0);
    private readonly CancellationTokenSource _cts = new();
    private readonly CancellationToken _token;
    private TaskCompletionSource _warmupTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
    private ITextSourceVersion? _readyVersion;
    private int _warmupStarted;
    private bool _inHighlightingGroup;
    private bool _disposed;
    private sealed record LineRange(int Offset, int TotalLength);
    private sealed record Work(CachedLine Cache, Document Document, ITextSourceVersion Version, LineRange Range, int Number);
    private sealed class CachedLine(HighlightedLine line, ITextSourceVersion version, object solution)
    {
        public HighlightedLine Line = line;
        public ITextSourceVersion Version = version;
        public object Solution = solution;
        public bool IsValid;
        public bool Queued;
        public TaskCompletionSource Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
    public RoslynSemanticHighlighter(AvalonTextDocument document, Func<Document?> getUserDocument,
        ClassificationHighlightColors highlightColors, ScriptAnalysisScheduler? scheduler = null)
    {
        _document = document; _getUserDocument = getUserDocument; _highlightColors = highlightColors; _scheduler = scheduler;
        _token = _cts.Token;
        _ = Task.Run(Worker);
    }
    public IDocument Document => _document;
    public event HighlightingStateChangedEventHandler? HighlightingStateChanged;
    public Task WarmupCompletion => _warmupTcs.Task;
    private bool _ready;
    public bool IsReady => _ready && _readyVersion != null && _document.Version.CompareAge(_readyVersion) == 0;
    public HighlightingColor DefaultTextColor => _highlightColors.DefaultBrush;
    public void TriggerWarmup()
    {
        if (_disposed || Interlocked.Exchange(ref _warmupStarted, 1) != 0) return;
        _ = WarmupAsync();
    }
    private async Task WarmupAsync()
    {
        try
        {
            while (!_token.IsCancellationRequested)
            {
                try
                {
                    var document = await _dispatcher.InvokeAsync(_getUserDocument);
                    if (document == null) return;
                    if (_scheduler != null) await _scheduler.RunAsync(false, ct => document.GetSemanticModelAsync(ct), _token).ConfigureAwait(false);
                    else await document.GetSemanticModelAsync(_token).ConfigureAwait(false);
                    var lines = await _dispatcher.InvokeAsync(() =>
                    {
                        if (_disposed || !ReferenceEquals(_getUserDocument()?.Project.Solution, document.Project.Solution))
                            throw new OperationCanceledException();
                        var tasks = new List<Task>();
                        for (var n = 1; n <= Math.Min(WarmupLineCount, _document.LineCount); n++)
                        {
                            HighlightLine(n);
                            if (_cachedLines.TryGetValue(n, out var cached)) tasks.Add(cached.Completion.Task);
                        }
                        return tasks.ToArray();
                    });
                    await Task.WhenAll(lines).WaitAsync(_token).ConfigureAwait(false);
                    var ready = await _dispatcher.InvokeAsync(() =>
                    {
                        _ready = !_disposed && ReferenceEquals(_getUserDocument()?.Project.Solution, document.Project.Solution)
                            && _cachedLines.Where(p => p.Key <= WarmupLineCount).All(p => p.Value.IsValid);
                        if (_ready) _readyVersion = _document.Version;
                        return IsReady;
                    });
                    if (ready) return;
                }
                catch (OperationCanceledException) when (!_token.IsCancellationRequested)
                { await Task.Delay(20, _token).ConfigureAwait(false); }
            }
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { _warmupTcs.TrySetException(ex); }
        finally { _warmupTcs.TrySetResult(); }
    }    public HighlightedLine HighlightLine(int lineNumber)
    {
        _dispatcher.VerifyAccess();
        var documentLine = _document.GetLineByNumber(lineNumber);
        var version = _document.Version;
        if (!_disposed && !IsReady && _warmupTcs.Task.IsCompleted)
        {
            _warmupTcs = new(TaskCreationOptions.RunContinuationsAsynchronously);
            Interlocked.Exchange(ref _warmupStarted, 0);
            TriggerWarmup();
        }
        var document = _getUserDocument();
        var solution = document?.Project.Solution;
        _cachedLines.TryGetValue(lineNumber, out var previous);
        if (previous != null && ReferenceEquals(previous.Solution, solution) && version.CompareAge(previous.Version) == 0)
        {
            if (!previous.IsValid && !previous.Queued && document != null) Enqueue(previous, document, version, lineNumber, documentLine);
            return previous.Line;
        }
        var line = new HighlightedLine(_document, documentLine);
        var previousHighlight = previous?.Line;
        if (previousHighlight != null)
        {
            var offsetShift = documentLine.Offset - previousHighlight.DocumentLine.Offset;
            foreach (var section in previousHighlight.Sections)
            {
                var offset = section.Offset + offsetShift;
                if (offset >= documentLine.Offset && offset < documentLine.EndOffset)
                    line.Sections.Add(new HighlightedSection { Color = section.Color, Offset = offset,
                        Length = Math.Min(section.Length, documentLine.EndOffset - offset) });
            }
            previous!.Completion.TrySetCanceled();
        }
        var cache = new CachedLine(line, version, solution!);
        _cachedLines[lineNumber] = cache;
        if (_cachedLines.Count > CacheSize)
            foreach (var key in _cachedLines.Keys.Where(k => k != lineNumber).Take(CacheSize / 2).ToArray())
            { _cachedLines[key].Completion.TrySetCanceled(); _cachedLines.Remove(key); }
        if (document != null && !_disposed) Enqueue(cache, document, version, lineNumber, documentLine);
        else cache.Completion.TrySetResult();
        return line;
    }
    private void Enqueue(CachedLine cache, Document document, ITextSourceVersion version, int number, IDocumentLine line)
    {
        if (cache.Completion.Task.IsCompleted && !cache.IsValid) cache.Completion = new(TaskCreationOptions.RunContinuationsAsynchronously);
        if (_queue.Count >= QueueCapacity) { cache.Completion.TrySetCanceled(); return; }
        cache.Queued = true;
        _queue.Enqueue(new(cache, document, version, new(line.Offset, line.TotalLength), number));
        _signal.Release();
    }
    private async Task Worker()
    {
        while (!_token.IsCancellationRequested)
        {
            try { await _signal.WaitAsync(_token).ConfigureAwait(false); }
            catch (OperationCanceledException) { return; }
            if (!_queue.TryDequeue(out var work)) continue;
            try
            {
                var current = await _dispatcher.InvokeAsync(() => IsCurrent(work));
                if (!current) { work.Cache.Completion.TrySetCanceled(); continue; }
                var spans = await GetClassifiedSpansAsync(work.Document, work.Range, _token).ConfigureAwait(false);
                await _dispatcher.InvokeAsync(() =>
                {
                    work.Cache.Queued = false;
                    if (!IsCurrent(work)) { work.Cache.Completion.TrySetCanceled(); return; }
                    var line = work.Cache.Line;
                    line.Sections.Clear();
                    foreach (var span in spans)
                    {
                        if (span.TextSpan.Start < work.Range.Offset || span.TextSpan.End > work.Range.Offset + work.Range.TotalLength) continue;
                        line.Sections.Add(new HighlightedSection { Color = _highlightColors.GetBrush(span.ClassificationType),
                            Offset = span.TextSpan.Start, Length = span.TextSpan.Length });
                    }
                    work.Cache.IsValid = true;
                    work.Cache.Completion.TrySetResult();
                    HighlightingStateChanged?.Invoke(work.Number, work.Number);
                });
            }
            catch (OperationCanceledException)
            {
                if (_token.IsCancellationRequested) return;
                await Task.Delay(15, _token).ConfigureAwait(false);
                await _dispatcher.InvokeAsync(() =>
                {
                    work.Cache.Queued = false;
                    if (IsCurrent(work)) Enqueue(work.Cache, work.Document, work.Version, work.Number, _document.GetLineByNumber(work.Number));
                    else work.Cache.Completion.TrySetCanceled();
                });
            }
            catch (Exception ex) { work.Cache.Completion.TrySetException(ex); }
        }
    }
    private bool IsCurrent(Work work) => !_disposed && _cachedLines.TryGetValue(work.Number, out var current)
        && ReferenceEquals(current, work.Cache) && _document.Version.CompareAge(work.Version) == 0
        && ReferenceEquals(_getUserDocument()?.Project.Solution, work.Document.Project.Solution);
    private async Task<IEnumerable<ClassifiedSpan>> GetClassifiedSpansAsync(Document document, LineRange documentLine, CancellationToken token)
    {
        var text = await document.GetTextAsync(token).ConfigureAwait(false);
        if (documentLine.Offset + documentLine.TotalLength > text.Length) return Array.Empty<ClassifiedSpan>();
        return _scheduler != null
            ? await _scheduler.RunAsync(false, ct => Classifier.GetClassifiedSpansAsync(document,
                new TextSpan(documentLine.Offset, documentLine.TotalLength), ct), token).ConfigureAwait(false)
            : await Classifier.GetClassifiedSpansAsync(document, new TextSpan(documentLine.Offset, documentLine.TotalLength), token).ConfigureAwait(false);
    }
    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true; _cts.Cancel();
        foreach (var cached in _cachedLines.Values) cached.Completion.TrySetCanceled();
        _warmupTcs.TrySetResult();
    }
    public void BeginHighlighting() { if (_inHighlightingGroup) throw new InvalidOperationException(); _inHighlightingGroup = true; }
    public void EndHighlighting() => _inHighlightingGroup = false;
    public HighlightingColor GetNamedColor(string name) => null!;
    public IEnumerable<HighlightingColor> GetColorStack(int lineNumber) => Array.Empty<HighlightingColor>();
    public void UpdateHighlightingState(int lineNumber) { }
}

internal sealed class RoslynHighlightingColorizer : HighlightingColorizer
{
    private readonly Func<Document?> _getUserDocument;
    private readonly ClassificationHighlightColors _colors;
    private readonly ScriptAnalysisScheduler? _scheduler;
    private RoslynSemanticHighlighter? _highlighter;
    public RoslynHighlightingColorizer(AvalonTextDocument document, Func<Document?> getUserDocument,
        ClassificationHighlightColors colors, ScriptAnalysisScheduler? scheduler = null)
    { _getUserDocument = getUserDocument; _colors = colors; _scheduler = scheduler; }
    protected override IHighlighter CreateHighlighter(TextView textView, AvalonTextDocument document)
    {
        _highlighter = new RoslynSemanticHighlighter(document, _getUserDocument, _colors, _scheduler);
        _highlighter.TriggerWarmup();
        return _highlighter;
    }
    public void Release() => _highlighter?.Dispose();
    public void Warmup() => _highlighter?.TriggerWarmup();
    public Task WarmupCompletion => _highlighter?.WarmupCompletion ?? Task.CompletedTask;
    public bool IsReady => _highlighter?.IsReady == true;
}
internal sealed class ClassificationHighlightColors
{
    private readonly bool _isDark;
    private readonly System.Windows.Media.Color _keywordColor;
    private readonly System.Windows.Media.Color _typeColor;
    private readonly System.Windows.Media.Color _methodColor;
    private readonly System.Windows.Media.Color _propertyColor;
    private readonly System.Windows.Media.Color _fieldLocalColor;
    private readonly System.Windows.Media.Color _stringColor;
    private readonly System.Windows.Media.Color _commentColor;
    private readonly System.Windows.Media.Color _numberColor;
    private readonly System.Windows.Media.Color _identifierColor;
    private readonly System.Windows.Media.Color _preprocessorColor;

    public ClassificationHighlightColors(bool isDarkTheme)
    {
        _isDark = isDarkTheme;
        if (isDarkTheme)
        {
            _keywordColor = MakeColor("#569CD6");       // 关键字：蓝（VS Dark）
            _typeColor = MakeColor("#4EC9B0");          // 类型：蓝绿/青（VS Dark）
            _methodColor = MakeColor("#DCDCAA");        // 方法：黄（VS Dark）
            _propertyColor = MakeColor("#DCDCAA");      // 属性：黄（VS Dark）
            _fieldLocalColor = MakeColor("#9CDCFE");    // 字段/局部/参数/常量：浅蓝（VS Dark）
            _stringColor = MakeColor("#CE9178");        // 字符串：橙（VS Dark）
            _commentColor = MakeColor("#6A9955");       // 注释：绿（VS Dark）
            _numberColor = MakeColor("#B5CEA8");        // 数字：浅绿灰（VS Dark）
            _identifierColor = MakeColor("#D4D4D4");    // 普通标识符：默认文本色（VS Dark）
            _preprocessorColor = MakeColor("#9A9A9A");  // 预处理指令：灰（VS Dark）
        }
        else
        {
            _keywordColor = MakeColor("#0000FF");       // 关键字：蓝（VS Light）
            _typeColor = MakeColor("#267F99");          // 类型：蓝青（VS Light）
            _methodColor = MakeColor("#74531F");        // 方法：棕（VS Light）
            _propertyColor = MakeColor("#74531F");      // 属性：棕（VS Light）
            _fieldLocalColor = MakeColor("#001080");    // 字段/局部/参数/常量：深蓝（VS Light）
            _stringColor = MakeColor("#A31515");        // 字符串：深红（VS Light）
            _commentColor = MakeColor("#008000");       // 注释：绿（VS Light）
            _numberColor = MakeColor("#000080");        // 数字：深蓝（VS Light）
            _identifierColor = MakeColor("#000000");    // 普通标识符：默认文本色（VS Light）
            _preprocessorColor = MakeColor("#888888");  // 预处理指令：灰（VS Light）
        }
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
            case ClassificationTypeNames.FieldName:
            case ClassificationTypeNames.ParameterName:
            case ClassificationTypeNames.ConstantName:
                color.Foreground = new SimpleHighlightingBrush(_fieldLocalColor);
                break;
            case ClassificationTypeNames.LocalName:
                // 对齐 VS Light：局部变量黑色不额外染色（落默认色）；暗色主题下局部变量用浅蓝（VS Dark）保证可见
                if (_isDark)
                    color.Foreground = new SimpleHighlightingBrush(_fieldLocalColor);
                break;
            case ClassificationTypeNames.Keyword:
            case ClassificationTypeNames.ControlKeyword:
                color.Foreground = new SimpleHighlightingBrush(_keywordColor);
                break;
            case ClassificationTypeNames.StringLiteral:
            case ClassificationTypeNames.VerbatimStringLiteral:
                color.Foreground = new SimpleHighlightingBrush(_stringColor);
                break;
            case ClassificationTypeNames.Comment:
                color.Foreground = new SimpleHighlightingBrush(_commentColor);
                break;
            case ClassificationTypeNames.NumericLiteral:
                color.Foreground = new SimpleHighlightingBrush(_numberColor);
                break;
            case ClassificationTypeNames.Identifier:
                // 普通标识符：主题默认文本色（对齐 RoslynPad 分类继承链 fallback 到默认前景）
                color.Foreground = new SimpleHighlightingBrush(_identifierColor);
                break;
            case ClassificationTypeNames.PreprocessorKeyword:
            case ClassificationTypeNames.PreprocessorText:
                color.Foreground = new SimpleHighlightingBrush(_preprocessorColor);
                break;
        }
        return color;
    }

    private static System.Windows.Media.Color MakeColor(string hex)
        => (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex);
}
