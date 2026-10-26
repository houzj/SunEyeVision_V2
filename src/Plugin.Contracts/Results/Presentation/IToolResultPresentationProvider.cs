namespace SunEyeVision.Plugin.Contracts.Results.Presentation;

/// <summary>Runtime-owned result declarations, available without a WPF companion.</summary>
public interface IToolResultPresentationProvider
{
    ToolSchema ResultSchema { get; }
    ResultProjectionDescriptor ResultProjection { get; }
}
