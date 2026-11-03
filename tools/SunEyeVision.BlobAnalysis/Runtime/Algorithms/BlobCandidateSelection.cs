namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

internal readonly record struct BlobSelectedCandidate<T>(T Candidate,int Slot);

/// <summary>Capacity slots and heap entries are distinct. No image handles or visual state are owned here.</summary>
internal static class BlobCandidateSelection
{
    private readonly record struct HeapEntry(float FloatingKey,int IntegerKey,int Slot);

    internal static BlobSelectedCandidate<T>[] SelectSlots<T>(IReadOnlyList<T> source,int capacity,BlobSortFeature feature,BlobSortMode mode,
        Func<T,float> area,Func<T,float> featureKey,Func<T,int>? integerKey,CancellationToken cancellation)
    {
        if(capacity is <1 or >20000)throw new ArgumentOutOfRangeException(nameof(capacity));
        if(!Enum.IsDefined(feature)||!Enum.IsDefined(mode))throw new ArgumentException("未知候选选择模式。");
        int count=Math.Min(source.Count,capacity);var slots=new BlobSelectedCandidate<T>[count];
        var capacityKey=mode==BlobSortMode.SortModeNotSort?area:featureKey;
        bool integerCapacity=mode!=BlobSortMode.SortModeNotSort&&integerKey is not null;
        HeapEntry Entry(T candidate,int slot)
        {if(integerCapacity)return new(0,integerKey!(candidate),slot);float key=capacityKey(candidate);RequireKey(key);return new(key,0,slot);}
        for(int i=0;i<count;i++){cancellation.ThrowIfCancellationRequested();_=Entry(source[i],i);slots[i]=new(source[i],i);}
        if(source.Count>capacity)
        {
            bool maximumHeap=mode==BlobSortMode.SortModeAscend;
            var heap=new HeapEntry[count];for(int i=0;i<count;i++)heap[i]=Entry(slots[i].Candidate,i);
            // Original bottom-up initialization, rather than insertion-order heap construction.
            for(int parent=count/2-1;parent>=0;parent--){cancellation.ThrowIfCancellationRequested();SiftDown(heap,parent,maximumHeap,integerCapacity);}
            for(int i=count;i<source.Count;i++)
            {
                cancellation.ThrowIfCancellationRequested();var candidate=source[i];var replacement=Entry(candidate,heap[0].Slot);
                int comparison=Compare(replacement,heap[0],integerCapacity);bool replace=maximumHeap?comparison<0:comparison>0;
                if(!replace)continue;
                int slot=heap[0].Slot;slots[slot]=new(candidate,slot); // Copy the complete immutable candidate, never selected individual measurements.
                heap[0]=replacement;SiftDown(heap,0,maximumHeap,integerCapacity);
            }
        }
        return slots;
    }
    internal static BlobSelectedCandidate<T>[] Order<T>(BlobSelectedCandidate<T>[] retained,BlobSortMode mode,
        Func<T,float> floatKey,Func<T,int>? integerKey,Func<T,int> componentLabel,CancellationToken cancellation)
    {
        var slots=retained.ToArray();
        if(mode==BlobSortMode.SortModeNotSort)return slots;
        foreach(var slot in slots){cancellation.ThrowIfCancellationRequested();if(integerKey is null)RequireKey(floatKey(slot.Candidate));else _=integerKey(slot.Candidate);}
        // A real final comparator, including the integer component label on equal float keys.
        Array.Sort(slots,(left,right)=>
        {
            int primary;
            if(integerKey is not null)primary=integerKey(left.Candidate).CompareTo(integerKey(right.Candidate));
            else
            {
                float a=floatKey(left.Candidate),b=floatKey(right.Candidate);RequireFinite(a);RequireFinite(b);
                float difference=a-b;RequireFinite(difference);primary=difference<0?-1:difference>0?1:0;
            }
            if(primary!=0)return mode==BlobSortMode.SortModeDecend?-primary:primary;
            return componentLabel(left.Candidate).CompareTo(componentLabel(right.Candidate));
        });
        cancellation.ThrowIfCancellationRequested();return slots;
    }
    private static void RequireFinite(float value)
    {if(!float.IsFinite(value))throw new BlobCapabilityException("BLOB_SELECTION_VALUE_NOT_READY","候选选择只接纳已核正常有限浮点特征。");}
    private static void RequireKey(float value)
    {RequireFinite(value);if(value<0)throw new BlobCapabilityException("BLOB_SELECTION_VALUE_NOT_READY","当前HALCON选择特征仅接纳正常非负值。");}
    private static int Compare(HeapEntry left,HeapEntry right,bool integer)=>integer?left.IntegerKey.CompareTo(right.IntegerKey):left.FloatingKey.CompareTo(right.FloatingKey);
    private static void SiftDown(HeapEntry[] heap,int parent,bool maximumHeap,bool integer)
    {
        var pending=heap[parent];int count=heap.Length;
        while(parent<count/2)
        {
            int child=parent*2+1,right=child+1;
            // Strict comparison selects the left sibling on equal keys.
            if(right<count&&(maximumHeap?Compare(heap[right],heap[child],integer)>0:Compare(heap[right],heap[child],integer)<0))child=right;
            int comparison=Compare(pending,heap[child],integer);if(maximumHeap?comparison>=0:comparison<=0)break;
            heap[parent]=heap[child];parent=child;
        }
        heap[parent]=pending;
    }
}

/// <summary>Per-invocation platform identity audit; these are not native permanent identifiers or new native result ports.</summary>
public sealed record BlobSelectionEntry(int ComponentLabel,int RegionIndex,int SelectionSlot,int OutputOrdinal);
