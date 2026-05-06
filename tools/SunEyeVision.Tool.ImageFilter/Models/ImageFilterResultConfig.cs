using SunEyeVision.Plugin.SDK.Models;

namespace SunEyeVision.Tool.ImageFilter.Models
{
    public class ImageFilterResultConfig : ObservableObject
    {
        private bool _isEmptyCheckEnabled;
        public bool IsEmptyCheckEnabled { get => _isEmptyCheckEnabled; set => SetProperty(ref _isEmptyCheckEnabled, value, "isEmptyCheck"); }

        private bool _isMeanValueCheckEnabled;
        public bool IsMeanValueCheckEnabled { get => _isMeanValueCheckEnabled; set => SetProperty(ref _isMeanValueCheckEnabled, value, "isMeanValueCheck"); }
        private double _meanValueMin;
        public double MeanValueMin { get => _meanValueMin; set => SetProperty(ref _meanValueMin, value, "MeanValueMin"); }
        private double _meanValueMax = 255;
        public double MeanValueMax { get => _meanValueMax; set => SetProperty(ref _meanValueMax, value, "MeanValueMax"); }

        private bool _isStdDevCheckEnabled;
        public bool IsStdDevCheckEnabled { get => _isStdDevCheckEnabled; set => SetProperty(ref _isStdDevCheckEnabled, value, "isStdDevCheck"); }
        private double _stdDevMin;
        public double StdDevMin { get => _stdDevMin; set => SetProperty(ref _stdDevMin, value, "StdDevMin"); }
        private double _stdDevMax = 255;
        public double StdDevMax { get => _stdDevMax; set => SetProperty(ref _stdDevMax, value, "StdDevMax"); }

        private bool _isPSNRCheckEnabled;
        public bool IsPSNRCheckEnabled { get => _isPSNRCheckEnabled; set => SetProperty(ref _isPSNRCheckEnabled, value, "isPSNRCheck"); }
        private double _psnrMin;
        public double PSNRMin { get => _psnrMin; set => SetProperty(ref _psnrMin, value, "PSNRMin"); }
        private double _psnrMax = 100;
        public double PSNRMax { get => _psnrMax; set => SetProperty(ref _psnrMax, value, "PSNRMax"); }
    }
}