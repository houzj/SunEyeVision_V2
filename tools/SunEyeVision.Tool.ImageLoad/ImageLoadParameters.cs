using System.Text.Json.Serialization;
using SunEyeVision.Plugin.SDK.Execution.Parameters;
using SunEyeVision.Plugin.SDK.Validation;

namespace SunEyeVision.Tool.ImageLoad
{
    /// <summary>
    /// 图像载入参数
    /// </summary>
    [JsonDerivedType(typeof(ImageLoadParameters), "ImageLoad")]
    public class ImageLoadParameters : ToolParameters
    {
        private string _filePath = string.Empty;

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
            if (string.IsNullOrEmpty(FilePath))
            {
                result.AddError("文件路径不能为空");
            }
            return result;
        }
    }
}
