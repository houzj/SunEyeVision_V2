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
/// - P2：GetDisplayLeafName 对 RoI 类型分组返回 "短名[]"（圆[]），与普通数组 "宽度[]" 同风格。
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

    private static AvailableDataSource MakeRoiGroupDataSource(string suffix)
    {
        return new AvailableDataSource
        {
            SourceNodeId = "Node1",
            SourceNodeName = "Blob分析2",
            PropertyName = "检测区域",
            DisplayName = "检测区域",
            PropertyType = typeof(RoiShape[]),
            VariableType = "ROI",
            BindingSuffix = suffix,
        };
    }

    [Fact]
    public void GetDisplayLeafName_RoiTypeGroup_ReturnsShortNameWithBrackets()
    {
        // P2：RoI 类型分组（[Type=Circle]）显示名应为 "圆[]"，而非旧 "检测区域[圆]"。
        var groupNode = new TreeNodeData
        {
            Text = "圆",
            DataSource = MakeRoiGroupDataSource("[Type=Circle]"),
        };

        var leafName = (string)GetDisplayLeafName.Invoke(null, new object[] { groupNode })!;
        Assert.Equal("圆[]", leafName);
    }

    [Fact]
    public void GetDisplayLeafName_RoiElement_ReturnsPropertyNameWithIndex()
    {
        // RoI 单元素（[0]）显示名保持 "检测区域[0]"（用户确认可接受）。
        var elemNode = new TreeNodeData
        {
            Text = "[0] Circle2d(...)",
            DataSource = MakeRoiGroupDataSource("[0]"),
        };

        var leafName = (string)GetDisplayLeafName.Invoke(null, new object[] { elemNode })!;
        Assert.Equal("检测区域[0]", leafName);
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
