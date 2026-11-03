using SunEyeVision.Application;
using SunEyeVision.Application.Editing;
using SunEyeVision.Plugin.Runtime;
using Xunit;

namespace SunEyeVision.Application.Editing.Tests;

public sealed class RootCollectionTests
{
    private static DocumentSessionManager Manager()
    {
        var path = Environment.GetEnvironmentVariable("SUNEYE_EDITING_TEST_PAYLOAD");
        if (string.IsNullOrWhiteSpace(path)) throw new InvalidOperationException("必须提供本工作区正式封包目录。");
        return new DocumentSessionManager(new ReleasePayloadAuthority(path).PrepareRuntimeDirectory());
    }

    [Fact]
    public void Add_rename_delete_and_undo_keep_actual_identity_and_retire_old_scope()
    {
        using var manager = Manager();
        var session = manager.CreateNew("集合事务");
        var host = session.Editing.Host;
        using var draft = host.BeginRootCollectionEdit();
        var handle = draft.Add(" 第二流程 ", 1);
        var prepared = DocumentEditProposal.Prepare(host, collection: draft);
        draft.Rename(handle, "之后的名称");
        session.ValidateEdit(prepared);
        var receipt = session.CommitEdit(prepared);
        var actual = receipt.AcceptedRoots[handle];
        Assert.NotSame(handle, actual);
        Assert.Equal("第二流程", actual.Name);
        Assert.True(session.GetWorkflows().Any(item => ReferenceEquals(item, actual)));
        using var rebased = host.RebaseAcceptedDraft(draft, prepared, receipt);
        var renamed = DocumentEditProposal.Prepare(host, collection: rebased);
        session.CommitEdit(renamed);
        Assert.Equal("之后的名称", actual.Name);
        var scope = session.OpenScope(actual.Id);
        var capturedRoot = host.BeginRootEdit(actual.Id);
        Assert.True(host.OwnsRoot(capturedRoot));
        using var deletion = host.BeginRootCollectionEdit();
        deletion.Remove(actual);
        session.CommitEdit(DocumentEditProposal.Prepare(host, collection: deletion));
        Assert.Throws<InvalidOperationException>(() => session.ValidateScope(scope));
        Assert.False(host.OwnsRoot(capturedRoot));
        Assert.True(session.Undo());
        Assert.Same(actual, session.GetWorkflows().Single(item => item.Id == actual.Id));
        Assert.Throws<InvalidOperationException>(() => session.ValidateScope(scope));
        Assert.False(host.OwnsRoot(capturedRoot));
        session.ValidateScope(session.OpenScope(actual.Id));
        Assert.Throws<InvalidOperationException>(() => capturedRoot.Configuration = capturedRoot.Configuration);
        var freshRoot = host.BeginRootEdit(actual.Id);
        Assert.True(host.OwnsRoot(freshRoot));
        freshRoot.Configuration = freshRoot.Configuration with { RunParameters = freshRoot.Configuration.RunParameters };
        Assert.True(host.OwnsRoot(freshRoot));
    }

