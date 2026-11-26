using System.Reflection;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Contracts.Results.Presentation;
using SunEyeVision.Tool.FrameMean.Runtime;
using Xunit;

namespace SunEyeVision.Tools.ImageProcessing.Tests;

/// <summary>帧均值契约面：六必需契约 + 运行时预热、端口声明、字段默认值与校验。</summary>
public sealed class FrameMeanDeclarationTests
{
    [Fact]
    public void Runtime_declares_catalog_stable_tool_identity_and_the_actual_component_role()
    {
        var tool=new FrameMeanTool();
        Assert.Equal("FrameMean",tool.ToolId);
        Assert.Equal("帧均值",tool.Catalog.DisplayName);
        Assert.Equal("图像处理",tool.Catalog.Category);
        Assert.False(string.IsNullOrWhiteSpace(tool.Catalog.Description));
        var components=typeof(FrameMeanTool).Assembly.GetCustomAttributes<ToolComponentAttribute>().ToArray();
        Assert.Equal(2,components.Length);
        var component=Assert.Single(components,item=>item.Metadata.Role==ToolComponentRole.Runtime);
        Assert.Equal("SunEyeVision.Tool.FrameMean.Runtime",component.Metadata.ComponentId);
        Assert.Equal(tool.ToolId,component.Metadata.ToolId);
        Assert.Equal(FoundationContract.Major,component.Metadata.ContractMajor);
        Assert.Equal(FoundationContract.Minor,component.Metadata.ContractMinor);
        Assert.Null(component.Metadata.CompanionToolId);
        Assert.Equal(typeof(FrameMeanTool).FullName,component.EntryType);
        var editor=Assert.Single(components,item=>item.Metadata.Role==ToolComponentRole.Editor);
        Assert.Equal("SunEyeVision.Tool.FrameMean.Wpf",editor.Metadata.ComponentId);
        Assert.Equal(tool.ToolId,editor.Metadata.CompanionToolId);
        Assert.IsAssignableFrom<IToolRuntimeInitialization>(tool);
    }

    [Fact]
    public void Describe_declares_one_image_reference_input_and_all_output_ports()
    {
        var schema=FrameMeanTool.Schema;
        var input=Assert.Single(schema.Inputs);
        Assert.Equal(FrameMeanTool.ImageInput,input.Id);
        Assert.Equal(TypeRef.ResourceOf(ResourceKind.Image),input.Type);
        Assert.True(input.Required);
        Assert.Equal(FieldEditModes.Reference,input.EditModes);
        Assert.Equal(FrameMeanTool.ImageInput,schema.WorkflowImageInputId);
        Assert.Equal(FrameMeanOutput.Ports,schema.Outputs);
        var tool=new FrameMeanTool();
        Assert.Equal(FrameMeanOutput.Ports,tool.ResultSchema.Outputs);
    }

    [Fact]
    public void Output_ports_keep_declared_types_nullability_and_result_panel_visibility()
    {
        var expected=new Dictionary<string,TypeRef> {
            [FrameMeanOutput.Image]=FrameMeanOutput.ImageType,
            [FrameMeanOutput.ImageCount]=DataType.Integer,
            [FrameMeanOutput.ModuleRunTime]=DataType.Number,
            [FrameMeanOutput.ModuleStatus]=DataType.Integer
        };
        Assert.Equal(expected.Keys.OrderBy(x=>x),FrameMeanOutput.Ports.Select(port=>port.Id).OrderBy(x=>x));
        foreach(var port in FrameMeanOutput.Ports)
        {
            Assert.Equal(expected[port.Id],port.Type);
            Assert.Equal(port.Id==FrameMeanOutput.Image,port.Nullable);
        }
        Assert.Equal(NumericStorage.Int32,FrameMeanOutput.Ports.Single(port=>port.Id==FrameMeanOutput.ImageCount).Storage);
        Assert.Equal(NumericStorage.Float32,FrameMeanOutput.Ports.Single(port=>port.Id==FrameMeanOutput.ModuleRunTime).Storage);
        var schema=FrameMeanTool.Schema;
        Assert.False(schema.FindOutputDisplay(FrameMeanOutput.ModuleStatus)!.DisplayInResultPanel);
        foreach(var portId in expected.Keys)
            Assert.NotNull(schema.FindOutputDisplay(portId));
    }

