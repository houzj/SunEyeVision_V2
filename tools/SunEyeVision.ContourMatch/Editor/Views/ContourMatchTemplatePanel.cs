using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Wpf.Adapters.Regions;
using SunEyeVision.Plugin.TemplateStudio;
using SunEyeVision.Plugin.TemplateStudio.Views;
using SunEyeVision.Tool.ContourMatch.Runtime;

namespace SunEyeVision.Tool.ContourMatch.Wpf.Views;

/// <summary>
/// 原厂 FeatureMatchConfigPanal 的轮廓匹配组装。列表、预览、创建/载入、编辑和删除操作
/// 使用原厂资源和控件尺寸，模板生成窗口由原厂 900x534 配置窗口承载。
/// </summary>
/// <summary>
/// 轮廓匹配的模板业务宿主（对应原厂 <c>FeaturepatMatchConfigPanalData</c>）：
/// 只负责创建 / 载入 / 编辑 / 删除与集合持久化。列表 UI 在共用面板
/// <c>Plugin.TemplateStudio/Views/TemplateConfigPanel</c>（对应原厂 <c>FeatureMatchConfigPanal</c>）。
/// 本类不再继承 <c>UserControl</c>，也不构建任何控件。
/// </summary>
public sealed class ContourMatchTemplatePanel : IDisposable, ITemplatePanelHost
{
    private readonly IToolSettingsDraft _draft;
    private readonly Action<FieldSettings>? _acceptCandidate;
    private readonly NativeRoiFrameSource? _frames;
    private string? _error;
    private bool _disposed;

    public ContourMatchTemplatePanel(IToolSettingsDraft draft, Action<FieldSettings>? acceptCandidate, NativeRoiFrameSource? frames)
    {
        _draft = draft;
        _acceptCandidate = acceptCandidate;
        _frames = frames;
        Reload();
    }

    public bool HasPendingEdits { get; private set; }

    // ── ITemplatePanelHost ──────────────────────────────────────────────
    // 共用面板（Plugin.TemplateStudio/Views/TemplateConfigPanel）只调这五个动作，
    // 列表/行内操作/预览由面板负责。本类原有的 UI 构建代码在接入完成后删除。
    string ITemplatePanelHost.ToolLabel => "轮廓匹配";

    (bool Ok, string? Error) ITemplatePanelHost.Create()
    {
        OpenCreate();
        return (string.IsNullOrEmpty(_error), _error);
    }

    (bool Ok, string? Error) ITemplatePanelHost.Load()
    {
        ImportTemplate();
        return (string.IsNullOrEmpty(_error), _error);
    }

    (bool Ok, string? Error) ITemplatePanelHost.Edit(int index)
    {
        _editIndex = index;
        ShowConfigWindow(requireStoredImage: true);
        return (string.IsNullOrEmpty(_error), _error);
    }

    (bool Ok, string? Error) ITemplatePanelHost.DeleteAll()
    {
        ClearTemplate();
        return (string.IsNullOrEmpty(_error), _error);
    }

    /// <summary>面板直接绑定本类的集合：排序/勾选/改名即宿主状态，无需两侧同步。</summary>
    TemplateModelCollection ITemplatePanelHost.Items => _models;

    /// <summary>面板改过顺序/勾选/名称后，把当前集合整体写回 ModelData。</summary>
    void ITemplatePanelHost.Persist() => WriteEntries();

    private void OpenCreate()
    {
        _error = "";
        // 原厂新建窗口在整个对话期间拥有一个固定的 ModelItem 槽位：
        // 首次“生成模型”追加到该槽位，后续再次生成只更新它，不能每次点击都追加副本。
        _editIndex = _models.Count;
        ShowConfigWindow(requireStoredImage: false);
    }

    private void OpenEdit()
    {
        _error = "";
        // 选中项索引由共用面板经 ITemplatePanelHost.Edit(index) 传入；单模板时按原厂默认选中它。
        if (_editIndex < 0 && _models.Count == 1) _editIndex = 0;
        if (_editIndex < 0)
        {
            _error = "请先在列表中选择要编辑的模板。";
            return;
        }
        ShowConfigWindow(requireStoredImage: true);
    }

