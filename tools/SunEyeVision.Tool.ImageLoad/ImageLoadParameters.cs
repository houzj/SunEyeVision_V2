using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;
using SunEyeVision.Plugin.SDK.Vision.Contracts;

namespace SunEyeVision.Tool.ImageLoad
{
    /// <summary>
    /// 图像载入参数
    /// </summary>
    [JsonDerivedType(typeof(ImageLoadParameters), "ImageLoad")]
    public class ImageLoadParameters : ToolParameters
    {
        private string _filePath = string.Empty;
        private ImageSourceKind _sourceKind;
        private string _cameraDeviceId = string.Empty;
        private int _cameraTimeoutMs = 3000;
        private ImageOutputFormat _outputFormat;
        private long _sequenceNumber = 1;

        public ImageSourceKind SourceKind
        {
            get => _sourceKind;
            set => SetProperty(ref _sourceKind, value, "图像来源");
        }

        public string CameraDeviceId
        {
            get => _cameraDeviceId;
            set => SetProperty(ref _cameraDeviceId, value, "关联相机");
        }

        public int CameraTimeoutMs
        {
            get => _cameraTimeoutMs;
            set => SetProperty(ref _cameraTimeoutMs, value, "取帧超时");
        }

        public ImageOutputFormat OutputFormat
        {
            get => _outputFormat;
            set => SetProperty(ref _outputFormat, value, "输出格式");
        }

        /// <summary>宿主按工作流运行代次提供的本轮逻辑序号。</summary>
        public long SequenceNumber
        {
            get => _sequenceNumber;
            set => SetProperty(ref _sequenceNumber, value, "SN");
        }

        /// <summary>
        /// 图像文件路径
        /// </summary>
        public string FilePath
        {
            get => _filePath;
            set => SetProperty(ref _filePath, value, "文件路径");
        }

        public override ValidationResult Validate()
        {
            var result = base.Validate();
            if (SourceKind == ImageSourceKind.Local && string.IsNullOrEmpty(FilePath))
            {
                result.AddError("文件路径不能为空");
            }
            if (SourceKind == ImageSourceKind.Camera && string.IsNullOrWhiteSpace(CameraDeviceId))
                result.AddError("请选择方案中的相机");
            if (SourceKind == ImageSourceKind.Camera && CameraTimeoutMs <= 0)
                result.AddError("取帧超时必须大于零");
            if (!System.Enum.IsDefined(OutputFormat))
                result.AddError("输出格式无效");
            if (SequenceNumber < 0)
                result.AddError("SN 不能为负数");
            return result;
        }
    }
}
