namespace SunEyeVision.Plugin.Contracts;

/// <summary>Text-only module records derived from this invocation's accepted output.</summary>
public interface IToolResultRecordOutput
{
    IEnumerable<string> DescribeResultRecords();
}