    [Theory]
    [InlineData("bad?name")]
    [InlineData("bad.name")]
    [InlineData("bad;name")]
    [InlineData("bad[name")]
    [InlineData("bad]name")]
    [InlineData("bad<name")]
    [InlineData("bad>name")]
    [InlineData("bad(name")]
    [InlineData("bad)name")]
    [InlineData("bad`name")]
    public void Invalid_name_does_not_change_the_live_document(string name)
    {
        using var manager = Manager();
        var session = manager.CreateNew("命名事务");
        using var draft = session.Editing.Host.BeginRootCollectionEdit();
        Assert.Throws<ArgumentException>(() => draft.Add(name, 1));
        Assert.Single(session.GetWorkflows());
    }
    [Fact]
    public void Copy_uses_same_batch_source_edits_and_remaps_graph_and_root_proxy()
    {
        using var manager = Manager();
        var session = manager.CreateNew("复制事务");
        var root = session.GetWorkflows().Single();
        var graph = session.PrepareGraphEdit(root.Id);
        var image = session.CreateNode(graph, "ImageLoad", new SunEyeVision.Domain.Workflows.CanvasPoint(0, 0));
        var blob = session.CreateNode(graph, "BlobAnalysis", new SunEyeVision.Domain.Workflows.CanvasPoint(200, 0));
        graph.AddConnection(new SunEyeVision.Domain.Workflows.ConnectionDefinition("edge", image.Id, blob.Id, "output.image", "input.image"));
        session.CommitGraphEdit(graph, "创建源图");
        var host = session.Editing.Host;
        var nodeEdit = host.BeginEdit(root.Id, image.Id);
        nodeEdit.ApplyFields(new Dictionary<string, SunEyeVision.Plugin.Contracts.ParameterSetting>
        { ["sequence.initial"] = new SunEyeVision.Plugin.Contracts.ConstantSetting(9L) });
        var rootEdit = host.BeginRootEdit(root.Id);
        var proxy = new SunEyeVision.Domain.Workflows.RootParameterProxy(Guid.NewGuid(), image.Id, image.ToolType,
            "sequence.initial", "sequence.initial", "起始序号");
        rootEdit.Configuration = rootEdit.Configuration with { RunParameters = System.Collections.Immutable.ImmutableArray.Create(proxy) };
        using var collection = host.BeginRootCollectionEdit();
        var handle = collection.Copy(root, "副本", 1);
        var sourceProposal = DocumentEditProposal.Prepare(host, nodes: new[] { nodeEdit }, roots: new[] { rootEdit });
        var collectionProposal = DocumentEditProposal.Prepare(host, collection: collection);
        var proposal = DocumentEditProposal.Combine(session, new[] { sourceProposal, collectionProposal });
        session.ValidateEdit(proposal);
        session.ValidateEdit(proposal);
        var receipt = session.CommitEdit(proposal);
        var copy = receipt.AcceptedRoots[handle];
        using var rebasedCollection = host.RebaseAcceptedDraft(collection, collectionProposal, receipt);
        Assert.Equal(2, copy.Nodes.Count);
        Assert.Single(copy.Connections);
        var copiedImage = copy.Nodes.Single(node => node.ToolType == "ImageLoad");
        Assert.NotEqual(image.Id, copiedImage.Id);
        Assert.Equal(9L, Assert.IsType<SunEyeVision.Plugin.Contracts.ConstantSetting>(copiedImage.Configuration.Fields.Read("sequence.initial")).Value);
        Assert.Equal(copiedImage.Id, copy.RootConfiguration.RunParameters.Single().NodeId);
        Assert.NotEqual(proxy.Id, copy.RootConfiguration.RunParameters.Single().Id);
        Assert.True(session.Undo());
        Assert.DoesNotContain(session.GetWorkflows(), flow => ReferenceEquals(flow, copy));
        Assert.True(session.Redo());
        Assert.Same(copy, session.GetWorkflows().Single(flow => flow.Id == copy.Id));
        var dataPath = Path.Combine(Environment.GetEnvironmentVariable("SUNEYE_EDITING_TEST_PAYLOAD")!, "_root-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dataPath);
        var file = Path.Combine(dataPath, "copy.solution");
        var types = new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(new Dictionary<string, string>());
        session.SaveAs(file, Path.Combine(dataPath, "backup"), dataPath, types, new System.Text.Json.JsonSerializerOptions(), _ => null);
        var reopened = manager.Open(file, _ => types, _ => null, new System.Text.Json.JsonSerializerOptions());
        var loadedCopy = reopened.GetWorkflows().Single(flow => flow.Id == copy.Id);
        Assert.Equal(2, loadedCopy.Nodes.Count);
        Assert.Single(loadedCopy.Connections);
        Assert.Equal(copiedImage.Id, loadedCopy.RootConfiguration.RunParameters.Single().NodeId);
    }
    [Fact]
    public void Delete_rejects_live_producer_unless_dependents_are_explicitly_marked_and_undo_restores_binding()
    {
        using var manager = Manager();
        var session = manager.CreateNew("跨流程引用");
        var sourceFlow = session.GetWorkflows().Single();
        var sourceGraph = session.PrepareGraphEdit(sourceFlow.Id);
        var image = session.CreateNode(sourceGraph, "ImageLoad", new SunEyeVision.Domain.Workflows.CanvasPoint(0, 0));
        session.CommitGraphEdit(sourceGraph, "图像源");
        var host = session.Editing.Host;
        using var collection = host.BeginRootCollectionEdit();
        var handle = collection.Add("消费者", 1);
        var addReceipt = session.CommitEdit(DocumentEditProposal.Prepare(host, collection: collection));
        var targetFlow = addReceipt.AcceptedRoots[handle];
        var targetGraph = session.PrepareGraphEdit(targetFlow.Id);
        var blob = session.CreateNode(targetGraph, "BlobAnalysis", new SunEyeVision.Domain.Workflows.CanvasPoint(0, 0));
        session.CommitGraphEdit(targetGraph, "Blob");
        var imagePort = session.GetNodeSchema(sourceFlow.Id, image.Id)!.Outputs.Single(port => port.Id == "output.image");
        using var variables = host.BeginVariableEdit();
        var source = new SunEyeVision.Plugin.Contracts.OutputReference(image.Id, imagePort.Id, imagePort.Type,
            new[] { "width" }, scopeId: sourceFlow.Id);
        variables.Variables.Add(new SunEyeVision.Domain.Solutions.GlobalVariable("宽度", 0L, "Integer")
        { ProducerBinding = new SunEyeVision.Domain.Solutions.ProducerBinding
            { ReferenceExpression = SunEyeVision.Plugin.Contracts.ParameterSettingCanonical.Serialize(source) } });
        session.CommitEdit(DocumentEditProposal.Prepare(host, variables: variables));
        var variable = session.Document.GlobalVariables.Single();
        var target = host.BeginEdit(targetFlow.Id, blob.Id);
        const string threshold = "field:43bda03261df55b88f963bd18f2ba4d9";
        target.ApplyFields(new Dictionary<string, SunEyeVision.Plugin.Contracts.ParameterSetting>
        { [threshold] = new SunEyeVision.Plugin.Contracts.VariableSetting(new SunEyeVision.Plugin.Contracts.VariableReference(
            session.Document.Id, variable.Id, SunEyeVision.Plugin.Contracts.TypeRef.ScalarOf(SunEyeVision.Plugin.Contracts.ScalarKind.Int64))) });
        session.CommitEdit(DocumentEditProposal.Prepare(host, nodes: new[] { target }));
        using (var rename = host.BeginRootCollectionEdit())
        {
            rename.Rename(sourceFlow, "新来源名称");
            var renameReceipt = session.CommitEdit(DocumentEditProposal.Prepare(host, collection: rename));
            Assert.Equal("新来源名称", variable.ProducerBinding!.WorkflowName);
            Assert.DoesNotContain(renameReceipt.Change!.ExecutionInvalidatedNodes, item => item.WorkflowId == targetFlow.Id);
            Assert.True(session.Undo());
        }        using var reject = host.BeginRootCollectionEdit();
        reject.Remove(sourceFlow);
        Assert.Throws<InvalidOperationException>(() => session.CommitEdit(DocumentEditProposal.Prepare(host, collection: reject)));
        Assert.NotNull(variable.ProducerBinding);
        using var remove = host.BeginRootCollectionEdit();
        remove.Remove(sourceFlow, RootDeletionPolicy.MarkDependentsForRebinding);
        session.CommitEdit(DocumentEditProposal.Prepare(host, collection: remove));
        Assert.Null(variable.ProducerBinding);
        Assert.False(string.IsNullOrWhiteSpace(variable.RebindingReason));
        Assert.False(string.IsNullOrWhiteSpace(Assert.IsType<SunEyeVision.Plugin.Contracts.VariableSetting>(
            blob.Configuration.Fields.Read(threshold)).RebindingReason));
        Assert.True(session.Undo());
        Assert.NotNull(variable.ProducerBinding);
        Assert.True(string.IsNullOrWhiteSpace(Assert.IsType<SunEyeVision.Plugin.Contracts.VariableSetting>(
            blob.Configuration.Fields.Read(threshold)).RebindingReason));
    }
    [Fact]
    public void Stale_receipt_cannot_replace_later_root_changes()
    {
        using var manager = Manager();
        var session = manager.CreateNew("回执身份");
        var host = session.Editing.Host;
        using var first = host.BeginRootCollectionEdit();
        var handle = first.Add("已提交", 1);
        var prepared = DocumentEditProposal.Prepare(host, collection: first);
        var receipt = session.CommitEdit(prepared);
        Assert.Same(receipt.AcceptedRoots[handle], session.GetWorkflows()[1]);
        using var later = host.BeginRootCollectionEdit();
        later.Add("后续新增", 2);
        session.CommitEdit(DocumentEditProposal.Prepare(host, collection: later));
        Assert.Throws<InvalidOperationException>(() => host.RebaseAcceptedDraft(first, prepared, receipt));
        Assert.Equal(3, session.GetWorkflows().Count);
    }

    [Fact]
    public void Last_root_cannot_be_removed_or_duplicated_by_name()
    {
        using var manager = Manager();
        var session = manager.CreateNew("最少根流程");
        using var draft = session.Editing.Host.BeginRootCollectionEdit();
        var only = session.GetWorkflows().Single();
        Assert.Throws<InvalidOperationException>(() => draft.Remove(only));
        Assert.Throws<InvalidOperationException>(() => draft.Add("  " + only.Name.ToUpperInvariant() + "  ", 1));
        Assert.Single(session.GetWorkflows());
    }
    [Theory]
    [InlineData("duplicate")]
    [InlineData("forbidden")]
    public void Invalid_saved_root_names_reject_open_without_changing_file_or_active_session(string fault)
    {
        using var manager = Manager();
        var session = manager.CreateNew("加载保护");
        var host = session.Editing.Host;
        using var draft = host.BeginRootCollectionEdit();
        draft.Add("第二流程", 1);
        session.CommitEdit(DocumentEditProposal.Prepare(host, collection: draft));
        var data = Path.Combine(Environment.GetEnvironmentVariable("SUNEYE_EDITING_TEST_PAYLOAD")!, "_root-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(data);
        var original = Path.Combine(data, "valid.solution");
        var types = new SunEyeVision.Domain.Persistence.FrozenVariableTypeCatalog(new Dictionary<string, string>());
        session.SaveAs(original, Path.Combine(data,"backup"), data, types, new System.Text.Json.JsonSerializerOptions(), _ => null);
        var json = System.Text.Json.Nodes.JsonNode.Parse(File.ReadAllBytes(original))!;
        var roots = json["Workflows"]!.AsArray();
        roots[1]!["Name"] = fault == "duplicate" ? roots[0]!["Name"]!.GetValue<string>() : "bad?name";
        var invalid = Path.Combine(data, "invalid.solution");
        File.WriteAllText(invalid, json.ToJsonString());
        var before = System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(invalid));
        Assert.ThrowsAny<Exception>(() => manager.Open(invalid, _ => types, _ => null, new System.Text.Json.JsonSerializerOptions()));
        Assert.Same(session, manager.CurrentSession);
        Assert.Equal(before, System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(invalid)));
    }

