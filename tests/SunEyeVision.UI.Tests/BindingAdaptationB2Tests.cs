using System.Text.Json;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Tool.ProtocolParse;
using SunEyeVision.Tool.SendData;
using SunEyeVision.Tool.ReceiveData;
using SunEyeVision.Workflow;
using Xunit;

namespace SunEyeVision.UI.Tests;

public sealed class BindingAdaptationB2Tests
{
    public sealed class SourceParameters : ToolParameters, IOutputContractProvider
    {
        public string Name { get; set; } = "value";
        public string TypeName { get; set; } = "String";
        public Guid FieldId { get; set; } = Guid.NewGuid();
        public IReadOnlyList<OutputVariable> GetOutputContract() => new[] { new OutputVariable { Name = Name, VariableType = TypeName, FieldId = FieldId, IsCollection = false, FixedCount = 1 } };
    }
    public sealed class MatrixSourceParameters : ToolParameters, IOutputContractProvider
    {
        public IReadOnlyList<OutputVariable> GetOutputContract() => new[] { Output(typeof(AffineMatrix[]), true) };
    }
    public sealed class ScalarParameters : ToolParameters { public double Value { get; set; } }
    public sealed class StaticParameters : ToolParameters { }
    public sealed class StaticResults : ToolResults
    {
        public double Scalar { get; set; }
        public double[] Items { get; set; } = Array.Empty<double>();
        public override List<PropertyMapEntry> GetPropertyMap() => new() { new("Scalar", "标量"), new("Items", "集合") };
    }
    [Tool("B2StaticContractProbe", "B2静态契约测试")]
    public sealed class StaticTool : IToolPlugin<StaticParameters, StaticResults> { public StaticResults Run(StaticParameters p) => new(); }
    public sealed class FrozenStaticParameters : ToolParameters { }
    public sealed class FrozenStaticResults : ToolResults
    {
        public double Scalar { get; set; }
        public SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage? OutputImage { get; set; }
        public override List<PropertyMapEntry> GetPropertyMap() => new() { new("Scalar", "数值"), new("OutputImage", "输出图像") };
    }
    [Tool("FrozenIdentityContractProbe", "冻结静态契约测试")]
    public sealed class FrozenStaticTool : IToolPlugin<FrozenStaticParameters, FrozenStaticResults>
    { public FrozenStaticResults Run(FrozenStaticParameters p) => new(); }

