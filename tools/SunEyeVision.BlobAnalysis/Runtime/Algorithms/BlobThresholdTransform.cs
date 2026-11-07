using System.Runtime.InteropServices;
using HalconDotNet;

namespace SunEyeVision.Tool.BlobAnalysis.Runtime;

/// <summary>Immutable receipt of this invocation, not an assertion about a native result getter.</summary>
public sealed record BlobThresholdExecutionSnapshot(BlobSegmentationMode Mode,int? Lower,int? Upper,string Polarity,string Provenance);

/// <summary>Reconstruction of the evidenced byte scalar operations; HALCON owns image/domain resources.</summary>
internal static class BlobThresholdTransform
{
    internal static (HImage Work,BlobThresholdExecutionSnapshot Receipt) Apply(HImage gray,HRegion effectiveDomain,
        BlobSettings settings,int width,int height,CancellationToken cancellation)
    {
        int count=checked(width*height);
        var source=ReadBytes(gray,width,height);
        bool histogramMode=settings.SegmentationMode is BlobSegmentationMode.AutoThreshold or BlobSegmentationMode.RelativeSoftThreshold;
        using var domainImage=RegionImage(effectiveDomain,width,height);
        var domain=ReadBytes(domainImage,width,height);
        // VM computes data-dependent thresholds over the effective working
        // domain.  Restrict the histogram input here instead of rejecting any
        // ROI or mask that leaves pixels outside that domain.
        var histogramSource=histogramMode
            ?source.Where((value,index)=>domain[index]!=0).ToArray()
            :Array.Empty<byte>();
        if(histogramMode&&histogramSource.Length==0)
            throw new BlobCapabilityException("BLOB_THRESHOLD_EMPTY_DOMAIN","有效检测区域没有可用于阈值统计的像素。");
        int low=settings.Threshold,high=settings.SegmentationMode==BlobSegmentationMode.SingleThreshold?255:settings.UpperThreshold;
        byte[]? table=null;
        string provenance="ConsumedInputEndpoints";
        switch(settings.SegmentationMode)
        {
            case BlobSegmentationMode.AutoThreshold:
                low=high=AutoThreshold(histogramSource,cancellation);provenance="LocalScalarFormulaCalculation";break;
            case BlobSegmentationMode.FixedSoftThreshold:
                low=settings.FixedSoftLowerThreshold;high=settings.FixedSoftUpperThreshold;
                table=SoftTable(low,high,settings.ThresholdSoftness);break;
            case BlobSegmentationMode.RelativeSoftThreshold:
                (low,high)=RelativeThresholds(histogramSource,settings,cancellation);
                table=SoftTable(low,high,settings.ThresholdSoftness);provenance="LocalScalarFormulaCalculation";break;
        }
        var output=new byte[count];
        bool dark=settings.Type==BlobThresholdMode.BinaryInv;
        for(int y=0;y<height;y++)
        {
            cancellation.ThrowIfCancellationRequested();
            for(int x=0;x<width;x++)
            {
                int i=y*width+x;if(domain[i]==0)continue;int p=source[i];
                if(settings.SegmentationMode==BlobSegmentationMode.None)output[i]=dark?(byte)(255-p):(byte)p;
                else if(table is not null)output[i]=dark?table[p]:(byte)(255-table[p]);
                else
                {
                    bool selected=settings.SegmentationMode switch
                    {
                        BlobSegmentationMode.AutoThreshold=>dark?p<=low:p>=low,
                        BlobSegmentationMode.DoubleThreshold=>low<=high?p>=low&&p<=high:p<high||p>low,
                        BlobSegmentationMode.SingleThreshold=>dark?p<low:p>=low,
                        _=>throw new BlobCapabilityException("BLOB_MODE_NOT_READY","该阈值模式未接入工作图。")
                    };
                    output[i]=selected?(byte)255:(byte)0;
                }
            }
        }
        string polarity=settings.SegmentationMode==BlobSegmentationMode.DoubleThreshold
            ?(low>high?BlobThresholdMode.BinaryInv:BlobThresholdMode.Binary).ToString():settings.Type.ToString();
        return(CreateImage(output,width,height),new(settings.SegmentationMode,
            settings.SegmentationMode==BlobSegmentationMode.None?null:low,settings.SegmentationMode==BlobSegmentationMode.None?null:high,polarity,provenance));
    }
    internal static byte[] SoftTable(int low,int high,int softness)
    {
        var table=new byte[256];int gap=high-low;
        float ratio,step;
        if(softness==0||gap==0){ratio=0;step=0;}
        else if(softness<gap){ratio=(float)softness/(float)gap;step=255f/(1f+(float)softness);}
        else {ratio=1f;step=255f/((float)gap+1f);}
        for(int p=0;p<256;p++)
        {
            if(p<=low){table[p]=255;continue;}
            if(p>=high){table[p]=0;continue;}
            // The inner CVTT and final CVTT are distinct; clamp the float before the latter.
            int bucket=(int)((float)(p-low)*ratio);
            float mapped=(255f-(float)(bucket+1)*step)+0.5f;
            table[p]=(byte)(int)Math.Clamp(mapped,0f,255f);
        }
        return table;
    }
    private static int AutoThreshold(byte[] pixels,CancellationToken cancellation)
    {
        var histogram=new float[256];
        for(int i=0;i<pixels.Length;i++){if((i&65535)==0)cancellation.ThrowIfCancellationRequested();histogram[pixels[i]]+=1f;}
        float scale=1f/(float)pixels.Length;
        for(int i=0;i<256;i++)histogram[i]*=scale;
        float pairedMean=0;
        // Original 1806e70e0 scalar order: bins 1,2 then 3,4 ... 253,254; bin 255 added in the score expression.
        for(int i=0;i<127;i++)
        {
            pairedMean=pairedMean+(float)(i*2+1)*histogram[i*2+1];
            pairedMean=pairedMean+(float)(i*2+2)*histogram[i*2+2];
        }
        float weight=0,moment=0,bestScore=0;int threshold=0;
        for(int i=0;i<256;i++)
        {
            weight+=histogram[i];if(weight>=1.00000011920928955078125f)break;
            moment+=(float)i*histogram[i];
            float delta=(pairedMean+255f*histogram[255])*weight-moment;
            float score=(delta*delta)/((weight-weight*weight)+1.1920928955078125e-7f);
            if(score>bestScore){bestScore=score;threshold=i;}
        }
        return threshold;
    }
    private static (int Low,int High) RelativeThresholds(byte[] pixels,BlobSettings settings,CancellationToken cancellation)
    {
        var histogram=new int[256];
        for(int i=0;i<pixels.Length;i++){if((i&65535)==0)cancellation.ThrowIfCancellationRequested();histogram[pixels[i]]++;}
        int left=(int)(((float)settings.RelativeLeftRatio*0.01f)*(float)pixels.Length);
        int right=(int)(((float)settings.RelativeRightRatio*0.01f)*(float)pixels.Length);
        int a=0,b=255;
        if(left>0){int sum=0;for(int i=0;i<256;i++){sum+=histogram[i];a=i;if(sum>=left)break;}}
        if(right>0){int sum=0;for(int i=255;i>=0;i--){sum+=histogram[i];b=i;if(sum>=right)break;}}
        int low=a+(int)((float)(b-a)*((float)settings.RelativeLowerRatio*0.01f));
        int high=a+(int)((float)(b-a)*((float)settings.RelativeUpperRatio*0.01f));
        return(Math.Min(low,high),Math.Max(low,high));
    }
    internal static HImage RegionImage(HRegion region,int width,int height)
    {
        if(region.CountObj()==0){var black=new HImage();try{black.GenImageConst("byte",width,height);return black;}catch{black.Dispose();throw;}}
        HOperatorSet.RegionToBin(region,out var image,255,0,width,height);using(image)return new HImage(image);
    }
    private static byte[] ReadBytes(HImage image,int width,int height)
    {
        var pointer=image.GetImagePointer1(out string type,out int w,out int h);
        if(type!="byte"||w!=width||h!=height)throw new ArgumentException("阈值工作图需要同尺寸8位单通道图像。");
        var bytes=new byte[checked(width*height)];Marshal.Copy(pointer,bytes,0,bytes.Length);return bytes;
    }
    private static HImage CreateImage(byte[] pixels,int width,int height)
    {
        var pinned=GCHandle.Alloc(pixels,GCHandleType.Pinned);var image=new HImage();
        try{image.GenImage1("byte",width,height,pinned.AddrOfPinnedObject());return image;}
        catch{image.Dispose();throw;}
        finally{pinned.Free();}
    }
}

internal sealed class BlobCapabilityException(string code,string message):InvalidOperationException(message)
{internal string Code{get;}=code;}
