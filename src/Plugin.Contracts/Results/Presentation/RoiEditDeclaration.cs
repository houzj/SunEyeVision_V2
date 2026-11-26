namespace SunEyeVision.Plugin.Contracts;

/// <summary>ROI 编辑组声明：组身份、标题、是否屏蔽组、容量与空集合覆盖语义。</summary>
public sealed class RoiEditGroupDeclaration
{
    public RoiEditGroupDeclaration(string Group,string Title,bool IsMask,int MaximumCount,string EmptyCoverage)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(Group);
        ArgumentException.ThrowIfNullOrWhiteSpace(Title);
        ArgumentException.ThrowIfNullOrWhiteSpace(EmptyCoverage);
        if(MaximumCount<=0)throw new ArgumentOutOfRangeException(nameof(MaximumCount));
        this.Group=Group;this.Title=Title;this.IsMask=IsMask;this.MaximumCount=MaximumCount;this.EmptyCoverage=EmptyCoverage;
    }
    public string Group {get;}
    public string Title {get;}
    public bool IsMask {get;}
    public int MaximumCount {get;}
    public string EmptyCoverage {get;}
}

/// <summary>掩膜继承声明：模式字段、来源字段与继承上限。</summary>
public sealed class RoiMaskInheritanceDeclaration
{
    public RoiMaskInheritanceDeclaration(string ModeFieldId,string SourceFieldId,int MaximumInherited)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(ModeFieldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(SourceFieldId);
        if(MaximumInherited<=0)throw new ArgumentOutOfRangeException(nameof(MaximumInherited));
        this.ModeFieldId=ModeFieldId;this.SourceFieldId=SourceFieldId;this.MaximumInherited=MaximumInherited;
    }
    public string ModeFieldId {get;}
    public string SourceFieldId {get;}
    public int MaximumInherited {get;}
}

/// <summary>
/// 工具 ROI 编辑声明。对应原厂参数树 ROISelecter 的常驻声明：结果视图与参数页
/// 使用同一份声明构造 ROI 文档读写与会话，工具只给字段身份与组元数据。
/// </summary>
public sealed class RoiEditDeclaration
{
    public RoiEditDeclaration(string DocumentFieldId,string ImageFieldId,IReadOnlyList<RoiEditGroupDeclaration> Groups,
        RoiInheritanceSpec? Inheritance=null,RoiMaskInheritanceDeclaration? MaskInheritance=null,IReadOnlyList<string>? ItemNames=null)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(DocumentFieldId);
        ArgumentException.ThrowIfNullOrWhiteSpace(ImageFieldId);
        ArgumentNullException.ThrowIfNull(Groups);
        if(Groups.Count==0)throw new ArgumentException("ROI 编辑至少需要一个组。",nameof(Groups));
        if(Groups.Select(group=>group.Group).Distinct(StringComparer.Ordinal).Count()!=Groups.Count)
            throw new ArgumentException("ROI 编辑组身份重复。",nameof(Groups));
        this.DocumentFieldId=DocumentFieldId;
        this.ImageFieldId=ImageFieldId;
        this.Groups=Array.AsReadOnly(Groups.ToArray());
        this.Inheritance=Inheritance;
        this.MaskInheritance=MaskInheritance;
        this.ItemNames=Array.AsReadOnly((ItemNames??this.Groups.Select(group=>group.Title)).ToArray());
    }

    public string DocumentFieldId {get;}
    public string ImageFieldId {get;}
    public IReadOnlyList<RoiEditGroupDeclaration> Groups {get;}
    /// <summary>形状继承声明；工具使用自有继承实现时为空。</summary>
    public RoiInheritanceSpec? Inheritance {get;}
    public RoiMaskInheritanceDeclaration? MaskInheritance {get;}
    public IReadOnlyList<string> ItemNames {get;}
    /// <summary>字段是否由该 ROI 声明独占写入（文档字段、模式与继承来源字段）。</summary>
    public bool IsRoiField(string fieldId)
    {
        if (string.IsNullOrWhiteSpace(fieldId)) return false;
        if (fieldId == DocumentFieldId || fieldId == Inheritance.ModeFieldId) return true;
        if (MaskInheritance is {} mask && (fieldId == mask.ModeFieldId || fieldId == mask.SourceFieldId)) return true;
        foreach (var family in Inheritance.Families)
        {
            if (fieldId == family.SourceFieldId) return true;
            foreach (var member in family.Members)
                if (fieldId == RoiInheritanceMaterializer.LeafId(family, member)) return true;
        }
        return false;
    }
}
