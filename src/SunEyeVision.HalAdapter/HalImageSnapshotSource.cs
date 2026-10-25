using System;
using System.Runtime.InteropServices;
using HalconDotNet;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.HalAdapter;

/// <summary>Copies one owned HALCON image and its actual Domain into a neutral managed snapshot.</summary>
internal static class HalImageSnapshotSource
{
    internal static FrozenImageResource Capture(HImage captured,ImageFrameAddress frame)
    {
        ArgumentNullException.ThrowIfNull(frame);
        using var count=captured.CountChannels();
        int channels=count.I;
        var format=channels switch {1=>PixelLayout.Gray8,3=>PixelLayout.Bgr24,4=>PixelLayout.Bgra32,
            _=>throw new NotSupportedException("Typed HALCON snapshots support byte Gray, RGB and RGBA images only.")};
        var planes=new byte[channels][];
        int width=0,height=0;
        for(int channel=0;channel<channels;channel++)
        {
            using var plane=captured.AccessChannel(channel+1);
            IntPtr pointer=plane.GetImagePointer1(out string type,out int w,out int h);
            if(type!="byte")throw new NotSupportedException("Typed image snapshots require byte channels.");
            if(channel==0){width=w;height=h;}
            else if(width!=w || height!=h)throw new ArgumentException("HALCON channels do not share a pixel grid.");
            var copy=new byte[checked(w*h)];
            Marshal.Copy(pointer,copy,0,copy.Length);
            planes[channel]=copy;
        }
        var layout=new ImageLayout(width,height,checked(width*channels),format);
        var pixels=new byte[layout.BufferLength];
        if(channels==1)planes[0].CopyTo(pixels,0);
        else
            for(int pixel=0;pixel<planes[0].Length;pixel++)
            {
                int offset=pixel*channels;
                // HALCON color planes are R/G/B[/A]; the neutral packed layout is B/G/R[/A].
                pixels[offset]=planes[2][pixel];pixels[offset+1]=planes[1][pixel];pixels[offset+2]=planes[0][pixel];
                if(channels==4)pixels[offset+3]=planes[3][pixel];
            }
        using var domain=captured.GetDomain();
        domain.GetRegionRuns(out var rows,out var first,out var last);
        using(rows)using(first)using(last)
        {
            if(rows.Length!=first.Length || rows.Length!=last.Length)throw new InvalidOperationException("HALCON returned inconsistent Domain runs.");
            var runs=new RegionRun[rows.Length];
            for(int i=0;i<runs.Length;i++)runs[i]=new RegionRun(rows[i].I,first[i].I,last[i].I);
            var coordinates=new CoordinateFrame("image:"+frame.SourceId,CoordinateDomain.Image,CoordinateUnit.Pixel);
            return new FrozenImageResource(layout,frame,coordinates,pixels,runs);
        }
    }
}