    [Fact]
    public void ProcStep_declares_original_default_and_the_three_original_commands()
    {
        // 原厂证据：IMVSFrameMeanModuAlgorithm.xml ProcStep DefaultValue="1"；
        // AlgorithmTab.xml Tab_Run Params 三项 DisplayTitle：统计当前图像/不统计当前图像/清空统计图像。
        Assert.Equal("1",new FrameMeanSettings().ProcStep);
        // 声明由 manifest 生成，不再有工具自建 Schema。
        var tool=new FrameMeanTool();
        var field=Assert.Single(tool.SettingsSchema.Presentation!.Fields);
        Assert.Equal(FrameMeanFields.ProcStep,field.FieldId);
        Assert.Equal("处理步骤",field.DisplayName);
        Assert.Equal(FieldEditorKind.Choice,field.Editor);
        Assert.Equal(FrameMeanFields.ProcStepKeys,field.ChoiceKeys.ToArray());
        Assert.Equal(new[]{"统计当前图像","不统计当前图像","清空统计图像"},
            FrameMeanFields.ProcStepKeys.Select(key=>field.ChoiceLabels[key]).ToArray());
        var read=tool.ReadSettings(new FrameMeanSettings());
        Assert.Equal("1",read.Get<string>(FrameMeanFields.ProcStep));
    }

    [Fact]
    public void Settings_roundtrip_through_declared_fields()
    {
        var tool=new FrameMeanTool();
        var values=tool.ReadSettings(new FrameMeanSettings{ProcStep="2"});
        var materialized=Assert.IsType<FrameMeanSettings>(tool.MaterializeSettings(values));
        Assert.Equal("2",materialized.ProcStep);
        Assert.Throws<ArgumentException>(()=>tool.MaterializeSettings(
            new InputValues(tool.SettingsSchema.Inputs,new Dictionary<string,object?>{[FrameMeanFields.ProcStep]="9"})));
        // 未声明的字段不在声明端口内：构造 InputValues 时就应被拒绝，不需要工具自己兜底。
        Assert.Throws<ArgumentException>(()=>new InputValues(tool.SettingsSchema.Inputs,
            new Dictionary<string,object?>{[FrameMeanFields.ProcStep]="1",["unknown.field"]="1"}));
    }

    [Fact]
    public void Invalid_proc_step_is_a_declared_settings_diagnostic()
    {
        var errors=new FrameMeanSettings{ProcStep="9"}.Validate();
        var error=Assert.Single(errors);
        Assert.Equal("FRAME_MEAN_SETTINGS",error.Code);
        Assert.Equal(FrameMeanFields.ProcStep,error.FieldId);
        Assert.Empty(new FrameMeanTool().Validate(new FrameMeanSettings()));
    }

    [Fact]
    public void Result_projection_and_display_requirements_fully_reference_declared_ports()
    {
        var tool=new FrameMeanTool();
        Assert.Empty(tool.ResultProjection.Validate(tool.ResultSchema));
        Assert.Equal(ResultProjectionKind.PropertyList,tool.ResultProjection.Kind);
        Assert.Equal(ResultCardinality.One,tool.ResultProjection.Cardinality);
        Assert.All(tool.ResultProjection.Columns,column=>Assert.NotNull(column.Value));
        // 显示需求声明完整：结果列引用的每个端口都有结果面板显示节点。
        foreach(var column in tool.ResultProjection.Columns)
        {
            var portId=column.Value!.PortId;
            var node=tool.ResultSchema.FindOutputDisplay(portId);
            Assert.NotNull(node);
            if(portId==FrameMeanOutput.Image)
                Assert.All(new[]{"data","width","height","pixel-format"},member=>Assert.NotNull(node!.Children.SingleOrDefault(child=>child.FieldId==member)));
        }
    }
}