    private void ShowConfigWindow(bool requireStoredImage)
    {
        EnsureDeclaredDefaults();
        // 原厂语义（decompiled FeaturepatMatchConfigPanalData.cs:313-318）：编辑已有模板必须有存档原图，
        // 否则弹「模型不包含原图，不允许编辑」并且不打开窗口。
        if (requireStoredImage)
        {
            if (_editIndex < 0 || _editIndex >= _models.Count)
            {
                _error = "尚未创建模板：请先点“创建”生成模板。";
                return;
            }
            if (ModelAt(_editIndex)?.Model?.Image is not { Length: > 0 })
            {
                _error = "模型不包含原图，不允许编辑。请在创建模板时保持“模板存图”开启。";
                return;
            }
        }
        // 每模板独立样本：编辑已有模板用该模板存档的样本集；新建时样本为空（不继承其他模板）。
        var initial = _editIndex >= 0 && _editIndex < _models.Count
            ? TemplateTrainingCodec.Decode(_models.Items[_editIndex].Training ?? "")
            : SunEyeVision.Plugin.Contracts.RoiDocumentValue.Empty;
        // 样本编辑复用平台 NativeRoiEditHost，必须拿到调试帧来源；无帧来源时明确提示，不降级为空画布。
        if (_frames is null)
        {
            _error = "参数页尚未接入调试输入：先对节点执行一次单步调试后再编辑模板样本。";
            return;
        }
        // 按原厂 xaml 逐字重写的新窗口（TemplateConfigWindow.xaml），替换原手写近似壳。
        var window = new TemplateConfigWindow(
            _draft, _acceptCandidate, () => { }, ExportTemplate, ImportTemplate,
            AcceptTrainingRois, initial, _frames, templateIndex: Math.Max(_editIndex, 0),
             matchPointLock: requireStoredImage,
             createTemplateWithMasks: CreateTemplateFromWindow,
             commitTemplate: CommitTemplateFromWindow)
        { };
        window.ShowDialog();
    }

    private RecordValue CurrentTrainingRois()
        => _draft.CaptureFields().Read(ContourMatchFields.TrainingRois) is ConstantSetting { Value: RecordValue document }
            ? document
            : SunEyeVision.Plugin.Contracts.RoiDocumentValue.Empty;

    /// <summary>
    /// 样本回写：写入当前编辑模板自己的样本集（原厂每个 ModelItem 有独立 modelData.ItemsData），
    /// 不再写全局字段，避免多模板共用同一批样本。
    /// </summary>
    private void AcceptTrainingRois(RecordValue document)
    {
        if (_editIndex >= 0 && _editIndex < _models.Count)
        {
            _models.Items[_editIndex].Training = TemplateTrainingCodec.Encode(document);
            WriteEntries();
            return;
        }
        // 新建模板尚未进入 ModelItem 列表；原厂不会把会话内 ItemsData
        // 提前写入宿主参数。Confirm 时由 CommitTemplateFromWindow 一次提交。
    }

