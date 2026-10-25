using System.IO;
using System.Reflection;
using SunEyeVision.Workflow;
using SunEyeVision.Tool.BlobAnalysis;
using SunEyeVision.Plugin.SDK.Logging;
using SunEyeVision.Plugin.SDK.Execution.Parameters;

internal static class S2DocumentEditingCases
{
    [STAThread]
    static int Main()
    {
        var root = Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT") ?? throw new Exception("缺少正式产物路径");
        System.Runtime.Loader.AssemblyLoadContext.Default.Resolving += (_, name) =>
        {
            var paths = new[] { Path.Combine(root, name.Name + ".dll"), Path.Combine(root, "plugins", name.Name!, name.Name + ".dll") };
            foreach (var path in paths) if (File.Exists(path)) return System.Runtime.Loader.AssemblyLoadContext.Default.LoadFromAssemblyPath(path);
            return null;
        };
        try { return Run(); }
        catch (Exception error) { Console.Error.WriteLine(error); return 1; }
    }
    static void Require(bool value, string message) { if (!value) throw new Exception(message); }
    static void Reject(Action action)
    { try { action(); } catch (InvalidOperationException) { return; } throw new Exception("应拒绝的事务被接受"); }
    sealed class Participant : IDocumentEditParticipant
    {
        readonly DocumentEditSession _session;
        public Guid ScopeId { get; } = Guid.NewGuid();
        public string ScopeKey { get; }
        public NodeConfigurationEditDraft Draft;
        public int Prepared, Accepted;
        public Action? AfterPrepare, AfterAccept;
        public bool ThrowOnAccept;
        public bool HasPendingChanges => _session.Host.HasPendingChanges(Draft);
        public Participant(DocumentEditSession session, string node)
        { _session = session; ScopeKey = "node:flow/" + node; Draft = session.Host.BeginEdit("flow", node); session.Register(this); }
        public BlobAnalysisParameters Parameters => (BlobAnalysisParameters)Draft.Node.Parameters;
        public DocumentEditProposal Prepare(NodeConfigurationEditHost host)
        { Prepared++; var result = DocumentEditProposal.ForNode(host, Draft, Draft); AfterPrepare?.Invoke(); return result; }
        public void Acknowledge(DocumentEditProposal proposal, BindingConfigurationPublication publication)
        {
            Accepted++; AfterAccept?.Invoke();
            if (ThrowOnAccept) throw new InvalidOperationException("意图注入：界面刷新失败");
            Draft = proposal.Matches(Draft) ? _session.Host.BeginEdit("flow", Draft.Identity.NodeId) : _session.Host.RebaseAcceptedDraft(Draft);
        }
        public void Cancel() => _session.Unregister(ScopeId);
    }
    sealed class DebugRequestProbe : SunEyeVision.Plugin.SDK.UI.Controls.ToolDebugControlBase
    {
        public SunEyeVision.Plugin.SDK.Core.ToolExecutionRequest Request(SunEyeVision.Plugin.SDK.Execution.Parameters.ToolParameters parameters,
            SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage image) => CreateLiveExecutionRequest(parameters, image);
        protected override object ExecuteTool() => throw new NotSupportedException();
    }
    sealed class ReleaseAction(Action release) : IDisposable
    {
        public void Dispose() => release();
    }
    sealed class VariableParticipant : IDocumentEditParticipant
    {
        readonly DocumentEditSession _session;
        public Guid ScopeId { get; } = Guid.NewGuid();
        public string ScopeKey => "variables";
        public GlobalVariableConfigurationEditDraft Draft;
        public Action? AfterPrepare;
        public bool HasPendingChanges => _session.Host.HasPendingChanges(Draft);
        public VariableParticipant(DocumentEditSession session)
        { _session = session; Draft = session.Host.BeginVariableEdit(); session.Register(this); }
        public DocumentEditProposal Prepare(NodeConfigurationEditHost host)
        { var result = DocumentEditProposal.ForVariables(host, Draft, Draft); AfterPrepare?.Invoke(); return result; }
        public void Acknowledge(DocumentEditProposal prepared, BindingConfigurationPublication publication)
        { Draft = prepared.Matches(Draft) ? _session.Host.BeginVariableEdit() : prepared.PreserveNewerVariableEdits(_session.Host, Draft); }
        public void Cancel() => _session.Unregister(ScopeId);
    }
    static GlobalVariable AddVariable(Fixture fixture)
    {
        var variable = new GlobalVariable { Id = "v", Name = "Count", Type = "Integer", Value = 10 };
        fixture.Document.GlobalVariables.Add(variable); return variable;
    }
    sealed class BlobViewFixture : IDisposable
    {
        public readonly Fixture Model = Create();
        public readonly SunEyeVision.Plugin.SDK.UI.Controls.ToolDebugControlBase Control;
        public readonly SunEyeVision.Plugin.SDK.UI.Windows.CustomDebugWindow Window;
        public readonly SunEyeVision.UI.Services.Editing.ProductionEditorContext Context;
        public BlobViewFixture()
        {
            var tool = new BlobAnalysisTool();
            Control = (SunEyeVision.Plugin.SDK.UI.Controls.ToolDebugControlBase)
                (SunEyeVision.UI.Services.Editing.ToolConfigurationEditors.CreateControl(tool)
                    ??throw new Exception("正式发布没有Blob配置入口"));
            Window = new(Control) { ShowActivated = false, ShowInTaskbar = false, Left = -10000, Width = 460, Height = 650 };
            var query = new SunEyeVision.Plugin.SDK.Execution.Parameters.DataSourceQueryService();
            void Bind(WorkflowNodeBase draft)
            { Control.SetCurrentNode(draft); Control.SetParameters(draft.Parameters); Control.SetDataProvider(query); }
            Context = new(Model.Editing.Host, "flow", Model.A, () => true, Bind, documentEdits: Model.Editing);
            query.ExecutionOwner = Context.DebugOwner; query.EditScopeCancellation = Context.DebugCancellation;
            Bind(Context.DraftNode);
            SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.AttachDraftContext(Control, Context);
            typeof(SunEyeVision.UI.Services.Editing.ProductionEditorContext).GetProperty("PrepareView", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Context, (Func<string?>)(() => SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.TryPrepare(Control, out var error) ? null : error));
            typeof(SunEyeVision.UI.Services.Editing.ProductionEditorContext).GetProperty("HasPendingViewEdits", BindingFlags.Instance | BindingFlags.NonPublic)!
                .SetValue(Context, (Func<bool>)(() => SunEyeVision.Plugin.SDK.UI.Controls.ToolEditorCommit.HasPendingEdits(Control)));
            Window.Show(); Pump();
        }
        public static void Pump()
        {
            var frame = new System.Windows.Threading.DispatcherFrame();
            System.Windows.Threading.Dispatcher.CurrentDispatcher.BeginInvoke(System.Windows.Threading.DispatcherPriority.ApplicationIdle,
                new Action(() => frame.Continue = false));
            System.Windows.Threading.Dispatcher.PushFrame(frame);
        }
        static IEnumerable<System.Windows.DependencyObject> Visuals(System.Windows.DependencyObject root)
        {
            yield return root;
            for (int i = 0; i < System.Windows.Media.VisualTreeHelper.GetChildrenCount(root); i++)
                foreach (var child in Visuals(System.Windows.Media.VisualTreeHelper.GetChild(root, i))) yield return child;
        }
        public System.Windows.Controls.TextBox ThresholdText()
        {
            var tabs = Logicals(Control).OfType<System.Windows.Controls.TabControl>().First(); tabs.SelectedIndex = 1; Pump(); Window.UpdateLayout();
            var fields=Logicals(Control).OfType<SunEyeVision.Plugin.Wpf.Fields.DeclaredFieldsView>().Single();
            var key=((IConfigurationFieldProvider)Context.DraftNode.Parameters).GetConfigurationFields().Single(item=>item.StoragePath=="Threshold").Port.Id;
            return (System.Windows.Controls.TextBox)fields.Fields.Single(item=>item.FieldId==key).InputElement!;
        }
        static IEnumerable<System.Windows.DependencyObject> Logicals(System.Windows.DependencyObject root)
        {yield return root;foreach(var item in System.Windows.LogicalTreeHelper.GetChildren(root))if(item is System.Windows.DependencyObject child)foreach(var next in Logicals(child))yield return next;}
        public void Dispose() { Context.Discard(); Window.Close(); }
    }
    sealed record Fixture(Solution Document, DocumentEditSession Editing, WorkflowNodeBase A, WorkflowNodeBase B);
    static Fixture Create()
    {
        var a = new WorkflowNodeBase("a", "A", "A", "BlobAnalysis") { Parameters = new BlobAnalysisParameters() };
        var b = new WorkflowNodeBase("b", "B", "B", "BlobAnalysis") { Parameters = new BlobAnalysisParameters() };
        var flow = new Workflow("flow", "flow"); flow.Nodes.Add(a); flow.Nodes.Add(b);
        var document = new Solution(); document.Workflows.Add(flow);
        var editing = (DocumentEditSession)Activator.CreateInstance(typeof(DocumentEditSession), BindingFlags.Instance | BindingFlags.NonPublic,
            null, new object[] { document }, null)!;
        return new(document, editing, a, b);
    }
    static int Run()
    {
        // This scope fixture deliberately constructs strongly typed Blob models. Register that
        // exact loaded type, with the real payload-verified component factories, instead of mixing
        // a default-context model with another model loaded by the isolated plugin context.
        // Actual isolated discovery/registration is exercised by S3 Components and S4 field checks.
        var authorityType=typeof(SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginManager).Assembly
            .GetType("SunEyeVision.Plugin.Infrastructure.Managers.Plugin.ReleasePayloadAuthority",true)!;
        var authority=Activator.CreateInstance(authorityType,BindingFlags.Instance|BindingFlags.NonPublic,null,
            new object[]{Environment.GetEnvironmentVariable("S2_PRODUCT_ROOT")!},null)!;
        var components=(IReadOnlyList<SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolComponentRegistration>)authorityType
            .GetMethod("LoadToolComponents",BindingFlags.Instance|BindingFlags.NonPublic)!.Invoke(authority,null)!;
        using(var commit=SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.PrepareRegistration(new[]{typeof(BlobAnalysisTool)},components).EnterCommit())commit.Publish();
        if(System.Windows.Application.Current is null)
        {
            var app=new System.Windows.Application {ShutdownMode=System.Windows.ShutdownMode.OnExplicitShutdown};
            foreach(var resource in new[]{"Shell/Resources/AppResources.xaml","Resources/Styles/NodeStyleResources.xaml",
                "Resources/Styles/ConnectionStyleResources.xaml","Resources/Styles/CanvasStyleResources.xaml",
                "Resources/Styles/CommonStyleResources.xaml","Resources/Styles/MenuStyleResources.xaml",
                "Resources/Styles/ToolTipStyleResources.xaml","Themes/Dark/DarkTheme.xaml"})
                app.Resources.MergedDictionaries.Add(new System.Windows.ResourceDictionary {Source=new Uri("pack://application:,,,/SunEyeVision.UI;component/"+resource)});
        }
        int passed = 0;
        void Case(string name, Action action) { action(); Console.WriteLine("通过：" + name); passed++; }
        Case("Apply仅当前范围，不接受另一个未确认草稿", () => {
            var f = Create(); var a = new Participant(f.Editing, "a"); var b = new Participant(f.Editing, "b");
            a.Parameters.Threshold = 10; b.Parameters.Threshold = 20; f.Editing.Apply(a.ScopeId);
            Require(((BlobAnalysisParameters)f.A.Parameters).Threshold == 10 && ((BlobAnalysisParameters)f.B.Parameters).Threshold == 128, "Apply越过范围");
            Require(b.Prepared == 0 && b.Accepted == 0 && f.Editing.HasPendingChanges, "其他草稿被处理或丢失");
        });
        Case("Save接受所有登记范围，与窗口加载状态无关", () => {
            var f = Create(); var a = new Participant(f.Editing, "a"); var b = new Participant(f.Editing, "b");
            a.Parameters.Threshold = 10; b.Parameters.Threshold = 20; f.Editing.AcceptAll();
            Require(((BlobAnalysisParameters)f.A.Parameters).Threshold == 10 && ((BlobAnalysisParameters)f.B.Parameters).Threshold == 20, "登记范围遗漏");
            Require(a.Accepted == 1 && b.Accepted == 1 && !f.Editing.HasPendingChanges, "保存后状态不正确");
        });
        Case("任一无效参数使整批拒绝，原草稿保留", () => {
            var f = Create(); var a = new Participant(f.Editing, "a"); var b = new Participant(f.Editing, "b");
            a.Parameters.Threshold = 10; b.Parameters.Threshold = -1; Reject(() => f.Editing.AcceptAll());
            Require(((BlobAnalysisParameters)f.A.Parameters).Threshold == 128 && a.Parameters.Threshold == 10 && b.Parameters.Threshold == -1, "出现部分接受或丢输入");
        });
        Case("准备结果冻结，后到输入不进入已准备事务", () => {
            var f = Create(); var a = new Participant(f.Editing, "a"); a.Parameters.Threshold = 10;
            a.AfterPrepare = () => a.Parameters.Threshold = 20; f.Editing.AcceptAll();
            Require(((BlobAnalysisParameters)f.A.Parameters).Threshold == 10 && a.Parameters.Threshold == 20, "准备结果被可变草稿穿透");
            a.AfterPrepare = null; f.Editing.AcceptAll(); Require(((BlobAnalysisParameters)f.A.Parameters).Threshold == 20, "新输入不能继续接受");
        });
        Case("刷新失败不回滚已经接受的配置", () => {
            var f = Create(); var a = new Participant(f.Editing, "a"); a.Parameters.Threshold = 10; a.ThrowOnAccept = true;
            f.Editing.AcceptAll(); Require(((BlobAnalysisParameters)f.A.Parameters).Threshold == 10 && f.Editing.LastRefreshError != null, "刷新失败改变了接受事实");
        });
        Case("取消只撤登记草稿，不回滚已经Apply的内容", () => {
            var f = Create(); var a = new Participant(f.Editing, "a"); a.Parameters.Threshold = 10; f.Editing.Apply(a.ScopeId);
            a.Parameters.Threshold = 20; a.Cancel(); f.Editing.AcceptAll();
            Require(((BlobAnalysisParameters)f.A.Parameters).Threshold == 10 && f.Editing.RegisteredScopeCount == 0, "取消回滚已接受内容");
        });
        Case("同一范围不能建立两个发布者", () => {
            var f = Create(); _ = new Participant(f.Editing, "a"); Reject(() => new Participant(f.Editing, "a"));
            Require(f.Editing.RegisteredScopeCount == 1, "重复登记污染注册表");
        });
        Case("关闭会话取消登记，不能继续提交", () => {
            var f = Create(); var a = new Participant(f.Editing, "a"); a.Parameters.Threshold = 10; f.Editing.Close();
            Reject(() => f.Editing.Apply(a.ScopeId)); Require(((BlobAnalysisParameters)f.A.Parameters).Threshold == 128, "关闭接受了草稿");
        });
        Case("变量草稿不因另一个节点Apply而被隐式接受或无故过期", () => {
            var f = Create(); var live = AddVariable(f); var a = new Participant(f.Editing, "a"); var v = new VariableParticipant(f.Editing);
            a.Parameters.Threshold = 10; v.Draft.Configuration.GlobalVariables[0].Name = "EditedCount";
            f.Editing.Apply(a.ScopeId); Require(live.Name == "Count", "节点Apply接受了变量草稿");
            f.Editing.AcceptAll(); Require(live.Name == "EditedCount", "保存无法接受仍合法的变量草稿");
        });
        Case("未编辑实时值时，保存定义保留最新运行值", () => {
            var f = Create(); var live = AddVariable(f); var v = new VariableParticipant(f.Editing);
            v.Draft.Configuration.GlobalVariables[0].Description = "changed"; live.Value = 30;
            f.Editing.AcceptAll(); Require((int)live.Value! == 30 && live.Description == "changed", "保存覆盖实时值");
            f.Editing.Host.Undo(); Require((int)live.Value! == 30, "配置Undo回滚实时值");
        });
        Case("纯值命令推进ValueRevision，不推进配置版本或Undo", () => {
            var f = Create(); var live = AddVariable(f); var v = new VariableParticipant(f.Editing);
            var version = f.Editing.Host.Capture().Version; var revision = live.ValueRevision;
            v.Draft.SetValue("v", 20); f.Editing.AcceptAll();
            Require((int)live.Value! == 20 && live.ValueRevision == revision + 1, "值命令未执行CAS接受");
            Require(f.Editing.Host.Capture().Version == version && !f.Editing.Host.CanUndo, "纯值命令污染配置Undo或版本");
        });
        Case("显式相同值也执行CAS，运行值ABA必须拒绝", () => {
            var f = Create(); var live = AddVariable(f); var v = new VariableParticipant(f.Editing);
            v.Draft.SetValue("v", 10); var revision = live.ValueRevision; f.Editing.AcceptAll();
            Require(live.ValueRevision == revision + 1, "显式相同值跳过CAS");
            v.Draft.SetValue("v", 20); live.Value = 11; live.Value = 10;
            Reject(() => f.Editing.AcceptAll()); Require((int)live.Value! == 10, "ABA覆盖了实时值");
        });
        Case("接受回执保留新的变量值命令，下一次可继续保存", () => {
            var f = Create(); var live = AddVariable(f); var v = new VariableParticipant(f.Editing);
            v.Draft.SetValue("v", 20); v.AfterPrepare = () => v.Draft.SetValue("v", 30); f.Editing.AcceptAll();
            Require((int)live.Value! == 20 && v.Draft.HasValueEdits, "后到命令被接受或丢弃");
            v.AfterPrepare = null; f.Editing.AcceptAll(); Require((int)live.Value! == 30, "保留的新命令无法接受");
        });
        Case("根和子流程在一个冻结配置中，不随随后Apply改变", () => {
            var f = Create(); f.A.SubroutineId = "child";
            var child = new Workflow("child", "child"); child.Nodes.Add(new WorkflowNodeBase("c", "C", "C", "BlobAnalysis") { Parameters = new BlobAnalysisParameters() });
            f.Document.Workflows.Add(child);
            var frozen = WorkflowRunConfiguration.Capture("flow", id => f.Document.Workflows.SingleOrDefault(w => w.Id == id));
            ((BlobAnalysisParameters)f.A.Parameters).Threshold = 200; ((BlobAnalysisParameters)child.Nodes[0].Parameters).Threshold = 201;
            Require(((BlobAnalysisParameters)frozen.CopyWorkflow("flow")!.Nodes[0].Parameters).Threshold == 128 &&
                ((BlobAnalysisParameters)frozen.CopyWorkflow("child")!.Nodes[0].Parameters).Threshold == 128, "同轮配置被后续变化穿透");
        });
        Case("调试结果归当前编辑范围，正式结果不被污染，关闭释放最后资源", () => {
            var f = Create(); using var editor = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(
                f.Editing.Host, "flow", f.A, () => true, _ => { }, documentEdits: f.Editing);
            var production = new WorkflowContext();
            var previous = new SunEyeVision.Plugin.SDK.Execution.Results.GenericToolResults();
            production.SetNodeResult("a", new SunEyeVision.Plugin.SDK.Execution.Results.NodeExecutionResult { NodeId = "a", Result = previous });
            int releases = 0;
            var preview = new SunEyeVision.Plugin.SDK.Execution.Results.GenericToolResults {
                ExecutionIdentity = editor.DebugOwner.Begin("flow/a", Guid.NewGuid(), "test", editor.DebugCancellation)
            };
            preview.RegisterOutputResource(new ReleaseAction(() => releases++));
            Require(editor.TryAcceptDebugResult(preview), "编辑范围拒绝自己的预览");
            preview.Resources.Dispose();
            Require(releases == 0 && ReferenceEquals(production.GetNodeResult("a")!.Result, previous), "调试污染正式结果或提前释放");
            editor.Discard();
            Require(editor.DebugCancellation.IsCancellationRequested && releases == 1 && editor.DebugResults.GetNodeResult("a") == null,
                "关闭没有撤销调试和释放结果");
            Require(!preview.ExecutionIdentity.IsCurrent, "关闭后结果仍可发布");
            production.ClearNodeResults();
        });
        Case("真实HALCON调试使用冻结草稿，和生产执行身份隔离", () => {
            var f = Create(); var formal = (BlobAnalysisParameters)f.A.Parameters;
            formal.IsPreprocessingEnabled = false; formal.IsMorphologyEnabled = false;
            formal.IsAreaFilterEnabled = false; formal.IsCircularityFilterEnabled = false;
            formal.IsRectangularityFilterEnabled = false; formal.IsBlobCountFilterEnabled = false;
            using var editor = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(
                f.Editing.Host, "flow", f.A, () => true, _ => { }, documentEdits: f.Editing);
            ((BlobAnalysisParameters)editor.DraftNode.Parameters).Threshold = 220;
            var pixels = new byte[32 * 32];
            for (int y = 4; y < 8; y++) for (int x = 3; x < 7; x++) pixels[y * 32 + x] = 200;
            using var input = SunEyeVision.HalAdapter.HalImage.CreateFromBytes(pixels, 32, 32, 1);
            var productionOwner = f.Document.ExecutionOwner; var productionRun = Guid.NewGuid();
            productionOwner.ActivateRoot(productionRun);
            try {
                var parameters = ToolParameterSnapshot.Create(editor.DraftNode.Parameters);
                var request = new SunEyeVision.Plugin.SDK.Core.ToolExecutionRequest(parameters) {
                    NodeId = "a", WorkflowId = "flow", ExecutionOwner = editor.DebugOwner,
                    CancellationToken = editor.DebugCancellation, ResolvedInputImage = input, RunId = Guid.NewGuid()
                };
                var executor = new SunEyeVision.Plugin.SDK.Core.ToolExecutor(
                    new SunEyeVision.Plugin.SDK.Execution.Parameters.ParameterResolver(), VisionLogger.Instance);
                var result = (BlobAnalysisResults)executor.Run(new BlobAnalysisTool(), request);
                try {
                    Require(result.IsSuccess && result.FilteredBlobCount == 0, "HALCON没有使用草稿阈值220");
                    Require(editor.TryAcceptDebugResult(result) && formal.Threshold == 128 && !f.Editing.Host.CanUndo,
                        "调试接受了正式配置或没有独立结果");
                    Require(ReferenceEquals(result.ExecutionIdentity!.Owner, editor.DebugOwner) && !ReferenceEquals(result.ExecutionIdentity.Owner, productionOwner),
                        "调试与生产共用owner");
                } finally { result.Resources?.Dispose(); }
            } finally { productionOwner.CompleteRoot(productionRun); }
        });
        Case("节点快照保留常量整数类型并隔离数组", () => {
            var f = Create(); var values = new long[] { 3, 4 };
            f.A.ParamSettings.SetSetting(SunEyeVision.Plugin.SDK.Execution.Parameters.ParamSetting.CreateConstant("Threshold", 64));
            f.A.ParamSettings.SetSetting(SunEyeVision.Plugin.SDK.Execution.Parameters.ParamSetting.CreateConstant("sample", values));
            var captured = WorkflowRunConfiguration.Capture(f.Document.Workflows).CopyWorkflow("flow")!.Nodes[0];
            Require(captured.ParamSettings.GetSetting("Threshold")!.ConstantValue is int value && value == 64, "整数变成了JsonElement或其他类型");
            values[0] = 99;
            Require(captured.ParamSettings.GetSetting("sample")!.ConstantValue is long[] copy && copy[0] == 3, "数组与正式配置共享可变存储");
        });
        Case("变量范围涉及已被另一个范围修改的节点时拒绝覆盖", () => {
            var f = Create(); AddVariable(f); var host = f.Editing.Host;
            var variables = host.BeginVariableEdit(); var node = host.BeginEdit("flow", "a");
            ((BlobAnalysisParameters)variables.Configuration.Workflows[0].Nodes[0].Parameters).Threshold = 200;
            ((BlobAnalysisParameters)node.Node.Parameters).Threshold = 10; host.Commit(node);
            Reject(() => host.CommitBatch(Array.Empty<NodeConfigurationEditDraft>(), variables));
            Require(((BlobAnalysisParameters)f.A.Parameters).Threshold == 10, "过期变量范围覆盖了已确认节点");
        });
        Case("真实Blob窗口未失焦输入在隐藏及卸载后仍参与文档保存", () => {
            using var view = new BlobViewFixture();
            var text = view.ThresholdText(); text.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, "181");
            view.Window.Hide(); view.Model.Editing.AcceptAll();
            Require(((BlobAnalysisParameters)view.Model.A.Parameters).Threshold == 181, "隐藏窗口漏掉尚未失焦的数值");
            text = view.ThresholdText(); text.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, "191");
            view.Window.Content = null; BlobViewFixture.Pump();
            Require(!view.Control.IsLoaded && view.Model.Editing.RegisteredScopeCount == 1, "卸载错误取消了编辑范围");
            view.Model.Editing.AcceptAll();
            Require(((BlobAnalysisParameters)view.Model.A.Parameters).Threshold == 191, "卸载后没有准备登记草稿");
        });
        Case("真实Blob窗口无效输入拒绝保存，取消保持正式参数并退出范围", () => {
            using var view = new BlobViewFixture(); var text = view.ThresholdText();
            text.SetCurrentValue(System.Windows.Controls.TextBox.TextProperty, "999999999999999999999");
            Reject(() => view.Model.Editing.AcceptAll());
            Require(text.Text == "999999999999999999999" && ((BlobAnalysisParameters)view.Model.A.Parameters).Threshold == 128,
                "无效输入丢失或部分接受");
            view.Window.Close();
            Require(view.Model.Editing.RegisteredScopeCount == 0 && ((BlobAnalysisParameters)view.Model.A.Parameters).Threshold == 128,
                "窗口关闭未取消草稿或改动了正式配置");
        });
        Case("文档草稿整体保存、锁文件失败与重试、重开保持同一确认配置", () => {
            var folder = Path.Combine(Environment.GetEnvironmentVariable("S2_CASE_ROOT")!, "save-reopen"); Directory.CreateDirectory(folder);
            SunEyeVision.Plugin.Infrastructure.Managers.Plugin.PluginLoader.MarkAsLoaded();
            var manager = new SolutionManager(folder); var document = manager.CreateUntitledSolution(); document.Name = "S2试用方案";
            document.Workflows.Clear(); var flow = new Workflow("flow", "flow");
            var a = new WorkflowNodeBase("a", "A", "A", "BlobAnalysis") { Parameters = new BlobAnalysisParameters() };
            var b = new WorkflowNodeBase("b", "B", "B", "BlobAnalysis") { Parameters = new BlobAnalysisParameters() };
            flow.Nodes.Add(a); flow.Nodes.Add(b); document.Workflows.Add(flow);
            var live = new GlobalVariable { Id = "v", Name = "Count", Type = "Integer", Value = 10 }; document.GlobalVariables.Add(live);
            var editing = manager.Session!.Editing; var node = new Participant(editing, "a"); var variables = new VariableParticipant(editing);
            var service = new SunEyeVision.UI.Services.Solutions.SolutionApplicationService(manager);
            var path = Path.Combine(folder, "s2-trial.solution");
            node.Parameters.Threshold = 181; variables.Draft.Configuration.GlobalVariables[0].Name = "SavedCount"; variables.Draft.SetValue("v", 20);
            var first = service.Save(path, () => editing.AcceptAll());
            Require(first.FileWritten && !manager.HasUnsavedChanges, "首次整批保存失败或仍报告未保存：" + first.Error);
            var original = File.ReadAllBytes(path);
            node.Parameters.Threshold = 191; variables.Draft.SetValue("v", 30);
            using (var locked = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None)) {
                var failed = service.Save(path, () => editing.AcceptAll());
                Require(!failed.FileWritten && manager.HasUnsavedChanges && ((BlobAnalysisParameters)a.Parameters).Threshold == 191 && (int)live.Value! == 30,
                    "磁盘失败丢弃了已接受配置或错误标为已保存");
            }
            Require(original.SequenceEqual(File.ReadAllBytes(path)), "保存失败改动原盘");
            var revision = live.ValueRevision; var configurationVersion = editing.Host.Capture().Version;
            var retried = service.Save(path, () => editing.AcceptAll());
            Require(retried.FileWritten && !manager.HasUnsavedChanges && live.ValueRevision == revision && editing.Host.Capture().Version == configurationVersion,
                "保存重试重复接受值命令或没有正确完成：" + retried.Error);
            var reopened = manager.LoadSolutionOnly(path) ?? throw new Exception(manager.LastError);
            Require(((BlobAnalysisParameters)reopened.Workflows.Single().Nodes[0].Parameters).Threshold == 191 &&
                reopened.GlobalVariables[0].Name == "SavedCount" && reopened.GlobalVariables[0].Value is long[] stored && stored.SequenceEqual(new long[] { 30 }),
                "保存重开与已确认参数、变量不一致");
            editing.Close();
        });
        Case("关闭节点编辑后新增节点可继续在同一方案编辑", () => {
            var f = Create();
            using (var first = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(
                f.Editing.Host, "flow", f.A, () => true, _ => { }, documentEdits: f.Editing))
                first.Discard();
            var added = new WorkflowNodeBase("c", "C", "C", "BlobAnalysis") { Parameters = new BlobAnalysisParameters() };
            f.Document.Workflows[0].Nodes.Add(added);
            using var next = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(
                f.Editing.Host, "flow", added, () => true, _ => { }, documentEdits: f.Editing);
            ((BlobAnalysisParameters)next.DraftNode.Parameters).Threshold = 173;
            Require(next.TryCommitEditor(out var error) && ((BlobAnalysisParameters)added.Parameters).Threshold == 173,
                "新增节点不能正常编辑：" + error);
        });
        Case("新增节点不丢其他草稿，参数Undo保留新节点且不回退实时值", () => {
            var f = Create(); var live = AddVariable(f);
            using var a = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(
                f.Editing.Host, "flow", f.A, () => true, _ => { }, documentEdits: f.Editing);
            ((BlobAnalysisParameters)a.DraftNode.Parameters).Threshold = 181; Require(a.TryCommitEditor(out _), "初次Apply失败");
            ((BlobAnalysisParameters)a.DraftNode.Parameters).Threshold = 191;
            var added = new WorkflowNodeBase("c", "C", "C", "BlobAnalysis") { Parameters = new BlobAnalysisParameters() };
            f.Document.Workflows[0].Nodes.Add(added);
            using var c = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(
                f.Editing.Host, "flow", added, () => true, _ => { }, documentEdits: f.Editing);
            ((BlobAnalysisParameters)c.DraftNode.Parameters).Threshold = 173; Require(c.TryCommitEditor(out _), "新增节点Apply失败");
            Require(((BlobAnalysisParameters)f.A.Parameters).Threshold == 181 && ((BlobAnalysisParameters)a.DraftNode.Parameters).Threshold == 191,
                "新增节点接受或丢失另一个草稿");
            f.Editing.AcceptAll(); live.Value = 99;
            f.Editing.Host.Undo(); Require(((BlobAnalysisParameters)f.A.Parameters).Threshold == 181 && (int)live.Value! == 99, "Undo回退了实时值或没有恢复A");
            f.Editing.Host.Undo(); f.Editing.Host.Undo();
            Require(f.Document.Workflows[0].Nodes.Contains(added) && ((BlobAnalysisParameters)f.A.Parameters).Threshold == 128 &&
                ((BlobAnalysisParameters)added.Parameters).Threshold == 128, "参数历史丢失或删除了新节点");
        });
        Case("删除节点仅退出该范围，其他草稿可保存且Undo不复活已删除节点", () => {
            var f = Create();
            using var a = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(f.Editing.Host, "flow", f.A, () => true, _ => { }, documentEdits: f.Editing);
            using var b = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(f.Editing.Host, "flow", f.B, () => true, _ => { }, documentEdits: f.Editing);
            ((BlobAnalysisParameters)a.DraftNode.Parameters).Threshold = 181; ((BlobAnalysisParameters)b.DraftNode.Parameters).Threshold = 191;
            f.Document.Workflows[0].Nodes.Remove(f.B); f.Editing.AcceptAll();
            Require(!b.IsActive && a.IsActive && ((BlobAnalysisParameters)f.A.Parameters).Threshold == 181 && f.Editing.RegisteredScopeCount == 1,
                "删除影响了其他草稿或失效范围仍存在");
            f.Editing.Host.Undo(); Require(!f.Document.Workflows[0].Nodes.Contains(f.B), "参数Undo复活删除节点");
        });
        Case("相同ID重建节点使旧范围失效，历史不串到新实例", () => {
            var f = Create();
            using var old = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(f.Editing.Host, "flow", f.A, () => true, _ => { }, documentEdits: f.Editing);
            ((BlobAnalysisParameters)old.DraftNode.Parameters).Threshold = 181; Require(old.TryCommitEditor(out _), "旧节点Apply失败");
            var replaced = new WorkflowNodeBase("a", "new A", "new A", "BlobAnalysis") { Parameters = new BlobAnalysisParameters() };
            f.Document.Workflows[0].Nodes[0] = replaced;
            using var current = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(f.Editing.Host, "flow", replaced, () => true, _ => { }, documentEdits: f.Editing);
            Require(!old.IsActive && !old.TryCommitEditor(out _), "旧实例仍可提交");
            ((BlobAnalysisParameters)current.DraftNode.Parameters).Threshold = 191; Require(current.TryCommitEditor(out _), "新实例不能确认");
            f.Editing.Host.Undo(); Require(((BlobAnalysisParameters)replaced.Parameters).Threshold == 128, "旧实例历史串入新节点");
        });
        Case("结构同步拒绝夹带事务外参数写入并保留草稿", () => {
            var f = Create();
            using var a = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(f.Editing.Host, "flow", f.A, () => true, _ => { }, documentEdits: f.Editing);
            ((BlobAnalysisParameters)a.DraftNode.Parameters).Threshold = 181;
            ((BlobAnalysisParameters)f.A.Parameters).Threshold = 200;
            f.Document.Workflows[0].Nodes.Add(new WorkflowNodeBase("c", "C", "C", "BlobAnalysis") { Parameters = new BlobAnalysisParameters() });
            var revision = f.Editing.Host.Capture().Version;
            Reject(() => f.Editing.Host.BeginEdit("flow", "c"));
            Require(f.Editing.Host.Capture().Version == revision && ((BlobAnalysisParameters)a.DraftNode.Parameters).Threshold == 181,
                "失败同步接受了部分状态或丢失草稿");
        });
        Case("同一调试窗口每次冻结最新已发布变量，实际HALCON绑定使用本次值", () => {
            var f = Create(); var live = AddVariable(f); live.Value = new long[] { 150 };
            var formal = (BlobAnalysisParameters)f.A.Parameters;
            formal.IsAreaFilterEnabled = false; formal.IsCircularityFilterEnabled = false; formal.IsRectangularityFilterEnabled = false;
            using var scope = new SunEyeVision.UI.Services.Editing.ProductionEditorContext(f.Editing.Host, "flow", f.A, () => true, _ => { }, documentEdits: f.Editing);
            scope.DraftNode.ParamSettings.SetSetting(SunEyeVision.Plugin.SDK.Execution.Parameters.ParamSetting.CreateBinding("Threshold", "__GlobalVars__", "Count[0]"));
            var query = new SunEyeVision.Plugin.SDK.Execution.Parameters.DataSourceQueryService {
                WorkflowId = "flow", ExecutionOwner = scope.DebugOwner, EditScopeCancellation = scope.DebugCancellation,
                CapturePublishedGlobals = () => GlobalVariable.CaptureDefinitions(f.Document.GlobalVariables).ToDictionary(v => v.Name, v => v.Value)
            };
            var probe = new DebugRequestProbe(); probe.SetCurrentNode(scope.DraftNode); probe.SetDataProvider(query);
            var pixels = new byte[32 * 32]; for(int y=4;y<8;y++)for(int x=3;x<7;x++)pixels[y*32+x]=200;
            using var image = SunEyeVision.HalAdapter.HalImage.CreateFromBytes(pixels,32,32,1);
            var executor = new SunEyeVision.Plugin.SDK.Core.ToolExecutor(new SunEyeVision.Plugin.SDK.Execution.Parameters.ParameterResolver(), VisionLogger.Instance);
            using var firstRequest = probe.Request(scope.DraftNode.Parameters, image);
            var first = (BlobAnalysisResults)executor.Run(new BlobAnalysisTool(), firstRequest);
            try { Require(first.IsSuccess && first.FilteredBlobCount == 1, "第一次Debug没有使用已发布变量150："+first.ErrorMessage); }
            finally { first.Resources?.Dispose(); }
            live.Value = new long[] { 220 };
            using var secondRequest = probe.Request(scope.DraftNode.Parameters, image);
            var second = (BlobAnalysisResults)executor.Run(new BlobAnalysisTool(), secondRequest);
            try { Require(second.IsSuccess && second.FilteredBlobCount == 0 && ((long[])firstRequest.GlobalVariables!["Count"]!)[0] == 150 && formal.Threshold == 128,
                "后续Debug使用旧值、污染上次快照或提交了参数："+second.ErrorMessage); }
            finally { second.Resources?.Dispose(); }
        });
        Case("关闭Blob仅释放自己的编辑覆盖，取消关闭不提前清理且保留共享正式结果", () => {
            using var view = new BlobViewFixture();
            var image = new SunEyeVision.Plugin.SDK.UI.Controls.ImageControl();
            var canvas = image.InteractionOverlayCanvasControl;
            var sentinel = new System.Windows.Controls.Border(); canvas.Children.Add(sentinel);
            var other = new SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Views.RoiEditorManagerControl();
            other.SetMainImageControl(image);
            var otherScope = canvas.Children[1];
            view.Control.SetMainImageControl(image);
            image.ResultRenderLayerControl.SetItems(Array.Empty<SunEyeVision.Plugin.SDK.Models.Visualization.DisplayItemBase>());
            var resultField = image.ResultRenderLayerControl.GetType().GetField("_regionItems", BindingFlags.Instance | BindingFlags.NonPublic)!;
            var originalResult = resultField.GetValue(image.ResultRenderLayerControl);
            System.ComponentModel.CancelEventHandler veto = (_, e) => e.Cancel = true;
            view.Window.Closing += veto; view.Window.Close();
            Require(view.Context.IsActive && canvas.Children.Count == 3, "取消关闭提前退出编辑范围或清除覆盖");
            view.Window.Closing -= veto; view.Window.Close();
            Require(!view.Context.IsActive && canvas.Children.Count == 2 && canvas.Children.Contains(sentinel) && canvas.Children.Contains(otherScope)
                && ReferenceEquals(originalResult, resultField.GetValue(image.ResultRenderLayerControl)), "关闭清除了别的范围或正式结果");
            other.SetMainImageControl(new SunEyeVision.Plugin.SDK.UI.Controls.ImageControl());
            Require(canvas.Children.Count == 1 && canvas.Children.Contains(sentinel), "重新绑定泄漏了原编辑覆盖");
            other.ClearMainOverlay(); other.ClearMainOverlay();
        });
        Console.WriteLine($"S2文档事务有限检查：{passed}通过。不是完整UI或用户验收。");
        return 0;
    }
}
