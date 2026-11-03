namespace SunEyeVision.DeviceDriver.Cameras.Contracts;
public enum CameraDiagnosticLevel { Information, Warning, Error }
public sealed record CameraDiagnostic(CameraDiagnosticLevel Level,string DeviceId,string? CorrelationId,string Message,Exception? Error);