    /// <summary>窗口内"生成模型"：样本已由窗口回写草稿，这里用草稿声明（含 TrainingRois）建模板。</summary>
    private string? CreateTemplateFromWindow(TemplateImageSnapshot? selectedImage, RecordValue trainingDocument,
        TemplateMaskSnapshot maskSnapshot)
    {
        _error = "";
        if (_frames is null && selectedImage is null) throw new InvalidOperationException("缺少调试帧来源，无法生成模型。");
        ContourMatchSettings settings;
        try { settings = ReadSettings(); } catch (Exception failure) { throw new InvalidOperationException(failure.Message, failure); }
        NativeRoiInputLease? lease = null;
        try
        {
            IReadOnlyImage? image = null;
            if (selectedImage is null)
            {
                lease = _frames!.CaptureInputs(new[] { ContourMatchSchema.ImageInput });
                if (lease is null || !lease.IsCurrent) throw new InvalidOperationException("请选择与输入一致的已发布图像后再生成模型。");
                image = lease.Inputs.Get<IReadOnlyImage>(ContourMatchSchema.ImageInput)
                    ?? throw new InvalidOperationException("缺少输入图像，无法建立模型。");
            }
            // 样本取当前模板自己的样本集（原厂每 ModelItem 独立）；无样本时 Create 明确报错，不退化全图。
            // 原厂新建模板的 ItemsData 属于当前编辑会话；窗口确认前会先把它回写到
            // TrainingRois。新建模板不能继续取空集合，否则“画了 ROI 后生成模型”会
            // 退化成无训练域，随后模板列表再打开也看不到刚画的样本。
            var training = trainingDocument;
            // 擦除掩膜（原厂 otherMask）与屏蔽掩膜（otherMask2）由窗口会话分别决定。
            // 未修改的层沿用存档值；明确清空的层以 null 为当前值，不能用 null-coalescing
            // 回退到旧载荷，否则会出现“画布已清空、建模仍使用旧擦除区域”。
            var currentTemplate = _editIndex >= 0 && _editIndex < _models.Count
                ? ModelAt(_editIndex)?.Model : null;
            var resolvedMasks = TemplateMaskResolver.Resolve(maskSnapshot, currentTemplate);
            var effectiveWidth = resolvedMasks.Width;
            var effectiveHeight = resolvedMasks.Height;
            var effectiveEraseMask = resolvedMasks.EraseMask;
            var effectivePaintMask = resolvedMasks.PaintMask;
            // 擦除层与屏蔽层方向相反（原厂 otherMask 扣除 / otherMask2 并入），
            // 必须分别传给建模端，不能先并集成一个掩膜。
            var built = selectedImage is not null
                ? ContourMatchTemplateBuilder.CreateGray8(selectedImage.GrayPixels, selectedImage.Width, selectedImage.Height,
                    settings with { TrainingRois = training },
                    effectiveEraseMask, effectiveWidth, effectiveHeight,
                    effectivePaintMask, effectiveWidth, effectiveHeight)
                : ContourMatchTemplateBuilder.Create(image!, null, settings with { TrainingRois = training },
                    effectiveEraseMask, effectiveWidth, effectiveHeight,
                    effectivePaintMask, effectiveWidth, effectiveHeight);
            // 建模时使用合并后的有效训练域，但持久化仍保留原厂的 two-mask 状态，
            // 这样再次打开窗口时 fun/fun2 可以分别继续编辑。
            var builtTemplate = ContourMatchTemplate.Decode(built)
                .WithMasks(effectiveEraseMask, effectivePaintMask, effectiveWidth, effectiveHeight);
            var encodedTemplate = builtTemplate.Encode();
            // 原厂在窗口内生成模型只更新当前会话 ModelData；只有“确定”后
            // AddModelToModuleCommandExecute 才把 ModelItem 放入模板列表。
            return encodedTemplate;
        }
        catch (ContourMatchCapabilityException failure) { _error = failure.Message; throw; }
        catch (Exception failure) { _error = "模型创建失败：" + failure.Message; throw; }
        finally { lease?.Dispose(); }
    }

