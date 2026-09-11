using System;
using System.Reflection;
using System.Windows.Controls;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Models.Geometry;
using SunEyeVision.Plugin.SDK.UI.Controls;
using SunEyeVision.Plugin.SDK.UI.Controls.RoiEditor.Models;
using Xunit;

namespace SunEyeVision.Plugin.SDK.Tests;

/// <summary>
/// 绑定树"显示名 ↔ 路径"双轨模型契约测试。
/// - 显示名 SSOT（P0.5）：叶子显示名一律由 <see cref="AvailableDataSource"/> 声明
///   （LeafDisplayName + IsArraySemantics），显示层不再按 BindingSuffix 形态推断；
///   同一语义在任意后缀形态下显示必须一致（历史缺陷：几何字段叶显示英文 "Center.X" 且丢 []）。
/// - P4：编辑显示名 []→[n] 时，OnBindingDisplayTextChanged 重建 BindingSource 保留类型过滤（[Type=Circle][2]）。
/// </summary>
public class ParamBindingDisplayContractTests
{
    private static readonly MethodInfo GetDisplayLeafName =
        typeof(ParamBinding).GetMethod("GetDisplayLeafName",
            BindingFlags.NonPublic | BindingFlags.Static)
        ?? throw new MissingMethodException("GetDisplayLeafName");