    [Fact]
    public void Frozen_factory_node_keeps_registered_tool_identity_for_source_contract_validation()
    {
        SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(typeof(FrozenStaticTool));
        var source = WorkflowNodeFactory.CreateAlgorithmNode("FrozenIdentityContractProbe", "source", "source", "source", new FrozenStaticParameters());
        Assert.NotNull(source); Assert.Equal("FrozenIdentityContractProbe", source.ToolType);
        var target = Node("target", new ScalarParameters());
        target.ParamSettings.SetSetting(ParamSetting.CreateBinding("Value", "source", "Scalar"));
        var imageParameters = new MeasureGuideParameters { ImageSource = new AvailableDataSource
            { SourceWorkflowId = "design", SourceNodeId = "source", PropertyName = "OutputImage", VariableType = "Image",
                PropertyType = typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage) } };
        var imageTarget = Node("imageTarget", ToolParameterSnapshot.Create(imageParameters));
        var frozen = Flow("frozen", source, target, imageTarget);
        OutputBindingChangeService.EnsureExecutable(frozen, "design");
        Assert.NotNull(new OutputBindingChangeService().ReadContract(source));
    }
    private static WorkflowNodeBase Node(string id, ToolParameters p) => new(id, id, id, "B2Test") { Parameters = p };
    private static SunEyeVision.Workflow.Workflow Flow(string id, params WorkflowNodeBase[] nodes)
    { var f = new SunEyeVision.Workflow.Workflow(id, id); foreach (var n in nodes) f.Nodes.Add(n); return f; }
    private static (Solution Solution, OutputBindingChangeService Service) Setup(params SunEyeVision.Workflow.Workflow[] flows)
    { var s = new Solution { Workflows = flows.ToList() }; var service = new OutputBindingChangeService(); service.Reset(s); return (s, service); }
    private static OutputVariable Output(Type type, bool? collection = null, int? count = null) => new()
    { Name = "value", VariableType = SdkTypeVocabulary.ToTypeVocabulary(type) ?? "Object", DeclaredType = type, IsCollection = collection ?? type.IsArray, FixedCount = count };
    private static BindingValidation Validate(OutputVariable output, string path, Type allowed) => BindingContractValidator.Validate(new[] { output }, path, new[] { allowed });

    [Fact]
    public void Duplicate_node_ids_are_isolated_by_workflow_and_bare_commit_is_rejected()
    {
        var a = new SourceParameters(); var b = new SourceParameters();
        var pa = new ProtocolParseParameters { InputText = "source.value" }; var pb = new ProtocolParseParameters { InputText = "source.value" };
        var (_, service) = Setup(Flow("a", Node("source", a), Node("parse", pa)), Flow("b", Node("source", b), Node("parse", pb)));
        Assert.Throws<InvalidOperationException>(() => service.Commit("source"));
        a.TypeName = "Integer"; service.Commit("a", "source");
        Assert.Empty(pa.InputText); Assert.Equal("source.value", pb.InputText);
        Assert.DoesNotContain(new ScopedNodeIdentity("b", "parse"), service.InvalidatedNodes);
    }

    [Fact]
    public void Unavailable_contract_is_retained_pending_persisted_and_blocked_until_formal_validation()
    {
        var parse = new ProtocolParseParameters { InputText = "source.value" };
        var (_, service) = Setup(Flow("f", Node("source", new GenericToolParameters()), Node("parse", parse)));
        service.Commit("f", "parse");
        Assert.Equal("source.value", parse.InputText); Assert.Single(parse.BindingValidationPending);
        var copy = (ProtocolParseParameters)ToolParameterSnapshot.Create(parse);
        Assert.Single(copy.BindingValidationPending); Assert.False(new ToolExecutor(new ParameterResolver()).ResolveRuntimeParameters(new(copy)).IsValid);
        service.StaticContractReader = _ => new[] { Output(typeof(string), false, 1) };
        service.Commit("f", "parse"); Assert.Empty(parse.BindingValidationPending);
        Assert.True(new ToolExecutor(new ParameterResolver()).ResolveRuntimeParameters(new(parse)).IsValid);
    }

    [Theory]
    [InlineData("value[-1]")]
    [InlineData("value[0][1]")]
    [InlineData("value[0]junk")]
    [InlineData("value[")]
    [InlineData("value..X")]
    [InlineData("value[Type=Fake]")]
    public void Invalid_paths_are_confirmed_incompatible(string path) => Assert.Equal(BindingCompatibility.Incompatible, Validate(Output(typeof(double[])), path, typeof(double)).State);

    [Fact]
    public void Scalar_tuple_only_allows_index_zero_and_array_shape_is_explicit()
    {
        Assert.Equal(BindingCompatibility.Compatible, Validate(Output(typeof(double), false, 1), "value[0]", typeof(double)).State);
        Assert.Equal(BindingCompatibility.Incompatible, Validate(Output(typeof(double), false, 1), "value[1]", typeof(double)).State);
        Assert.Equal(BindingCompatibility.Incompatible, Validate(Output(typeof(double[])), "value[]", typeof(double)).State);
        Assert.Equal(BindingCompatibility.Compatible, Validate(Output(typeof(double[])), "value[]", typeof(double[])).State);
    }

    [Fact]
    public void Variable_length_index_is_runtime_data_validation_not_binding_deletion()
    {
        Assert.Equal(BindingCompatibility.Compatible, Validate(Output(typeof(double[])), "value[1000]", typeof(double)).State);
        Assert.Equal(BindingCompatibility.Incompatible, Validate(Output(typeof(double[]), true, 3), "value[3]", typeof(double)).State);
    }

    [Fact]
    public void Geometry_members_and_filtered_roi_projection_are_formally_validated()
    {
        Assert.Equal(BindingCompatibility.Compatible, Validate(Output(typeof(Circle2d[])), "value[0].Center.X", typeof(double)).State);
        Assert.Equal(BindingCompatibility.Compatible, Validate(Output(typeof(RoiShape[])), "value[Type=Circle][].Geometry.Center.X", typeof(double[])).State);
        Assert.Equal(BindingCompatibility.Incompatible, Validate(Output(typeof(RoiShape[])), "value[Type=Circle][].Geometry.NoSuchField", typeof(double[])).State);
        Assert.Equal(BindingCompatibility.PendingValidation, Validate(Output(typeof(RoiShape[])), "value[0].Geometry.Center.X", typeof(double)).State);
    }

    [Fact]
    public void Rename_preserves_field_identity_but_same_name_recreation_is_detached()
    {
        var source = new SourceParameters(); var parse = new ProtocolParseParameters { InputText = "source.value" };
        var (_, service) = Setup(Flow("f", Node("source", source), Node("parse", parse)));
        source.Name = "renamed"; service.Commit("f", "source"); Assert.Equal("source.renamed", parse.InputText);
        source.FieldId = Guid.NewGuid(); service.Commit("f", "source"); Assert.Empty(parse.InputText); Assert.Single(parse.RebindingRequired);
    }

    [Fact]
    public void Saved_identity_survives_reload_before_source_rename_reconciliation()
    {
        var source = new SourceParameters(); var parse = new ProtocolParseParameters { InputText = "source.value" };
        Setup(Flow("f", Node("source", source), Node("parse", parse)));
        var copy = (ProtocolParseParameters)ToolParameterSnapshot.Create(parse); source.Name = "newValue";
        var (_, service) = Setup(Flow("f", Node("source", source), Node("parse", copy)));
        service.ReconcileAll(); Assert.Equal("source.newValue", copy.InputText); Assert.Empty(copy.RebindingRequired);
    }

    [Fact]
    public void Global_rename_uses_variable_id_and_same_name_recreation_does_not_reconnect()
    {
        var parse = new ProtocolParseParameters { InputText = "__GlobalVars__.v" };
        var (solution, service) = Setup(Flow("f", Node("parse", parse)));
        var v = new GlobalVariable { Id = "stable", Name = "v", Type = "String", Value = "hello" }; solution.GlobalVariables.Add(v); service.Reset(solution);
        v.Name = "newName"; service.ReconcileAll(); Assert.Equal("__GlobalVars__.newName", parse.InputText);
        solution.GlobalVariables.Clear(); solution.GlobalVariables.Add(new() { Id = "new", Name = "newName", Type = "String" });
        service.ReconcileAll(); Assert.Empty(parse.InputText);
    }

    [Fact]
    public void Ordinary_global_binding_and_receive_mapping_generate_the_same_producer_edge()
    {
        var source = new SourceParameters { TypeName = "Double" }; var target = Node("target", new ScalarParameters());
        target.ParamSettings.SetSetting(ParamSetting.CreateBinding("Value", "__GlobalVars__", "v"));
        var receive = new ReceiveDataParameters { SourceKind = "Variable" }; receive.Variables[0].VariableId = "id"; receive.Variables[0].DataType = "Double";
        var (solution, service) = Setup(Flow("a", Node("source", source)), Flow("b", target, Node("receive", receive)));
        solution.GlobalVariables.Add(new() { Id = "id", Name = "v", Type = "Double", ProducerBinding = new() { WorkflowId = "a", NodeId = "source", ResultName = "value" } }); service.Reset(solution);
        var graph = SolutionExecutionDependencyGraph.Compile(solution);
        Assert.Contains("a/source", graph.GetPredecessors("b/target")); Assert.Contains("a/source", graph.GetPredecessors("b/receive"));
    }

    [Fact]
    public void Candidates_include_explicit_no_edge_sources_but_exclude_self_and_descendants()
    {
        var a = new ProtocolParseParameters(); var b = new ProtocolParseParameters { InputText = "a." + a.Outputs[0].OutputDefinition().BindingKey };
        var (_, service) = Setup(Flow("f", Node("a", a), Node("b", b), Node("c", new SourceParameters())));
        Assert.Equal(new[] { "c" }, service.GetCandidateNodeIds("f", "a"));
        Assert.Contains("a", service.GetCandidateNodeIds("f", "b"));
    }

    [Fact]
    public void Subroutine_inputs_and_internal_output_mappings_have_separate_scope()
    {
        var source = new SourceParameters(); var sub = new SubroutineParameters { SubroutineId = "inner" };
        sub.Inputs.Add(new() { Name = "text", TypeName = "String", BindingSource = "source.value" });
        sub.Outputs.Add(new() { Name = "result", TypeName = "String", BindingSource = "source.value" });
        var (solution, service) = Setup(Flow("outer", Node("source", source), Node("sub", sub)), Flow("inner", Node("source", new SourceParameters())));
        Assert.Empty(service.Commit("outer", "sub"));
        Assert.Contains("source", WorkflowExecutionDependencyGraph.Compile(solution.Workflows[0]).GetPredecessors("sub"));
        ((SourceParameters)solution.Workflows[1].Nodes[0].Parameters).TypeName = "Integer";
        service.Commit("inner", "source"); Assert.Empty(sub.Outputs[0].BindingSource); Assert.NotEmpty(sub.Inputs[0].BindingSource);
    }

    [Fact]
    public void Roi_bindings_generate_dependencies_only_when_active_and_clear_without_local_fallback()
    {
        var parameters = new RoiParameters(); var roi = new Roi { Source = RoiGeometrySource.WholeRegionSub, Binding = ParamSetting.CreateBinding("roi", "source", "value") };
        parameters.InspectionRois.Rois.Add(roi);
        var (solution, service) = Setup(Flow("f", Node("source", new GenericToolParameters()), Node("target", parameters)));
        service.StaticContractReader = _ => new[] { Output(typeof(RoiShape[])) };
        Assert.Contains("source", WorkflowExecutionDependencyGraph.Compile(solution.Workflows[0]).GetPredecessors("target"));
        service.StaticContractReader = _ => new[] { Output(typeof(string), false, 1) }; service.Commit("f", "source");
        Assert.Null(roi.Binding); Assert.Equal(RoiGeometrySource.WholeRegionSub, roi.Source); Assert.NotEmpty(parameters.RebindingRequired);
    }

    [Fact]
    public void Frozen_execution_validation_does_not_mutate_design_or_use_previous_values()
    {
        var source = new SourceParameters(); var parse = new ProtocolParseParameters { InputText = "source.value" };
        var f = Flow("f", Node("source", source), Node("parse", parse)); OutputBindingChangeService.EnsureExecutable(f);
        source.TypeName = "Integer"; Assert.Throws<InvalidOperationException>(() => OutputBindingChangeService.EnsureExecutable(f));
        Assert.Equal("source.value", parse.InputText); Assert.Empty(parse.RebindingRequired);
    }

    [Fact]
    public void Explicit_empty_constant_recovers_but_automatically_cleared_nested_input_does_not()
    {
        var node = Node("target", new GenericToolParameters()); node.Parameters.RebindingRequired["InputText"] = "重配";
        node.ParamSettings.SetSetting(ParamSetting.CreateConstant("InputText", ""));
        var (_, service) = Setup(Flow("f", node)); service.Commit("f", "target"); Assert.Empty(node.Parameters.RebindingRequired);
        var send = new SendDataParameters(); send.RebindingRequired["Row:" + send.Rows[0].FieldId] = "重配";
        (_, service) = Setup(Flow("f", Node("send", send))); service.Commit("f", "send"); Assert.Single(send.RebindingRequired);
    }

    [Fact]
    public void Static_registered_tool_preserves_declared_shape_without_running()
    {
        SunEyeVision.Plugin.Infrastructure.Managers.Tool.ToolRegistry.RegisterTool(typeof(StaticTool));
        var node = new WorkflowNodeBase("source", "source", "source", "B2StaticContractProbe") { Parameters = new StaticParameters() };
        var contract = new OutputBindingChangeService().ReadContract(node)!;
        Assert.False(contract.Single(o => o.Name == "Scalar").IsCollection);
        Assert.True(contract.Single(o => o.Name == "Items").IsCollection);
        Assert.Equal(BindingCompatibility.Incompatible, BindingContractValidator.Validate(contract, "Scalar[1]", new[] { typeof(double) }).State);
        Assert.Equal(BindingCompatibility.Compatible, BindingContractValidator.Validate(contract, "Items[500]", new[] { typeof(double) }).State);
    }

    [Fact]
    public void Candidates_use_same_formal_contract_and_propagate_scope_and_field_identity()
    {
        var source = new SourceParameters(); var f = Flow("f", Node("source", source), Node("target", new ScalarParameters()));
        var (_, service) = Setup(f);
        var query = new DataSourceQueryService(f, f) { WorkflowId = "f", BindingCandidateNodeIds = id => service.GetCandidateNodeIds("f", id),
            FormalOutputContractReader = id => service.ReadContract(f.Nodes.Single(n => n.Id == id)) };
        Assert.Empty(query.GetAvailableDataSources("target", typeof(string)));
        var candidate = Assert.Single(query.GetAvailableDataSources("target", typeof(string), true));
        Assert.Equal("f", candidate.SourceWorkflowId); Assert.Equal(source.FieldId, candidate.SourceFieldId);
        Assert.Equal(BindingCompatibility.Compatible, BindingContractValidator.Validate(service.ReadContract(f.Nodes[0]), candidate.PropertyName, new[] { typeof(string) }).State);
        Assert.Empty(query.GetAvailableDataSources("target", typeof(double), true));
    }

    [Fact]
    public void Missing_global_producer_contract_marks_variable_and_consumers_pending_in_same_commit()
    {
        var parse = new ProtocolParseParameters { InputText = "__GlobalVars__.v" };
        var (solution, service) = Setup(Flow("f", Node("source", new GenericToolParameters()), Node("parse", parse)));
        solution.GlobalVariables.Add(new() { Id = "id", Name = "v", Type = "String", ProducerBinding = new() { WorkflowId = "f", NodeId = "source", ResultName = "value" } }); service.Reset(solution);
        service.ReconcileAll(); Assert.NotEmpty(solution.GlobalVariables[0].BindingValidationPending); Assert.Single(parse.BindingValidationPending); Assert.NotEmpty(parse.InputText);
        Assert.Throws<InvalidOperationException>(() => SolutionExecutionDependencyGraph.Compile(solution));
    }

    [Fact]
    public void Global_producer_rename_preserves_stable_output_and_deletion_blocks_initial_fallback()
    {
        var source = new SourceParameters(); var (solution, service) = Setup(Flow("f", Node("source", source)));
        var variable = new GlobalVariable { Id = "id", Name = "v", Type = "String", ProducerBinding = new() { WorkflowId = "f", NodeId = "source", ResultName = "value" } };
        solution.GlobalVariables.Add(variable); service.Reset(solution); source.Name = "renamed";
        service.Commit("f", "source"); Assert.Equal("renamed", variable.ProducerBinding!.ResultName); Assert.Equal(source.FieldId, variable.ProducerBinding.SourceFieldId);
        source.FieldId = Guid.NewGuid(); service.Commit("f", "source"); Assert.Null(variable.ProducerBinding); Assert.NotEmpty(variable.RebindingReason);
    }

    [Fact]
    public void Cyclic_candidate_rejects_reconciliation_without_erasing_other_valid_references()
    {
        var a = new SourceParameters(); var b = new SourceParameters(); var na = Node("a", a); var nb = Node("b", b);
        var (_, service) = Setup(Flow("f", na, nb));
        na.ParamSettings.SetSetting(ParamSetting.CreateBinding("X", "b", "value"));
        nb.ParamSettings.SetSetting(ParamSetting.CreateBinding("Y", "a", "value"));
        Assert.Throws<InvalidOperationException>(() => service.ReconcileAll());
        Assert.Single(na.ParamSettings.Settings); Assert.Single(nb.ParamSettings.Settings);
        Assert.Empty(a.BindingValidationPending); Assert.Empty(b.BindingValidationPending);
    }

    [Fact]
    public void Image_reference_preserves_index_identity_scope_and_never_auto_selects_first()
    {
        var id = Guid.NewGuid(); var p = new MeasureGuideParameters();
        var source = new AvailableDataSource { SourceWorkflowId = "f", SourceNodeId = "source", SourceFieldId = id,
            PropertyName = "images", PropertyType = typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage[]), VariableType = "Image", BindingSuffix = "[0]" };
        Assert.False(p.RestoreImageSource(new[] { source })); Assert.Null(p.ImageSource);
        p.ImageSource = source; var copy = (MeasureGuideParameters)ToolParameterSnapshot.Create(p);
        Assert.Equal("images[0]", copy.ImageSourceRef!.PropertyName);
        var candidate = new AvailableDataSource { SourceWorkflowId = "f", SourceNodeId = "source", SourceFieldId = id,
            PropertyName = "renamed", PropertyType = source.PropertyType, VariableType = "Image" };
        Assert.True(copy.RestoreImageSource(new[] { candidate })); Assert.Equal("source.renamed[0]", copy.ImageSource!.GetBindingPath());
    }

    [Fact]
    public void Roi_vector_subscriptions_accept_arrays_and_block_only_when_active()
    {
        var p = new RoiParameters(); var roi = new Roi { Source = RoiGeometrySource.GeometricSub, ComposeShapeType = ShapeType.Circle,
            ParameterBindings = new() { ["Radius"] = ParamSetting.CreateBinding("Radius", "source", "value[Type=Circle][].Geometry.Radius") } };
        p.InspectionRois.Rois.Add(roi);
        var (_, service) = Setup(Flow("f", Node("source", new GenericToolParameters()), Node("target", p)));
        service.StaticContractReader = _ => new[] { Output(typeof(RoiShape[])) }; service.Commit("f", "target"); Assert.Empty(p.RebindingRequired);
        service.StaticContractReader = _ => new[] { Output(typeof(string), false, 1) }; service.Commit("f", "source");
        Assert.False(new ToolExecutor(new ParameterResolver()).ResolveRuntimeParameters(new(p)).IsValid);
        roi.Source = RoiGeometrySource.Drawing; Assert.True(new ToolExecutor(new ParameterResolver()).ResolveRuntimeParameters(new(p)).IsValid);
        roi.Source = RoiGeometrySource.GeometricSub; Assert.False(new ToolExecutor(new ParameterResolver()).ResolveRuntimeParameters(new(p)).IsValid);
    }

    [Fact]
    public async Task Sending_variable_publication_uses_scoped_identity_even_when_node_ids_match()
    {
        var p = new SendDataParameters { DestinationKind = "Variable" }; var v = new GlobalVariable("v", "DESIGN", "String"); p.Rows[0].TargetId = v.Id;
        var solution = new Solution { Workflows = new() { Flow("a", Node("send", p)), Flow("b", Node("send", new SendDataParameters())) }, GlobalVariables = new() { v } };
        await using var runtime = new SunEyeVision.DeviceDriver.Communications.CommunicationRuntimeManager();
        var service = new SunEyeVision.UI.Services.Communication.WorkflowCommunicationSendService(() => solution, runtime);
        var mapping = new SunEyeVision.Plugin.SDK.Models.Communication.SendVariablePublication(v.Id, p.VariableFields()[0].BindingKey, "String");
        Assert.Throws<InvalidOperationException>(() => service.ConfigureVariablePublications("send", new[] { mapping }));
        service.ConfigureVariablePublications("a", "send", new[] { mapping });
        var input = new SunEyeVision.Plugin.SDK.Models.Communication.CommunicationSendInput(p.Rows[0].FieldId, v.Id, "CURRENT", "String");
        var options = new SunEyeVision.Plugin.SDK.Models.Communication.CommunicationSendOptions();
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.PrepareAsync(new("Variable", "", "", new[] { input }, options, "send", "b")));
        var plan = await service.PrepareAsync(new("Variable", "", "", new[] { input }, options, "send", "a"));
        Assert.Equal(1, await plan.SubmitAsync()); Assert.Equal("DESIGN", v.Value);
    }

    [Fact]
    public void Parameter_binding_snapshot_preserves_scope_and_rejects_foreign_scope()
    {
        var setting = ParamSetting.CreateBinding("Value", "source", "value"); setting.SourceWorkflowId = "a"; setting.SourceFieldId = Guid.NewGuid();
        var definition = ParameterBindingDefinition.FromSetting(setting); Assert.Equal(setting.SourceFieldId, definition.ToSetting().SourceFieldId);
        var request = new ToolExecutionRequest(new ScalarParameters()) { WorkflowId = "b", ParameterBindings = new ParameterBindingSet(new[] { definition }) };
        Assert.False(new ToolExecutor(new ParameterResolver()).ResolveRuntimeParameters(request).IsValid);
    }

    [Fact]
    public void Moving_consumer_does_not_reconnect_to_same_id_static_source_in_another_workflow()
    {
        var p = new ProtocolParseParameters { InputText = "source.value" }; var target = Node("target", p);
        var (solution, service) = Setup(Flow("a", Node("source", new GenericToolParameters()), target), Flow("b", Node("source", new GenericToolParameters())));
        service.StaticContractReader = _ => new[] { Output(typeof(string), false, 1) }; service.ReconcileAll();
        solution.Workflows[0].Nodes.Remove(target); solution.Workflows[1].Nodes.Add(target);
        service.ReconcileAll(); Assert.Empty(p.InputText); Assert.Single(p.RebindingRequired);
    }

    [Fact]
    public void Subroutine_matrix_array_input_survives_reconcile_and_frozen_execution_precheck()
    {
        var p = new SubroutineParameters();
        var row = new SubroutineInputConfig { Name = "in_2", TypeName = "Transform",
            BindingSource = "source.value[]", Source = new AvailableDataSource { SourceNodeId = "source",
                PropertyName = "value", PropertyType = typeof(AffineMatrix[]), VariableType = "Transform" } };
        p.Inputs.Add(row);
        Assert.Equal("Transform", row.TypeName);
        var image = new SubroutineInputConfig { Source = new AvailableDataSource {
            PropertyType = typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage), VariableType = "Image" } };
        Assert.Equal("Image", image.TypeName);
        Assert.Equal(typeof(SunEyeVision.Plugin.SDK.Vision.Contracts.IVisionImage), image.ParamType);
        var flow = Flow("f", Node("source", new MatrixSourceParameters()), Node("target", p));
        var (_, service) = Setup(flow);
        service.ReconcileAll();
        Assert.Empty(p.RebindingRequired); Assert.Empty(p.BindingValidationPending);
        Assert.NotNull(row.Source); Assert.Equal("source.value[]", row.BindingSource);
        var frozen = Flow("f", Node("source", new MatrixSourceParameters()), Node("target", ToolParameterSnapshot.Create(p)));
        OutputBindingChangeService.EnsureExecutable(frozen);
        Assert.Equal("source.value[]", ((SubroutineParameters)frozen.Nodes[1].Parameters).Inputs[0].BindingSource);
        Assert.Equal(BindingCompatibility.Incompatible, Validate(Output(typeof(AffineMatrix[]), true), "value[]", typeof(AffineMatrix)).State);
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void Dedicated_position_correction_alias_is_not_a_missing_scalar_parameter(bool collection)
    {
        var p = new MeasureGuideParameters { IsPositionCorrectionEnabled = true, PositionCorrectionBindingSource = new AvailableDataSource
            { SourceNodeId = "source", PropertyName = "value", PropertyType = collection ? typeof(AffineMatrix[]) : typeof(AffineMatrix), VariableType = "Transform" } };
        var target = Node("target", p); target.ParamSettings.SetSetting(ParamSetting.CreateBinding("位置修正", "source", collection ? "value[]" : "value"));
        var (_, service) = Setup(Flow("f", Node("source", new GenericToolParameters()), target));
        service.StaticContractReader = _ => new[] { Output(collection ? typeof(AffineMatrix[]) : typeof(AffineMatrix), collection, collection ? null : 1) }; service.Commit("f", "target");
        Assert.Empty(p.BindingValidationPending); Assert.Empty(p.RebindingRequired);
        Assert.NotNull(p.PositionCorrectionBindingSource);
        var frozen = ToolParameterSnapshot.Create(p);
        var input = BindingInputCatalog.Describe(frozen).Single(i => i.Key == "PositionCorrectionBindingSource");
        Assert.Equal(BindingCompatibility.Compatible, BindingContractValidator.Validate(
            service.StaticContractReader!(target), collection ? "value[]" : "value", input.AllowedTypes).State);
    }
}
