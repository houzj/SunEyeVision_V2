namespace SunEyeVision.Domain.Workflows;

/// <summary>Display navigation identities. Slot order is the opacity and stacking order.</summary>
public sealed class ImageOverlaySelection
{
    public const int SlotCount=3;
    public ImageOverlaySelection(string background,IEnumerable<string?> slots)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(background);
        var values=slots.ToArray();
        var selected=values.OfType<string>().ToArray();
        if(values.Length!=SlotCount||selected.Length==0||selected.Any(string.IsNullOrWhiteSpace)||
            selected.Contains(background,StringComparer.Ordinal)||selected.Distinct(StringComparer.Ordinal).Count()!=selected.Length)
            throw new ArgumentException("叠加图像要求一个背景和最多三个不同的图层。");
        Background=background;
        Slots=Array.AsReadOnly(values);
    }

    public string Background { get; }
    public IReadOnlyList<string?> Slots { get; }
    public IEnumerable<string> References()=>Slots.OfType<string>().Prepend(Background);
    public bool SameSelection(ImageOverlaySelection? other)
        =>other is not null&&Background==other.Background&&Slots.SequenceEqual(other.Slots);
}
