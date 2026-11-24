using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SunEyeVision.Tool.ContourMatch.Runtime;

/// <summary>
/// 多模板载荷容器。原厂每个模板是一个独立 ModelItem，模块侧用
/// GetNextAddModelIndex / SpareModelName 逐个挂到原生模块
/// （decompiled FeaturepatMatchConfigPanalDataBase.cs:152-180、:328-342）。
/// 本方把多个模板序列化进同一个 ModelData：
/// - 旧载荷（单个 Model/1 对象）解析为 1 个模板，保证存量方案可读；
/// - 新载荷为 ModelSet/1，models 数组逐项复用 ContourMatchTemplate 的单模板编解码。
/// </summary>
public static class ContourMatchTemplateSet
{
    public const string Format = "SunEyeVision.ContourMatch.ModelSet/1";
    private const string FormatKey = "format";
    private const string ModelsKey = "models";
    private const string NameKey = "name";
    private const string SelectedKey = "selected";
    private const string TemplateKey = "template";
    private const string TrainingKey = "training";

    /// <summary>
    /// 模板集合中的一项：模板本体 + 原厂列表元数据（名称、是否加入搜索列表）+ 该模板独立的训练样本集。
    /// 样本集为 <see cref="Encoding"/> 无关的 JSON 文本（见 TemplateTrainingCodec），算法侧不消费。
    /// </summary>
    public sealed record Entry(ContourMatchTemplate Template, string Name, bool Selected, string Training = "");

    /// <summary>解析模板集合（只取模板本体，保持旧调用方兼容）。</summary>
    public static IReadOnlyList<ContourMatchTemplate> Decode(string? payload)
        => DecodeEntries(payload).Select(entry => entry.Template).ToArray();

    /// <summary>解析模板集合，含每项名称与勾选状态（原厂 ModelItem.Name / IsSelected）。</summary>
    public static IReadOnlyList<Entry> DecodeEntries(string? payload)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_REQUIRED", "尚未创建模板：请先在“模板参数”中新建模板。");
        JsonDocument document;
        try { document = JsonDocument.Parse(payload); }
        catch (JsonException error)
        {
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板数据不是有效载荷：" + error.Message);
        }
        using (document)
        {
            var root = document.RootElement;
            if (!root.TryGetProperty(FormatKey, out var format) || format.GetString() != Format)
            {
                // 非集合格式 → 按单模板解析（兼容存量方案），默认选中。
                return new[] { new Entry(ContourMatchTemplate.Decode(payload), "", true) };
            }
            if (!root.TryGetProperty(ModelsKey, out var models) || models.ValueKind != JsonValueKind.Array)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板集合缺少 models 数组。");
            var result = new List<Entry>();
            foreach (var model in models.EnumerateArray())
            {
                if (model.ValueKind != JsonValueKind.Object)
                    throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_INVALID", "模板集合中的项必须是对象。");
                // 兼容两种项形态：带 template 包装（含名称/勾选）或裸单模板对象。
                if (model.TryGetProperty(TemplateKey, out var templateNode) && templateNode.ValueKind == JsonValueKind.Object)
                {
                    var name = model.TryGetProperty(NameKey, out var nameNode) ? nameNode.GetString() ?? "" : "";
                    var selected = !model.TryGetProperty(SelectedKey, out var selectedNode) || selectedNode.ValueKind != JsonValueKind.False;
                    var training = model.TryGetProperty(TrainingKey, out var trainingNode) ? trainingNode.GetString() ?? "" : "";
                    result.Add(new Entry(ContourMatchTemplate.Decode(templateNode.GetRawText()), name, selected, training));
                }
                else
                {
                    result.Add(new Entry(ContourMatchTemplate.Decode(model.GetRawText()), "", true));
                }
            }
            if (result.Count == 0)
                throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_EMPTY", "模板集合为空：请先创建模板。");
            return result.AsReadOnly();
        }
    }

    /// <summary>编码模板集合；只有 1 个模板时保持旧单模型格式，避免无谓改写存量数据。</summary>
    public static string Encode(IReadOnlyList<ContourMatchTemplate> models)
    {
        ArgumentNullException.ThrowIfNull(models);
        var entries = new List<Entry>(models.Count);
        for (var index = 0; index < models.Count; index++)
            entries.Add(new Entry(models[index], NewTemplateName(index), true));
        return Encode(entries);
    }

    /// <summary>编码模板集合（含名称与勾选）。单模板时若元数据为默认值则写旧格式。</summary>
    public static string Encode(IReadOnlyList<Entry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        if (entries.Count == 0)
            throw new ContourMatchCapabilityException("CONTOURMATCH_TEMPLATE_EMPTY", "模板集合为空：请先创建模板。");
        if (entries.Count == 1 && entries[0].Selected && string.IsNullOrEmpty(entries[0].Name))
            return entries[0].Template.Encode();
        using (var stream = new MemoryStream())
        {
            using (var writer = new Utf8JsonWriter(stream))
            {
                writer.WriteStartObject();
                writer.WriteString(FormatKey, Format);
                writer.WriteStartArray(ModelsKey);
                foreach (var entry in entries)
                {
                    writer.WriteStartObject();
                    writer.WriteString(NameKey, entry.Name ?? "");
                    writer.WriteBoolean(SelectedKey, entry.Selected);
                    if (!string.IsNullOrEmpty(entry.Training)) writer.WriteString(TrainingKey, entry.Training);
                    writer.WritePropertyName(TemplateKey);
                    writer.WriteRawValue(entry.Template.Encode(), skipInputValidation: true);
                    writer.WriteEndObject();
                }
                writer.WriteEndArray();
                writer.WriteEndObject();
            }
            return Encoding.UTF8.GetString(stream.ToArray());
        }
    }

    /// <summary>原厂默认模板名：Lang_NewTemp + 序号（"新建模板1"）。</summary>
    public static string NewTemplateName(int index) => "新建模板" + (index + 1);
}
