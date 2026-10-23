using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Composition.Hosting;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;
using ICSharpCode.AvalonEdit;
using ICSharpCode.AvalonEdit.CodeCompletion;
using ICSharpCode.AvalonEdit.Document;
using ICSharpCode.AvalonEdit.Editing;
using ICSharpCode.AvalonEdit.Highlighting;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Completion;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Host.Mef;
using Microsoft.CodeAnalysis.SignatureHelp;
using Microsoft.CodeAnalysis.Text;
using SunEyeVision.Plugin.SDK.Logging;

namespace CommonControl;

/// <summary>
/// 公共代码编辑器内核：封装 AvalonEdit 编辑 + 语义高亮 + 补全 + 签名帮助 + 诊断波浪线 + workspace + 增量桥。
/// 脚本工具编辑器与全局脚本编辑器组合本控件，复用同一套编辑器能力，避免维护两套系统。
///
/// 编辑器能力通过 <see cref="SetDocumentProvider"/> 委托注入"当前文档"。
/// workspace 的引用 / 生成代码 / 用户代码来源通过 <see cref="SetReferenceProvider"/>、
/// <see cref="SetCodeSource"/> 委托注入（工具层特有依赖，内核不直接引用）。
/// </summary>
public partial class ScriptEditorCore : UserControl
{
    public static readonly RoutedUICommand CompleteCommand = new("补全", "CompleteScript", typeof(ScriptEditorCore),
        new InputGestureCollection { new KeyGesture(Key.Space, ModifierKeys.Control) });
    public sealed record CompletionTiming(double PreparationMs, double QueryMs, double PresentationMs, int Candidates,
        double PopupConstructionMs, double PopulationMs, double ShowMs,
        double SourceInitializationMs, double MeasureMs, double ArrangeMs, double BeforeSourceInitializationMs);
    private double _popupConstructionMs, _popupPopulationMs, _popupShowMs;
    private double _popupSourceMs, _popupMeasureMs, _popupArrangeMs;
    private double _popupBeforeSourceMs;
    public sealed record EditorConstructionTiming(double XamlMs, double SetupMs, double WarmupSchedulingMs);
    public EditorConstructionTiming? ConstructionTiming { get; private set; }
    public Task AnalysisIdle => _analysisScheduler.WhenIdle;
    public CompletionTiming? LastCompletionTiming { get; private set; }
    public async Task RequestCompletionAsync()
    {
        _throttleCts?.Cancel(); _throttleCts?.Dispose();
        _throttleCts = new CancellationTokenSource();
        await ThrottledRefreshAndShow('\0', _throttleCts.Token);
    }
    private readonly string typeName = "ScriptEditorCore";

    // ── 对外接口（供调用方设置"当前文档"与代码）──

    /// <summary>
    /// 注入"当前文档"获取委托。补全/语义高亮/诊断都通过它拿 Roslyn Document。
    /// 脚本工具传其 workspace 的 user 文档；全局脚本编辑器传自己的文档。
    /// </summary>
    public void SetDocumentProvider(Func<Document?> getDocument)
    {
        _getDocument = getDocument ?? throw new ArgumentNullException(nameof(getDocument));
    }

    /// <summary>
    /// 注入 workspace 引用来源。工具层传 host 引用 + ScriptRefs 目录引用的合并列表
    /// （对齐 ScriptDebugControl 中 MetadataReferenceProvider + SharedMetadataReferenceProvider 的组合）。
    /// </summary>
    public void SetReferenceProvider(Func<IEnumerable<MetadataReference>> getReferences)
    {
        _getReferences = getReferences ?? throw new ArgumentNullException(nameof(getReferences));
    }

    /// <summary>
    /// 注入 workspace 的"生成代码"与"用户代码"来源。
    /// 生成代码是工具特有的字段声明 partial class；用户代码是编辑器当前内容。
    /// </summary>
    public void SetCodeSource(Func<string> getGeneratedCode, Func<string> getUserCode)
    {
        _getGeneratedCode = getGeneratedCode ?? throw new ArgumentNullException(nameof(getGeneratedCode));
        _getUserCode = getUserCode ?? throw new ArgumentNullException(nameof(getUserCode));
    }

    /// <summary>
    /// 注入补全项图标提供者。工具层传"根据 CompletionItem 渲染 ImageSource"的函数
    /// （内部走 GlyphMapping + Glyphs.xaml 资源）。未注入时补全项无图标（列表仍可用）。
    /// </summary>
    public void SetCompletionIconProvider(Func<CompletionItem, ImageSource?> getIcon)
    {
        _completionIconProvider = getIcon ?? throw new ArgumentNullException(nameof(getIcon));
    }

    /// <summary>加载代码到编辑器（调用方打开编辑器时调用）。</summary>
    public void LoadCode(string code) => CodeEditor.Text = code ?? string.Empty;

    /// <summary>获取当前编辑器代码文本。</summary>
    public string GetCode() => CodeEditor.Text;

    /// <summary>诊断结果更新事件：内核不绑死显示方式，由调用方决定如何展示。</summary>
    public event Action<IReadOnlyList<Diagnostic>>? DiagnosticsUpdated;

    /// <summary>Roslyn workspace（内部管理，补全/诊断/高亮共享）。</summary>
    public ScriptWorkspace? Workspace => _workspace;

    /// <summary>用户文档 ID（内部管理，由 RebuildWorkspace 赋值）。</summary>
    public DocumentId? UserDocumentId => _userDocumentId;

    /// <summary>
    /// 底层 AvalonEdit 编辑器。供调用方访问光标/滚动等编辑器能力（经 InternalsVisibleTo）。
    /// </summary>
    internal ICSharpCode.AvalonEdit.TextEditor Editor => CodeEditor;

    // ── 内部字段（workspace / 高亮 / 补全 / 诊断）──

    private ScriptAnalysisSession _analysisSession = new();
    private ScriptAnalysisScheduler _analysisScheduler = new();
    private bool _sessionClosed;
    private Func<ScriptAnalysisReferences>? _getReferenceSnapshot;
    private Func<CancellationToken, Task<ScriptAnalysisReferences>>? _getAsyncReferenceSnapshot;
    private bool _analysisEnvironmentValid = true;
    private Func<bool>? _canAnalyze;
    public void SetAnalysisValidator(Func<bool> validator) => _canAnalyze = validator;
    public void InvalidateAnalysisEnvironment()
    {
        _analysisEnvironmentValid = false;
        _analysisSession.AdvanceDocument();
        _diagnosticsCts?.Cancel(); _throttleCts?.Cancel();
        _completionWindow?.Close(); _insightWindow?.Close();
        _currentDiagnostics = []; DiagnosticsUpdated?.Invoke([]);
        ResetSemanticHighlighter(); RedrawVisibleLines();
    }
    private readonly SemaphoreSlim _workspacePrepareGate = new(1, 1);
    public bool IsWorkspaceReady => _workspaceInitialized && !_sessionClosed && _analysisEnvironmentValid;
    public bool IsSemanticReady => !_sessionClosed && _semanticColorizer?.IsReady == true;
    public async Task WaitForSemanticReadyAsync()
    {
        await Dispatcher.Yield(DispatcherPriority.Render);
        CodeEditor.TextArea.TextView.EnsureVisualLines();
        _semanticColorizer?.Warmup();
        if (_semanticColorizer != null) await _semanticColorizer.WarmupCompletion;
        if (!IsSemanticReady) throw new OperationCanceledException("语义准备已被新版本取代。");
    }
    public void SetAsyncReferenceSnapshotProvider(Func<CancellationToken, Task<ScriptAnalysisReferences>> provider)
        => _getAsyncReferenceSnapshot = provider;
    private string? _referenceIdentity;
    public ScriptAnalysisSession AnalysisSession => _analysisSession;
    public void SetReferenceSnapshotProvider(Func<ScriptAnalysisReferences> provider)
        => _getReferenceSnapshot = provider ?? throw new ArgumentNullException(nameof(provider));

    private Func<Document?>? _getDocument;
    private Func<IEnumerable<MetadataReference>>? _getReferences;
    private Func<string>? _getGeneratedCode;
    private Func<string>? _getUserCode;

    // workspace 骨架
    private ScriptWorkspace? _workspace;
    private bool _workspaceInitialized;                    // workspace 是否已建（含引用/项目/文档骨架），用于实例级复用
    private DocumentId? _documentId;                       // 生成的字段声明文档 ID
    private DocumentId? _userDocumentId;                   // 用户代码文档 ID
    private AvalonEditTextContainer? _userTextContainer;   // 用户文档增量文本通道

    // 语义高亮（Layer 2b：Roslyn 分类着色）。生命周期由 RoslynHighlightingColorizer 管理。
    // 挂载标志：构造函数只创建不挂载（workspace 未就绪），RebuildWorkspace 完成后首次挂载（对齐参考时序）。
    private RoslynHighlightingColorizer? _semanticColorizer;
    private bool _semanticColorizerMounted;

    // 实时诊断（Phase 1）：波浪线 + ToolTip + DiagnosticsUpdated 事件
    private CancellationTokenSource? _diagnosticsCts;             // 500ms 诊断节流
    private IReadOnlyList<Diagnostic> _currentDiagnostics = [];   // 当前错误/警告列表（波浪线 + ToolTip 消费）
    private HashSet<int> _lastDiagnosedLines = [];                // 上次有诊断的行号集合（诊断清空时重绘旧行，清除残留波浪线）
    private string? _lastDiagnosedCode;                           // 上次已诊断的代码，用于跳过不变代码
    private string? _lastGeneratedCode;                           // 上次已同步到 workspace 的 GeneratedCode，用于跳过无意义的 solution 重建
    private System.Windows.Controls.ToolTip? _diagnosticToolTip;  // 诊断悬停详情浮窗
    private int _lastDiagToolTipOffset = -1;                      // 上次已显示 ToolTip 的诊断偏移（防 MouseMove 闪烁）

    // 初始化阶段抑制诊断（避免 TextChanged 在 workspace 就绪前触发诊断，消除日志噪声）
    private bool _suppressDiagnostic;

    // 补全（Phase 2）：弹出窗口 + 数据源 + 节流取消
    private CompletionWindow? _completionWindow;
    private CompletionItem[] _currentItems = [];                 // 唯一数据源（单层，无后台刷新）
    private CancellationTokenSource? _throttleCts;               // 补全查询取消令牌（新查询取消旧查询）
    private OverloadInsightWindow? _insightWindow;               // 签名帮助浮窗（参数提示，复用实例）
    private Func<CompletionItem, ImageSource?>? _completionIconProvider;  // 补全项图标提供者（工具层注入，走 GlyphMapping + Glyphs 资源）

