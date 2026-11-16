using System;
using System.Runtime.InteropServices;
using HalconDotNet;
using SunEyeVision.Plugin.Contracts;
using SunEyeVision.Plugin.Runtime;

namespace SunEyeVision.HalAdapter;

/// <summary>Copies one owned HALCON image and its actual Domain into a neutral managed snapshot.</summary>
public static class HalImageSnapshotSource
{
    public static FrozenImageResource Capture(HImage captured,ImageFrameAddress frame)
        => CaptureCore(captured, frame, (layout, coordinates, pixels, runs) => new FrozenImageResource(layout, frame, coordinates, pixels, runs));

    public static IReadOnlyImage Capture(HImage captured, ImageFrameAddress frame, IOutputResources resources, bool rgb24=false)
        => CaptureCore(captured, frame, (layout, coordinates, pixels, runs) => {
            using var domain = new FrozenRegionResource(coordinates, frame, runs);
            return resources.CreateImage(layout, frame, domain, pixels);
        }, rgb24:rgb24);

    /// <summary>Capture an unchanged pixel grid in the caller's explicitly declared image coordinate slot.</summary>
    public static IReadOnlyImage Capture(HImage captured,ImageFrameAddress frame,CoordinateFrame coordinates,IOutputResources resources)
    {
        ArgumentNullException.ThrowIfNull(coordinates);ArgumentNullException.ThrowIfNull(resources);
        if(string.IsNullOrWhiteSpace(coordinates.Id)||coordinates.Domain!=CoordinateDomain.Image||coordinates.Unit!=CoordinateUnit.Pixel)
            throw new ArgumentException("Explicit image coordinates must be a named image pixel frame.",nameof(coordinates));
        return CaptureCore(captured,frame,(layout,actual,pixels,runs)=>
        {
            using var domain=new FrozenRegionResource(actual,frame,runs);
            return resources.CreateImage(layout,frame,domain,pixels);
        },coordinates);
    }

    private static T CaptureCore<T>(HImage captured, ImageFrameAddress frame, Func<ImageLayout, CoordinateFrame, byte[], RegionRun[], T> create,
        CoordinateFrame? declaredCoordinates=null, bool rgb24=false)
    {
        ArgumentNullException.ThrowIfNull(frame);
        using var count=captured.CountChannels();
        int channels=count.I;
        if(rgb24&&channels!=3)throw new ArgumentException("RGB24 图像必须有三个颜色通道。",nameof(captured));
        var format=channels switch {1=>PixelLayout.Gray8,3=>rgb24?PixelLayout.Rgb24:PixelLayout.Bgr24,4=>PixelLayout.Bgra32,
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
        {
            var leadingPlane=planes[rgb24?0:2];
            var trailingPlane=planes[rgb24?2:0];
            for(int pixel=0;pixel<leadingPlane.Length;pixel++)
            {
                int offset=pixel*channels;
                pixels[offset]=leadingPlane[pixel];pixels[offset+1]=planes[1][pixel];pixels[offset+2]=trailingPlane[pixel];
                if(channels==4)pixels[offset+3]=planes[3][pixel];
            }
        }
        using var domain=captured.GetDomain();
        domain.GetRegionRuns(out var rows,out var first,out var last);
        using(rows)using(first)using(last)
        {
            if(rows.Length!=first.Length || rows.Length!=last.Length)throw new InvalidOperationException("HALCON returned inconsistent Domain runs.");
            var runs=new RegionRun[rows.Length];
            for(int i=0;i<runs.Length;i++)runs[i]=new RegionRun(rows[i].I,first[i].I,last[i].I);
            var coordinates=declaredCoordinates??new CoordinateFrame("image:"+frame.SourceId,CoordinateDomain.Image,CoordinateUnit.Pixel);
            return create(layout,coordinates,pixels,runs);
        }
    }
}