    private static readonly FieldInfo BindingDisplayField =
        typeof(ParamBinding).GetField("_bindingDisplay",
            BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingFieldException("_bindingDisplay");

    private static readonly MethodInfo OnBindingDisplayTextChanged =
        typeof(ParamBinding).GetMethod("OnBindingDisplayTextChanged",
            BindingFlags.NonPublic | BindingFlags.Instance)
        ?? throw new MissingMethodException("OnBindingDisplayTextChanged");

    private static AvailableDataSource MakeRoiGroupDataSource(
        string suffix, string? leafDisplayName = null, bool? isArraySemantics = null)
    {
        return new AvailableDataSource
        {
            SourceNodeId = "Node1",
            SourceNodeName = "Blob分析2",
            PropertyName = "检测区域",
            DisplayName = "检测区域",
            LeafDisplayName = leafDisplayName,
            IsArraySemantics = isArraySemantics,
            PropertyType = typeof(RoiShape[]),
            VariableType = "ROI",
            BindingSuffix = suffix,
        };
    }

    [Fact]
    public void GetDisplayLeafName_RoiTypeGroup_ReturnsDeclaredShortNameWithBrackets()
    {
        // RoI 类型分组（[Type=Circle]）：叶子名由构造方声明（ArrayTreeFormatter 声明为图形短名"圆"），
        // 数组语义显式声明 → "圆[]"（而非旧 "检测区域[圆]"）。
        var groupNode = new TreeNodeData
        {
            Text = "圆",
            DataSource = MakeRoiGroupDataSource("[Type=Circle]", leafDisplayName: "圆", isArraySemantics: true),
        };

        var leafName = (string)GetDisplayLeafName.Invoke(null, new object[] { groupNode })!;
        Assert.Equal("圆[]", leafName);
    }

    [Fact]
    public void GetDisplayLeafName_GeometryFieldLeaf_UsesDeclaredChineseNameNotClrPath()
    {
        // 回归（用户实测缺陷）：几何字段叶曾显示英文 CLR 路径 "Center.X" 且丢 []。
        // 现由构造方声明 LeafDisplayName（父名.字段名，如 "圆心.X"）+ IsArraySemantics=true。
        var fieldNode = new TreeNodeData
        {
            Text = "X",
            DataSource = new AvailableDataSource
            {
                SourceNodeId = "Node1",
                SourceNodeName = "圆查找1",
                PropertyName = "Center",
                DisplayName = "X",
                LeafDisplayName = "圆心.X",
                IsArraySemantics = true,
                PropertyType = typeof(double),
                VariableType = "Double",
                BindingSuffix = ".X",
            },
        };

        var leafName = (string)GetDisplayLeafName.Invoke(null, new object[] { fieldNode })!;

        Assert.Equal("圆心.X[]", leafName);
        Assert.DoesNotContain("Center", leafName);
    }

    [Fact]
    public void GetDisplayLeafName_SameSemantics_DifferentSuffixForms_RenderIdentically()
    {
        // 显示名 SSOT 核心验收：显示名只由声明字段决定，与 BindingSuffix 的形态无关。
        // 历史缺陷：ROI 字段叶（[Type=X][].Geometry.Center.X → "中心X[]"）与几何字段叶
        // （.Center.X → "Center.X"）因后缀形态不同而显示不同。现两者必须一致。
        static TreeNodeData MakeNode(string suffix) => new()
        {
            Text = "中心X",
            DataSource = new AvailableDataSource
            {
                SourceNodeId = "Node1",
                SourceNodeName = "Blob分析1",
                PropertyName = "检测区域",
                DisplayName = "中心X",
                LeafDisplayName = "中心X",
                IsArraySemantics = true,
                PropertyType = typeof(double),
                VariableType = "Double",
                BindingSuffix = suffix,
            },
        };

        var roiForm = (string)GetDisplayLeafName.Invoke(
            null, new object[] { MakeNode("[Type=RotatedRectangle][].Geometry.Center.X") })!;
        var geometryForm = (string)GetDisplayLeafName.Invoke(
            null, new object[] { MakeNode(".Center.X") })!;

        Assert.Equal("中心X[]", roiForm);
        Assert.Equal(roiForm, geometryForm);
    }

    [Fact]
    public void GetDisplayLeafName_PlainArray_ReturnsDisplayNameWithBrackets()
    {
        // 普通数组（无 BindingSuffix）→ "宽度[]"（与用户参照 图像载入1.宽度[] 一致）。
        var arrNode = new TreeNodeData
        {
            Text = "宽度[]",
            DataSource = new AvailableDataSource
            {
                SourceNodeId = "Node1",
                SourceNodeName = "图像载入1",
                PropertyName = "宽度",
                DisplayName = "宽度",
                PropertyType = typeof(int[]),
                VariableType = "Integer",
                BindingSuffix = null,
            },
        };

        var leafName = (string)GetDisplayLeafName.Invoke(null, new object[] { arrNode })!;
        Assert.Equal("宽度[]", leafName);
    }

    // ParamBinding 是 WPF 控件，构造需 STA 线程（xunit 默认 MTA）。三个编辑联动测试在 STA 线程上执行。
    private static T RunOnSta<T>(Func<T> action)
    {
        T result = default!;
        Exception? error = null;
        var thread = new Thread(() =>
        {
            try { result = action(); }
            catch (Exception ex) { error = ex; }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        thread.Join();
        if (error != null)
            throw error;
        return result;
    }

    private static string EditIndex(string initialSource, string displayText)
    {
        return RunOnSta<string>(() =>
        {
            var control = new ParamBinding();
            BindingSourceProperty(control, initialSource);
            BindingDisplayField.SetValue(control, new TextBox { Text = displayText });

            OnBindingDisplayTextChanged.Invoke(control, new object[] { null!, null! });

            return control.BindingSource;
        });
    }

    [Fact]
    public void EditDisplayIndex_TypeGroup_PreservesTypeFilter()
    {
        // P4：用户把显示名 "圆[]" 改成 "圆[2]"（对应 BindingSource "Node1.检测区域[Type=Circle]"），
        // 重建后应保留 [Type=Circle]，得到 "Node1.检测区域[Type=Circle][2]"，绝不丢类型过滤。
        Assert.Equal("Node1.检测区域[Type=Circle][2]",
            EditIndex("Node1.检测区域[Type=Circle]", "Blob分析2.圆[2]"));
    }

    [Fact]
    public void EditDisplayIndex_PlainArray_KeepsNoTypeSuffix()
    {
        // 普通数组：显示名 "宽度[2]"（BindingSource "Node1.宽度[]"）→ "Node1.宽度[2]"（无类型过滤可加）。
        Assert.Equal("Node1.宽度[2]",
            EditIndex("Node1.宽度[]", "图像载入1.宽度[2]"));
    }

    [Fact]
    public void EditDisplayIndex_EmptyBrackets_KeepsWholeArray()
    {
        // 用户把 "圆[2]" 改回 "圆[]" → 整个数组（保留类型过滤）："Node1.检测区域[Type=Circle][]"。
        Assert.Equal("Node1.检测区域[Type=Circle][]",
            EditIndex("Node1.检测区域[Type=Circle][2]", "Blob分析2.圆[]"));
    }

    private static void BindingSourceProperty(ParamBinding control, string value)
    {
        control.SetValue(ParamBinding.BindingSourceProperty, value);
    }
}