    // 签名帮助：用公开语法树 + IMethodSymbol 定位方法调用（见 ShowSignatureHelpAsync）。
    // 手写聚合 C# ISignatureHelpProvider（对齐 RoslynPad AggregateSignatureHelpProvider），不用 SignatureHelpService（4.12 不稳定）。
    private static ImmutableArray<ISignatureHelpProvider> _signatureHelpProviders;
    private static readonly object _signatureHelpLock = new();

    // 进程生命周期内共享的 MefHostServices（首次初始化慢，后续缓存命中快）
    private static MefHostServices? _sharedMefHost;
    private static object _mefLock = new();

    // 预热 ScriptWorkspace 缓存
    private static ScriptWorkspace? _preWarmedWorkspace;

    // 补全引擎就绪标志（MEF + Workspace 预热完成后置 true）
    private static volatile bool _completionReady;

    // 补全就绪回调（预热完成时通知等待中的调用方重建 workspace）
    private static Action? _onCompletionReady;

    private static bool _warmupStarted;
    private static readonly object _warmupLock = new();

    /// <summary>
    /// 线程安全地获取共享 MefHostServices（LazyInitializer 消除预热与主线程的竞态）。
    /// </summary>
    private static MefHostServices GetSharedMefHost()
    {
        return LazyInitializer.EnsureInitialized(ref _sharedMefHost, ref _mefLock, () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            var host = MefHostServices.Create(MefHostServices.DefaultAssemblies);
            sw.Stop();
            PluginLogger.Info($"[补全] MefHostServices 首次初始化: {sw.ElapsedMilliseconds}ms",
                nameof(ScriptEditorCore));
            return host;
        });
    }

    /// <summary>
    /// 应用启动后台预热补全引擎（MEF + Workspace）。对齐新版 RoslynPad 的 Task.Run 启动初始化。
    /// 幂等：双重检查锁保证进程内只预热一次；完成后置 _completionReady 并通知等待中的调用方。
    /// </summary>
    internal static void WarmupForStartup()
    {
        lock (_warmupLock)
        {
            if (_warmupStarted) return;
            _warmupStarted = true;
        }

        _ = Task.Run(async () =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            PluginLogger.Info("[补全] 启动后台预热 Roslyn 开始...", nameof(ScriptEditorCore));
            await WarmupRoslynAsync();
            await WarmupWorkspaceAsync();
            sw.Stop();
            _completionReady = true;
            PluginLogger.Info($"[补全] 启动后台预热完成: {sw.ElapsedMilliseconds}ms", nameof(ScriptEditorCore));

            var cb = Interlocked.Exchange(ref _onCompletionReady, null);
            cb?.Invoke();
        });
    }

    private static Task WarmupRoslynAsync()
    {
        return Task.Run(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            PluginLogger.Info("[补全] 后台预热 Roslyn 开始...", nameof(ScriptEditorCore));
            GetSharedMefHost();
            sw.Stop();
            PluginLogger.Info($"[补全] 后台预热 Roslyn 完成: {sw.ElapsedMilliseconds}ms", nameof(ScriptEditorCore));
        });
    }

    private static Task WarmupWorkspaceAsync()
    {
        return Task.Run(() =>
        {
            var sw = System.Diagnostics.Stopwatch.StartNew();
            PluginLogger.Info("[补全] 后台预热 Workspace 开始...", nameof(ScriptEditorCore));
            var host = GetSharedMefHost();
            var ws = new ScriptWorkspace(host);
            ws.TryApplyChanges(ws.CurrentSolution.AddProject(
                ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "ScriptCompletion", "ScriptCompletion", LanguageNames.CSharp)));
            Interlocked.Exchange(ref _preWarmedWorkspace, ws);
            sw.Stop();
            PluginLogger.Info($"[补全] 后台预热 Workspace 完成: {sw.ElapsedMilliseconds}ms", nameof(ScriptEditorCore));
        });
    }

    /// <summary>
    /// 构造函数：初始化 AvalonEdit 控件。编辑器能力（高亮/补全/诊断/workspace）在初始化时挂载。
    /// </summary>
    public ScriptEditorCore()
    {
        var construction = System.Diagnostics.Stopwatch.StartNew();
        InitializeComponent();
        var xamlMs = construction.Elapsed.TotalMilliseconds;
        CodeEditor.CommandBindings.Add(new CommandBinding(CompleteCommand, async (_, e) =>
        {
            e.Handled = true;
            try { await RequestCompletionAsync(); } catch (OperationCanceledException) { }
        }));
        InitializeEditor();
        var setupMs = construction.Elapsed.TotalMilliseconds - xamlMs;
        WarmupForStartup();
        ConstructionTiming = new(xamlMs, setupMs, construction.Elapsed.TotalMilliseconds - xamlMs - setupMs);
        // 控件卸载时释放 workspace 与增量桥资源
        Unloaded += (_, _) =>
        {
            _sessionClosed = true;
            _analysisSession.Dispose();
            _analysisScheduler.Dispose();
            _semanticColorizer?.Release();
            _workspaceInitialized = false;
            _userTextContainer?.Dispose();
            _userTextContainer = null;
            _workspace?.Dispose();
            _workspace = null;
        };
        Loaded += (_, _) =>
        {
            if (!_sessionClosed) return;
            _sessionClosed = false;
            _analysisSession = new ScriptAnalysisSession();
            _analysisScheduler = new ScriptAnalysisScheduler();
            CodeEditor.TextChanged += OnCodeEditorTextChanged;
            CodeEditor.TextArea.MouseMove += TextArea_MouseMove;
            CodeEditor.TextArea.MouseLeave += TextArea_MouseLeave;
            CodeEditor.TextArea.TextEntered += TextArea_TextEntered;
            CodeEditor.TextArea.TextEntering += TextArea_TextEntering;
            if (_getGeneratedCode != null && _getAsyncReferenceSnapshot == null) InitializeWorkspace();
        };
    }

    /// <summary>
    /// 初始化编辑器：加载 xshd 语法高亮 + 创建 Roslyn 语义高亮着色器 + 当前行高亮。
    /// ★ 设计迁移（对齐参考时序）：语义 colorizer 只创建不挂载！
    ///   构造函数里 workspace 尚未建（要等 RebuildWorkspace 才建），此时挂载会导致首帧渲染 worker
    ///   拿到 document==null 丢弃可视行、缓存白行永不重分类（"打开白、编辑才变青"根因）。
    ///   改为 RebuildWorkspace 完成后（UI线程）由 EnsureSemanticHighlighterMounted 挂载。
    /// </summary>
    private void InitializeEditor()
    {
        // 对齐 RoslynPad 新版：不使用 xshd 语法高亮，仅由 Roslyn 语义分类器统一着色。
        // CodeEditor.SyntaxHighlighting 保持 null，未就绪行由编辑器默认前景（主题色）兜底，
        // 语义模型就绪后 DoHighlightLine 同步分类（含语法 fallback），单一色板无跳变。

        // Layer 2a: 实时诊断标记着色器（错误/警告在 span 下画波浪线，随主题，保留语法颜色）
        var errorMarker = new ErrorMarkerColorizer(() => _currentDiagnostics, ThemeResource.IsDarkTheme());
        CodeEditor.TextArea.TextView.BackgroundRenderers.Add(errorMarker);
        PluginLogger.Info("[Highlighting] 已加载实时诊断波浪线着色器", typeName);

        // Layer 2b: Roslyn 语义高亮着色器（IHighlighter 架构：按需逐行 + 防闪烁，对齐 RoslynPad）。
        //   字段/局部变量/参数/常量色随主题（暗色浅蓝 #9CDCFE，浅色深蓝 #001080），避免暗色主题下黑色不可见。
        _semanticColorizer = new RoslynHighlightingColorizer(
            CodeEditor.Document,
            () => GetUserDocument(),
            new ClassificationHighlightColors(ThemeResource.IsDarkTheme()), _analysisScheduler);

        // 当前行高亮：光标所在行浅蓝色背景，替代纯光标闪烁
        CodeEditor.Options.HighlightCurrentLine = true;
        CodeEditor.TextArea.TextView.CurrentLineBackground =
            new SolidColorBrush(Color.FromArgb(0x18, 0x00, 0x7A, 0xCC));
        if (CodeEditor.TextArea.TextView.CurrentLineBackground.CanFreeze)
            CodeEditor.TextArea.TextView.CurrentLineBackground.Freeze();

        // 实时诊断：文本变更 → 500ms 节流 → Roslyn 诊断 → 波浪线
        CodeEditor.TextChanged += OnCodeEditorTextChanged;
        // 诊断悬停详情：鼠标移到错误/警告 span 上显示消息
        CodeEditor.TextArea.MouseMove += TextArea_MouseMove;
        CodeEditor.TextArea.MouseLeave += TextArea_MouseLeave;
        // 自动补全 + 签名帮助
        CodeEditor.TextArea.TextEntered += TextArea_TextEntered;
        CodeEditor.TextArea.TextEntering += TextArea_TextEntering;
        // 控件卸载时释放编辑器资源
        Unloaded += (_, _) =>
        {
            CodeEditor.TextChanged -= OnCodeEditorTextChanged;
            CodeEditor.TextArea.MouseMove -= TextArea_MouseMove;
            CodeEditor.TextArea.MouseLeave -= TextArea_MouseLeave;
            CodeEditor.TextArea.TextEntered -= TextArea_TextEntered;
            CodeEditor.TextArea.TextEntering -= TextArea_TextEntering;
            _diagnosticsCts?.Cancel();
            _diagnosticsCts?.Dispose();
            _diagnosticsCts = null;
            _throttleCts?.Cancel();
            _throttleCts?.Dispose();
            _throttleCts = null;
            _completionWindow?.Close();
            _insightWindow?.Close();
        };
    }

    /// <summary>
    /// RebuildWorkspace 完成后（UI线程）挂载语义高亮器 + 触发语义模型预热。
    /// ★ 设计迁移（对齐参考时序）：workspace/文档就绪后才挂载，worker 首帧渲染 GetUserDocument()
    ///   必非 null，可视行直接正确分类，根治"打开白、编辑才变青"。
    /// </summary>
    public void EnsureSemanticHighlighterMounted()
    {
        if (!_semanticColorizerMounted)
        {
            CodeEditor.TextArea.TextView.LineTransformers.Add(_semanticColorizer);
            _semanticColorizerMounted = true;
            PluginLogger.Info("[Highlighting] 已挂载 Roslyn 语义高亮着色器（workspace 就绪后，对齐参考时序）", typeName);
        }
        _semanticColorizer?.Warmup();
    }

    private void ResetSemanticHighlighter()
    {
        var mounted = _semanticColorizerMounted;
        if (mounted) CodeEditor.TextArea.TextView.LineTransformers.Remove(_semanticColorizer);
        _semanticColorizer?.Release();
        _semanticColorizer = new RoslynHighlightingColorizer(CodeEditor.Document,
            () => GetUserDocument(), new ClassificationHighlightColors(ThemeResource.IsDarkTheme()), _analysisScheduler);
        if (mounted) CodeEditor.TextArea.TextView.LineTransformers.Add(_semanticColorizer);
    }

    /// <summary>
    /// 编辑器就绪完整流程（调用方在注入引用/代码来源后调用）：
    /// 重建 workspace → 挂载语义高亮 → 调度诊断 → 重绘可见行 → 预热补全。
    /// 对齐 ScriptDebugControl 原 ScheduleRebuildForOpen 的编排。
    /// </summary>
    public void InitializeWorkspace()
    {
        var perfSw = System.Diagnostics.Stopwatch.StartNew();
        RebuildWorkspace();
        PluginLogger.Info($"[Perf] RebuildWorkspace: {perfSw.ElapsedMilliseconds}ms", typeName);
        SetSuppressDiagnostic(false);
        EnsureSemanticHighlighterMounted();
        ScheduleDiagnosticAsync();
        RedrawVisibleLines();
        _ = WarmupCompletionAsync();
    }

    /// <summary>后台准备不可变Roslyn工程，UI线程只安装增量文档桥；输入变化则丢弃并重试当前版。</summary>
    public async Task PrepareWorkspaceAsync(CancellationToken token = default)
    {
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _analysisSession.LifetimeToken);
        token = linked.Token;
        await _workspacePrepareGate.WaitAsync(token);
        try
        {
            while (!_sessionClosed)
            {
                if (_canAnalyze?.Invoke() == false) { InvalidateAnalysisEnvironment(); throw new InvalidOperationException("输入输出定义无效，请先修正变量表草稿。"); }
                var stamp = _analysisSession.Capture();
                var generated = _getGeneratedCode?.Invoke() ?? "";
                var user = CodeEditor.Text;
                var references = _getAsyncReferenceSnapshot != null ? await _getAsyncReferenceSnapshot(token)
                    : _getReferenceSnapshot?.Invoke() ?? new ScriptAnalysisReferences("external-provider", _getReferences!());
                token.ThrowIfCancellationRequested();
                if (!_analysisSession.IsCurrent(stamp)) continue;
                var wasInvalid = !_analysisEnvironmentValid;
                _analysisEnvironmentValid = true;
                if (_workspaceInitialized && _referenceIdentity == references.Identity)
                {
                    if (_lastGeneratedCode != generated || wasInvalid)
                    {
                        _analysisSession.SetEnvironment(references.Identity, generated);
                        _workspace!.SetCurrentSolution(_workspace.CurrentSolution.WithDocumentText(_documentId!, SourceText.From(generated)));
                        _lastGeneratedCode = generated;
                        _lastDiagnosedCode = null;
                        _currentDiagnostics = [];
                        DiagnosticsUpdated?.Invoke([]);
                        ResetSemanticHighlighter(); RedrawVisibleLines();
                    }
                    return;
                }
                var prepared = await Task.Run(() =>
                {
                    var workspace = new ScriptWorkspace(GetSharedMefHost());
                    try
                    {
                        var projectId = ProjectId.CreateNewId();
                        var project = workspace.CurrentSolution.AddProject(ProjectInfo.Create(projectId, VersionStamp.Create(),
                            "ScriptCompletion", "ScriptCompletion", LanguageNames.CSharp)).GetProject(projectId)!
                            .WithCompilationOptions(new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary).WithAllowUnsafe(true))
                            .WithMetadataReferences(references.References);
                        var genId = DocumentId.CreateNewId(project.Id);
                        var userId = DocumentId.CreateNewId(project.Id);
                        workspace.SetCurrentSolution(project.Solution.AddDocument(genId, "ScriptGenerated.cs", SourceText.From(generated))
                            .AddDocument(userId, "ScriptUser.cs", SourceText.From(user)));
                        return (workspace, genId, userId);
                    }
                    catch { workspace.Dispose(); throw; }
                }, token);
                if (token.IsCancellationRequested || !_analysisSession.IsCurrent(stamp))
                { prepared.workspace.Dispose(); token.ThrowIfCancellationRequested(); continue; }
                _semanticColorizer?.Release();
                _diagnosticsCts?.Cancel();
                _completionWindow?.Close(); _insightWindow?.Close();
                _userTextContainer?.Dispose(); _workspace?.Dispose();
                _workspace = prepared.workspace; _documentId = prepared.genId; _userDocumentId = prepared.userId;
                _userTextContainer = new AvalonEditTextContainer(CodeEditor.Document);
                _workspace.OpenDocument(_userDocumentId, _userTextContainer);
                _analysisSession.SetEnvironment(references.Identity, generated);
                _referenceIdentity = references.Identity; _lastGeneratedCode = generated; _lastDiagnosedCode = null;
                _currentDiagnostics = []; DiagnosticsUpdated?.Invoke([]);
                _workspaceInitialized = true;
                ResetSemanticHighlighter(); EnsureSemanticHighlighterMounted(); RedrawVisibleLines();
                SetSuppressDiagnostic(false);
                return;
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        { InvalidateAnalysisEnvironment(); throw; }
        finally { _workspacePrepareGate.Release(); }
    }

    /// <summary>
    /// 打开前预热：重建 workspace + 完成语义着色，供窗口显示前调用（对齐"打开即着色，无白色过渡"）。
    /// 在 InitializeAsync（窗口 Show 前被 await）中调用，同步完成 RebuildWorkspace + 挂载高亮，
    /// 再等待语义模型构建 + 可视区前 N 行分类完成。
    /// </summary>
    public async Task InitializeAndWaitAsync()
    {
        InitializeWorkspace();
        await WaitForInitialHighlightAsync();
    }

    /// <summary>
    /// 等待语义高亮预热完成：确保 highlighter 已创建（触发一次布局），再等其 WarmupCompletion。
    /// </summary>
    private async Task WaitForInitialHighlightAsync()
    {
        if (_semanticColorizer == null) return;

        // 确保 highlighter 已创建（AvalonEdit 布局时才 CreateHighlighter + TriggerWarmup）
        if (CodeEditor.TextArea.TextView.VisualLinesValid)
            _ = CodeEditor.TextArea.TextView.VisualLines;

        await _semanticColorizer.WarmupCompletion;
    }

    /// <summary>
    /// 编辑/重建后调度语义着色：只重绘当前可见行（不全量）。
    /// IHighlighter 架构下，AvalonEdit 对可见行调 HighlightLine，内部用 CompareAge 自动判断
    /// "行没变就复用旧缓存（不闪烁）、变了才重算"，无需手动清缓存。
    /// </summary>
    public void RedrawVisibleLines()
    {
        if (CodeEditor == null) return;

        var textView = CodeEditor.TextArea.TextView;
        if (textView.VisualLinesValid && textView.VisualLines.Count > 0)
        {
            var first = textView.VisualLines.First().FirstDocumentLine.LineNumber;
            var last = textView.VisualLines.Last().LastDocumentLine.LineNumber;
            textView.Redraw(first, last, DispatcherPriority.Background);
        }
        else
        {
            textView.Redraw(DispatcherPriority.Background);
        }
    }

    // =======================================================
    // Phase 1: 实时诊断（Roslyn SemanticModel.GetDiagnostics → 红色波浪线）
    // =======================================================

    /// <summary>
    /// 编辑器文本变更 → 同步到代码来源 → 500ms 节流 → 诊断。
    /// 调用方如需在文本变更时做额外处理（如更新状态栏），可订阅 TextChanged。
    /// </summary>
    private void OnCodeEditorTextChanged(object? sender, EventArgs e)
    {
        _analysisSession.AdvanceDocument();
        // 初始化期间抑制诊断（workspace 尚未就绪时触发只会打印日志噪声）
        if (_suppressDiagnostic) return;

        ScheduleDiagnosticAsync();
        RedrawVisibleLines();
    }

    /// <summary>
    /// 诊断节流入口：500ms 防抖后执行 Roslyn 诊断收集。
    /// 内部通过委托（SetCodeSource 注入）读取 generated/user 代码，不依赖工具参数。
    /// </summary>
    public async void ScheduleDiagnosticAsync()
    {
        if (_sessionClosed || _suppressDiagnostic) return;
        _diagnosticsCts?.Cancel(); _diagnosticsCts?.Dispose();
        _diagnosticsCts = new CancellationTokenSource();
        var token = _diagnosticsCts.Token;
        try
        {
            await Task.Delay(500, token);
            await PrepareWorkspaceAsync(token);
            var stamp = _analysisSession.Capture();
            var generated = _getGeneratedCode?.Invoke() ?? "";
            var user = CodeEditor.Text;
            var document = GetUserDocument();
            if (document == null) return;
            await _analysisScheduler.RunAsync(false, async scheduledToken =>
            {
                await RunDiagnosticAsync(document, user, generated, stamp, scheduledToken);
                return true;
            }, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { PluginLogger.Warning($"[Diagnostic] {ex.Message}", typeName); }
    }
    /// <summary>
    /// 更新 workspace 文本（双文档） → 获取诊断 → 波浪线重绘 + 抛 DiagnosticsUpdated。
    /// user 文档已绑定增量通道，随编辑器实时同步；generated 文档仅在实际变化时 WithDocumentText 更新。
    /// </summary>
    private async Task RunDiagnosticAsync(Document userDoc, string userCodeSnapshot, string generatedCode, ScriptAnalysisSession.Stamp stamp, CancellationToken token)
    {
        if (_workspace == null || _userDocumentId == null)
        {
            PluginLogger.Warning($"[Diagnostic] 跳过: workspace={_workspace != null}, userDocumentId={_userDocumentId != null}", typeName);
            return;
        }


        // 代码未变则跳过诊断（避免同段代码重复 Roslyn 分析）
        if (!_analysisSession.IsCurrent(stamp) || $"{userCodeSnapshot.Length}:{userCodeSnapshot}{generatedCode}" == _lastDiagnosedCode)
        {
            PluginLogger.Info("[Diagnostic] 代码未变，跳过诊断", typeName);
            return;
        }


        try
        {
            // 1. 更新 generated 文档文本：仅当 GeneratedCode 实际变化才 WithDocumentText。
            //    user 文档已绑定 SourceTextContainer 增量通道，随编辑器实时增量同步。
            // Document 是 UI 线程捕获的不可变快照，后台不得写回共享 workspace。

            // 获取用户文档的语法树，用于过滤诊断
            var userSyntaxTree = await userDoc.GetSyntaxTreeAsync(token);
            var model = await userDoc.GetSemanticModelAsync(token);

            if (token.IsCancellationRequested) return;

            // 诊断 — 取用户文档中的错误与警告（Error 红波浪 / Warning 蓝波浪，行号直接对应编辑器行）
            var rawDiagnostics = model!.GetDiagnostics(cancellationToken: token)
                .Where(d => d.Severity is DiagnosticSeverity.Error or DiagnosticSeverity.Warning
                    && d.Location.SourceTree == userSyntaxTree)
                .ToImmutableArray();

            // 修正 CS1002（缺分号）的蔓延位置：
            // 多错误恢复时，Roslyn 会把缺分号的错误 span 标到"下一行的 token"上（如 var / LogInfo），
            // 导致波浪线画在下一行。真正的缺分号位置 = 该 token 的前一个 token 的末尾。
            // 对齐参考项目"位置精确"的体验，纯 public API（SyntaxTree.FindToken / GetPreviousToken）。
            var root = await userSyntaxTree.GetRootAsync(token);
            var diagnostics = new List<Diagnostic>(rawDiagnostics.Length);
            foreach (var d in rawDiagnostics)
            {
                var span = d.Location.SourceSpan;
                if (d.Id == "CS1002" && span.Length > 0)
                {
                    var spreadToken = root.FindToken(span.Start);
                    var prevToken = spreadToken.GetPreviousToken();
                    // 仅当 prevToken 在 span 之前（确认为蔓延到下一个 token）时才修正
                    if (prevToken.Span.End <= span.Start)
                    {
                        var correctedLoc = Location.Create(userSyntaxTree, new TextSpan(prevToken.Span.End, 0));
                        diagnostics.Add(Diagnostic.Create(d.Descriptor, correctedLoc, d.GetMessage()));
                        continue;
                    }
                }
                diagnostics.Add(d);
            }
            if (!_analysisSession.IsCurrent(stamp) || token.IsCancellationRequested) return;

            PluginLogger.Info($"[Diagnostic] Errors+Warnings={rawDiagnostics.Length}", typeName);

            // 检测到 CS0518（预定义类型缺失）时输出 Warning 级汇总提示（仅一次，非逐条刷屏）
            if (diagnostics.Any(d => d.Id == "CS0518"))
            {
                PluginLogger.Warning(
                    "[Diagnostic] ⚠ 检测到 CS0518（预定义类型缺失），可能原因：运行时 BCL 引用（System.Private.CoreLib / System.Runtime）未正确加载。",
                    typeName);
            }

            // 通过事件抛出诊断结果（内核不绑死显示方式，由调用方决定如何展示）


            // 2. UI 线程重绘诊断波浪线：重绘"旧诊断行 ∪ 新诊断行"。
            //    ★ 诊断清空（错误已修复）时只重绘新行会漏掉旧行，旧波浪线像素残留直到
            //      点击/移动才触发重绘。用并集确保"从有到无"也立即清除残留波浪线。
            var diag = diagnostics;
            await Dispatcher.InvokeAsync(() =>
            {
                if (!_analysisSession.IsCurrent(stamp) || token.IsCancellationRequested) return;
                _lastDiagnosedCode = $"{userCodeSnapshot.Length}:{userCodeSnapshot}{generatedCode}";
                _currentDiagnostics = diagnostics.ToImmutableArray();
                DiagnosticsUpdated?.Invoke(diagnostics);
                var textView = CodeEditor.TextArea.TextView;
                var lineCount = CodeEditor.Document.LineCount;

                var newLines = diag
                    .Select(d => d.Location.GetLineSpan().StartLinePosition.Line + 1)
                    .Distinct()
                    .Where(n => n >= 1 && n <= lineCount)
                    .ToHashSet();

                var redrawLines = new HashSet<int>(_lastDiagnosedLines);
                redrawLines.UnionWith(newLines);
                foreach (var n in redrawLines)
                {
                    textView.Redraw(n, n, DispatcherPriority.Background);
                }

                _lastDiagnosedLines = newLines;   // 更新"上次有诊断的行"

                // ★ 诊断更新后：若鼠标位置已无诊断（含诊断清空），关闭残留 ToolTip。
                //   修复代码后鼠标未移动，TextArea_MouseMove 不触发，需此处兜底；
                //   否则 ToolTip 赖着不消失，直到编辑器失焦。
                if (_diagnosticToolTip?.IsOpen == true)
                {
                    var hasDiagUnderMouse = HasDiagnosticAtMousePosition();
                    if (!hasDiagUnderMouse)
                    {
                        _diagnosticToolTip.IsOpen = false;
                        _lastDiagToolTipOffset = -1;
                    }
                }
            }, DispatcherPriority.Background, token);
        }
        catch (OperationCanceledException) { }
        catch (Exception ex)
        {
            PluginLogger.Warning($"[ScriptEditorCore] Diagnostic error: {ex.Message}", typeName);
        }
    }

    /// <summary>鼠标移到诊断 span 上：显示错误/警告消息（对齐 RoslynPad TextMarkerToolTipProvider）。</summary>
    private void TextArea_MouseMove(object? sender, MouseEventArgs e)
    {
        if (_currentDiagnostics.Count == 0)
        {
            // 诊断已清空（修复后无诊断区）：关闭残留 ToolTip，否则赖着不消失。
            if (_diagnosticToolTip?.IsOpen == true)
                _diagnosticToolTip.IsOpen = false;
            _lastDiagToolTipOffset = -1;
            return;
        }

        TextViewPosition? position;
        try
        {
            position = CodeEditor.TextArea.TextView.GetPositionFloor(
                e.GetPosition(CodeEditor.TextArea.TextView) + CodeEditor.TextArea.TextView.ScrollOffset);
        }
        catch (ArgumentOutOfRangeException) { return; }

        if (!position.HasValue || position.Value.Location.IsEmpty) return;

        var offset = CodeEditor.Document.GetOffset(position.Value.Location);
        // 同一诊断内移动不重复刷新（防 MouseMove 高频闪烁）
        if (_diagnosticToolTip?.IsOpen == true && offset == _lastDiagToolTipOffset) return;

        // 命中判断：正常 span 用区间；零长度 span（缺分号 CS1002）用精确点命中
        var diag = _currentDiagnostics.FirstOrDefault(d =>
        {
            var s = d.Location.SourceSpan;
            return s.Length > 0
                ? (s.Start <= offset && offset < s.End)
                : (s.Start == offset);
        });
        if (diag == null)
        {
            // 鼠标移出诊断区：关闭已打开的提示（防弹窗赖着不隐藏）
            if (_diagnosticToolTip?.IsOpen == true)
                _diagnosticToolTip.IsOpen = false;
            return;
        }

        if (_diagnosticToolTip == null)
        {
            _diagnosticToolTip = new System.Windows.Controls.ToolTip
            {
                MaxWidth = 480,
                PlacementTarget = CodeEditor.TextArea,
                Content = diag.GetMessage(),
            };
            _diagnosticToolTip.Closed += (_, _) => _diagnosticToolTip = null;
        }
        else
        {
            _diagnosticToolTip.Content = diag.GetMessage();
        }
        _lastDiagToolTipOffset = offset;
        _diagnosticToolTip.IsOpen = true;
    }

    /// <summary>鼠标移开编辑区：关闭诊断悬停详情浮窗。</summary>
    private void TextArea_MouseLeave(object? sender, MouseEventArgs e)
    {
        if (_diagnosticToolTip != null)
            _diagnosticToolTip.IsOpen = false;
        _lastDiagToolTipOffset = -1;
    }

    /// <summary>
    /// 判断鼠标当前位置是否命中诊断 span。
    /// 供诊断更新后兜底关闭 ToolTip：修复代码后鼠标未移动（MouseMove 不触发），
    /// 诊断区已消失，若鼠标位置无诊断则关闭残留 ToolTip。
    /// </summary>
    /// <returns>true = 鼠标位置命中诊断；false = 无诊断（应关闭 ToolTip）</returns>
    private bool HasDiagnosticAtMousePosition()
    {
        if (_currentDiagnostics.Count == 0)
            return false;

        try
        {
            var mousePos = Mouse.GetPosition(CodeEditor.TextArea.TextView)
                + CodeEditor.TextArea.TextView.ScrollOffset;
            var position = CodeEditor.TextArea.TextView.GetPositionFloor(mousePos);
            if (!position.HasValue || position.Value.Location.IsEmpty)
                return false;

            var offset = CodeEditor.Document.GetOffset(position.Value.Location);
            return _currentDiagnostics.Any(d =>
            {
                var s = d.Location.SourceSpan;
                return s.Length > 0
                    ? (s.Start <= offset && offset < s.End)
                    : (s.Start == offset);
            });
        }
        catch (Exception)
        {
            return false;   // 位置解析失败视为无诊断，安全关闭
        }
    }

    /// <summary>设置是否抑制诊断（初始化/参数加载期间调用方应置 true）。</summary>
    public void SetSuppressDiagnostic(bool suppress) => _suppressDiagnostic = suppress;

    // =======================================================
    // Phase 2: Roslyn 语义自动补全（单层数据源 + 零闪烁）
    // =======================================================

    /// <summary>
    /// 统一入口：字母 / 小数点 → 取消旧查询 → 单次 Roslyn 解析 → 单层弹窗。
    /// 窗口打开期间：AvalonEdit CompletionList 接管客户端过滤（内置质量评分算法）。
    /// </summary>
    private async void TextArea_TextEntered(object? sender, TextCompositionEventArgs e)
    {
        if (_sessionClosed) return;

        bool isTriggerChar = e.Text == "." ||
            (!string.IsNullOrEmpty(e.Text) && (char.IsLetterOrDigit(e.Text[0]) || e.Text[0] == '_'));
        // 签名帮助：输入 '(' / ',' / ')' 都触发 Roslyn 内置 SignatureHelpService 查询。
        if (e.Text is "(" or "," or ")")
        {
            _throttleCts?.Cancel();
            _throttleCts?.Dispose();
            _throttleCts = new CancellationTokenSource();
            var sigToken = _throttleCts.Token;
            await ShowSignatureHelpAsync(CodeEditor.TextArea.Caret.Offset, e.Text[0], sigToken);
            return;
        }

        if (!isTriggerChar)
        {
            return;
        }

        // 新查询取消旧查询（对齐 RoslynPad：TextEntered 直接查询，无节流滞后感）
        _throttleCts?.Cancel();
        _throttleCts?.Dispose();
        _throttleCts = new CancellationTokenSource();
        var token = _throttleCts.Token;

        try
        {
            bool isDot = e.Text == ".";

            if (isDot)
            {
                await ThrottledRefreshAndShow('.', token);
                return;
            }

            // 字母数字：窗口已开 → 沉默，AvalonEdit CompletionList 自动过滤（内置质量评分）
            // 无窗口 → 首次 Roslyn 查询
            bool windowActive = _completionWindow != null && _currentItems.Length > 0;
            if (!windowActive)
            {
                await ThrottledRefreshAndShow(e.Text[0], token);
            }
        }
        catch (OperationCanceledException)
        {
            // 节流取消，忽略
        }
        catch (Exception ex) { PluginLogger.Warning($"[补全] 弹窗失败: {ex.Message}", typeName); }
    }

    /// <summary>
    /// 文本输入中处理补全窗口的插入确认。
    /// '.' 特殊处理：前缀完全匹配 → 关闭窗口让 '.' 通过触发成员补全；
    /// 部分匹配 → RequestInsertion 补全文本 + 手动插入 '.' + 触发成员补全。
    /// 其他非字母数字 → RequestInsertion（提交选中项，字符通过）。
    /// </summary>
    private void TextArea_TextEntering(object? sender, TextCompositionEventArgs e)
    {
        if (e.Text.Length > 0 && _completionWindow != null)
        {
            if (!char.IsLetterOrDigit(e.Text[0]) && e.Text[0] != '_')
            {
                if (e.Text[0] == '.')
                {
                    var prefix = GetPrefixFromCaret();
                    var selectedItem = _completionWindow.CompletionList.ListBox?.SelectedItem;
                    if (selectedItem is ICompletionData completionData &&
                        string.Equals(completionData.Text, prefix, StringComparison.OrdinalIgnoreCase))
                    {
                        // 完全匹配：关闭窗口，'.' 通过 → TextEntered 触发成员补全
                        _completionWindow.Close();
                        _completionWindow = null;
                        return;
                    }

                    // 部分匹配：RequestInsertion 补全文本 → 手动插入 '.' → 触发成员补全
                    _completionWindow.CompletionList.RequestInsertion(e);
                    _completionWindow = null;

                    e.Handled = true;

                    var offset = CodeEditor.TextArea.Caret.Offset;
                    CodeEditor.Document.Insert(offset, ".");
                    CodeEditor.TextArea.Caret.Offset = offset + 1;

                    _ = TriggerMemberCompletionAfterDot();
                    return;
                }

                _completionWindow.CompletionList.RequestInsertion(e);
            }
        }
    }

    /// <summary>在 '.' 手动插入后，直接触发成员补全查询。</summary>
    private async Task TriggerMemberCompletionAfterDot()
    {
        // '.' 是独立触发动作（非连续打字路径），无竞态需取消，用 CancellationToken.None 免分配 CTS
        _throttleCts?.Cancel(); _throttleCts?.Dispose();
        _throttleCts = new CancellationTokenSource();
        await ThrottledRefreshAndShow('.', _throttleCts.Token);
    }

    /// <summary>单次 Roslyn 解析 + 弹窗/更新窗口（仅从 ThrottledRefreshAndShow 调用）。</summary>
    private async Task ThrottledRefreshAndShow(char triggerChar, CancellationToken token)
    {
        if (_sessionClosed) return;
        var timer = System.Diagnostics.Stopwatch.StartNew();
        try { await PrepareWorkspaceAsync(token); }
        catch (Exception ex)
        {
            PluginLogger.Warning($"[补全] 引用快照无效: {ex.Message}", typeName);
            return;
        }
        var stamp = _analysisSession.Capture();
        var preparationMs = timer.Elapsed.TotalMilliseconds;
        var items = await FetchFreshCompletions(triggerChar, token);
        var queryMs = timer.Elapsed.TotalMilliseconds - preparationMs;
        if (token.IsCancellationRequested || !_analysisSession.IsCurrent(stamp))
        {
            return;
        }
        if (items.IsDefault || items.Length == 0)
        {
            return;
        }

        // ★ 唯一数据源写入点：只有这里会修改 _currentItems ★
        _currentItems = items.ToArray();

        ShowCompletionWindow(items);
        LastCompletionTiming = new(preparationMs, queryMs, timer.Elapsed.TotalMilliseconds - preparationMs - queryMs,
            items.Length, _popupConstructionMs, _popupPopulationMs, _popupShowMs, _popupSourceMs, _popupMeasureMs, _popupArrangeMs, _popupBeforeSourceMs);
    }

    /// <summary>用 Roslyn 结果渲染/更新补全窗口（单次渲染，不重复创建）。</summary>
    private void ShowCompletionWindow(ImmutableArray<CompletionItem> items)
    {
        var presentation = System.Diagnostics.Stopwatch.StartNew();
        _popupConstructionMs = _popupPopulationMs = _popupShowMs = 0;
        _popupSourceMs = _popupMeasureMs = _popupArrangeMs = 0;
        _popupBeforeSourceMs = 0;
        var caretOffset = CodeEditor.TextArea.Caret.Offset;
        var prefix = GetPrefixFromCaret();

        if (_completionWindow != null)
        {
            // 已有窗口 → 更新数据源（Roslyn 刷新场景）
            PopulateData(_completionWindow.CompletionList.CompletionData, items);
            _popupPopulationMs = presentation.Elapsed.TotalMilliseconds;
            return;
        }

        var startOffset = caretOffset - prefix.Length;
        var endOffset = FindEndOfWord(caretOffset);

        // 创建新窗口，IsFiltering=true 为默认值，AvalonEdit 自动做智能过滤
        var window = new MeasuredCompletionWindow(CodeEditor.TextArea)
        {
            StartOffset = startOffset,
            EndOffset = endOffset,
            Background = ThemeResource.GetBrush("ThemePanelBackgroundBrush"),
            Foreground = ThemeResource.GetBrush("ThemeTextPrimaryBrush"),
        };
        // 列表项背景/选中高亮也跟主题（ListBox 模板内资源）
        window.CompletionList.Background = ThemeResource.GetBrush("ThemePanelBackgroundBrush");
        window.CompletionList.Foreground = ThemeResource.GetBrush("ThemeTextPrimaryBrush");
        _popupConstructionMs = presentation.Elapsed.TotalMilliseconds;
        PopulateData(window.CompletionList.CompletionData, items);
        _popupPopulationMs = presentation.Elapsed.TotalMilliseconds - _popupConstructionMs;
        window.CompletionList.ListBox.Background = ThemeResource.GetBrush("ThemePanelBackgroundBrush");
        window.CompletionList.ListBox.Foreground = ThemeResource.GetBrush("ThemeTextPrimaryBrush");
        // 固定可滚动视口，避免首次度量为每个候选创建可视行。
        window.CompletionList.MaxHeight = 300;
        window.CompletionList.ListBox.MaxHeight = 300;
        VirtualizingPanel.SetIsVirtualizing(window.CompletionList.ListBox, true);
        VirtualizingPanel.SetVirtualizationMode(window.CompletionList.ListBox, VirtualizationMode.Recycling);
        ScrollViewer.SetCanContentScroll(window.CompletionList.ListBox, true);

        window.Closed += (_, _) =>
        {
            _completionWindow = null;
            _currentItems = [];    // 窗口关闭 → 数据作废，下次重新解析
        };

        var pureShow = System.Diagnostics.Stopwatch.StartNew();
        window.ShowClock = pureShow;
        window.Show();
        _popupShowMs = pureShow.Elapsed.TotalMilliseconds;
        _popupSourceMs = window.SourceInitializationMs;
        _popupMeasureMs = window.MeasureMs;
        _popupArrangeMs = window.ArrangeMs;
        _popupBeforeSourceMs = window.BeforeSourceInitializationMs;

        _completionWindow = window;
    }

    private sealed class MeasuredCompletionWindow(TextArea area) : CompletionWindow(area)
    {
        internal double SourceInitializationMs, MeasureMs, ArrangeMs;
        internal double BeforeSourceInitializationMs;
        internal System.Diagnostics.Stopwatch? ShowClock;
        protected override void OnSourceInitialized(EventArgs e)
        {
            BeforeSourceInitializationMs = ShowClock?.Elapsed.TotalMilliseconds ?? 0;
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try { base.OnSourceInitialized(e); }
            finally { SourceInitializationMs += timer.Elapsed.TotalMilliseconds; }
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try { return base.MeasureOverride(availableSize); }
            finally { MeasureMs += timer.Elapsed.TotalMilliseconds; }
        }
        protected override Size ArrangeOverride(Size finalSize)
        {
            var timer = System.Diagnostics.Stopwatch.StartNew();
            try { return base.ArrangeOverride(finalSize); }
            finally { ArrangeMs += timer.Elapsed.TotalMilliseconds; }
        }
    }

    /// <summary>将 Roslyn CompletionItem[] 直接注入 CompletionList（AvalonEdit 接管客户端过滤）。</summary>
    private void PopulateData(IList<ICompletionData> data, ImmutableArray<CompletionItem> items)
    {
        var doc = _workspace.CurrentSolution.GetDocument(_userDocumentId);
        data.Clear();
        foreach (var item in items)
            data.Add(new RoslynCompletionData(item, doc, _completionIconProvider, _analysisScheduler, _analysisSession.LifetimeToken));
    }

    /// <summary>从光标位置向左提取标识符前缀（字母数字+下划线）。</summary>
    private string GetPrefixFromCaret()
    {
        var code = CodeEditor.Text;
        var caretOffset = CodeEditor.TextArea.Caret.Offset;
        int startOffset = caretOffset;
        while (startOffset > 0 && (char.IsLetterOrDigit(code[startOffset - 1]) || code[startOffset - 1] == '_'))
            startOffset--;
        return code.Substring(startOffset, caretOffset - startOffset);
    }

    /// <summary>从光标位置向右扫描到标识符单词末尾（用于设置 CompletionWindow.EndOffset）。</summary>
    private int FindEndOfWord(int fromOffset)
    {
        var code = CodeEditor.Text;
        int offset = fromOffset;
        while (offset < code.Length &&
               (char.IsLetterOrDigit(code[offset]) || code[offset] == '_'))
            offset++;
        return Math.Max(offset, fromOffset);
    }

    /// <summary>从 Roslyn 获取最新补全结果的原始 CompletionItem 列表。</summary>
    private async Task<ImmutableArray<CompletionItem>> FetchFreshCompletions(
        char triggerChar, CancellationToken token)
    {
        if (_workspace == null || _userDocumentId == null)
        {
            PluginLogger.Warning($"[补全] FetchFreshCompletions 跳过: workspace={_workspace != null}, userDocumentId={_userDocumentId != null}", typeName);
            return default;
        }

        if (_sessionClosed) return default;
        var stamp = _analysisSession.Capture();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _analysisSession.LifetimeToken);
        token = linked.Token;
        var code = CodeEditor.Text;
        var caretOffset = CodeEditor.TextArea.Caret.Offset;

        if (string.IsNullOrEmpty(code))
        {
            PluginLogger.Warning("[补全] FetchFreshCompletions 跳过: 代码为空", typeName);
            return default;
        }
        if (caretOffset <= 0)
        {
            PluginLogger.Warning($"[补全] FetchFreshCompletions 跳过: caretOffset={caretOffset}", typeName);
            return default;
        }

        try
        {
            var doc = _workspace.CurrentSolution.GetDocument(_userDocumentId);
            if (doc == null)
            {
                PluginLogger.Warning($"[补全] workspace 中找不到 user document: {_userDocumentId}", typeName);
                return default;
            }

            var completionService = CompletionService.GetService(doc);
            if (completionService == null)
            {
                PluginLogger.Warning($"[补全] CompletionService 不可用, caretOffset={caretOffset}", typeName);
                return default;
            }

            var trigger = triggerChar != '\0'
                ? CompletionTrigger.CreateInsertionTrigger(triggerChar)
                : CompletionTrigger.Default;

            var results = await _analysisScheduler.RunAsync(true,
                scheduledToken => completionService.GetCompletionsAsync(doc, caretOffset, trigger, cancellationToken: scheduledToken), token);

            if (token.IsCancellationRequested || !_analysisSession.IsCurrent(stamp) || CodeEditor.TextArea.Caret.Offset != caretOffset)
            {
                return default;
            }
            if (results == null)
            {
                PluginLogger.Warning("[补全] GetCompletionsAsync 返回 null", typeName);
                return default;
            }

            return results.ItemsList.ToImmutableArray();
        }
        catch (OperationCanceledException)
        {
            PluginLogger.Info("[补全] 查询被取消（新输入接管）", typeName);
            return default;
        }
        catch (Exception ex)
        {
            PluginLogger.Warning($"[补全] FetchCompletions 异常: {ex.GetType().Name}: {ex.Message}\n{ex.StackTrace}", typeName);
            return default;
        }
    }

    /// <summary>补全预热：后台对 user 文档跑一次 GetCompletionsAsync，让 CompletionService 缓存命中。</summary>
    public async Task WarmupCompletionAsync()
    {
        try
        {
            if (_workspace == null || _userDocumentId == null) return;
            var doc = _workspace.CurrentSolution.GetDocument(_userDocumentId);
            if (doc == null) return;

            var caretOffset = CodeEditor.Text.Length;
            var completionService = CompletionService.GetService(doc);
            if (completionService == null) return;

            var trigger = CompletionTrigger.CreateInsertionTrigger('a');
            await _analysisScheduler.RunAsync(false, ct => completionService.GetCompletionsAsync(doc, caretOffset, trigger, cancellationToken: ct), _analysisSession.LifetimeToken)
                .ConfigureAwait(false);
        }
        catch { /* 预热失败不阻塞主流程 */ }
    }

    // =======================================================
    // Phase 0: 签名帮助（语法树定位 InvocationExpression → OverloadInsightWindow 参数提示）
    // =======================================================

    /// <summary>
    /// 输入 '(' / ',' / ')' 后，用 Roslyn 内置签名帮助服务定位光标处方法调用并显示参数提示。
    /// 触发频率低（仅括号字符）+ 复用浮窗 + 取消竞态，无性能问题。
    /// </summary>
    private async Task ShowSignatureHelpAsync(int position, char triggerChar, CancellationToken token)
    {
        if (_sessionClosed) return;
        try { await PrepareWorkspaceAsync(token); }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { PluginLogger.Warning($"[签名帮助] {ex.Message}", typeName); return; }
        var stamp = _analysisSession.Capture();
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(token, _analysisSession.LifetimeToken);
        token = linked.Token;
        if (_workspace == null || _userDocumentId == null) return;

        var doc = _workspace.CurrentSolution.GetDocument(_userDocumentId);
        if (doc == null) return;

        // 触发原因：'(' 首次触发 → TypeCharCommand；',' / ')' 为补全中途续触发 → RetriggerCommand。
        var reason = triggerChar == '('
            ? SignatureHelpTriggerReason.TypeCharCommand
            : SignatureHelpTriggerReason.RetriggerCommand;
        var triggerInfo = new SignatureHelpTriggerInfo(reason, triggerChar);

        SignatureHelpItems? items = null;
        try
        {
            // 手写聚合全部 C# ISignatureHelpProvider（复制 RoslynPad AggregateSignatureHelpProvider.GetItemsAsync：
            // 取 ApplicableSpan 覆盖光标且最靠近光标的那组），不用 SignatureHelpService（其 API 在 4.12 不稳定）。
            items = await _analysisScheduler.RunAsync(true, async scheduledToken =>
            {
                var providers = GetSignatureHelpProviders();
                Microsoft.CodeAnalysis.SignatureHelp.SignatureHelpItems? best = null;
                foreach (var provider in providers)
                {
                    scheduledToken.ThrowIfCancellationRequested();
                    var current = await provider.GetItemsAsync(doc, position, triggerInfo, default, scheduledToken).ConfigureAwait(false);
                    if (current != null && current.ApplicableSpan.IntersectsWith(position)
                        && (best == null || current.ApplicableSpan.Start > best.ApplicableSpan.Start))
                        best = current;
                }
                return best;
            }, token);
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception ex)
        {
            PluginLogger.Warning($"[签名帮助] Roslyn 查询异常: {ex.GetType().Name}: {ex.Message}", typeName);
            return;
        }

        if (token.IsCancellationRequested) return;

        // 查询在后台线程完成（上面 ConfigureAwait(false)），所有 UI 操作必须切回 UI 线程（WPF 控件需 STA）。
        await CodeEditor.Dispatcher.InvokeAsync(() =>
        {
            if (token.IsCancellationRequested || !_analysisSession.IsCurrent(stamp)
                || CodeEditor.TextArea.Caret.Offset != position) return;
            // 返回 null：光标不在任何可签名帮助的方法调用内 → 关闭已打开的浮窗。
            if (items == null || items.Items.Count == 0)
            {
                PluginLogger.Info($"[签名帮助] 无适用方法调用，关闭浮窗: triggerChar='{triggerChar}'", typeName);
                _insightWindow?.Close();
                return;
            }

            PluginLogger.Info($"[签名帮助] 返回 {items.Items.Count} 个重载, SemanticParameterIndex={items.SemanticParameterIndex}", typeName);

            // 复用浮窗实例（对齐 RoslynPad _insightWindow 复用）
            if (_insightWindow == null)
            {
                _insightWindow = new OverloadInsightWindow(CodeEditor.TextArea);
                _insightWindow.Closed += (_, _) => { _insightWindow = null; };
            }
            // 每次显示前刷新主题色（Popup 不走可视化树 DynamicResource，切主题后复用实例必须重取）
            _insightWindow.Background = ThemeResource.GetBrush("ThemePanelBackgroundBrush");
            _insightWindow.Foreground = ThemeResource.GetBrush("ThemeTextPrimaryBrush");
            if (_insightWindow.Provider is ScriptOverloadProvider existing)
            {
                existing.Update(items);
            }
            else
            {
                _insightWindow.Provider = new ScriptOverloadProvider(items);
            }
            _insightWindow.Show();
        });
    }

    /// <summary>线程安全地获取全部 C# ISignatureHelpProvider（MEF 组合，进程内一次构建）。</summary>
    private static ImmutableArray<ISignatureHelpProvider> GetSignatureHelpProviders()
    {
        if (!_signatureHelpProviders.IsDefault) return _signatureHelpProviders;
        lock (_signatureHelpLock)
        {
            if (!_signatureHelpProviders.IsDefault) return _signatureHelpProviders;

            var sw = System.Diagnostics.Stopwatch.StartNew();
            // 从 Roslyn 默认 MEF 程序集构建组合容器，枚举所有 [Export(ISignatureHelpProvider)] 的 C# 提供器。
            // 关键：排除 Pythia 相关 part。Roslyn 4.12 的 PythiaSignatureHelpProvider 是 IDE/AI 场景的壳，
            // 它 [Import] IPythiaSignatureHelpProviderImplementation（由 VS Pythia 集成层导出，独立工具没有）。
            // 若不过滤，MEF 激活该 provider 时抛 CompositionFailedException，导致整个枚举失败。
            var partTypes = MefHostServices.DefaultAssemblies
                .SelectMany(a => a.DefinedTypes)
                .Select(t => t.AsType())
                .Where(t => !t.Name.Contains("Pythia"))
                .ToArray();
            using var container = new ContainerConfiguration().WithParts(partTypes).CreateContainer();
            var providers = container
                .GetExports<Lazy<ISignatureHelpProvider, OrderableLanguageMetadata>>()
                .Where(p => p.Metadata.Language == LanguageNames.CSharp)
                .Select(p => p.Value)
                .ToImmutableArray();
            sw.Stop();
            PluginLogger.Info($"[签名帮助] ISignatureHelpProvider 聚合初始化: {providers.Length} 个提供器, {sw.ElapsedMilliseconds}ms", nameof(ScriptEditorCore));

            _signatureHelpProviders = providers;
            return _signatureHelpProviders;
        }
    }

    /// <summary>
    /// 重建/复用工作区。首次建好后（含引用/项目/文档骨架），后续 SetParameters / 文本变化不再 Dispose 重建，
    /// 只更新文档文本。引用/代码来源必须已通过 SetReferenceProvider / SetCodeSource 注入。
    /// </summary>
    /// <param name="refreshReferences">用户增删 DLL 时传 true，强制刷新引用并完整重建；默认 false 走复用路径。</param>
    public void RebuildWorkspace(bool refreshReferences = false)
    {
        if (_getReferences == null && _getReferenceSnapshot == null)
            throw new InvalidOperationException("未注入引用来源：请先调用 SetReferenceProvider（rule-008 报错优先）。");
        if (_getGeneratedCode == null || _getUserCode == null)
            throw new InvalidOperationException("未注入代码来源：请先调用 SetCodeSource（rule-008 报错优先）。");

        var generatedCode = _getGeneratedCode();
        var userCode = _getUserCode();
        ScriptAnalysisReferences? snapshot;
        try { snapshot = _getReferenceSnapshot?.Invoke(); }
        catch
        {
            _analysisSession.AdvanceDocument();
            _diagnosticsCts?.Cancel();
            _throttleCts?.Cancel();
            _completionWindow?.Close();
            _currentDiagnostics = [];
            DiagnosticsUpdated?.Invoke([]);
            _insightWindow?.Close();
            _semanticColorizer?.Release();
            _workspaceInitialized = false;
            _userTextContainer?.Dispose();
            _userTextContainer = null;
            _workspace?.Dispose();
            _workspace = null;
            ResetSemanticHighlighter();
            RedrawVisibleLines();
            throw;
        }
        var identity = snapshot?.Identity ?? "external-provider";
        var previousStamp = _analysisSession.Capture();
        _analysisSession.SetEnvironment(identity, generatedCode);
        var environmentChanged = !_analysisSession.IsCurrent(previousStamp) || refreshReferences;
        if (environmentChanged)
        {
            _lastDiagnosedCode = null;
            _currentDiagnostics = [];
            DiagnosticsUpdated?.Invoke([]);
            _insightWindow?.Close();
            _semanticColorizer?.Release();
        }
        if (_workspaceInitialized && !refreshReferences && _referenceIdentity == identity)
        {
            if (_lastGeneratedCode != generatedCode)
            {
                _workspace!.SetCurrentSolution(_workspace.CurrentSolution
                    .WithDocumentText(_documentId!, SourceText.From(generatedCode)));
                _lastGeneratedCode = generatedCode;
            }
            if (environmentChanged) { ResetSemanticHighlighter(); RedrawVisibleLines(); }
            return;
        }
        _referenceIdentity = identity;
        _lastDiagnosedCode = null;
        _diagnosticsCts?.Cancel();
        _completionWindow?.Close();
        _currentDiagnostics = [];
        var totalSw = System.Diagnostics.Stopwatch.StartNew();

        PluginLogger.Info($"[补全] RebuildWorkspace 开始: generatedCodeLen={generatedCode.Length}, userCodeLen={userCode.Length}", typeName);

        _workspace?.Dispose();

        // 加载 MefHostServices 确保所有 CompletionProvider 可用（缓存机制，首次慢后续快）
        var sw = System.Diagnostics.Stopwatch.StartNew();
        var host = GetSharedMefHost();
        PluginLogger.Info($"[补全] MefHostServices 创建完成: {sw.ElapsedMilliseconds}ms", typeName);

        Project project;
        var preparedWorkspace = Interlocked.Exchange(ref _preWarmedWorkspace, null);
        if (preparedWorkspace != null)
        {
            _workspace = preparedWorkspace;
            PluginLogger.Info("[补全] 复用预热 Workspace", typeName);
        }
        else
        {
            _workspace = new ScriptWorkspace(host);
            PluginLogger.Info("[补全] ScriptWorkspace 实例已创建", typeName);
        }

        sw.Restart();
        var existingProject = _workspace.CurrentSolution.Projects.FirstOrDefault();
        if (existingProject != null)
        {
            project = existingProject.WithCompilationOptions(
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithAllowUnsafe(true));
            PluginLogger.Info($"[补全] 复用预热项目: {sw.ElapsedMilliseconds}ms", typeName);
        }
        else
        {
            var projectInfo = ProjectInfo.Create(ProjectId.CreateNewId(), VersionStamp.Create(), "ScriptCompletion", "ScriptCompletion", LanguageNames.CSharp);
            var newSolution = _workspace.CurrentSolution.AddProject(projectInfo);
            project = newSolution.GetProject(projectInfo.Id)!.WithCompilationOptions(
                new CSharpCompilationOptions(OutputKind.DynamicallyLinkedLibrary)
                    .WithAllowUnsafe(true));
            PluginLogger.Info($"[补全] 项目创建完成: {sw.ElapsedMilliseconds}ms", typeName);
        }

        // 扫描引用：引用来源由调用方（工具层）注入，合并 host 引用 + ScriptRefs 目录引用
        sw.Restart();
        var refs = snapshot?.References.ToList() ?? _getReferences!().ToList();
        var seenRefs = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var refCount = 0;
        foreach (var mdRef in refs)
        {
            if (mdRef is PortableExecutableReference per && per.FilePath != null)
            {
                string key;
                try { key = System.IO.Path.GetFullPath(per.FilePath); }
                catch { key = per.FilePath; }
                if (!seenRefs.Add(key)) continue;
            }
            project = project.AddMetadataReference(mdRef);
            refCount++;
        }
        PluginLogger.Info($"[补全] 已添加 {refCount} 个 MetadataReference: {sw.ElapsedMilliseconds}ms", typeName);

        sw.Restart();
        // 用 SetCurrentSolution 直接替换（对齐 RoslynPad SetSubmissionDocument），
        // 接受 AddProject + AddMetadataReference 全部变化，避免 TryApplyChanges 钩子链异常。
        _workspace.SetCurrentSolution(project.Solution);
        PluginLogger.Info($"[补全] SetCurrentSolution(项目+引用): {sw.ElapsedMilliseconds}ms", typeName);

        sw.Restart();

        // 添加两个文档：字段声明 partial class + 用户完整 partial class。
        var genDocId = DocumentId.CreateNewId(project.Id);
        var userDocId = DocumentId.CreateNewId(project.Id);

        var solution = project.Solution
            .AddDocument(DocumentInfo.Create(genDocId, "ScriptGenerated.cs", folders: null,
                sourceCodeKind: SourceCodeKind.Regular))
            .AddDocument(DocumentInfo.Create(userDocId, "ScriptUser.cs", folders: null,
                sourceCodeKind: SourceCodeKind.Regular));

        // 先以当前 generatedCode 文本同步 generated 文档（后续由诊断用 WithDocumentText 更新）
        solution = solution.WithDocumentText(genDocId, SourceText.From(generatedCode));

        // 用 SetCurrentSolution 直接替换（对齐 RoslynPad SetSubmissionDocument）：
        // TryApplyChanges 对 AddDocument 走 OnDocumentAdded 钩子链，在 Workspace 基类下会抛异常。
        _workspace.SetCurrentSolution(solution);
        _documentId = genDocId;

        // 用户文档绑定 SourceTextContainer 增量通道：容器每次编辑事件携带局部 TextChangeRange，
        // 经 ScriptWorkspace.OpenDocument 注册，Roslyn 只重解析受影响区域（对齐 RoslynPad workspace.OpenDocument 模式）。
        _userTextContainer?.Dispose();
        _userTextContainer = new AvalonEditTextContainer(CodeEditor.Document);
        _workspace.OpenDocument(userDocId, _userTextContainer);
        _userDocumentId = userDocId;

        PluginLogger.Info($"[补全] 文档添加完成: genDocId={_documentId}, userDocId={_userDocumentId}: {sw.ElapsedMilliseconds}ms", typeName);

        _workspaceInitialized = true;
        _lastGeneratedCode = generatedCode;
        if (environmentChanged) { ResetSemanticHighlighter(); RedrawVisibleLines(); }

        var refsCount = _workspace.CurrentSolution.Projects.First().MetadataReferences.Count;
        PluginLogger.Info($"[补全] Workspace 重建完成: {refsCount} 个引用, 总耗时={totalSw.ElapsedMilliseconds}ms", typeName);
    }

    /// <summary>获取用户文档（补全/诊断/高亮消费）。</summary>
    public Document? GetUserDocument()
        => !_analysisEnvironmentValid || _sessionClosed ? null : _getDocument?.Invoke() ?? _workspace?.CurrentSolution.GetDocument(_userDocumentId);

    // =======================================================
    // 支持类型：CompletionData 包装 / OverloadProvider 适配
    // =======================================================

    /// <summary>
    /// 将 Roslyn CompletionItem 包装为 AvalonEdit ICompletionData。
    /// 使用 CompletionService.GetChangeAsync 获取精确的 TextChange 插入（含后缀补全如 ()）。
    /// 图标通过委托注入（工具层走 GlyphMapping + Glyphs 资源），未注入时返回 null。
    /// </summary>
    private sealed class RoslynCompletionData(CompletionItem item, Document? document, Func<CompletionItem, ImageSource?>? iconProvider,
        ScriptAnalysisScheduler scheduler, CancellationToken lifetime) : ICompletionData
    {
        private ImageSource? _image;
        private object? _description;
        private bool _loadingDescription;

        public ImageSource? Image => _image ??= iconProvider?.Invoke(item);

        public string Text => item.FilterText;

        public object Content => item.DisplayText;

        public object? Description
        {
            get
            {
                // 延迟加载签名/文档描述：首次访问异步填充，未就绪时回退内联描述（不阻塞弹窗）
                if (!_loadingDescription)
                {
                    _loadingDescription = true;
                    _ = LoadDescriptionAsync();
                }
                return _description ?? item.InlineDescription;
            }
        }

        private async Task LoadDescriptionAsync()
        {
            if (document == null) return;
            try
            {
                var service = CompletionService.GetService(document);
                if (service == null) return;
                var description = await scheduler.RunAsync(false,
                    ct => service.GetDescriptionAsync(document, item, cancellationToken: ct), lifetime).ConfigureAwait(false);
                _description = string.Join(" ", description.TaggedParts.Select(p => p.Text));
            }
            catch
            {
                // 描述加载失败保持 InlineDescription（UI 提示降级，不阻塞补全）
                _description = item.InlineDescription;
            }
        }

        public double Priority => 0;

        public void Complete(TextArea textArea, ISegment completionSegment, EventArgs insertionRequestEventArgs)
        {
            // ★ 不走 GetChangeAsync：CompletionItem.Span 是查询时快照值，
            //    AvalonEdit 自动过滤不会更新它，导致替换范围偏小（如只替换 "s" 而非 "str"）
            // ★ 直接从编辑器实际文本计算替换范围
            var code = textArea.Document.Text;
            var start = completionSegment.Offset;
            var end = start;
            while (end < code.Length &&
                   (char.IsLetterOrDigit(code[end]) || code[end] == '_'))
                end++;
            var actualLen = Math.Min(end - start, 200);
            textArea.Document.Replace(start, actualLen, item.DisplayText);
        }
    }

    /// <summary>
    /// 把 Roslyn SignatureHelpItems 适配为 AvalonEdit IOverloadProvider（OverloadInsightWindow 数据源）。
    /// 对齐 RoslynPad RoslynOverloadProvider：Header 用 TaggedText 拼方法签名（当前参数加粗），
    /// Content 显示方法文档 + 当前参数文档（DocumentationFactory 惰性求值）。
    /// </summary>
    private sealed class ScriptOverloadProvider : System.ComponentModel.INotifyPropertyChanged, IOverloadProvider
    {
        private SignatureHelpItems _signatureHelp;
        private IList<SignatureHelpItem> _items;
        private int _selectedIndex;

        public ScriptOverloadProvider(SignatureHelpItems signatureHelp)
        {
            _signatureHelp = signatureHelp;
            _items = signatureHelp.Items;
            _selectedIndex = signatureHelp.SelectedItemIndex ?? 0;
        }

        public void Update(SignatureHelpItems signatureHelp)
        {
            _signatureHelp = signatureHelp;
            _items = signatureHelp.Items;
            _selectedIndex = Math.Clamp(signatureHelp.SelectedItemIndex ?? _selectedIndex, 0, Math.Max(0, _items.Count - 1));
            RaisePropertyChanged(nameof(CurrentIndexText));
            RaisePropertyChanged(nameof(CurrentHeader));
            RaisePropertyChanged(nameof(CurrentContent));
        }

        public int Count => _items.Count;

        public int SelectedIndex
        {
            get => _selectedIndex;
            set
            {
                if (_selectedIndex != value && value >= 0 && value < _items.Count)
                {
                    _selectedIndex = value;
                    RaisePropertyChanged(nameof(SelectedIndex));
                    RaisePropertyChanged(nameof(CurrentIndexText));
                    RaisePropertyChanged(nameof(CurrentHeader));
                    RaisePropertyChanged(nameof(CurrentContent));
                }
            }
        }

        public string CurrentIndexText => $"{_selectedIndex + 1}/{Count}";

        public object CurrentHeader => BuildHeader();

        public object CurrentContent => BuildContent();

        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        private void RaisePropertyChanged(string name)
            => PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(name));

        private object BuildHeader()
        {
            var item = _items[_selectedIndex];
            var header = new System.Windows.Controls.WrapPanel
            {
                Orientation = System.Windows.Controls.Orientation.Horizontal,
            };
            header.Children.Add(item.PrefixDisplayParts.ToTextBlock());
            if (!item.Parameters.IsDefault)
            {
                for (var i = 0; i < item.Parameters.Length; i++)
                {
                    var param = item.Parameters[i];
                    var isSelected = i == _signatureHelp.SemanticParameterIndex;
                    header.Children.Add(param.DisplayParts.ToTextBlock(isBold: isSelected));
                    if (i != item.Parameters.Length - 1)
                        header.Children.Add(item.SeparatorDisplayParts.ToTextBlock());
                }
            }
            header.Children.Add(item.SuffixDisplayParts.ToTextBlock());
            return header;
        }

        private object BuildContent()
        {
            var item = _items[_selectedIndex];
            var content = new System.Windows.Controls.StackPanel();
            // 方法文档（惰性求值）
            var methodDoc = item.DocumentationFactory(System.Threading.CancellationToken.None).ToTextBlock();
            if (methodDoc.Inlines.Count > 0)
                content.Children.Add(methodDoc);
            // 当前参数文档 + 参数名
            if (!item.Parameters.IsDefault)
            {
                var index = _signatureHelp.SemanticParameterIndex;
                if (index >= 0 && index < item.Parameters.Length)
                {
                    var param = item.Parameters[index];
                    var paramDoc = param.DocumentationFactory(System.Threading.CancellationToken.None).ToTextBlock();
                    if (paramDoc.Inlines.Count > 0)
                    {
                        content.Children.Add(new System.Windows.Controls.WrapPanel
                        {
                            Orientation = System.Windows.Controls.Orientation.Horizontal,
                            Children =
                            {
                                new System.Windows.Controls.TextBlock
                                {
                                    Text = param.Name + ": ",
                                    FontWeight = System.Windows.FontWeights.Bold,
                                },
                                paramDoc,
                            },
                        });
                    }
                }
            }
            if (content.Children.Count == 0)
            {
                content.Children.Add(new System.Windows.Controls.TextBlock
                { Text = "无参数", TextWrapping = System.Windows.TextWrapping.Wrap });
            }
            return content;
        }
    }
}