    private void CommitTemplateFromWindow(string payload, string training)
    {
        if (string.IsNullOrWhiteSpace(payload))
            throw new InvalidOperationException("模板数据为空，不能提交到模板列表。");
        try
        {
            // 配置窗口的会话载荷已经是完整模板集合；原厂此时才执行
            // AddModelToModuleCommandExecute，把 ModelItem 一次性提交到列表。
            var models = ContourMatchTemplateSet.DecodeModels(payload);
            if (_editIndex >= 0 && _editIndex < models.Count)
                models.Items[_editIndex].Training = training;
            ReplaceModels(models);
            _draft.ApplyFields(
                new Dictionary<string, ParameterSetting>
                {
                    [ContourMatchFields.ModelData] = new ConstantSetting(ContourMatchTemplateSet.EncodeModels(models))
                },
                candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
            Reload();
        }
        catch (Exception error) when (error is ContourMatchCapabilityException or ArgumentException)
        {
            // 导入/旧调用仍可能传入单模型载荷，保留原有单项提交路径。
            Apply(payload, training, throwOnError: true);
        }
    }

    private void CreateTemplate()
    {
        _error = "";
        if (_frames is null) { _error = "参数页尚未接入调试输入：先对节点执行一次单步调试后再新建模型。"; return; }
        ContourMatchSettings settings;
        try { settings = ReadSettings(); } catch (Exception failure) { throw new InvalidOperationException(failure.Message, failure); }
        NativeRoiInputLease? lease = null;
        try
        {
            lease = _frames.CaptureInputs(new[] { ContourMatchSchema.ImageInput, ContourMatchSchema.RoiInput });
            if (lease is null || !lease.IsCurrent) { _error = "请选择与输入一致的已发布图像后再新建模型。"; return; }
            var image = lease.Inputs.Get<IReadOnlyImage>(ContourMatchSchema.ImageInput);
            if (image is null) { _error = "缺少输入图像，无法建立模型。"; return; }
            var roi = lease.Inputs.Contains(ContourMatchSchema.RoiInput) ? lease.Inputs.Get<IReadOnlyRegion?>(ContourMatchSchema.RoiInput) : null;
            if (roi is null) { _error = "请先连接搜索区域 ROI（原件必须框选区域）后再新建模型。"; return; }
            Apply(ContourMatchTemplateBuilder.Create(image, roi, settings));
        }
        catch (ContourMatchCapabilityException failure) { _error = failure.Message; throw; }
        catch (Exception failure) { _error = "模型创建失败：" + failure.Message; throw; }
        finally { lease?.Dispose(); }
    }

    private void EnsureDeclaredDefaults()
    {
        var fields = _draft.CaptureFields();
        var changes = new Dictionary<string, ParameterSetting>(StringComparer.Ordinal);
        foreach (var spec in ContourMatchFields.All)
        {
            if (spec.DefaultPresence != FieldDefaultPresence.UseDeclaredDefault) continue;
            if (fields.Read(spec.Id) is UnsetSetting)
                changes[spec.Id] = new ConstantSetting(spec.Default);
        }
        if (changes.Count != 0)
            _draft.ApplyFields(changes, candidate => _acceptCandidate?.Invoke(candidate));
    }
    private ContourMatchSettings ReadSettings()
    {
        var fields = _draft.CaptureFields();
        var values = new Dictionary<string, object?>(StringComparer.Ordinal);
        foreach (var spec in ContourMatchFields.All)
        {
            // TrainingRois 由当前模板会话单独传给建模入口，不参与匹配参数读取。
            // ModelData 仍是声明必需的字符串端口；新建模时用空载荷占位，生成后的模板由面板写回。
            if (spec.Name == nameof(ContourMatchSettings.TrainingRois))
            {
                values[spec.Id] = CurrentTrainingRois();
                continue;
            }
            if (spec.Name == nameof(ContourMatchSettings.ModelData))
            {
                values[spec.Id] = string.Empty;
                continue;
            }
            values[spec.Id] = fields.Read(spec.Id) switch
            {
                ConstantSetting constant => constant.Value,
                UnsetSetting => throw new InvalidOperationException("模板参数“" + spec.Label + "”尚未赋值。"),
                _ => throw new InvalidOperationException("模板参数“" + spec.Label + "”必须是固定值，不能绑定引用或表达式。")
            };
        }
        return ContourMatchFields.Materialize(new InputValues(ContourMatchFields.Schema.Inputs, values));
    }

    private void ClearTemplate() { _error = ""; _editIndex = -1; Apply(""); }

    private void ExportTemplate()
    {
        var payload = CurrentPayload();
        if (payload.Length == 0) { _error = "尚未创建模型，无法导出。"; return; }
        var dialog = new Microsoft.Win32.SaveFileDialog { Filter = "轮廓匹配模型(*" + ContourMatchTemplate.FileExtension + ")|*" + ContourMatchTemplate.FileExtension, DefaultExt = ContourMatchTemplate.FileExtension, FileName = "ContourMatchTemplate" + ContourMatchTemplate.FileExtension };
        if (dialog.ShowDialog() == true) File.WriteAllText(dialog.FileName, payload, new UTF8Encoding(false));
    }

    private void ImportTemplate()
    {
        var dialog = new Microsoft.Win32.OpenFileDialog { Filter = "轮廓匹配模型(*" + ContourMatchTemplate.FileExtension + ")|*" + ContourMatchTemplate.FileExtension };
        if (dialog.ShowDialog() != true) return;
        try
        {
            var payload = File.ReadAllText(dialog.FileName, Encoding.UTF8);
            if (!ContourMatchTemplate.TryDecode(payload, out _, out var error)) { _error = "模型文件无效：" + error; return; }
            Apply(payload);
        }
        catch (Exception failure) { _error = "读取模型文件失败：" + failure.Message; }
    }

    private string CurrentPayload() => _draft.CaptureFields().Read(ContourMatchFields.ModelData) is ConstantSetting { Value: string text } ? text : "";

    /// <summary>当前编辑的模板序号；-1 表示新建（追加）。原厂 AddModelToModule 追加、编辑写回选中项。</summary>
    private int _editIndex = -1;

    private void Apply(string payload, string? training = null, bool throwOnError = false)
    {
        try
        {
            if (payload.Length == 0)
            {
                // 删除所有模板
                _models.Clear();
            }
            else
            {
                var template = ContourMatchTemplate.Decode(payload);
                if (_editIndex >= 0 && _editIndex < _models.Count)
                {
                    var existing = ModelAt(_editIndex);
                    if (existing is not null) existing.Model = template;
                    if (existing is not null) existing.Training = training ?? existing.Training;
                }
                else
                {
                    _editIndex = _models.Count;
                    // 原厂 MaxModelNum=36：满额时 Add 返回 null，此处不新增、不抛错。
                    var added = new TemplateModelItem<ContourMatchTemplate>(0, ContourMatchTemplateSet.NewTemplateName(_models.Count));
                    if (added is not null)
                    {
                        added.Model = template;
                        added.Training = training ?? "";
                        added.Selected = true;
                    }
                }
            }
            var encoded = _models.Count == 0 ? "" : ContourMatchTemplateSet.EncodeModels(_models);
            _draft.ApplyFields(
                new Dictionary<string, ParameterSetting> { [ContourMatchFields.ModelData] = new ConstantSetting(encoded) },
                candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
        }
        catch (Exception failure) when (failure is ArgumentException or ContourMatchCapabilityException)
        {
            _error = failure.Message;
            if (throwOnError) throw;
        }
        finally
        {
            // 保留当前窗口的槽位，保证重复 Match/Confirm 都更新同一个 ModelItem。
            // 面板关闭后下一次 OpenCreate/OpenEdit 会重新设定它。
            Reload();
        }
    }

    /// <summary>
    /// 模板集合（内存模型）。对应原厂 <c>FeaturepatMatchConfigPanalDataDecorate</c> 的模型列表：
    /// 列表操作作用在它上面，再整体编码回 ModelData。上限 36 与原厂 <c>MaxModelNum</c> 一致。
    /// </summary>
    private TemplateModelCollection _models = new();

    /// <summary>重新读取集合缓存；列表由共用面板从集合渲染，本类不再构建任何控件。</summary>
    private void Reload() => ReplaceModels(ReadModels());

    /// <summary>
    /// 原地替换集合内容。面板直接绑定 <see cref="_models"/> 实例，
    /// 若此处换成新实例，XAML 的 ItemsSource 会仍旧指向旧对象而不再刷新。
    /// </summary>
    private void ReplaceModels(TemplateModelCollection source)
    {
        ArgumentNullException.ThrowIfNull(source);
        _models.Clear();
        foreach (var item in source.Items) _models.Add(item);
    }

    private TemplateModelCollection ReadModels()
    {
        var payload = CurrentPayload();
        if (payload.Length == 0) return new();
        try
        {
            return ContourMatchTemplateSet.DecodeModels(payload);
        }
        catch (Exception error) when (error is ContourMatchCapabilityException or ArgumentException)
        {
            _error = "模板数据无法解析：" + error.Message;
            return new();
        }
    }

    /// <summary>
    /// 按下标取本工具的强类型模板项。集合只认识基类（与原厂 ModelItem 一致），
    /// 工具在读模板本体时向下转型；不是本工具类型时返回 null。
    /// </summary>
    private TemplateModelItem<ContourMatchTemplate>? ModelAt(int index)
        => _models.At(index) as TemplateModelItem<ContourMatchTemplate>;

    /// <summary>
    /// 把当前模板集合写回 ModelData；空集合写空串（等价"删除所有模板"）。
    /// <paramref name="reload"/> 为 false 时只持久化、不重建列表行——
    /// 勾选这类不改变行内容的操作按原厂数据绑定语义不应重建整表。
    /// </summary>
    private void WriteEntries(bool reload = true)
    {
        try
        {
            var payload = _models.Count == 0 ? "" : ContourMatchTemplateSet.EncodeModels(_models);
            _draft.ApplyFields(
                new Dictionary<string, ParameterSetting> { [ContourMatchFields.ModelData] = new ConstantSetting(payload) },
                candidate => _acceptCandidate?.Invoke(candidate));
            HasPendingEdits = true;
        }
        catch (Exception error) when (error is ContourMatchCapabilityException or ArgumentException)
        {
            _error = "写入模板失败：" + error.Message;
        }
        finally
        {
            if (reload) Reload();
        }
    }


    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
    }
}












