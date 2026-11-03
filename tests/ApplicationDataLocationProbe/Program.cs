using System.Text.Json.Nodes;
using SunEyeVision.Infrastructure.Persistence.ApplicationData;

try
{
    static void Check(bool condition, string reason) { if (!condition) throw new Exception(reason); }
    static void Reject(Action operation)
    {
        try { operation(); } catch (Exception e) when (e is IOException or InvalidDataException or System.Text.Json.JsonException or InvalidOperationException) { return; }
        throw new Exception("Invalid application data was accepted.");
    }
    var temporaryBase = Path.GetFullPath(Path.GetTempPath());
    var root = Path.GetFullPath(Path.Combine(temporaryBase, "SunEyeVision-AppData-Probe-" + Guid.NewGuid().ToString("N")));
    if (!root.StartsWith(temporaryBase, StringComparison.OrdinalIgnoreCase) || Path.GetFileName(root).StartsWith("SunEyeVision-AppData-Probe-", StringComparison.Ordinal) == false)
        throw new Exception("Test data root is not contained in the intended temporary directory.");
    try
    {
        var service = new ApplicationDataLocationService(Path.Combine(root, "locator.json"));
        Check(service.Locate() is null, "Missing locator must not initialize or guess a data directory.");
        var paths = service.Prepare(Path.Combine(root, "data"));
        File.WriteAllText(paths.ConfigFile, "{\"Theme\":\"Dark\"}");
        service.Remember(paths);
        Check(service.Locate()!.Root == paths.Root, "Remember/Locate lost actual directory identity.");
        var marker = JsonNode.Parse(File.ReadAllText(paths.MarkerFile))!.AsObject();
        var original = marker.ToJsonString(); marker["dataId"] = Guid.NewGuid().ToString(); File.WriteAllText(paths.MarkerFile, marker.ToJsonString());
        Reject(() => service.Locate()); File.WriteAllText(paths.MarkerFile, original);
        File.Delete(paths.ConfigFile); Reject(paths.ValidateAvailable);
        File.WriteAllText(paths.ConfigFile, "[]"); Reject(paths.ValidateAvailable);
        File.WriteAllText(paths.ConfigFile, "{}");
        marker["schemaVersion"] = 1; File.WriteAllText(paths.MarkerFile, marker.ToJsonString()); Reject(paths.ValidateAvailable);
        Reject(() => service.Prepare(AppContext.BaseDirectory));
        Console.WriteLine("PASS application data prepare, identity, preferences loss/corruption, old schema and output-directory rejection");
    }
    finally { if (Directory.Exists(root)) Directory.Delete(root, recursive: true); }
    return 0;
}
catch (Exception error) { Console.Error.WriteLine(error); return 1; }