    [Fact]
    public void Disposed_collection_draft_cannot_commit_a_prepared_proposal()
    {
        using var manager = Manager();
        var session = manager.CreateNew("取消保护");
        var host = session.Editing.Host;
        var draft = host.BeginRootCollectionEdit();
        draft.Add("未提交", 1);
        var prepared = DocumentEditProposal.Prepare(host, collection: draft);
        draft.Dispose();
        Assert.Throws<ObjectDisposedException>(() => session.CommitEdit(prepared));
        Assert.Single(session.GetWorkflows());
    }
    [Theory]
    [InlineData("device")]
    [InlineData("trigger")]
    public void Delete_rejects_device_and_communication_hard_references(string kind)
    {
        using var manager = Manager();
        var session = manager.CreateNew("设备通信引用");
        var source = session.GetWorkflows().Single();
        var host = session.Editing.Host;
        using (var add = host.BeginRootCollectionEdit())
        {
            add.Add("另一个流程", 1);
            session.CommitEdit(DocumentEditProposal.Prepare(host, collection: add));
        }
        if (kind == "device")
        {
            var device = new SunEyeVision.Domain.Solutions.Device();
            typeof(SunEyeVision.Domain.Solutions.Device).GetProperty("WorkflowRef")!.SetValue(device, source.Id);
            typeof(SunEyeVision.Domain.Solutions.SolutionDefinition).GetProperty("Devices")!.SetValue(session.Document, new[] { device });
        }
        else
        {
            var trigger = new SunEyeVision.Domain.Solutions.CommunicationTriggerDefinition { TargetId = source.Id };
            typeof(SunEyeVision.Domain.Solutions.SolutionDefinition).GetProperty("CommunicationTriggers")!.SetValue(session.Document, new[] { trigger });
        }
        foreach (var policy in new[] { RootDeletionPolicy.RejectReferenced, RootDeletionPolicy.MarkDependentsForRebinding })
        {
            using var draft = host.BeginRootCollectionEdit();
            draft.Remove(source, policy);
            Assert.Throws<InvalidOperationException>(() => session.CommitEdit(DocumentEditProposal.Prepare(host, collection: draft)));
            Assert.Contains(session.GetWorkflows(), item => ReferenceEquals(item, source));
        }
    }
    [Fact]
    public void Add_then_remove_private_handle_is_a_noop()
    {
        using var manager = Manager();
        var session = manager.CreateNew("无变更");
        var host = session.Editing.Host;
        var revision = session.ChangeRevision;
        using var draft = host.BeginRootCollectionEdit();
        var handle = draft.Add("临时", 1);
        draft.Remove(handle);
        var receipt = session.CommitEdit(DocumentEditProposal.Prepare(host, collection: draft));
        Assert.Null(receipt.Change);
        Assert.Equal(revision, session.ChangeRevision);
        Assert.Empty(receipt.AcceptedRoots);
        Assert.Single(session.GetWorkflows());
    }
    [Fact]
    public void Nested_participants_cannot_reuse_a_consumed_root_proposal()
    {
        using var manager = Manager();
        var session = manager.CreateNew("嵌套提案");
        var host = session.Editing.Host;
        var root = session.GetWorkflows().Single();
        using var collection = host.BeginRootCollectionEdit();
        collection.Add("新增", 1);
        var collectionPart = DocumentEditProposal.Prepare(host, collection: collection);
        var rootPart = DocumentEditProposal.Prepare(host, roots: new[] { host.BeginRootEdit(root.Id) });
        using var variables = host.BeginVariableEdit();
        var variablePart = DocumentEditProposal.Prepare(host, variables: variables);
        var nested = DocumentEditProposal.Combine(session, new[] { collectionPart, rootPart });
        Assert.Throws<InvalidOperationException>(() => DocumentEditProposal.Combine(session, new[] { nested, collectionPart }));
        Assert.Single(session.GetWorkflows());
        var combined = DocumentEditProposal.Combine(session, new[] { nested, variablePart });
        session.CommitEdit(combined);
        Assert.Throws<InvalidOperationException>(() => session.ValidateEdit(collectionPart));
        Assert.Equal(2, session.GetWorkflows().Count);
    }
}
