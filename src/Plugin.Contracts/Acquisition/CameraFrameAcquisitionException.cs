namespace SunEyeVision.Plugin.Contracts.Acquisition;

/// <summary>A classified failure of an attempted frame acquisition, distinct from admission, cancellation and cleanup.</summary>
public sealed class CameraFrameAcquisitionException : Exception
{
    public CameraFrameAcquisitionException(CameraErrorCode errorCode,string message,Exception? inner=null):base(message,inner)
    {
        if(!Enum.IsDefined(errorCode)||errorCode is CameraErrorCode.None or CameraErrorCode.OperationCancelled)
            throw new ArgumentOutOfRangeException(nameof(errorCode));
        ErrorCode=errorCode;
    }
    public CameraErrorCode ErrorCode {get;}
}
