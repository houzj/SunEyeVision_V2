using System.IO;
using System.Text.Json;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using SunEyeVision.HalAdapter;
using SunEyeVision.Plugin.SDK.Core;
using SunEyeVision.Plugin.SDK.Models.Calibration;
using Xunit;

namespace SunEyeVision.Tool.NPointCalibration.Tests;

public class NPointCalibrationTests
{
    private static NPointCalibrationParameters Fixture(bool mirror=false,int sign=1,double reference=17,double[]? angles=null)
    {
        var p=new NPointCalibrationParameters {AcquisitionMode=NPointAcquisitionMode.Manual,InputMode=NPointInputMode.Coordinates,
            SourceDeviceId="camera-1",ImageWidth=1920,ImageHeight=1080,WorkPlaneId="plane-1",
            ReferenceRAngle=reference,RotationSign=sign,MaximumTrainingErrorMm=1e-6};
        double a=mirror?-2:2,b=.3,c=100,d=-.2,e=3,f=50,dx=5,dy=-3;
        for(int i=0;i<9;i++)
        {
            double u=i%3*20,v=i/3*20;
            p.Samples.Add(new NPointSample {Id=i,Kind=NPointSampleKind.Translation,ImageX=u,ImageY=v,
                AxisX=a*u+b*v+c-dx,AxisY=d*u+e*v+f-dy,AxisAngle=reference});
        }
        var center=p.Samples[4];
        angles??=new[]{0.0,90,180};
        for(int i=0;i<angles.Length;i++)
        {
            double phi=sign*angles[i]*Math.PI/180;
            double x=center.AxisX+Math.Cos(phi)*dx-Math.Sin(phi)*dy-c;
            double y=center.AxisY+Math.Sin(phi)*dx+Math.Cos(phi)*dy-f;
            p.Samples.Add(new NPointSample {Id=9+i,Kind=NPointSampleKind.Rotation,
                ImageX=(e*x-b*y)/(a*e-b*d),ImageY=(-d*x+a*y)/(a*e-b*d),
                AxisX=center.AxisX,AxisY=center.AxisY,AxisAngle=Math.IEEERemainder(reference+angles[i],360)});
        }
        return p;
    }
    private static NPointCalibrationResults Solve(NPointCalibrationParameters p)
    {
        var r=new NPointCalibrationTool().Run(p);Assert.True(r.IsSuccess,r.ErrorMessage);Assert.True(r.TrainingPassed);return r;
    }
    private static CalibrationAcquisitionEvidence Evidence(long step=1,long frame=101)
    {
        var now=DateTimeOffset.UtcNow;
        return new("acquisition-1",step,true,frame,now,now,"camera-1",1920,1080,"Raw","plane-1",0,0,0);
    }
    private static NPointSample[] HeldOut()
    {
        var now=DateTimeOffset.UtcNow;
        double angle=45*Math.PI/180,dx=5,dy=-3,sx=140,sy=150;
        double x=sx+Math.Cos(angle)*dx-Math.Sin(angle)*dy-100;
        double y=sy+Math.Sin(angle)*dx+Math.Cos(angle)*dy-50;
        return [
            new NPointSample {Id=0,Kind=NPointSampleKind.Translation,ImageX=70,ImageY=10,AxisX=238,AxisY=69,AxisAngle=17,
                Source="VerificationWorkflow",SessionId="verify",StepId=1,FrameId=1001,FrameCapturedAt=now,AxisReadAt=now,CameraId="camera-1",WorkPlaneId="plane-1"},
            new NPointSample {Id=1,Kind=NPointSampleKind.Rotation,ImageX=(3*x-.3*y)/6.06,ImageY=(.2*x+2*y)/6.06,
                AxisX=sx,AxisY=sy,AxisAngle=62,Source="VerificationWorkflow",SessionId="verify",StepId=2,
                FrameId=1002,FrameCapturedAt=now,AxisReadAt=now,CameraId="camera-1",WorkPlaneId="plane-1"}];
    }
    private sealed class Files : IDisposable
    {
        public string Root {get;}=Path.Combine(Path.GetTempPath(),"SunEyeVision-NativeCalibration-"+Guid.NewGuid().ToString("N"));
        public string Active=>Path.Combine(Root,"active.json");
        public string Candidate=>NPointCalibrationParameters.CandidatePath(Active);
        public Files(){Directory.CreateDirectory(Root);}
        public void Dispose(){Directory.Delete(Root,true);}
        public NPointCalibrationParameters Write()
        {
            var p=Fixture();p.CalibrationFilePath=Active;
            NPointCalibrationParameters.WriteCandidateFile(Active,Solve(p),p.Samples,p.MaximumTrainingErrorMm);return p;
        }
    }