/// <summary>主题资源工具：读取 UI 全局资源（工具插件/宿主 UI 靠资源键访问，不直接引用具体主题程序集）。</summary>
internal static class ThemeResource
{
    public static Brush? GetBrush(string key)
    {
        var res = Application.Current?.Resources;
        if (res == null || string.IsNullOrEmpty(key)) return null;
        return res[key] as Brush;
    }

    /// <summary>判断当前是否暗色主题：扫描 MergedDictionaries 是否含 DarkTheme 字典（靠 URI 识别，不引用具体程序集）。</summary>
    public static bool IsDarkTheme()
    {
        var res = Application.Current?.Resources;
        if (res == null) return false;
        foreach (var dict in res.MergedDictionaries)
        {
            if (dict.Source != null &&
                dict.Source.OriginalString.Contains("Themes/Dark/DarkTheme.xaml", StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }
}

/// <summary>
/// Roslyn TaggedText → WPF TextBlock（签名帮助 Header/Content 渲染）。
/// 对齐 RoslynPad SymbolDisplayPartExtensions.ToTextBlock：逐段 TaggedText 生成 Run，
/// 支持 isBold 高亮当前参数，按文本标签着色。
/// 必须是命名空间顶层静态类（扩展方法不能定义在嵌套类中）。
/// </summary>
internal static class TaggedTextExtensions
{
    public static TextBlock ToTextBlock(this IEnumerable<TaggedText> text, bool isBold = false)
    {
        var result = new TextBlock { TextWrapping = TextWrapping.Wrap };
        foreach (var part in text)
        {
            result.Inlines.Add(part.ToRun(isBold));
        }
        return result;
    }

    public static Run ToRun(this TaggedText text, bool isBold = false)
    {
        var run = new Run(text.Text);
        if (isBold) run.FontWeight = FontWeights.Bold;

        // 对齐 RoslynPad：关键词蓝、类型青（签名帮助浮窗为辅助 Popup，直接用固定 Brushes 色）。
        switch (text.Tag)
        {
            case TextTags.Keyword:
                run.Foreground = Brushes.Blue;
                break;
            case TextTags.Struct:
            case TextTags.Enum:
            case TextTags.TypeParameter:
            case TextTags.Class:
            case TextTags.Delegate:
            case TextTags.Interface:
                run.Foreground = Brushes.Teal;
                break;
        }
        return run;
    }
}

/// <summary>
/// 支持打开文档的 Workspace（对齐 RoslynPad RoslynWorkspace.CanOpenDocuments）。
/// AdhocWorkspace 是 sealed 且 CanOpenDocuments=false，无法派生；故继承 Workspace 基类。
/// 用户文档经 AvalonEditTextContainer（SourceTextContainer）注册到 OpenDocument，
/// Roslyn 走增量文本通道，仅重解析受影响语法树区域（替代 TextLoader 全量通道，消除打字卡顿）。
/// </summary>
public sealed class ScriptWorkspace : Workspace
{
    public ScriptWorkspace(MefHostServices host) : base(host, WorkspaceKind.Host) { }

    public override bool CanOpenDocuments => true;

    // RebuildWorkspace 运行期重建 workspace：加项目、加元数据引用、加文档；
    // 后续诊断更新 generated 文档文本（ChangeDocument）。全部放行，
    // 否则 TryApplyChanges 对不支持的变化类型抛 NotSupportedException 导致 workspace 半初始化。
    public override bool CanApplyChange(ApplyChangesKind feature)
        => feature is ApplyChangesKind.AddProject
            or ApplyChangesKind.AddMetadataReference
            or ApplyChangesKind.AddDocument
            or ApplyChangesKind.ChangeDocument;

    /// <summary>将编辑器文档（SourceTextContainer）注册为增量文本通道。</summary>
    public void OpenDocument(DocumentId documentId, SourceTextContainer textContainer)
    {
        OnDocumentOpened(documentId, textContainer);
        OnDocumentContextUpdated(documentId);
    }

    /// <summary>
    /// 直接替换整个 solution（暴露基类 protected SetCurrentSolution，对齐 RoslynPad RoslynWorkspace）。
    /// RebuildWorkspace 用它一次性接受 AddProject/AddMetadataReference/AddDocument/WithDocumentText 全部变化，
    /// 不走 TryApplyChanges 的 OnProjectAdded/OnDocumentAdded 钩子链（Workspace 基类下这些钩子可能抛异常）。
    /// </summary>
    public new void SetCurrentSolution(Solution solution)
        => base.SetCurrentSolution(solution);

    protected override void ApplyDocumentTextChanged(DocumentId document, SourceText newText)
        => OnDocumentTextChanged(document, newText, PreservationMode.PreserveIdentity);
}
