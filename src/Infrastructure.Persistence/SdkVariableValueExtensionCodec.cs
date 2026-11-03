using System.Text.Json;
using OpenCvSharp;
using SunEyeVision.Persistence.Variables;
using SunEyeVision.Persistence;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Execution.Results;
using SunEyeVision.Plugin.SDK.Models;
using SunEyeVision.Plugin.SDK.Models.Communication;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Infrastructure.Persistence;

/// <summary>Explicit host codec for existing SDK and OpenCV variable values.</summary>
public sealed class SdkVariableValueExtensionCodec : IVariableValueExtensionCodec
{
    private readonly DecodedResourcesScope _scope;
    public SdkVariableValueExtensionCodec(DecodedResourcesScope scope) => _scope = scope ?? throw new ArgumentNullException(nameof(scope));
    public bool SupportsType(string typeName)
    {
        var type=SdkTypeVocabulary.ToSystemType(typeName);
        return type == typeof(BinaryPayload) || type == typeof(AffineMatrix) || type == typeof(AffineMatrix[]) || type == typeof(IVisionImage) || type == typeof(RoiShape[]);
    }
    public object? Read(string typeName, JsonElement value, JsonSerializerOptions options)
    {
        var type=SdkTypeVocabulary.ToSystemType(typeName);
        if(type==typeof(BinaryPayload)) return ParsePure(value,e=>new BinaryPayload(Convert.FromBase64String(e.GetString() ?? throw new FormatException("字节序列必须为Base64字符串。"))));
        if(type==typeof(AffineMatrix)) return ReadMatrix(value);
        if(type==typeof(AffineMatrix[])) return value.ValueKind==JsonValueKind.Array ? value.EnumerateArray().Select(ReadMatrix).ToArray() : throw new JsonException("此声明要求数组值。");
        if(type==typeof(IVisionImage)) return ReadImage(value);
        if(type==typeof(RoiShape[]))
        {
            if(value.ValueKind==JsonValueKind.Array) return JsonSerializer.Deserialize<RoiShape[]>(value.GetRawText(),options);
            var one=JsonSerializer.Deserialize<RoiShape>(value.GetRawText(),options);
            return one is null ? null : new[]{one};
        }
        throw new NotSupportedException("全局变量宿主未提供类型："+typeName);
    }
    public bool TryWrite(object value, JsonSerializerOptions options, out string typeName, out object? serializedValue)
    {
        typeName = (value is Mat ? "Image" : SdkTypeVocabulary.ToTypeVocabulary(value.GetType())) ?? string.Empty;
        serializedValue = value switch
        {
            BinaryPayload item => item.ToBase64(),
            BinaryPayload[] items => items.Select(item=>item.ToBase64()).ToArray(),
            RoiShape item => new[]{item},
            RoiShape[] items => items,
            Mat image => WriteImage(image),
            AffineMatrix matrix => WriteMatrix(matrix),
            AffineMatrix[] matrices => matrices.Select(WriteMatrix).ToArray(),
            _ => null
        };
        return !string.IsNullOrEmpty(typeName) && serializedValue is not null;
    }
    private static object ParsePure<T>(JsonElement element, Func<JsonElement,T> parse) => element.ValueKind == JsonValueKind.Array ? element.EnumerateArray().Select(parse).ToArray() : parse(element)!;
    private static AffineMatrix ReadMatrix(JsonElement value) => new(value.GetProperty("M11").GetDouble(),value.GetProperty("M12").GetDouble(),value.GetProperty("M21").GetDouble(),value.GetProperty("M22").GetDouble(),value.GetProperty("OffsetX").GetDouble(),value.GetProperty("OffsetY").GetDouble());
    private static Dictionary<string,double> WriteMatrix(AffineMatrix value) => new() { ["M11"]=value.M11,["M12"]=value.M12,["M21"]=value.M21,["M22"]=value.M22,["OffsetX"]=value.OffsetX,["OffsetY"]=value.OffsetY };
    private Mat ReadImage(JsonElement value)
    {
        if(value.ValueKind!=JsonValueKind.String || string.IsNullOrEmpty(value.GetString())) throw new JsonException("Image requires nonempty Base64 PNG data.");
        var image=Mat.FromImageData(Convert.FromBase64String(value.GetString()!),ImreadModes.Unchanged);
        if(image.Empty()){image.Dispose();throw new JsonException("Image decode returned empty pixels.");}
        _scope.RegisterNew(image);
        return image;
    }
    private static string WriteImage(Mat image)
    {
        if(image.Empty()) throw new JsonException("Cannot persist an empty image.");
        if(image.Depth() is not (0 or 2) || image.Channels() is not (1 or 3 or 4)) throw new JsonException("PNG persistence supports unsigned 8/16-bit, 1/3/4-channel images only; refusing lossy conversion.");
        var bytes=image.ToBytes(".png"); if(bytes.Length==0)throw new JsonException("Image encoder returned no data.");return Convert.ToBase64String(bytes);
    }
}