    [Theory]
    [InlineData(false,1,17)] [InlineData(true,1,17)] [InlineData(false,-1,17)] [InlineData(false,1,355)]
    public void NativeFitRecoversCoordinatesAndOffset(bool mirror,int sign,double reference)
    {
        var r=Solve(Fixture(mirror,sign,reference));
        Assert.InRange(Math.Abs(r.AxisToMarkX-5),0,1e-8);Assert.InRange(Math.Abs(r.AxisToMarkY+3),0,1e-8);
        Assert.InRange(r.RmsError,0,1e-8);Assert.InRange(r.RepeatErrorMm,0,1e-8);
        Assert.InRange(Math.Abs(r.HomMat2D[2]-100),0,1e-8);Assert.InRange(Math.Abs(r.HomMat2D[5]-50),0,1e-8);
    }
    [Fact] public void SmallArcUsesActualEncoderAngles()=>Solve(Fixture(angles:new[]{-10.0,0,10}));
    [Fact] public void NativeCoordinateOrderIsExplicit()
    {
        var h=Solve(Fixture()).HomMat2D;var q=HalconPlanarCalibration.Transform(h,70,10);
        Assert.InRange(Math.Abs(q.X-243),0,1e-8);Assert.InRange(Math.Abs(q.Y-66),0,1e-8);
    }
    [Fact] public void RotationAxisDriftIsRejected()
    {var p=Fixture();p.Samples[10].AxisX+=1;Assert.Contains("轴心",new NPointCalibrationTool().Run(p).ErrorMessage);}
    [Fact] public void TranslationAngleDriftIsRejected()
    {var p=Fixture();p.Samples[0].AxisAngle+=1;Assert.False(new NPointCalibrationTool().Run(p).IsSuccess);}
    [Fact] public void CollinearTranslationPointsAreRejected()
    {var p=Fixture();for(int i=0;i<9;i++){p.Samples[i].ImageX=i*20;p.Samples[i].ImageY=i*20;}Assert.Contains("共线",new NPointCalibrationTool().Run(p).ErrorMessage);}
    [Fact] public void DuplicatePointsAreRejected()
    {var p=Fixture();p.Samples[0].ImageX=p.Samples[1].ImageX;p.Samples[0].ImageY=p.Samples[1].ImageY;Assert.False(new NPointCalibrationTool().Run(p).IsSuccess);}
    [Fact] public void MissingRepeatCannotBeReportedAsPassed()
    {var p=Fixture(angles:new[]{30.0,90,180});Assert.Contains("复拍",new NPointCalibrationTool().Run(p).ErrorMessage);}
    [Fact] public void RepeatMismatchIsRejected()
    {var p=Fixture();p.Samples[9].ImageX+=1;Assert.Contains("复拍",new NPointCalibrationTool().Run(p).ErrorMessage);}
    [Fact] public void WrongRDirectionFailsCompleteResidualGate()
    {var p=Fixture();p.RotationSign=-1;Assert.Contains("超限",new NPointCalibrationTool().Run(p).ErrorMessage);}
    [Fact] public void TrainingThresholdMustBeConfigured()
    {var p=Fixture();p.MaximumTrainingErrorMm=0;Assert.False(new NPointCalibrationTool().Run(p).IsSuccess);}
    [Fact] public void NonFiniteDraftFailsCleanlyWithoutCrashingFingerprint()
    {var p=Fixture();p.Samples[0].ImageX=double.NaN;Assert.False(new NPointCalibrationTool().Run(p).IsSuccess);}
    [Fact] public async Task BoundAxisMustMatchEvidenceSnapshot()
    {
        var p=new NPointCalibrationParameters {TeachSignal=true,InputMode=NPointInputMode.Coordinates,AxisX=42};
        var r=await new NPointCalibrationTool().ExecuteAsync(new(p){CalibrationEvidence=Evidence()});Assert.False(r.IsSuccess);
    }
    [Fact] public void ChangedSettingsRejectStaleCommit()
    {var p=Fixture();var r=Solve(p);p.ReferenceRAngle++;Assert.False(p.TryCommit(r,out _));}
    [Fact] public void ChangedSamplesRejectStaleCommit()
    {var p=Fixture();var r=Solve(p);p.ReplaceSamples(p.Samples);Assert.False(p.TryCommit(r,out _));}
    [Fact] public void OldDraftContractIsRejectedNotMigrated()
    {
        var p=JsonSerializer.Deserialize<NPointCalibrationParameters>("{\"Samples\":[]}")!;
        p.Samples=Fixture().Samples;Assert.False(new NPointCalibrationTool().Run(p).IsSuccess);
    }
    [Fact] public void OldWeightsHaveNoRuntimeProperties()
    {
        Assert.Null(typeof(NPointCalibrationParameters).GetProperty("WeightFunction"));
        Assert.Null(typeof(NPointCalibrationResults).GetProperty("RobustScaleMm"));
    }
    [Fact] public void DisabledPointsRemainArchived()
    {
        var p=Fixture();var rows=p.Samples.Select(s=>s.Copy()).ToList();rows[0].Enabled=false;p.ReplaceSamples(rows);
        Assert.Equal(11,p.Samples.Count);Assert.Contains(p.ArchivedSamples,s=>s.Id==0);
        p.ReplaceSamples(Array.Empty<NPointSample>());Assert.NotEmpty(p.ArchivedSamples);
    }
    [Fact] public void CsvHasExplicitVersionAndUnitsAndRoundTrips()
    {
        var rows=NPointSampleCsv.Parse(NPointSampleCsv.Export(Fixture().Samples),9,3);
        Assert.Equal(12,rows.Length);Assert.All(rows,s=>Assert.Equal("ManualImported",s.Source));
    }
    [Fact] public void OldCsvAndNonFiniteCsvAreRejected()
    {
        Assert.Throws<FormatException>(()=>NPointSampleCsv.Parse(new[]{NPointSampleCsv.Columns},9,3));
        var p=Fixture();p.Samples[0].ImageX=double.NaN;
        Assert.Throws<FormatException>(()=>NPointSampleCsv.Parse(NPointSampleCsv.Export(p.Samples),9,3));
    }
    [Fact] public async Task TriggerWithoutUpstreamEvidenceFails()
    {
        var p=new NPointCalibrationParameters {TeachSignal=true,InputMode=NPointInputMode.Coordinates};
        var r=await new NPointCalibrationTool().ExecuteAsync(new(p));Assert.False(r.IsSuccess);Assert.Contains("证据",r.ErrorMessage);
    }
    [Fact] public async Task EvidenceCommitsOneSampleAndRejectsDuplicateFrame()
    {
        var p=new NPointCalibrationParameters {TeachSignal=true,InputMode=NPointInputMode.Coordinates};
        var tool=new NPointCalibrationTool();var e=Evidence();
        var r=await tool.ExecuteAsync(new(p){CalibrationEvidence=e});Assert.True(r.IsSuccess,r.ErrorMessage);Assert.True(p.TryCommit(r,out _));
        Assert.Single(p.Samples);Assert.Equal("camera-1",p.SourceDeviceId);
        r=await tool.ExecuteAsync(new(p){CalibrationEvidence=e with {StepId=2}});Assert.False(r.IsSuccess);Assert.Single(p.Samples);
    }
    [Fact] public async Task NotInPositionAndOutOfWindowFail()
    {
        var p=new NPointCalibrationParameters {TeachSignal=true,InputMode=NPointInputMode.Coordinates};var e=Evidence();
        Assert.False((await new NPointCalibrationTool().ExecuteAsync(new(p){CalibrationEvidence=e with{InPosition=false}})).IsSuccess);
        Assert.False((await new NPointCalibrationTool().ExecuteAsync(new(p){CalibrationEvidence=e with{AxisReadAt=e.FrameAt.AddSeconds(-3)}})).IsSuccess);
    }
    [Fact] public async Task CancellationDoesNotCommit()
    {
        var p=Fixture();using var c=new CancellationTokenSource();c.Cancel();
        await Assert.ThrowsAsync<OperationCanceledException>(async()=>await new NPointCalibrationTool().ExecuteAsync(new(p){CancellationToken=c.Token}));
    }
    [Fact] public void CandidateNeverOverwritesActiveFileAndUsesNewAlgorithm()
    {
        using var f=new Files();File.WriteAllText(f.Active,"existing active");f.Write();
        Assert.Equal("existing active",File.ReadAllText(f.Active));
        var (candidate,_)=NPointCalibrationFile.ReadCandidate(f.Candidate);
        Assert.Equal(3,candidate.SchemaVersion);Assert.Equal(PlanarCalibrationFit.Algorithm,candidate.AlgorithmId);
        Assert.DoesNotContain("WeightFunction",File.ReadAllText(f.Candidate));
    }
    [Fact] public void OldCandidateIsRejected()
    {
        using var f=new Files();File.WriteAllText(f.Candidate,"{\"SchemaVersion\":2,\"State\":\"SolvedUnverified\"}");
        Assert.Throws<InvalidDataException>(()=>NPointCalibrationFile.ReadCandidate(f.Candidate));
    }
    [Fact] public void IndependentValidationPublishesExactCandidate()
    {
        using var f=new Files();f.Write();var (_,hash)=NPointCalibrationFile.ReadCandidate(f.Candidate);
        NPointCalibrationFile.Publish(f.Candidate,f.Active,hash,HeldOut(),1e-6);
        var active=NPointCalibrationFile.ReadVerified(f.Active,"camera-1",1920,1080,"Raw");Assert.Equal(hash,active.CandidateSha256);
        Assert.InRange(active.ValidationMaxErrorMm,0,1e-8);
    }
    [Fact] public void ChangedCandidatePreservesOldActive()
    {
        using var f=new Files();f.Write();File.WriteAllText(f.Active,"old");
        var (_,hash)=NPointCalibrationFile.ReadCandidate(f.Candidate);File.AppendAllText(f.Candidate," ");
        Assert.Throws<InvalidOperationException>(()=>NPointCalibrationFile.Publish(f.Candidate,f.Active,hash,HeldOut(),1e-6));
        Assert.Equal("old",File.ReadAllText(f.Active));
    }
    [Fact] public void ReusedTrainingAndWrongEnvironmentAreRejected()
    {
        using var f=new Files();f.Write();var (candidate,_)=NPointCalibrationFile.ReadCandidate(f.Candidate);
        var samples=HeldOut();samples[0].ImageX=candidate.Samples[0].ImageX;samples[0].ImageY=candidate.Samples[0].ImageY;
        samples[0].AxisX=candidate.Samples[0].AxisX;samples[0].AxisY=candidate.Samples[0].AxisY;samples[0].AxisAngle=17;
        Assert.Throws<InvalidOperationException>(()=>candidate.Validate(samples,1e-6));
        samples=HeldOut();samples[0].WorkPlaneId="other";Assert.Throws<InvalidOperationException>(()=>candidate.Validate(samples,1e-6));
    }
    [Fact] public void CalibrationTabsAndPointEditorLoadAndRenderWithoutHost()
    {
        Exception? failure=null;var thread=new Thread(()=>
        {
            try
            {
                var control=new Views.NPointCalibrationDebugControl();
                var tabs=Assert.IsType<TabControl>(control.FindName("ParameterTabs"));Assert.Equal(3,tabs.Items.Count);
                control.Parameters.AcquisitionMode=NPointAcquisitionMode.Manual;Flush(control);
                Assert.Equal(Visibility.Collapsed,((StackPanel)control.FindName("TriggerInputsPanel")).Visibility);
                control.Parameters.AcquisitionMode=NPointAcquisitionMode.Trigger;control.Parameters.TeachEnabled=true;Flush(control);
                Assert.Equal(Visibility.Visible,((StackPanel)control.FindName("TeachFieldsPanel")).Visibility);
                control.SetParameters(Fixture());var solved=Solve(control.Parameters);
                Assert.True(control.Parameters.TryCommit(solved,out _));control.OnExternalResultArrived();
                Assert.Contains("训练合格",control.ResultText);
                RenderTabs(control,tabs,"npoint");
                var editor=new Views.NPointSampleEditor(new());var grid=(DataGrid)editor.FindName("SampleGrid");
                Assert.Equal(12,grid.Items.Count);editor.Close();
            }
            catch(Exception e){failure=e;}
        });
        thread.SetApartmentState(ApartmentState.STA);thread.Start();Assert.True(thread.Join(TimeSpan.FromSeconds(30)));
        Assert.Null(failure);
    }
    private static void Flush(FrameworkElement c)
    {c.Dispatcher.Invoke(()=>{},System.Windows.Threading.DispatcherPriority.DataBind);c.UpdateLayout();}
    private static void RenderTabs(FrameworkElement c,TabControl tabs,string prefix)
    {
        var folder=Environment.GetEnvironmentVariable("NPOINT_UI_SNAPSHOT_DIR");
        if(string.IsNullOrEmpty(folder))return;Directory.CreateDirectory(folder);
        c.Measure(new Size(650,850));c.Arrange(new Rect(0,0,650,850));
        for(int i=0;i<3;i++)
        {
            tabs.SelectedIndex=i;Flush(c);
            var image=new System.Windows.Media.Imaging.RenderTargetBitmap(650,850,96,96,System.Windows.Media.PixelFormats.Pbgra32);
            image.Render(c);var encoder=new System.Windows.Media.Imaging.PngBitmapEncoder();
            encoder.Frames.Add(System.Windows.Media.Imaging.BitmapFrame.Create(image));
            using var output=File.Create(Path.Combine(folder,$"{prefix}-{i+1}.png"));encoder.Save(output);
        }
    }
}
