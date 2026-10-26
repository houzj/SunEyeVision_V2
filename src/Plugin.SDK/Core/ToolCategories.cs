using System;
using System.Collections.Generic;
using System.Linq;
namespace SunEyeVision.Plugin.SDK.Core;
/// <summary>VM分类语义；实际工具来自部署目录，分类不是可运行工具清单。</summary>
public static class ToolCategories
{
    public const string Acquisition="采集", Location="定位", Measurement="测量", Recognition="识别",
        DefectDetection="缺陷检测", Calibration="标定", Calculation="运算", Preprocessing="图像处理",
        ColorProcessing="颜色处理", SplitCombination="拆分组合", ImageGeneration="图形生成",
        Control="逻辑工具", Communication="通信", GroupTools="自定义组工具", UserTools="自定义工具";
    public static IReadOnlyList<ToolCategoryDefinition> Definitions {get;}=ReadDefinitions();
    private static IReadOnlyList<ToolCategoryDefinition> ReadDefinitions()
    {
        using var stream=typeof(ToolCategories).Assembly.GetManifestResourceStream("SunEyeVision.Plugin.SDK.Metadata.ToolGroups.xml")
            ?? throw new InvalidOperationException("分类定义资源缺失。");
        return Array.AsReadOnly(System.Xml.Linq.XDocument.Load(stream).Root!.Elements("Group")
            .Select(g=>new ToolCategoryDefinition((string)g.Attribute("Name")!,(int)g.Attribute("Order")!,(string)g.Attribute("IconKey")!))
            .OrderBy(g=>g.Order).ToArray());
    }
    public static ToolCategoryDefinition GetDefinition(string name)=>Definitions.FirstOrDefault(d=>d.Name==name)??new(name,1000,"generic");
}
public sealed record ToolCategoryDefinition(string Name,int Order,string IconKey);